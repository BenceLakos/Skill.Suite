using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Authorization;
using Skill.Suite.Infra.Persistence;

namespace Skill.Suite.Infra.Identity;

public sealed class IdentitySeeder(
    IServiceProvider services,
    IOptions<IdentitySeedOptions> options,
    ILogger<IdentitySeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.MigrateAsync(cancellationToken);

        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to seed role '{roleName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
                logger.LogInformation("Seeded role {Role}", roleName);
            }
        }

        var admin = options.Value.Admin;
        if (admin is null || string.IsNullOrWhiteSpace(admin.Username) || string.IsNullOrWhiteSpace(admin.Password))
            return;

        var existing = await userManager.FindByNameAsync(admin.Username);
        if (existing is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = admin.Username,
            FullName = admin.FullName,
        };

        var createResult = await userManager.CreateAsync(user, admin.Password);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(
                $"Failed to seed admin user: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Admin);
        if (!roleResult.Succeeded)
            throw new InvalidOperationException(
                $"Failed to assign admin role: {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");

        logger.LogInformation("Seeded admin user {Username}", admin.Username);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
