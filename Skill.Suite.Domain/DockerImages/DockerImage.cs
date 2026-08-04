using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages.Events;

namespace Skill.Suite.Domain.DockerImages;

public sealed class DockerImage : AuditableEntity<Guid>
{
    private DockerImage() { }

    public string Name { get; private set; } = string.Empty;
    public string ImageName { get; private set; } = string.Empty;
    public DockerImageSource Source { get; private set; }

    // Build-only fields. Null when Source == Pull.
    public string? BuildContext { get; private set; }
    public string? DockerfilePath { get; private set; }
    public Dictionary<string, string> BuildArgs { get; private set; } = new();

    /// <summary>Registry credential used for pushing built images or pulling private ones.</summary>
    public Guid? NexusCredentialId { get; private set; }

    public static DockerImage CreateBuildable(
        string name,
        string imageName,
        string buildContext,
        string? dockerfilePath,
        IReadOnlyDictionary<string, string> buildArgs,
        Guid? nexusCredentialId)
    {
        var image = new DockerImage
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ImageName = imageName.Trim(),
            Source = DockerImageSource.Build,
            BuildContext = buildContext.Trim(),
            DockerfilePath = NormalizeOptional(dockerfilePath),
            BuildArgs = new Dictionary<string, string>(buildArgs),
            NexusCredentialId = nexusCredentialId,
        };

        image.RaiseDomainEvent(new DockerImageCreatedEvent(image.Id));
        return image;
    }

    public static DockerImage CreatePullable(
        string name,
        string imageName,
        Guid? nexusCredentialId)
    {
        var image = new DockerImage
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ImageName = imageName.Trim(),
            Source = DockerImageSource.Pull,
            BuildContext = null,
            DockerfilePath = null,
            BuildArgs = new Dictionary<string, string>(),
            NexusCredentialId = nexusCredentialId,
        };

        image.RaiseDomainEvent(new DockerImageCreatedEvent(image.Id));
        return image;
    }

    public void UpdateBuildable(
        string name,
        string imageName,
        string buildContext,
        string? dockerfilePath,
        IReadOnlyDictionary<string, string> buildArgs,
        Guid? nexusCredentialId)
    {
        Name = name.Trim();
        ImageName = imageName.Trim();
        Source = DockerImageSource.Build;
        BuildContext = buildContext.Trim();
        DockerfilePath = NormalizeOptional(dockerfilePath);
        BuildArgs = new Dictionary<string, string>(buildArgs);
        NexusCredentialId = nexusCredentialId;

        RaiseDomainEvent(new DockerImageUpdatedEvent(Id));
    }

    public void UpdatePullable(
        string name,
        string imageName,
        Guid? nexusCredentialId)
    {
        Name = name.Trim();
        ImageName = imageName.Trim();
        Source = DockerImageSource.Pull;
        BuildContext = null;
        DockerfilePath = null;
        BuildArgs = new Dictionary<string, string>();
        NexusCredentialId = nexusCredentialId;

        RaiseDomainEvent(new DockerImageUpdatedEvent(Id));
    }

    public Result MarkPushed(string tag)
    {
        if (Source != DockerImageSource.Build)
            return Result.Failure(DockerImageErrors.PushNotAllowedForPulledImage);

        if (NexusCredentialId is null)
            return Result.Failure(DockerImageErrors.MissingNexusCredential);

        RaiseDomainEvent(new DockerImagePushedEvent(Id, tag));
        return Result.Success();
    }

    public void MarkRemoved() =>
        RaiseDomainEvent(new DockerImageRemovedEvent(Id));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
