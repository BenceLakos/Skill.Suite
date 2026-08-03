using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Infra.Persistence.Configurations;

public sealed class TestFixtureResultConfiguration : IEntityTypeConfiguration<TestFixtureResult>
{
    public void Configure(EntityTypeBuilder<TestFixtureResult> builder)
    {
        builder.ToTable("test_fixture_results");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TestRunId).IsRequired();
        // Width comes from the domain constant that also truncates at ingest, so the two cannot drift.
        builder.Property(x => x.Name).IsRequired().HasMaxLength(TestRunLimits.NameMaxLength);
        builder.Property(x => x.Kind).IsRequired().HasConversion<int>();
        builder.Property(x => x.TestsRun).IsRequired();
        builder.Property(x => x.TestsPassed).IsRequired();
        builder.Property(x => x.TestsFailed).IsRequired();
        builder.Property(x => x.DurationMs);
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.FinishedAt);
        builder.Property(x => x.Quality);

        builder.HasMany(x => x.UnitTests)
            .WithOne()
            .HasForeignKey(x => x.FixtureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TestRunId);
    }
}
