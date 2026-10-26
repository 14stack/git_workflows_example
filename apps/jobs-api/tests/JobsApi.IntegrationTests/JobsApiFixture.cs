using JobsApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(JobsApi.IntegrationTests.JobsApiFixture))]

// Tests share one database and one broker, so run them one at a time.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace JobsApi.IntegrationTests;

/// <summary>
/// Boots real PostgreSQL and RabbitMQ containers once per test run and hosts the API
/// in-memory against them. Images match <c>deploy/compose/compose.yaml</c>.
/// </summary>
public sealed class JobsApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("jobs")
        .WithUsername("jobs")
        .WithPassword("jobs")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("Fixture has not been initialized.");

    public HttpClient CreateClient() => Factory.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:JobsDb", _postgres.GetConnectionString());
            builder.UseSetting("RabbitMq:Uri", _rabbitMq.GetConnectionString());
        });

        // Apply the real migrations, exactly like the db-migrate container does.
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JobsDbContext>().Database.MigrateAsync();
    }

    public async Task<IConnection> ConnectToBrokerAsync() =>
        await new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) }.CreateConnectionAsync();

    public async Task ResetAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JobsDbContext>().Jobs.ExecuteDeleteAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _rabbitMq.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
