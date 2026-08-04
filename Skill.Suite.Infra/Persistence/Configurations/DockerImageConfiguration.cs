using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Infra.Persistence.Configurations;

public sealed class DockerImageConfiguration : IEntityTypeConfiguration<DockerImage>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private static readonly ValueComparer<Dictionary<string, string>> BuildArgsComparer = new(
        (a, b) => (a == null && b == null)
                  || (a != null && b != null && a.Count == b.Count && !a.Except(b).Any()),
        v => v == null
            ? 0
            : v.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
        v => new Dictionary<string, string>(v));

    public void Configure(EntityTypeBuilder<DockerImage> builder)
    {
        builder.ToTable("docker_images");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.ImageName).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Source).IsRequired().HasConversion<int>();
        builder.Property(x => x.BuildContext).HasMaxLength(1000);
        builder.Property(x => x.DockerfilePath).HasMaxLength(1000);
        builder.Property(x => x.NexusCredentialId);

        builder.Property(x => x.BuildArgs)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions)
                     ?? new Dictionary<string, string>())
            .Metadata.SetValueComparer(BuildArgsComparer);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.NexusCredentialId);
        builder.Ignore(x => x.DomainEvents);
    }
}
