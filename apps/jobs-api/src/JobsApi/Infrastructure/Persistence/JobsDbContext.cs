using JobsApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobsApi.Infrastructure.Persistence;

/// <summary>
/// The API owns the database schema (EF Core migrations). The ML worker only updates
/// status/metrics columns of existing rows.
/// </summary>
public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobsDbContext).Assembly);
    }
}
