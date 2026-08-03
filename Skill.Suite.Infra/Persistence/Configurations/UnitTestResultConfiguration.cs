using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Infra.Persistence.Configurations;

public sealed class UnitTestResultConfiguration : IEntityTypeConfiguration<UnitTestResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    // Reference equality keeps the comparer cheap; AppendEvent rebuilds the list each
    // call so EF sees a new instance and writes on every save.
    private static readonly ValueComparer<List<TestEventRecord>> EventsComparer = new(
        (a, b) => ReferenceEquals(a, b),
        v => v == null ? 0 : v.Count,
        v => v.ToList());

    public void Configure(EntityTypeBuilder<UnitTestResult> builder)
    {
        builder.ToTable("test_unit_results");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FixtureId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(TestRunLimits.NameMaxLength);
        builder.Property(x => x.Outcome).IsRequired().HasConversion<int>();
        builder.Property(x => x.Aspect).HasMaxLength(TestRunLimits.AspectMaxLength);
        builder.Property(x => x.AspectCompetitorVisible).IsRequired();
        builder.Property(x => x.DurationMs);
        builder.Property(x => x.Error).HasColumnType("text");
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.FinishedAt);

        builder.Property(x => x.Events)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<TestEventRecord>>(v, JsonOptions)
                     ?? new List<TestEventRecord>())
            .Metadata.SetValueComparer(EventsComparer);

        builder.HasIndex(x => x.FixtureId);
    }
}
