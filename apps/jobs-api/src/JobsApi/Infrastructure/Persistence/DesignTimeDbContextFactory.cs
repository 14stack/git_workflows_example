using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobsApi.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> (migrations, migration bundle) so the tooling never has to boot the web host.
/// The bundle receives the real connection string via <c>--connection</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JobsDbContext>
{
    public JobsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__JobsDb")
            ?? "Host=localhost;Port=5432;Database=jobs;Username=jobs;Password=jobs";

        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new JobsDbContext(options);
    }
}
