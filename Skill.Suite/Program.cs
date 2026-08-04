using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using Scalar.AspNetCore;
using Skill.Suite.Application;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Components;
using Skill.Suite.Endpoints;
using Skill.Suite.Infra;
using Skill.Suite.Services;
using Skill.Suite.Services.Theme;
using Skill.Suite.Services.Translation;

var builder = WebApplication.CreateBuilder(args);

// --- Blazor / MudBlazor UI -----------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddCascadingAuthenticationState();

// --- Authentication ------------------------------------------------------------------
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<Skill.Suite.Application.TestRuns.ITestRunChangeNotifier, TestRunChangeNotifier>();

// --- Translation + Theme -------------------------------------------------------------
builder.Services.AddSingleton<TranslationStore>();
builder.Services.AddScoped<ITranslator, JsonTranslator>();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<ThemePersistence>();

// --- Application & Infrastructure ----------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Minimal APIs / OpenAPI / Scalar -------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

// --- HTTP pipeline -------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAccountEndpoints();
app.MapWebhookEndpoints();
app.MapTestRunEndpoints();

app.Run();
