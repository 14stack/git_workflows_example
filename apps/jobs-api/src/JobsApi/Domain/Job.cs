namespace JobsApi.Domain;

/// <summary>An ML training job. Created by the API, executed and completed by the ML worker.</summary>
public sealed class Job
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 100;
    public const double MinTestSize = 0.1;
    public const double MaxTestSize = 0.5;

    // Required by EF Core.
    private Job()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Dataset Dataset { get; private set; }

    public Algorithm Algorithm { get; private set; }

    public double TestSize { get; private set; }

    public JobStatus Status { get; private set; }

    /// <summary>Written by the ML worker when the job succeeds.</summary>
    public JobMetrics? Metrics { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static Job Create(string name, Dataset dataset, Algorithm algorithm, double testSize, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        if (trimmed.Length is < NameMinLength or > NameMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name), $"Name must be between {NameMinLength} and {NameMaxLength} characters.");
        }

        if (testSize is < MinTestSize or > MaxTestSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(testSize), $"Test size must be between {MinTestSize} and {MaxTestSize}.");
        }

        return new Job
        {
            // Version 7 GUIDs are time-ordered, which keeps the primary key index append-only.
            Id = Guid.CreateVersion7(now),
            Name = trimmed,
            Dataset = dataset,
            Algorithm = algorithm,
            TestSize = testSize,
            Status = JobStatus.Pending,
            CreatedAt = now,
        };
    }

    /// <summary>Marks a job that never reached the broker as failed.</summary>
    public void MarkDispatchFailed(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status != JobStatus.Pending)
        {
            throw new InvalidOperationException($"Only pending jobs can fail dispatch, but job {Id} is {Status}.");
        }

        Status = JobStatus.Failed;
        Error = reason;
        CompletedAt = now;
    }
}
