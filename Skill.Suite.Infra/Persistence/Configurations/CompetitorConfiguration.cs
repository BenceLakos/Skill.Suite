using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skill.Suite.Domain.Competitors;

namespace Skill.Suite.Infra.Persistence.Configurations;

public sealed class CompetitorConfiguration : IEntityTypeConfiguration<Competitor>
{
    public void Configure(EntityTypeBuilder<Competitor> builder)
    {
        builder.ToTable("competitors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username).IsRequired().HasMaxLength(120);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.EncryptedPassword).IsRequired();
        builder.Property(x => x.IpAddress).IsRequired().HasMaxLength(45);

        // Nullable and the same width: an optional second device, holding an address of the same kind.
        builder.Property(x => x.MobileIpAddress).HasMaxLength(45);
        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(3).IsFixedLength();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Username).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
