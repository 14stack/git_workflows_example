using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace JobsApi.Infrastructure.Messaging;

/// <summary>
/// Owns the single, long-lived AMQP connection for the process (connections are expensive,
/// channels are cheap). The connection is opened lazily so the API can start while the broker
/// is still booting; readiness is reported by <see cref="RabbitMqHealthCheck"/>.
/// </summary>
public sealed class RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            var factory = new ConnectionFactory
            {
                Uri = options.Value.Uri,
                ClientProvidedName = options.Value.ClientName,
                AutomaticRecoveryEnabled = true,
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await RabbitMqTopology.DeclareAsync(channel, cancellationToken);

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _lock.Dispose();
    }
}
