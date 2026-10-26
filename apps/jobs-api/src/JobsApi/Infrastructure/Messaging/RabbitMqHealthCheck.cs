using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JobsApi.Infrastructure.Messaging;

internal sealed class RabbitMqHealthCheck(RabbitMqConnectionProvider connections) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connections.GetConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ connection is closed.");
        }
#pragma warning disable CA1031 // A health check must report, not throw.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ is unreachable.", ex);
        }
    }
}
