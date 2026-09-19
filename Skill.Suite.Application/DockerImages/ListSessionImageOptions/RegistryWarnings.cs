namespace Skill.Suite.Application.DockerImages.ListSessionImageOptions;

/// <summary>
/// The reasons the registry listing can come back empty that are not failures of the registry itself, and so
/// have no error to borrow a message from.
/// </summary>
internal static class RegistryWarnings
{
    public const string MissingCredential =
        "No Gitea credential is configured, so registry images are not listed. Add one on the Credentials page.";

    public const string TimedOut =
        "Listing the registry images took too long and was stopped; only preconfigured images are offered.";
}
