using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Account.Repositories;

public class AccountRepository(TaskTrackerDbContext dbContext) : IAccountRepository
{
    public async Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
