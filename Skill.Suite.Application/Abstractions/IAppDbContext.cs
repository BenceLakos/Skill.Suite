using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Read/write contract over the application's EF Core DbContext, exposed to the Application
/// layer so handlers can depend on this instead of the concrete AppDbContext in Infra.
/// Per the architecture rules, no repository pattern — handlers use this DbSet surface directly.
/// </summary>
public interface IAppDbContext
{
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
