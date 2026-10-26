using JobsApi.Domain;
using JobsApi.Infrastructure;

namespace JobsApi.UnitTests.Infrastructure;

public sealed class SnakeCaseTests
{
    [Theory]
    [InlineData(Dataset.Iris, "iris")]
    [InlineData(Dataset.Wine, "wine")]
    [InlineData(Dataset.BreastCancer, "breast_cancer")]
    public void Dataset_round_trips(Dataset value, string expected)
    {
        value.ToSnakeCase().ShouldBe(expected);
        SnakeCase.ParseSnakeCase<Dataset>(expected).ShouldBe(value);
    }

    [Theory]
    [InlineData(Algorithm.LogisticRegression, "logistic_regression")]
    [InlineData(Algorithm.RandomForest, "random_forest")]
    [InlineData(Algorithm.Svm, "svm")]
    public void Algorithm_round_trips(Algorithm value, string expected)
    {
        value.ToSnakeCase().ShouldBe(expected);
        SnakeCase.ParseSnakeCase<Algorithm>(expected).ShouldBe(value);
    }

    [Fact]
    public void Unknown_value_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SnakeCase.ParseSnakeCase<JobStatus>("exploded"));
    }
}
