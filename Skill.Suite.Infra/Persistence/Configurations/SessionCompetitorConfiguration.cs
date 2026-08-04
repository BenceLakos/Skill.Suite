using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Infra.Persistence.Configurations;

public sealed class SessionCompetitorConfiguration : IEntityTypeConfiguration<SessionCompetitor>
{
    public void Configure(EntityTypeBuilder<SessionCompetitor> builder)
    {
        builder.ToTable("session_competitors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionId).IsRequired();
        builder.Property(x => x.CompetitorId).IsRequired();
        builder.Property(x => x.RepositoryName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RepositoryUrl).HasMaxLength(1000);
        builder.Property(x => x.ProvisionStatus).IsRequired().HasConversion<int>();
        builder.Property(x => x.ProvisionError).HasMaxLength(SessionCompetitor.MaxErrorLength);
        builder.Property(x => x.ProvisionedAt);

        // The webhook resolves an incoming push by (session, repository name), so this index is on the read
        // path of every submission. Unique because two rows for one repository would make attribution
        // ambiguous — exactly the failure the enrolment row exists to remove.
        builder.HasIndex(x => new { x.SessionId, x.RepositoryName }).IsUnique();
        builder.HasIndex(x => new { x.SessionId, x.CompetitorId }).IsUnique();
        builder.HasIndex(x => x.CompetitorId);

        builder.HasOne<Session>()
            .WithMany(s => s.Competitors)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // No cascade from the competitor side: deleting a competitor mid-competition must not silently drop
        // the mapping that explains who owns an existing repository.
        builder.HasIndex(x => x.ProvisionStatus);

        builder.Ignore(x => x.DomainEvents);
    }
}
