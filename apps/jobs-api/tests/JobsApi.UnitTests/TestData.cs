using JobsApi.Domain;

namespace JobsApi.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public static Job PendingJob(string name = "Iris baseline") =>
        Job.Create(name, Dataset.Iris, Algorithm.RandomForest, 0.2, Now);
}
