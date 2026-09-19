namespace Skill.Suite.Application.Competitors.Accounts;

public sealed class MsSqlOptions
{
    public const string SectionName = "MsSql";

    /// <summary>
    /// The SQL Server as THIS PROCESS reaches it, over the container network — not as a competitor's laptop
    /// reaches it.
    /// </summary>
    /// <remarks>
    /// The default names the compose service, which resolves because the app container joins the same external
    /// <c>skill-suite</c> network the server is on. A public name here would work from a developer machine and
    /// fail in the deployment that matters.
    /// </remarks>
    public string Server { get; set; } = "mssql,1433";

    /// <summary>
    /// Accept the server's certificate without validating its chain.
    /// </summary>
    /// <remarks>
    /// On by default and effectively mandatory: the image presents a self-signed certificate and a competition
    /// LAN has no CA to have signed a real one. The connection is still encrypted.
    /// </remarks>
    public bool TrustServerCertificate { get; set; } = true;

    /// <summary>How long to wait for the connection itself. Short: the UI blocks on it.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 5;

    /// <summary>How long a single statement may run. <c>CREATE DATABASE</c> is the slow one.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
