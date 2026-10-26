using JobsApi.Domain;

namespace JobsApi.Infrastructure.Messaging;

/// <summary>
/// Wire format of <c>contracts/job-requested.v1.schema.json</c>. Serialized with
/// <see cref="SnakeCase.JsonOptions"/>, so <c>JobId</c> becomes <c>job_id</c>.
/// </summary>
public sealed record JobRequestedMessage(
    Guid MessageId,
    string MessageType,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid JobId,
    string Name,
    Dataset Dataset,
    Algorithm Algorithm,
    double TestSize)
{
    public const string Type = "job.requested";
    public const int Version = 1;

    public static JobRequestedMessage From(Job job, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(job);

        return new JobRequestedMessage(
            MessageId: Guid.CreateVersion7(now),
            MessageType: Type,
            SchemaVersion: Version,
            OccurredAt: now,
            JobId: job.Id,
            Name: job.Name,
            Dataset: job.Dataset,
            Algorithm: job.Algorithm,
            TestSize: job.TestSize);
    }
}
