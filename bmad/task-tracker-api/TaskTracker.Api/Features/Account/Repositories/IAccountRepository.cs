using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Account.Repositories;

public interface IAccountRepository
{
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
