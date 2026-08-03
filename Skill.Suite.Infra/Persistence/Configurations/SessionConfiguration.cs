using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Infra.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    // Reference equality keeps the comparer cheap; UpdateDetails always assigns a fresh
    // List<SessionDockerImage> so EF sees the new instance and writes on every save.
    private static readonly ValueComparer<List<SessionDockerImage>> DockerImagesComparer = new(
        (a, b) => ReferenceEquals(a, b),
        v => v == null ? 0 : v.Count,
        v => v.ToList());

    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.StartsAt).IsRequired();
        builder.Property(x => x.EndsAt).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasConversion<int>();

        builder.Property(x => x.TemplateFolder).HasMaxLength(1000);
        builder.Property(x => x.JudgementImage).HasMaxLength(500);

        builder.Property(x => x.DatabaseName).HasMaxLength(120);
        builder.Property(x => x.DatabaseReadAccess).IsRequired();
        builder.Property(x => x.DatabaseWriteAccess).IsRequired();

        builder.Property(x => x.GitCredentialId);
        builder.Property(x => x.JudgementImagePullCredentialId);

        builder.Property(x => x.DockerImages)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<SessionDockerImage>>(v, JsonOptions)
                     ?? new List<SessionDockerImage>())
            .Metadata.SetValueComparer(DockerImagesComparer);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.GitCredentialId);
        builder.HasIndex(x => x.JudgementImagePullCredentialId);
        builder.Ignore(x => x.DomainEvents);
    }
}
