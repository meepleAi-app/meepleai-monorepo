using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Application.Validators.Infrastructure;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class InfrastructureValidatorsTests
{
    [Fact]
    public void RestartValidator_ValidService_Passes()
    {
        var validator = new RestartServiceCommandValidator();
        var result = validator.TestValidate(new RestartServiceCommand("embedding"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RestartValidator_UnknownService_Fails()
    {
        var validator = new RestartServiceCommandValidator();
        var result = validator.TestValidate(new RestartServiceCommand("unknown-service"));
        result.ShouldHaveValidationErrorFor(x => x.ServiceName);
    }

    [Fact]
    public void RestartValidator_EmptyService_Fails()
    {
        var validator = new RestartServiceCommandValidator();
        var result = validator.TestValidate(new RestartServiceCommand(""));
        result.ShouldHaveValidationErrorFor(x => x.ServiceName);
    }

    [Fact]
    public void TriggerHealthCheckValidator_ValidService_Passes()
    {
        var validator = new TriggerHealthCheckCommandValidator();
        var result = validator.TestValidate(new TriggerHealthCheckCommand("reranker"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void TriggerHealthCheckValidator_UnknownService_Fails()
    {
        var validator = new TriggerHealthCheckCommandValidator();
        var result = validator.TestValidate(new TriggerHealthCheckCommand("fake-service"));
        result.ShouldHaveValidationErrorFor(x => x.ServiceName);
    }

    [Fact]
    public void UpdateConfigValidator_ValidParams_Passes()
    {
        var validator = new UpdateServiceConfigCommandValidator();
        var command = new UpdateServiceConfigCommand("reranker",
            new Dictionary<string, string> { ["batch_size"] = "64" });
        var result = validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateConfigValidator_EmptyParams_Fails()
    {
        var validator = new UpdateServiceConfigCommandValidator();
        var command = new UpdateServiceConfigCommand("reranker",
            new Dictionary<string, string>());
        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Parameters);
    }

    [Fact]
    public void UpdateConfigValidator_UnknownService_Fails()
    {
        var validator = new UpdateServiceConfigCommandValidator();
        var command = new UpdateServiceConfigCommand("unknown",
            new Dictionary<string, string> { ["key"] = "value" });
        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ServiceName);
    }
}
