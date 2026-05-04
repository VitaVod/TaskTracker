using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Infrastructure.Persistence;

public class TaskTrackerDbContext(DbContextOptions<TaskTrackerDbContext> options) : DbContext(options)
{
	public DbSet<User> Users => Set<User>();

	public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

	public DbSet<PasswordRecoveryToken> PasswordRecoveryTokens => Set<PasswordRecoveryToken>();

	public DbSet<EmailChangeToken> EmailChangeTokens => Set<EmailChangeToken>();

	public DbSet<TaskItem> Tasks => Set<TaskItem>();

	public DbSet<TaskCompletionEvent> TaskCompletionEvents => Set<TaskCompletionEvent>();

	public DbSet<XpLedgerEntry> XpLedgerEntries => Set<XpLedgerEntry>();

	public DbSet<UserStreakSnapshot> UserStreakSnapshots => Set<UserStreakSnapshot>();

	public DbSet<StreakRecoveryTokenEvent> StreakRecoveryTokenEvents => Set<StreakRecoveryTokenEvent>();

	public DbSet<NotificationReminderDispatch> NotificationReminderDispatches => Set<NotificationReminderDispatch>();

	public DbSet<AccountNotificationDispatch> AccountNotificationDispatches => Set<AccountNotificationDispatch>();

	public DbSet<ModerationActionAudit> ModerationActionAudits => Set<ModerationActionAudit>();

	public DbSet<PrivilegedActionAudit> PrivilegedActionAudits => Set<PrivilegedActionAudit>();

	public DbSet<IntegrationCredential> IntegrationCredentials => Set<IntegrationCredential>();

	public DbSet<IntegrationCredentialScope> IntegrationCredentialScopes => Set<IntegrationCredentialScope>();

	public DbSet<IntegrationTaskSyncBinding> IntegrationTaskSyncBindings => Set<IntegrationTaskSyncBinding>();

	public DbSet<IntegrationEventIdempotencyRecord> IntegrationEventIdempotencyRecords => Set<IntegrationEventIdempotencyRecord>();

