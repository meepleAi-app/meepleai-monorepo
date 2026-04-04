using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.Tests.BoundedContexts.Administration.TestHelpers;

/// <summary>
/// Builder for creating AuditLog test instances with sensible defaults.
/// </summary>
internal class AuditLogBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid? _userId = Guid.NewGuid();
    private string _action = "TestAction";
    private string _resource = "TestResource";
    private string _result = "success";
    private readonly string? _resourceId = null;
    private string? _details;
    private string? _ipAddress;
    private readonly string? _userAgent = null;

    public AuditLogBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public AuditLogBuilder WithUserId(Guid? userId)
    {
        _userId = userId;
        return this;
    }

    public AuditLogBuilder WithAction(string action)
    {
        _action = action;
        return this;
    }

    public AuditLogBuilder WithResource(string resource)
    {
        _resource = resource;
        return this;
    }

    public AuditLogBuilder WithResult(string result)
    {
        _result = result;
        return this;
    }

    public AuditLogBuilder WithDetails(string details)
    {
        _details = details;
        return this;
    }

    public AuditLogBuilder WithIpAddress(string ipAddress)
    {
        _ipAddress = ipAddress;
        return this;
    }

    public AuditLogBuilder ForUserLogin()
    {
        _action = "user.login";
        _resource = "authentication";
        _result = "success";
        return this;
    }

    public AuditLogBuilder ForFailedLogin()
    {
        _action = "user.login";
        _resource = "authentication";
        _result = "failure";
        _details = "Invalid credentials";
        return this;
    }

    public AuditLog Build()
    {
        return new AuditLog(
            _id,
            _userId,
            _action,
            _resource,
            _result,
            _resourceId,
            _details,
            _ipAddress,
            _userAgent);
    }

    public static implicit operator AuditLog(AuditLogBuilder builder) => builder.Build();
}
