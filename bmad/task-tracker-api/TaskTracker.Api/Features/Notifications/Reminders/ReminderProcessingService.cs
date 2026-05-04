using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.Auth.Email;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;
using TimeZoneConverter;

namespace TaskTracker.Api.Features.Notifications.Reminders;

public class ReminderProcessingService(
    TaskTrackerDbContext dbContext,
    ITransactionalEmailService transactionalEmailService,
    ILogger<ReminderProcessingService> logger) : IReminderProcessingService
{
    private const int MaxUsersPerRun = 100;
    private const int MaxTasksPerUser = 25;
    private const int MaxDeliveryAttempts = 3;
    private const int NearMissTierThresholdDays = 3;
    private const int QuietHoursStartHour = 22;
    private const int QuietHoursEndHour = 7;
    private const int NudgeWindowStartHour = 9;
    private const int NudgeWindowEndHour = 21;

    public async Task<ReminderProcessingRunResult> ProcessAsync(string traceId, CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;

        var eligibleUsers = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.ReminderEmailEnabled)
            .OrderBy(user => user.Id)
            .Take(MaxUsersPerRun)
            .ToListAsync(cancellationToken);

        var sentCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var processedUserCount = 0;

        logger.LogInformation(
            "Reminder job started. EligibleUsers={EligibleUsers}. TraceId={TraceId}",
            eligibleUsers.Count,
            traceId);

        foreach (var user in eligibleUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processedUserCount++;

            var nowUtc = DateTime.UtcNow;
            if (!TryResolveTimeZone(user.TimeZoneId, out var timeZone))
            {
                skippedCount++;
                continue;
            }

            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
            if (!IsWithinNudgeWindow(localNow) || IsWithinQuietHours(localNow))
            {
                skippedCount++;
                continue;
            }

            var isNearMiss = await IsNearMissCandidateAsync(user.Id, timeZone, nowUtc, cancellationToken);
            if (!isNearMiss)
            {
                skippedCount++;
                continue;
            }

            var cadenceWindow = ResolveDailyLocalWindow(localNow.Date, timeZone);

            var hasCompletedWindow = await dbContext.NotificationReminderDispatches
                .AsNoTracking()
                .AnyAsync(dispatch => dispatch.UserId == user.Id
                    && dispatch.Cadence == cadenceWindow.Cadence
                    && dispatch.WindowStartUtc == cadenceWindow.WindowStartUtc
                    && dispatch.Status == NotificationReminderDispatchStatus.Succeeded,
                    cancellationToken);

            if (hasCompletedWindow)
            {
                skippedCount++;
                continue;
            }

            var pendingTasks = await dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == user.Id && !task.IsCompleted)
                .OrderBy(task => task.DueAtUtc == null)
                .ThenBy(task => task.DueAtUtc)
                .ThenBy(task => task.CreatedAtUtc)
                .ThenBy(task => task.Id)
                .Take(MaxTasksPerUser)
                .Select(task => new TaskReminderTaskSummary(
                    task.Id,
                    task.Title,
                    task.DueAtUtc,
                    task.Priority,
                    task.Category))
                .ToListAsync(cancellationToken);

            if (pendingTasks.Count == 0)
            {
                skippedCount++;
                continue;
            }

            var dispatch = new NotificationReminderDispatch
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Cadence = cadenceWindow.Cadence,
                WindowStartUtc = cadenceWindow.WindowStartUtc,
                WindowEndUtc = cadenceWindow.WindowEndUtc,
                Status = NotificationReminderDispatchStatus.Processing,
                AttemptCount = 0,
                TaskCount = pendingTasks.Count,
                CreatedAtUtc = nowUtc,
                TraceId = traceId
            };

            dbContext.NotificationReminderDispatches.Add(dispatch);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // A concurrent run already claimed this user/cadence window.
                dbContext.Entry(dispatch).State = EntityState.Detached;
                skippedCount++;
                continue;
            }

            var delivered = false;
            TransactionalEmailSendOutcome? lastOutcome = null;
            for (var attempt = 1; attempt <= MaxDeliveryAttempts; attempt++)
            {
                logger.LogInformation(
                    "Reminder email delivery attempt {Attempt}/{MaxAttempts}. UserId={UserId}. Cadence={Cadence}. WindowStartUtc={WindowStartUtc}. TraceId={TraceId}",
                    attempt,
                    MaxDeliveryAttempts,
                    user.Id,
                    cadenceWindow.Cadence,
                    cadenceWindow.WindowStartUtc,
                    traceId);

                dispatch.AttemptCount = attempt;
                dispatch.LastAttemptAtUtc = DateTime.UtcNow;

                TransactionalEmailSendOutcome sendOutcome;
                try
                {
                    sendOutcome = await transactionalEmailService.SendTaskReminderAsync(
                        new TaskReminderEmailMessage(
                            user.Id,
                            user.Email,
                            cadenceWindow.Cadence,
                            cadenceWindow.WindowStartUtc,
                            cadenceWindow.WindowEndUtc,
                            pendingTasks),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Reminder provider threw an exception. UserId={UserId}. Attempt={Attempt}. Cadence={Cadence}. WindowStartUtc={WindowStartUtc}. TraceId={TraceId}",
                        user.Id,
                        attempt,
                        cadenceWindow.Cadence,
                        cadenceWindow.WindowStartUtc,
                        traceId);

                    sendOutcome = TransactionalEmailSendOutcome.TransientFailure(
                        providerErrorCode: ex.GetType().Name,
                        providerStatus: "exception");
                }

                lastOutcome = sendOutcome;
                var sendResult = sendOutcome.Result;

                logger.LogInformation(
                    "Reminder provider response. UserId={UserId}. Attempt={Attempt}. SendResult={SendResult}. ProviderStatus={ProviderStatus}. ProviderMessageId={ProviderMessageId}. ProviderErrorCode={ProviderErrorCode}. TraceId={TraceId}",
                    user.Id,
                    attempt,
                    sendResult,
                    sendOutcome.ProviderStatus ?? "unknown",
                    sendOutcome.ProviderMessageId ?? "none",
                    sendOutcome.ProviderErrorCode ?? "none",
                    traceId);

                if (sendResult == TransactionalEmailSendResult.Success)
                {
                    dispatch.Status = NotificationReminderDispatchStatus.Succeeded;
                    dispatch.SentAtUtc = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    sentCount++;
                    delivered = true;

                    logger.LogInformation(
                        "Reminder delivery succeeded. UserId={UserId}. Attempt={Attempt}. ProviderMessageId={ProviderMessageId}. TraceId={TraceId}",
                        user.Id,
                        attempt,
                        sendOutcome.ProviderMessageId ?? "none",
                        traceId);

                    break;
                }

                if (sendResult == TransactionalEmailSendResult.PermanentFailure)
                {
                    dispatch.Status = NotificationReminderDispatchStatus.FailedPermanent;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    failedCount++;
                    logger.LogWarning(
                        "Reminder delivery failed permanently. UserId={UserId}. Attempt={Attempt}. ProviderStatus={ProviderStatus}. ProviderErrorCode={ProviderErrorCode}. TraceId={TraceId}",
                        user.Id,
                        attempt,
                        sendOutcome.ProviderStatus ?? "unknown",
                        sendOutcome.ProviderErrorCode ?? "none",
                        traceId);
                    break;
                }
            }

            if (!delivered && dispatch.Status == NotificationReminderDispatchStatus.Processing)
            {
                dispatch.Status = NotificationReminderDispatchStatus.FailedTransient;
                await dbContext.SaveChangesAsync(cancellationToken);

                failedCount++;
                logger.LogError(
                    "Reminder delivery exhausted transient retries. UserId={UserId}. LastProviderStatus={ProviderStatus}. LastProviderErrorCode={ProviderErrorCode}. TraceId={TraceId}",
                    user.Id,
                    lastOutcome?.ProviderStatus ?? "unknown",
                    lastOutcome?.ProviderErrorCode ?? "none",
                    traceId);
            }
        }

        var completedAtUtc = DateTime.UtcNow;

        logger.LogInformation(
            "Reminder job completed. EligibleUsers={EligibleUsers}. ProcessedUsers={ProcessedUsers}. Sent={Sent}. Skipped={Skipped}. Failed={Failed}. TraceId={TraceId}",
            eligibleUsers.Count,
            processedUserCount,
            sentCount,
            skippedCount,
            failedCount,
            traceId);

        return new ReminderProcessingRunResult(
            startedAtUtc,
            completedAtUtc,
            eligibleUsers.Count,
            processedUserCount,
            sentCount,
            skippedCount,
            failedCount);
    }

    private static ReminderCadenceWindow ResolveWindow(NotificationReminderCadence cadence, DateTime nowUtc)
    {
        return cadence == NotificationReminderCadence.Weekly
            ? ResolveWeeklyWindow(nowUtc)
            : ResolveDailyWindow(nowUtc);
    }

    private async Task<bool> IsNearMissCandidateAsync(
        Guid userId,
        TimeZoneInfo timeZone,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.UserStreakSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(existing => existing.OwnerId == userId, cancellationToken);

        if (snapshot is null || snapshot.CurrentStreakDays < NearMissTierThresholdDays)
        {
            return false;
        }

        var localNowDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone));
        var localLastEvaluatedDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(snapshot.LastEvaluatedAtUtc), timeZone));
        var localDayGap = localNowDate.DayNumber - localLastEvaluatedDate.DayNumber;

        return localDayGap == 1;
    }

    private static ReminderCadenceWindow ResolveDailyLocalWindow(DateTime localDate, TimeZoneInfo timeZone)
    {
        var localStart = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(localDate.Date.AddDays(1), DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
        return new ReminderCadenceWindow(NotificationReminderCadence.Daily, startUtc, endUtc);
    }

    private static bool IsWithinNudgeWindow(DateTime localNow)
    {
        var hour = localNow.Hour;
        return hour >= NudgeWindowStartHour && hour < NudgeWindowEndHour;
    }

    private static bool IsWithinQuietHours(DateTime localNow)
    {
        var hour = localNow.Hour;
        return hour >= QuietHoursStartHour || hour < QuietHoursEndHour;
    }

    private static bool TryResolveTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TZConvert.GetTimeZoneInfo(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static ReminderCadenceWindow ResolveDailyWindow(DateTime nowUtc)
    {
        var start = nowUtc.Date;
        var end = start.AddDays(1);
        return new ReminderCadenceWindow(NotificationReminderCadence.Daily, start, end);
    }

    private static ReminderCadenceWindow ResolveWeeklyWindow(DateTime nowUtc)
    {
        var dayOffset = ((int)nowUtc.DayOfWeek + 6) % 7;
        var start = nowUtc.Date.AddDays(-dayOffset);
        var end = start.AddDays(7);
        return new ReminderCadenceWindow(NotificationReminderCadence.Weekly, start, end);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var sqlException = exception.InnerException as SqlException;
        return sqlException is not null && (sqlException.Number == 2627 || sqlException.Number == 2601);
    }
}
