namespace Skill.Suite.Components.Pages.StarterPackages;

/// <summary>A file an upload did not write, and why, as the administrator is told once the upload is over.</summary>
/// <param name="RelativePath">From the folder uploaded into, as the file was chosen.</param>
/// <param name="Reason">Shown as it is: the command's own message, or the page's for what never reached it.</param>
public sealed record StarterPackageUploadProblem(string RelativePath, string Reason);
