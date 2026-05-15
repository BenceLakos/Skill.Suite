using FluentValidation;
using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Behaviors;

/// <summary>
/// Pipeline behavior that runs FluentValidation against every incoming message.
/// If validation fails and the message returns a Result/Result&lt;T&gt;, we surface a
/// validation Error instead of throwing — keeping with the Result&lt;T&gt; pattern
/// (no exceptions for flow control).
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(message, cancellationToken);

        var context = new ValidationContext<TMessage>(message);
        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(message, cancellationToken);

        var error = Error.Validation(
            "Validation",
            string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

        // If the handler returns a Result or Result<T>, surface the error there
        // instead of throwing — preserves the Result<T> pattern.
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failureMethod = typeof(Result)
                .GetMethods()
                .First(m => m is { Name: nameof(Result.Failure), IsGenericMethod: true })
                .MakeGenericMethod(typeof(TResponse).GetGenericArguments()[0]);
            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        throw new ValidationException(failures);
    }
}
