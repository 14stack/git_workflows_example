using JobsApi.Domain;

namespace JobsApi.Features.Jobs;

public sealed record JobMetricsResponse(
    double Accuracy,
    double F1Macro,
    int TrainSamples,
    int TestSamples,
    long DurationMs);

public sealed record JobResponse(
    Guid Id,
    string Name,
    Dataset Dataset,
    Algorithm Algorithm,
    double TestSize,
    JobStatus Status,
    JobMetricsResponse? Metrics,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    public static JobResponse From(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return new JobResponse(
            job.Id,
            job.Name,
            job.Dataset,
            job.Algorithm,
            job.TestSize,
            job.Status,
            job.Metrics is { } m
                ? new JobMetricsResponse(m.Accuracy, m.F1Macro, m.TrainSamples, m.TestSamples, m.DurationMs)
                : null,
            job.Error,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt);
    }
}
