using System.Text.Json;
using RabbitMQ.Client;

namespace JobsApi.Infrastructure.Messaging;

public interface IJobDispatcher
{
    /// <summary>Publishes the message and waits for the broker to confirm it was routed and persisted.</summary>
    Task DispatchAsync(JobRequestedMessage message, CancellationToken cancellationToken);
}

internal sealed class RabbitMqJobDispatcher(RabbitMqConnectionProvider connections) : IJobDispatcher
{
    private static readonly CreateChannelOptions ConfirmedChannel = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    public async Task DispatchAsync(JobRequestedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var connection = await connections.GetConnectionAsync(cancellationToken);

        // A short-lived channel per publish keeps this simple and thread-safe; fine at this scale.
        await using var channel = await connection.CreateChannelAsync(ConfirmedChannel, cancellationToken);

        var properties = new BasicProperties
        {
            MessageId = message.MessageId.ToString(),
            Type = message.MessageType,
            ContentType = "application/json",
            Persistent = true,
            Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message, SnakeCase.JsonOptions);

        // With confirmation tracking enabled this throws if the broker nacks or returns the message.
        await channel.BasicPublishAsync(
            RabbitMqTopology.Exchange,
            RabbitMqTopology.JobRequestedRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
