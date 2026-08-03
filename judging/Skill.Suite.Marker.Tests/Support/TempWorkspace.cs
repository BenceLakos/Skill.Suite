namespace Skill.Suite.Marker.Tests.Support;

/// <summary>A throwaway directory for tests that write files.</summary>
internal sealed class TempWorkspace : IDisposable
{
    internal string Path { get; } = Directory.CreateTempSubdirectory("skill-marker-").FullName;

    /// <summary>Writes a file inside the workspace and returns its path.</summary>
    internal string WriteFile(string name, string content)
    {
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Copies a fixture into the workspace so tests can mutate it.</summary>
    internal string CopyFixture(string fixtureName, string? targetName = null)
    {
        var path = System.IO.Path.Combine(Path, targetName ?? fixtureName);
        File.Copy(Fixture.Path(fixtureName), path, overwrite: true);
        return path;
    }

    /// <summary>Path inside the workspace, whether or not it exists yet.</summary>
    internal string PathTo(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ }
    }
}
