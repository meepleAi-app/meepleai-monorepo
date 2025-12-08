using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to simulate various error types for testing runbooks and alerting.
/// Admin-only endpoint, disabled in production environments.
/// Issue #2004: Enable runbook validation for high-error-rate and error-spike scenarios.
/// </summary>
/// <param name="ErrorType">Type of error to simulate: "500", "400", "timeout", "exception"</param>
public record SimulateErrorCommand(string ErrorType) : IRequest<Unit>;