	public DbSet<IntegrationProcessingFailureEvent> IntegrationProcessingFailureEvents => Set<IntegrationProcessingFailureEvent>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<User>(entity =>
		{
			entity.ToTable("Users");

			entity.HasKey(user => user.Id);

			entity.Property(user => user.Email)
				.HasMaxLength(320)
				.IsRequired();

			entity.Property(user => user.PasswordHash)
				.HasMaxLength(256)
				.IsRequired();

			entity.Property(user => user.PasswordSalt)
				.HasMaxLength(256)
				.IsRequired();

			entity.Property(user => user.DisplayName)
				.HasMaxLength(80)
				.IsRequired();

			entity.Property(user => user.TimeZoneId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(user => user.Locale)
				.HasMaxLength(16)
				.IsRequired();

			entity.Property(user => user.Role)
				.HasMaxLength(16)
				.IsRequired();

			entity.Property(user => user.LeaderboardParticipationMode)
				.HasMaxLength(16)
				.HasConversion(
					mode => mode == LeaderboardParticipationMode.Public
						? "public"
						: mode == LeaderboardParticipationMode.Anonymous
							? "anonymous"
							: "hidden",
					value => value == "public"
						? LeaderboardParticipationMode.Public
						: value == "anonymous"
							? LeaderboardParticipationMode.Anonymous
							: LeaderboardParticipationMode.Hidden)
				.HasSentinel((LeaderboardParticipationMode)(-1))
				.HasDefaultValue(LeaderboardParticipationMode.Public)
				.IsRequired();

			entity.Property(user => user.ReminderEmailEnabled)
				.HasDefaultValue(true)
				.IsRequired();

			entity.Property(user => user.ReminderCadence)
				.HasMaxLength(16)
				.HasConversion(
					cadence => cadence == NotificationReminderCadence.Weekly
						? "weekly"
						: "daily",
					value => value == "weekly"
						? NotificationReminderCadence.Weekly
						: NotificationReminderCadence.Daily)
				.HasDefaultValue(NotificationReminderCadence.Daily)
				.IsRequired();

			entity.Property(user => user.AccountEmailEnabled)
				.HasDefaultValue(true)
				.IsRequired();

			entity.Property(user => user.IsSuspiciousFlagged)
				.HasDefaultValue(false)
				.IsRequired();

			entity.Property(user => user.CreatedAtUtc)
				.IsRequired();

			entity.Property(user => user.ModifiedAtUtc)
				.IsRequired();

			entity.HasIndex(user => user.Email)
				.IsUnique();
		});

		modelBuilder.Entity<ModerationActionAudit>(entity =>
		{
			entity.ToTable("ModerationActionAudits");

			entity.HasKey(audit => audit.Id);

			entity.Property(audit => audit.CaseId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(audit => audit.CorrelationRef)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(audit => audit.ActorUserId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(audit => audit.ActorRole)
				.HasMaxLength(32)
				.IsRequired();

			entity.Property(audit => audit.ActionType)
				.HasMaxLength(32)
				.IsRequired();

			entity.Property(audit => audit.ReasonCode)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(audit => audit.ReasonText)
				.HasMaxLength(512)
				.IsRequired();

			entity.Property(audit => audit.ConfirmationToken)
				.HasMaxLength(256);

			entity.Property(audit => audit.Outcome)
				.HasMaxLength(32)
				.IsRequired();

			entity.Property(audit => audit.IntentKey)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(audit => audit.TraceId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(audit => audit.CreatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(audit => audit.TargetUserId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasIndex(audit => audit.IntentKey)
				.IsUnique();
			entity.HasIndex(audit => new { audit.CaseId, audit.CreatedAtUtc });
			entity.HasIndex(audit => new { audit.TargetUserId, audit.CreatedAtUtc });
		});

		modelBuilder.Entity<PrivilegedActionAudit>(entity =>
		{
			entity.ToTable("PrivilegedActionAudits");

			entity.HasKey(audit => audit.Id);

			entity.Property(audit => audit.ActorUserId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(audit => audit.ActorRole)
				.HasMaxLength(32)
				.IsRequired();

			entity.Property(audit => audit.ActionType)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(audit => audit.ReasonCode)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(audit => audit.ReasonText)
				.HasMaxLength(512)
				.IsRequired();

			entity.Property(audit => audit.Outcome)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(audit => audit.CorrelationId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(audit => audit.TraceId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(audit => audit.IntentKey)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(audit => audit.OccurredAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(audit => audit.TargetUserId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasIndex(audit => new { audit.TargetUserId, audit.OccurredAtUtc });
			entity.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAtUtc });
			entity.HasIndex(audit => audit.OccurredAtUtc);
			entity.HasIndex(audit => audit.IntentKey)
				.IsUnique();
		});

		modelBuilder.Entity<NotificationReminderDispatch>(entity =>
		{
			entity.ToTable("NotificationReminderDispatches");

			entity.HasKey(dispatch => dispatch.Id);

			entity.Property(dispatch => dispatch.Cadence)
				.HasMaxLength(16)
				.HasConversion(
					cadence => cadence == NotificationReminderCadence.Weekly
						? "weekly"
						: "daily",
					value => value == "weekly"
						? NotificationReminderCadence.Weekly
						: NotificationReminderCadence.Daily)
				.IsRequired();

			entity.Property(dispatch => dispatch.Status)
				.HasMaxLength(24)
				.HasConversion(
					status => status == NotificationReminderDispatchStatus.Succeeded
						? "succeeded"
						: status == NotificationReminderDispatchStatus.FailedTransient
							? "failed_transient"
							: status == NotificationReminderDispatchStatus.FailedPermanent
								? "failed_permanent"
								: "processing",
					value => value == "succeeded"
						? NotificationReminderDispatchStatus.Succeeded
						: value == "failed_transient"
							? NotificationReminderDispatchStatus.FailedTransient
							: value == "failed_permanent"
								? NotificationReminderDispatchStatus.FailedPermanent
								: NotificationReminderDispatchStatus.Processing)
				.IsRequired();

			entity.Property(dispatch => dispatch.WindowStartUtc)
				.IsRequired();

			entity.Property(dispatch => dispatch.WindowEndUtc)
				.IsRequired();

			entity.Property(dispatch => dispatch.AttemptCount)
				.IsRequired();

			entity.Property(dispatch => dispatch.TaskCount)
				.IsRequired();

			entity.Property(dispatch => dispatch.CreatedAtUtc)
				.IsRequired();

			entity.Property(dispatch => dispatch.TraceId)
				.HasMaxLength(128)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(dispatch => dispatch.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(dispatch => new { dispatch.UserId, dispatch.Cadence, dispatch.WindowStartUtc })
				.IsUnique();
			entity.HasIndex(dispatch => new { dispatch.Status, dispatch.WindowStartUtc });
		});

		modelBuilder.Entity<AccountNotificationDispatch>(entity =>
		{
			entity.ToTable("AccountNotificationDispatches");

			entity.HasKey(dispatch => dispatch.Id);

			entity.Property(dispatch => dispatch.EventKey)
				.HasMaxLength(160)
				.IsRequired();

			entity.Property(dispatch => dispatch.EventType)
				.HasMaxLength(48)
				.HasConversion(
					eventType => eventType == AccountNotificationEventType.PasswordRecoveryRequested
						? "password_recovery_requested"
						: eventType == AccountNotificationEventType.PasswordResetCompleted
							? "password_reset_completed"
							: eventType == AccountNotificationEventType.EmailChangeRequested
								? "email_change_requested"
								: "email_change_completed",
					value => value == "password_recovery_requested"
						? AccountNotificationEventType.PasswordRecoveryRequested
						: value == "password_reset_completed"
							? AccountNotificationEventType.PasswordResetCompleted
							: value == "email_change_requested"
								? AccountNotificationEventType.EmailChangeRequested
								: AccountNotificationEventType.EmailChangeCompleted)
				.IsRequired();

			entity.Property(dispatch => dispatch.ToEmail)
				.HasMaxLength(320)
				.IsRequired();

			entity.Property(dispatch => dispatch.Status)
				.HasMaxLength(24)
				.HasConversion(
					status => status == AccountNotificationDispatchStatus.Queued
						? "queued"
						: status == AccountNotificationDispatchStatus.Succeeded
							? "succeeded"
							: status == AccountNotificationDispatchStatus.FailedTransient
								? "failed_transient"
								: status == AccountNotificationDispatchStatus.FailedPermanent
									? "failed_permanent"
									: "processing",
					value => value == "queued"
						? AccountNotificationDispatchStatus.Queued
						: value == "succeeded"
							? AccountNotificationDispatchStatus.Succeeded
							: value == "failed_transient"
								? AccountNotificationDispatchStatus.FailedTransient
								: value == "failed_permanent"
									? AccountNotificationDispatchStatus.FailedPermanent
									: AccountNotificationDispatchStatus.Processing)
				.IsRequired();

			entity.Property(dispatch => dispatch.AttemptCount)
				.IsRequired();

			entity.Property(dispatch => dispatch.CreatedAtUtc)
				.IsRequired();

			entity.Property(dispatch => dispatch.LastUpdatedAtUtc)
				.IsRequired();

			entity.Property(dispatch => dispatch.TraceId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(dispatch => dispatch.CorrelationId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(dispatch => dispatch.LastFailureCategory)
				.HasMaxLength(24);

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(dispatch => dispatch.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(dispatch => dispatch.EventKey)
				.IsUnique();
			entity.HasIndex(dispatch => new { dispatch.Status, dispatch.CreatedAtUtc });
			entity.HasIndex(dispatch => new { dispatch.UserId, dispatch.EventType, dispatch.CreatedAtUtc });
		});

		modelBuilder.Entity<RefreshSession>(entity =>
		{
			entity.ToTable("RefreshSessions");

			entity.HasKey(s => s.Id);

			entity.Property(s => s.UserEmail)
				.HasMaxLength(320)
				.IsRequired();

			entity.Property(s => s.RevokedReason)
				.HasMaxLength(32);

			entity.Property(s => s.IssuedAtUtc)
				.IsRequired();

			entity.Property(s => s.ExpiresAtUtc)
				.IsRequired();

			entity.Property(s => s.CreatedAtUtc)
				.IsRequired();

			entity.Property(s => s.RowVersion)
				.IsRowVersion();

			// Index for looking up all sessions belonging to a user (e.g. family revocation)
			entity.HasIndex(s => s.UserId);

			// Index for expiry-based cleanup queries
			entity.HasIndex(s => s.ExpiresAtUtc);
		});

		modelBuilder.Entity<IntegrationCredential>(entity =>
		{
			entity.ToTable("IntegrationCredentials");

			entity.HasKey(credential => credential.Id);

			entity.Property(credential => credential.KeyId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(credential => credential.IntegrationId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(credential => credential.IntegrationName)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(credential => credential.SecretHash)
				.HasMaxLength(256)
				.IsRequired();

			entity.Property(credential => credential.SecretSalt)
				.HasMaxLength(256)
				.IsRequired();

			entity.Property(credential => credential.Status)
				.HasMaxLength(24)
				.HasConversion(
					status => status == IntegrationCredentialStatus.Active ? "active" : "revoked",
					value => value == "active"
						? IntegrationCredentialStatus.Active
						: IntegrationCredentialStatus.Revoked)
				.IsRequired();

			entity.Property(credential => credential.CreatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(credential => credential.OwnerUserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(credential => credential.KeyId)
				.IsUnique();

			entity.HasIndex(credential => new { credential.OwnerUserId, credential.Status, credential.CreatedAtUtc });

			entity.HasIndex(credential => new { credential.Status, credential.ExpiresAtUtc });
		});

		modelBuilder.Entity<IntegrationCredentialScope>(entity =>
		{
			entity.ToTable("IntegrationCredentialScopes");

			entity.HasKey(scope => scope.Id);

			entity.Property(scope => scope.Scope)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(scope => scope.CreatedAtUtc)
				.IsRequired();

			entity.HasOne(scope => scope.Credential)
				.WithMany(credential => credential.Scopes)
				.HasForeignKey(scope => scope.CredentialId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(scope => new { scope.CredentialId, scope.Scope })
				.IsUnique();

			entity.HasIndex(scope => scope.Scope);
		});

		modelBuilder.Entity<IntegrationTaskSyncBinding>(entity =>
		{
			entity.ToTable("IntegrationTaskSyncBindings");

			entity.HasKey(binding => binding.Id);

			entity.Property(binding => binding.IntegrationId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(binding => binding.ExternalTaskId)
				.HasMaxLength(160)
				.IsRequired();

			entity.Property(binding => binding.CreatedAtUtc)
				.IsRequired();

			entity.Property(binding => binding.UpdatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(binding => binding.OwnerUserId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasOne<TaskItem>()
				.WithMany()
				.HasForeignKey(binding => binding.TaskId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(binding => new { binding.OwnerUserId, binding.IntegrationId, binding.ExternalTaskId })
				.IsUnique();

			entity.HasIndex(binding => binding.TaskId)
				.IsUnique();
		});

		modelBuilder.Entity<IntegrationEventIdempotencyRecord>(entity =>
		{
			entity.ToTable("IntegrationEventIdempotencyRecords");

			entity.HasKey(record => record.Id);

			entity.Property(record => record.IntegrationId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(record => record.IdempotencyKey)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(record => record.ExternalTaskId)
				.HasMaxLength(160)
				.IsRequired();

			entity.Property(record => record.Operation)
				.HasMaxLength(16)
				.IsRequired();

			entity.Property(record => record.TraceId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(record => record.CorrelationId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(record => record.ProcessedAtUtc)
				.IsRequired();

			entity.Property(record => record.CreatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(record => record.OwnerUserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne<TaskItem>()
				.WithMany()
				.HasForeignKey(record => record.TaskId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasIndex(record => new { record.OwnerUserId, record.IntegrationId, record.IdempotencyKey })
				.IsUnique();

			entity.HasIndex(record => new { record.OwnerUserId, record.IntegrationId, record.ExternalTaskId, record.ProcessedAtUtc });
		});

		modelBuilder.Entity<IntegrationProcessingFailureEvent>(entity =>
		{
			entity.ToTable("IntegrationProcessingFailureEvents");

			entity.HasKey(failureEvent => failureEvent.Id);

			entity.Property(failureEvent => failureEvent.IntegrationId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(failureEvent => failureEvent.ExternalTaskId)
				.HasMaxLength(160);

			entity.Property(failureEvent => failureEvent.IdempotencyKey)
				.HasMaxLength(64);

			entity.Property(failureEvent => failureEvent.ErrorClass)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(failureEvent => failureEvent.ErrorCode)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(failureEvent => failureEvent.HttpStatus)
				.IsRequired();

			entity.Property(failureEvent => failureEvent.CorrelationId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(failureEvent => failureEvent.TraceId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(failureEvent => failureEvent.OccurredAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(failureEvent => failureEvent.OwnerUserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(failureEvent => failureEvent.OccurredAtUtc);
			entity.HasIndex(failureEvent => new { failureEvent.IntegrationId, failureEvent.OccurredAtUtc });
			entity.HasIndex(failureEvent => new { failureEvent.OwnerUserId, failureEvent.OccurredAtUtc });
			entity.HasIndex(failureEvent => failureEvent.CorrelationId);
			entity.HasIndex(failureEvent => failureEvent.TraceId);
		});

		modelBuilder.Entity<PasswordRecoveryToken>(entity =>
		{
			entity.ToTable("PasswordRecoveryTokens");

			entity.HasKey(token => token.TokenId);

			entity.Property(token => token.TokenHash)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(token => token.IssuedAtUtc)
				.IsRequired();

			entity.Property(token => token.ExpiresAtUtc)
				.IsRequired();

			entity.Property(token => token.DeliveryAttemptCount)
				.IsRequired();

			entity.Property(token => token.CreatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(token => token.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(token => token.UserId);
			entity.HasIndex(token => token.ExpiresAtUtc);
		});

		modelBuilder.Entity<EmailChangeToken>(entity =>
		{
			entity.ToTable("EmailChangeTokens");

			entity.HasKey(token => token.TokenId);

			entity.Property(token => token.NewEmail)
				.HasMaxLength(320)
				.IsRequired();

			entity.Property(token => token.TokenHash)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(token => token.RequestedAtUtc)
				.IsRequired();

			entity.Property(token => token.ExpiresAtUtc)
				.IsRequired();

			entity.Property(token => token.CreatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(token => token.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(token => token.UserId);
			entity.HasIndex(token => token.ExpiresAtUtc);
			entity.HasIndex(token => token.NewEmail);
		});

		modelBuilder.Entity<TaskItem>(entity =>
		{
			entity.ToTable("Tasks");

			entity.HasKey(task => task.Id);

			entity.Property(task => task.Title)
				.HasMaxLength(160)
				.IsRequired();

			entity.Property(task => task.Description)
				.HasMaxLength(2000)
				.IsRequired();

			entity.Property(task => task.Priority)
				.HasMaxLength(16)
				.IsRequired();

			entity.Property(task => task.Category)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(task => task.Difficulty)
				.HasMaxLength(16)
				.HasConversion(
					difficulty => difficulty == TaskDifficulty.Hard
						? "hard"
						: difficulty == TaskDifficulty.Medium
							? "medium"
							: "easy",
					value => value == "hard"
						? TaskDifficulty.Hard
						: value == "medium"
							? TaskDifficulty.Medium
							: TaskDifficulty.Easy)
				.HasDefaultValue(TaskDifficulty.Easy)
				.IsRequired();

			entity.Property(task => task.EnergyLevel)
				.HasMaxLength(16)
				.HasConversion(
					energyLevel => energyLevel == TaskEnergyLevel.Low
						? "low"
						: energyLevel == TaskEnergyLevel.High
							? "high"
							: "medium",
					value => value == "low"
						? TaskEnergyLevel.Low
						: value == "high"
							? TaskEnergyLevel.High
							: TaskEnergyLevel.Medium)
				.HasSentinel((TaskEnergyLevel)(-1))
				.HasDefaultValue(TaskEnergyLevel.Medium)
				.IsRequired();

			entity.Property(task => task.ContextTag)
				.HasMaxLength(64);

			entity.Property(task => task.EffortPoints);

			entity.Property(task => task.IsCompleted)
				.IsRequired();

			entity.Property(task => task.CreatedAtUtc)
				.IsRequired();

			entity.Property(task => task.UpdatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(task => task.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(task => new { task.UserId, task.CreatedAtUtc });
			entity.HasIndex(task => new { task.UserId, task.IsCompleted, task.DueAtUtc });
			entity.HasIndex(task => new { task.UserId, task.EnergyLevel, task.ContextTag });
		});

		modelBuilder.Entity<TaskCompletionEvent>(entity =>
		{
			entity.ToTable("TaskCompletionEvents");

			entity.HasKey(completionEvent => completionEvent.Id);

			entity.Property(completionEvent => completionEvent.EventName)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(completionEvent => completionEvent.IdempotencyKey)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(completionEvent => completionEvent.OccurredAtUtc)
				.IsRequired();

			entity.Property(completionEvent => completionEvent.CreatedAtUtc)
				.IsRequired();

			entity.HasOne<TaskItem>()
				.WithMany()
				.HasForeignKey(completionEvent => completionEvent.TaskId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(completionEvent => completionEvent.OwnerId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasIndex(completionEvent => new
				{ completionEvent.TaskId, completionEvent.OwnerId, completionEvent.IdempotencyKey })
				.IsUnique();
			entity.HasIndex(completionEvent => completionEvent.OccurredAtUtc);
			entity.HasIndex(completionEvent => new
				{ completionEvent.OwnerId, completionEvent.EventName, completionEvent.OccurredAtUtc });
		});

		modelBuilder.Entity<XpLedgerEntry>(entity =>
		{
			entity.ToTable("XpLedgerEntries");

			entity.HasKey(xpLedgerEntry => xpLedgerEntry.Id);

			entity.Property(xpLedgerEntry => xpLedgerEntry.EventName)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(xpLedgerEntry => xpLedgerEntry.IdempotencyKey)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(xpLedgerEntry => xpLedgerEntry.XpGranted)
				.IsRequired();

			entity.Property(xpLedgerEntry => xpLedgerEntry.OccurredAtUtc)
				.IsRequired();

			entity.Property(xpLedgerEntry => xpLedgerEntry.CreatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(xpLedgerEntry => xpLedgerEntry.OwnerId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasOne<TaskItem>()
				.WithMany()
				.HasForeignKey(xpLedgerEntry => xpLedgerEntry.TaskId)
				.OnDelete(DeleteBehavior.NoAction);

			entity.HasOne<TaskCompletionEvent>()
				.WithMany()
				.HasForeignKey(xpLedgerEntry => xpLedgerEntry.TaskCompletionEventId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(xpLedgerEntry => xpLedgerEntry.TaskCompletionEventId)
				.IsUnique();
			entity.HasIndex(xpLedgerEntry => new { xpLedgerEntry.OwnerId, xpLedgerEntry.TaskId, xpLedgerEntry.IdempotencyKey })
				.IsUnique();
			entity.HasIndex(xpLedgerEntry => new { xpLedgerEntry.OwnerId, xpLedgerEntry.OccurredAtUtc });
		});

		modelBuilder.Entity<UserStreakSnapshot>(entity =>
		{
			entity.ToTable("UserStreakSnapshots");

			entity.HasKey(snapshot => snapshot.OwnerId);

			entity.Property(snapshot => snapshot.Outcome)
				.HasMaxLength(16)
				.HasConversion(
					outcome => outcome == TaskStreakOutcome.Continue
						? "continue"
						: outcome == TaskStreakOutcome.Reset
							? "reset"
							: "restart",
					value => value == "continue"
						? TaskStreakOutcome.Continue
						: value == "reset"
							? TaskStreakOutcome.Reset
							: TaskStreakOutcome.Restart)
				.IsRequired();

			entity.Property(snapshot => snapshot.CurrentStreakDays)
				.IsRequired();

			entity.Property(snapshot => snapshot.LongestStreakDays)
				.IsRequired();

			entity.Property(snapshot => snapshot.TimeZoneId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(snapshot => snapshot.EvaluationWindowStartUtc)
				.IsRequired();

			entity.Property(snapshot => snapshot.EvaluationWindowEndUtc)
				.IsRequired();

			entity.Property(snapshot => snapshot.RecoveryTokenBalance)
				.HasDefaultValue(0)
				.IsRequired();

			entity.Property(snapshot => snapshot.RecoveryTokenWeekKey)
				.HasMaxLength(16)
				.HasDefaultValue(string.Empty)
				.IsRequired();

			entity.Property(snapshot => snapshot.LastRecoveryTokenGrantedAtUtc);

			entity.Property(snapshot => snapshot.LastRecoveryTokenConsumedAtUtc);

			entity.Property(snapshot => snapshot.LastEvaluationTraceId)
				.HasMaxLength(128)
				.IsRequired();

			entity.Property(snapshot => snapshot.LastEvaluatedAtUtc)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(snapshot => snapshot.OwnerId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(snapshot => new { snapshot.CurrentStreakDays, snapshot.OwnerId });
		});

		modelBuilder.Entity<StreakRecoveryTokenEvent>(entity =>
		{
			entity.ToTable("StreakRecoveryTokenEvents");

			entity.HasKey(tokenEvent => tokenEvent.Id);

			entity.Property(tokenEvent => tokenEvent.EventType)
				.HasMaxLength(16)
				.HasConversion(
					eventType => eventType == StreakRecoveryTokenEventType.Consumed ? "consumed" : "granted",
					value => value == "consumed"
						? StreakRecoveryTokenEventType.Consumed
						: StreakRecoveryTokenEventType.Granted)
				.IsRequired();

			entity.Property(tokenEvent => tokenEvent.TimeZoneId)
				.HasMaxLength(64)
				.IsRequired();

			entity.Property(tokenEvent => tokenEvent.LocalDate)
				.HasMaxLength(10)
				.IsRequired();

			entity.Property(tokenEvent => tokenEvent.WeekKey)
				.HasMaxLength(16)
				.IsRequired();

			entity.Property(tokenEvent => tokenEvent.BalanceAfter)
				.IsRequired();

			entity.Property(tokenEvent => tokenEvent.OccurredAtUtc)
				.IsRequired();

			entity.Property(tokenEvent => tokenEvent.TraceId)
				.HasMaxLength(128)
				.IsRequired();

			entity.HasOne<User>()
				.WithMany()
				.HasForeignKey(tokenEvent => tokenEvent.OwnerId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(tokenEvent => new { tokenEvent.OwnerId, tokenEvent.OccurredAtUtc });
			entity.HasIndex(tokenEvent => new { tokenEvent.OwnerId, tokenEvent.WeekKey });
		});
	}
}
