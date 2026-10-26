using RabbitMQ.Client;

namespace JobsApi.Infrastructure.Messaging;

/// <summary>
/// Broker topology shared with the ML worker. Keep in sync with <c>contracts/README.md</c>
/// and <c>apps/ml-worker/src/ml_worker/topology.py</c>: both sides declare it idempotently.
/// </summary>
public static class RabbitMqTopology
{
    public const string Exchange = "jobs";
    public const string DeadLetterExchange = "jobs.dlx";
    public const string JobRequestedRoutingKey = "job.requested";
    public const string WorkerQueue = "ml-worker.jobs";
    public const string WorkerDeadLetterQueue = "ml-worker.jobs.dlq";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var quorum = new Dictionary<string, object?> { ["x-queue-type"] = "quorum" };

        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(WorkerDeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: quorum, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(WorkerDeadLetterQueue, DeadLetterExchange, routingKey: string.Empty, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            WorkerQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>(quorum) { ["x-dead-letter-exchange"] = DeadLetterExchange },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(WorkerQueue, Exchange, JobRequestedRoutingKey, cancellationToken: cancellationToken);
    }
}
