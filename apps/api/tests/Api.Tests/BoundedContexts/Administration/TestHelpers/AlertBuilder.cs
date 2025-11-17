using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.Administration.TestHelpers;

/// <summary>
/// Builder for creating Alert test instances with sensible defaults.
/// </summary>
public class AlertBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _alertType = "SystemError";
    private AlertSeverity _severity = AlertSeverity.Medium;
    private string _message = "Test alert message";
    private string? _metadata;
    private bool _shouldResolve;

    public AlertBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public AlertBuilder WithType(string alertType)
    {
        _alertType = alertType;
        return this;
    }

    public AlertBuilder WithSeverity(AlertSeverity severity)
    {
        _severity = severity;
        return this;
    }

    public AlertBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    public AlertBuilder WithMetadata(string metadata)
    {
        _metadata = metadata;
        return this;
    }

    public AlertBuilder ThatIsCritical()
    {
        _severity = AlertSeverity.Critical;
        _alertType = "CriticalError";
        return this;
    }

    public AlertBuilder ThatIsResolved()
    {
        _shouldResolve = true;
        return this;
    }

    public Alert Build()
    {
        var alert = new Alert(_id, _alertType, _severity, _message, _metadata);

        if (_shouldResolve)
        {
            alert.Resolve();
        }

        return alert;
    }

    public static implicit operator Alert(AlertBuilder builder) => builder.Build();
}
