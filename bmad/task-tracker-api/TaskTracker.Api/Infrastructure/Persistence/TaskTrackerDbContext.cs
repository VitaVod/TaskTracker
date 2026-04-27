using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Infrastructure.Persistence;

public class TaskTrackerDbContext(DbContextOptions<TaskTrackerDbContext> options) : DbContext(options)
{
	public DbSet<User> Users => Set<User>();

	public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

	public DbSet<PasswordRecoveryToken> PasswordRecoveryTokens => Set<PasswordRecoveryToken>();

	public DbSet<TaskItem> Tasks => Set<TaskItem>();

	public DbSet<TaskCompletionEvent> TaskCompletionEvents => Set<TaskCompletionEvent>();

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

			entity.Property(user => user.CreatedAtUtc)
				.IsRequired();

			entity.Property(user => user.ModifiedAtUtc)
				.IsRequired();

			entity.HasIndex(user => user.Email)
				.IsUnique();
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
		});
	}
}
