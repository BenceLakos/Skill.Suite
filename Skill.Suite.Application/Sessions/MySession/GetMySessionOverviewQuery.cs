namespace Skill.Suite.Application.Sessions.MySession;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// The signed-in competitor's credentials, database access and session services.
/// </summary>
/// <param name="RequestHost">
/// The host the competitor reached this application on, with no port — the caller reads it off the browser's
/// base address. It is the one thing the handler cannot know on its own, and both the SQL Server name and the
/// services' addresses are derived from it, because everything a competitor connects to is published by the
/// same host that served them this page.
/// </param>
public sealed record GetMySessionOverviewQuery(string? RequestHost)
    : IRequest<Result<MySessionOverviewDto>>;
