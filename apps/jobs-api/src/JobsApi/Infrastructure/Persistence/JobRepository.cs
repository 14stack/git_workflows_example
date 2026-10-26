using JobsApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobsApi.Infrastructure.Persistence;

/// <summary>Thin persistence port so handlers can be unit tested without a database.</summary>
public interface IJobRepository
{
    void Add(Job job);

    Task<IReadOnlyList<Job>> ListAsync(int limit, CancellationToken cancellationToken);

    Task<Job?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed class JobRepository(JobsDbContext db) : IJobRepository
{
    public void Add(Job job) => db.Jobs.Add(job);

    public async Task<IReadOnlyList<Job>> ListAsync(int limit, CancellationToken cancellationToken) =>
        await db.Jobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<Job?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
