using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Skill.Suite.Domain.Competitors;
using Skill.Suite.Domain.Credentials;
using Skill.Suite.Domain.DockerImages;
using Skill.Suite.Domain.Experts;
using Skill.Suite.Domain.Sessions;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Read/write contract over the application's EF Core DbContext, exposed to the Application
/// layer so handlers can depend on this instead of the concrete AppDbContext in Infra.
/// Per the architecture rules, no repository pattern — handlers use this DbSet surface directly.
/// </summary>
public interface IAppDbContext
{
    DatabaseFacade Database { get; }
    DbSet<Session> Sessions { get; }
    DbSet<SessionCompetitor> SessionCompetitors { get; }
    DbSet<Competitor> Competitors { get; }
    DbSet<Expert> Experts { get; }
    DbSet<Credential> Credentials { get; }
    DbSet<DockerImage> DockerImages { get; }
    DbSet<TestRun> TestRuns { get; }
    DbSet<TestFixtureResult> TestFixtureResults { get; }
    DbSet<UnitTestResult> UnitTestResults { get; }

    /// <summary>
    /// Forces an entity into the <c>Added</c> state, bypassing EF's "navigation
    /// collection" heuristic that would otherwise mistake an application-generated Guid
    /// key for an existing database row. Use this for fixtures/unit-test rows created
    /// during a test run.
    /// </summary>
    EntityEntry Add(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
