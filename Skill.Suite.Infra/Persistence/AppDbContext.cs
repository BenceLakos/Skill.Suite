using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Common;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;
using Skill.Suite.Domain.Credentials;
using Skill.Suite.Domain.DockerImages;
using Skill.Suite.Domain.Experts;
using Skill.Suite.Domain.Sessions;
using Skill.Suite.Domain.TestRuns;
using Skill.Suite.Infra.Identity;

namespace Skill.Suite.Infra.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService currentUser,
    IDomainEventDispatcher domainEventDispatcher)
    : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>(options), IAppDbContext
{
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionCompetitor> SessionCompetitors => Set<SessionCompetitor>();
    public DbSet<Competitor> Competitors => Set<Competitor>();
    public DbSet<Expert> Experts => Set<Expert>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<DockerImage> DockerImages => Set<DockerImage>();
    public DbSet<TestRun> TestRuns => Set<TestRun>();
    public DbSet<TestFixtureResult> TestFixtureResults => Set<TestFixtureResult>();
    public DbSet<UnitTestResult> UnitTestResults => Set<UnitTestResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var events = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

        ApplyAuditing();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (events.Count > 0)
            await domainEventDispatcher.DispatchAsync(events, cancellationToken);

        foreach (var entity in entitiesWithEvents)
            entity.ClearDomainEvents();

        return result;
    }

    private void ApplyAuditing()
    {
        var now = DateTime.UtcNow;
        var actor = currentUser.UserName ?? currentUser.UserId?.ToString();

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = actor;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
            }
        }
    }
}
