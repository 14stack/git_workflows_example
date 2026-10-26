using System.ComponentModel.DataAnnotations;
using JobsApi.Domain;
using JobsApi.Infrastructure;
using JobsApi.Infrastructure.Messaging;
using JobsApi.Infrastructure.Persistence;

namespace JobsApi.Features.Jobs;

/// <summary>Body of <c>POST /api/jobs</c>. Validated by the built-in Minimal API validation.</summary>
public sealed class CreateJobRequest
{
    [Required]
    [TrimmedLength(Job.NameMinLength, Job.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public Dataset? Dataset { get; init; }

    [Required]
    public Algorithm? Algorithm { get; init; }

    [Range(Job.MinTestSize, Job.MaxTestSize)]
    public double TestSize { get; init; } = 0.2;
}

public enum CreateJobOutcome
{
    Dispatched,
    DispatchFailed,
}

public sealed record CreateJobResult(CreateJobOutcome Outcome, Job Job);

/// <summary>
/// Persists a new job and hands it to the ML worker. If the broker is unavailable the job is
/// kept (for auditability) but marked <see cref="JobStatus.Failed"/>.
/// </summary>
public sealed partial class CreateJobHandler(
    IJobRepository repository,
    IJobDispatcher dispatcher,
    TimeProvider time,
    ILogger<CreateJobHandler> logger)
{
    public const string DispatchFailedError = "The job could not be handed over to the ML worker. Please retry.";

    public async Task<CreateJobResult> HandleAsync(CreateJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var job = Job.Create(
            request.Name,
            request.Dataset ?? throw new ArgumentException("Dataset is required.", nameof(request)),
            request.Algorithm ?? throw new ArgumentException("Algorithm is required.", nameof(request)),
            request.TestSize,
            time.GetUtcNow());

        repository.Add(job);
        await repository.SaveChangesAsync(cancellationToken);

        try
        {
            await dispatcher.DispatchAsync(JobRequestedMessage.From(job, time.GetUtcNow()), cancellationToken);
        }
#pragma warning disable CA1031 // Any broker failure must be recorded on the job instead of surfacing as a 500.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogDispatchFailed(logger, ex, job.Id);
            job.MarkDispatchFailed(DispatchFailedError, time.GetUtcNow());
            await repository.SaveChangesAsync(CancellationToken.None);
            return new CreateJobResult(CreateJobOutcome.DispatchFailed, job);
        }

        LogDispatched(logger, job.Id, job.Dataset, job.Algorithm);
        return new CreateJobResult(CreateJobOutcome.Dispatched, job);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatched job {JobId} ({Dataset}/{Algorithm})")]
    private static partial void LogDispatched(ILogger logger, Guid jobId, Dataset dataset, Algorithm algorithm);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispatch job {JobId}")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception, Guid jobId);
}
