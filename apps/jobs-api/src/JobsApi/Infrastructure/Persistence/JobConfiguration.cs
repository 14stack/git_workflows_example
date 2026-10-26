using JobsApi.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobsApi.Infrastructure.Persistence;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs", t =>
        {
            t.HasCheckConstraint("ck_jobs_status", "status IN ('pending', 'running', 'succeeded', 'failed')");
            t.HasCheckConstraint("ck_jobs_test_size", "test_size >= 0.1 AND test_size <= 0.5");
        });

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.Name).HasMaxLength(Job.NameMaxLength).IsRequired();
        builder.Property(j => j.Dataset).HasConversion<SnakeCaseEnumConverter<Dataset>>().HasMaxLength(32);
        builder.Property(j => j.Algorithm).HasConversion<SnakeCaseEnumConverter<Algorithm>>().HasMaxLength(32);
        builder.Property(j => j.Status).HasConversion<SnakeCaseEnumConverter<JobStatus>>().HasMaxLength(16);
        builder.Property(j => j.Error).HasMaxLength(2000);

        // The worker writes metrics as a snake_case JSON document.
        builder.OwnsOne(j => j.Metrics, metrics =>
        {
            metrics.ToJson("metrics");
            metrics.Property(m => m.Accuracy).HasJsonPropertyName("accuracy");
            metrics.Property(m => m.F1Macro).HasJsonPropertyName("f1_macro");
            metrics.Property(m => m.TrainSamples).HasJsonPropertyName("train_samples");
            metrics.Property(m => m.TestSamples).HasJsonPropertyName("test_samples");
            metrics.Property(m => m.DurationMs).HasJsonPropertyName("duration_ms");
        });

        builder.HasIndex(j => j.CreatedAt).IsDescending();
    }
}
