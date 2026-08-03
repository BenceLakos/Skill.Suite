using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Common;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Infra.BackgroundTasks;
using Skill.Suite.Infra.Containers;
using Skill.Suite.Infra.Git;
using Skill.Suite.Infra.GitHost;
using Skill.Suite.Infra.Identity;
using Skill.Suite.Infra.Persistence;
using Skill.Suite.Infra.Security;

namespace Skill.Suite.Infra;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException(
                                   "Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUserAuthService, IdentityAuthService>();
        services.AddScoped<IUserAccountService, IdentityUserAccountService>();

        // Persist the data-protection key ring to a configurable directory so it survives
        // container recreations. The default lives at ~/.aspnet/DataProtection-Keys, which
        // resets every time the container restarts — and once the keys reset, anything
        // previously encrypted (auth cookies, competitor passwords, credential secrets)
        // throws `CryptographicException: The key {…} was not found in the key ring`.
        // The application name is pinned so multiple deployments cannot accidentally share
        // a key ring just because they wrote to the same folder.
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("SkillSuite");

        var keyRingPath = configuration["DataProtection:KeyRingPath"];
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            Directory.CreateDirectory(keyRingPath);
            dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        }

        services.AddSingleton<IPasswordVault, DataProtectionPasswordVault>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 1;
                options.Password.RequiredUniqueChars = 0;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<IdentitySeedOptions>(configuration.GetSection(IdentitySeedOptions.SectionName));
        services.AddHostedService<IdentitySeeder>();

        services.Configure<WebhookOptions>(configuration.GetSection(WebhookOptions.SectionName));
        services.AddSingleton<IBackgroundTaskQueue, ChannelBackgroundTaskQueue>();
        services.AddSingleton<IActiveTestRunRegistry, ActiveTestRunRegistry>();
        services.AddScoped<IGitClient, ProcessGitClient>();
        services.AddScoped<IContainerRunner, ProcessContainerRunner>();

        // BaseAddress carries the trailing /api/v1/ so the client's relative paths stay readable. It must end
        // in a slash: without one, Uri resolution discards the last segment and every call 404s.
        services.AddHttpClient<IGitHostClient, GiteaGitHostClient>(client =>
        {
            var baseUrl = configuration.GetSection(WebhookOptions.SectionName)
                              .GetValue<string>(nameof(WebhookOptions.GitInternalBaseUrl))
                          ?? "http://gitea:3000";

            client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/v1/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHostedService<TestRunWorker>();

        return services;
    }
}
