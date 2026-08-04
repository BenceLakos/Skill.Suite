using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skill.Suite.Domain.Experts;

namespace Skill.Suite.Infra.Persistence.Configurations;

public sealed class ExpertConfiguration : IEntityTypeConfiguration<Expert>
{
    public void Configure(EntityTypeBuilder<Expert> builder)
    {
        builder.ToTable("experts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.Ignore(x => x.DomainEvents);
    }
}
