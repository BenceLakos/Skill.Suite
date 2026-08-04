namespace Skill.Suite.Domain.DockerImages;

public enum DockerImageSource
{
    /// <summary>Image is built locally from a Dockerfile (BuildContext + DockerfilePath).</summary>
    Build = 0,

    /// <summary>Image is pulled as-is from a registry — no Dockerfile, no build args.</summary>
    Pull = 1,
}
