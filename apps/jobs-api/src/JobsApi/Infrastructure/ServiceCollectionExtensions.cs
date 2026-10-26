using JobsApi.Infrastructure.Messaging;
using JobsApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobsApi.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "JobsDb";

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<JobsDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IJobRepository, JobRepository>();
        return services;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IJobDispatcher, RabbitMqJobDispatcher>();
        return services;
    }

    public static IHealthChecksBuilder AddReadinessChecks(this IHealthChecksBuilder builder)
    {
        builder.AddDbContextCheck<JobsDbContext>("postgres", tags: [HealthTags.Ready]);
        builder.AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [HealthTags.Ready]);
        return builder;
    }
}

public static class HealthTags
{
    public const string Ready = "ready";
}
