using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Infra.Persistence.Configurations;

public sealed class TestRunConfiguration : IEntityTypeConfiguration<TestRun>
{
    public void Configure(EntityTypeBuilder<TestRun> builder)
    {
        builder.ToTable("test_runs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionId).IsRequired();
        builder.Property(x => x.CompetitorId);

        builder.Property(x => x.RepositoryUrl).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.RepositoryName).HasMaxLength(200);
        builder.Property(x => x.Branch).HasMaxLength(200);
        builder.Property(x => x.CommitSha).HasMaxLength(64);

        builder.Property(x => x.FolderName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.JudgementImage).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasConversion<int>();

        builder.Property(x => x.StartedAt);
        builder.Property(x => x.FinishedAt);
        builder.Property(x => x.FailureReason).HasColumnType("text");

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasMany(x => x.Fixtures)
            .WithOne()
            .HasForeignKey(x => x.TestRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.CompetitorId);
        builder.HasIndex(x => x.CreatedAt);

        builder.Ignore(x => x.DomainEvents);
    }
}
