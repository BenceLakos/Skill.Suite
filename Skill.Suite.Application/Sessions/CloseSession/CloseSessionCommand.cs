namespace Skill.Suite.Application.Sessions.CloseSession;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Closes the session to submissions and tears down the service containers starting it brought up.
/// </summary>
/// <remarks>
/// The teardown is reported rather than awaited for success: see <see cref="CloseSessionResult"/>.
/// </remarks>
public sealed record CloseSessionCommand(Guid Id) : IRequest<Result<CloseSessionResult>>;
