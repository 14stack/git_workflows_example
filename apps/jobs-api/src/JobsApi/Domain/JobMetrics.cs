namespace JobsApi.Domain;

/// <summary>Evaluation results reported by the ML worker (stored as <c>jsonb</c>).</summary>
public sealed record JobMetrics(
    double Accuracy,
    double F1Macro,
    int TrainSamples,
    int TestSamples,
    long DurationMs);
