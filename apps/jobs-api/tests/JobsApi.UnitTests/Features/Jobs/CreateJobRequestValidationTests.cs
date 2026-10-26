using System.ComponentModel.DataAnnotations;
using JobsApi.Domain;
using JobsApi.Features.Jobs;

namespace JobsApi.UnitTests.Features.Jobs;

/// <summary>
/// The endpoint relies on the built-in Minimal API validation, which evaluates these same
/// DataAnnotations. The integration tests cover the HTTP 400 response itself.
/// </summary>
public sealed class CreateJobRequestValidationTests
{
    [Fact]
    public void Valid_request_has_no_errors()
    {
        var errors = Validate(ValidRequest());

        errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("   ")]
    [InlineData("  ab  ")]
    public void Name_must_be_present_and_long_enough_after_trimming(string name)
    {
        var errors = Validate(new CreateJobRequest { Name = name, Dataset = Dataset.Iris, Algorithm = Algorithm.Svm });

        errors.ShouldContain(e => e.MemberNames.Contains(nameof(CreateJobRequest.Name)));
    }

    [Fact]
    public void Name_must_not_exceed_maximum_length()
    {
        var errors = Validate(new CreateJobRequest
        {
            Name = new string('x', Job.NameMaxLength + 1),
            Dataset = Dataset.Iris,
            Algorithm = Algorithm.Svm,
        });

        errors.ShouldContain(e => e.MemberNames.Contains(nameof(CreateJobRequest.Name)));
    }

    [Fact]
    public void Dataset_and_algorithm_are_required()
    {
        var errors = Validate(new CreateJobRequest { Name = "Valid name" });

        errors.ShouldContain(e => e.MemberNames.Contains(nameof(CreateJobRequest.Dataset)));
        errors.ShouldContain(e => e.MemberNames.Contains(nameof(CreateJobRequest.Algorithm)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.09)]
    [InlineData(0.51)]
    public void Test_size_must_be_within_range(double testSize)
    {
        var errors = Validate(new CreateJobRequest
        {
            Name = "Valid name",
            Dataset = Dataset.Iris,
            Algorithm = Algorithm.Svm,
            TestSize = testSize,
        });

        errors.ShouldContain(e => e.MemberNames.Contains(nameof(CreateJobRequest.TestSize)));
    }

    private static CreateJobRequest ValidRequest() => new()
    {
        Name = "Iris baseline",
        Dataset = Dataset.Iris,
        Algorithm = Algorithm.LogisticRegression,
        TestSize = 0.3,
    };

    private static List<ValidationResult> Validate(CreateJobRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
