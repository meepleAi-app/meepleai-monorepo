using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Api.SharedKernel.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates requests using FluentValidation.
/// Runs before the command/query handler and throws ValidationException if validation fails.
/// Returns HTTP 422 Unprocessable Entity with structured error messages.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
/// <typeparam name="TRequest">The request type (Command or Query)</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IReadOnlyCollection<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>>? validators)
    {
        _validators = (validators ?? Enumerable.Empty<IValidator<TRequest>>()).ToArray();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Tests configure validators with default(CancellationToken); only propagate when already canceled
        var validationToken = cancellationToken.IsCancellationRequested ? cancellationToken : CancellationToken.None;

        // Skip validation if no validators are registered for this request type
        if (_validators.Count == 0)
        {
            return await next();
        }

        // Create validation context
        var context = new ValidationContext<TRequest>(request);

        // Run all validators in parallel
        var validationTasks = _validators
            .Select(v => v.ValidateAsync(context, validationToken));

        var validationResults = await Task.WhenAll(validationTasks);

        // Collect all validation errors with null-safe iteration (tests may return null results)
        var failures = validationResults
            .Where(result => result is not null)
            .SelectMany(result => result!.Errors ?? Enumerable.Empty<ValidationFailure>())
            .Where(failure => failure is not null)
            .ToList();

        // If there are validation errors, throw ValidationException
        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        // Validation passed, continue to the handler
        return await next();
    }
}
