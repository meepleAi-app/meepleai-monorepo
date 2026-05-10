using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application;

[Trait("Category", "Unit")]
public sealed class ProbeProviderCommandHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToProbeService()
    {
        var actorId = Guid.NewGuid();
        var expected = new ProviderProbeResultDto(
            ProviderName: "openrouter",
            TokenConfigured: true,
            TokenAuthenticated: true,
            ModelAvailable: null,
            ExpectedModel: null,
            TokenFingerprint: "abcd1234",
            ErrorCode: null,
            ErrorMessage: null,
            LatencyMs: 250,
            ProbedAt: DateTime.UtcNow);

        var svc = new Mock<IProviderProbeService>();
        svc.Setup(s => s.ProbeAsync("openrouter", actorId, null, It.IsAny<CancellationToken>()))
           .ReturnsAsync(expected);
        var handler = new ProbeProviderCommandHandler(svc.Object);

        var result = await handler.Handle(new ProbeProviderCommand("openrouter", actorId, null), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_PropagatesExpectedModel()
    {
        var actorId = Guid.NewGuid();
        var expected = new ProviderProbeResultDto(
            ProviderName: "openrouter",
            TokenConfigured: true,
            TokenAuthenticated: true,
            ModelAvailable: false,
            ExpectedModel: "missing-model",
            TokenFingerprint: "abcd1234",
            ErrorCode: null,
            ErrorMessage: null,
            LatencyMs: 200,
            ProbedAt: DateTime.UtcNow);

        var svc = new Mock<IProviderProbeService>();
        svc.Setup(s => s.ProbeAsync("openrouter", actorId, "missing-model", It.IsAny<CancellationToken>()))
           .ReturnsAsync(expected);
        var handler = new ProbeProviderCommandHandler(svc.Object);

        var result = await handler.Handle(new ProbeProviderCommand("openrouter", actorId, "missing-model"), CancellationToken.None);

        result.ModelAvailable.Should().BeFalse();
        result.ExpectedModel.Should().Be("missing-model");
    }

    [Fact]
    public void Validator_EmptyProviderName_Fails()
    {
        var v = new ProbeProviderCommandValidator();
        var r = v.Validate(new ProbeProviderCommand(string.Empty, Guid.NewGuid(), null));
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongProviderName_Fails()
    {
        var v = new ProbeProviderCommandValidator();
        var r = v.Validate(new ProbeProviderCommand(new string('x', 65), Guid.NewGuid(), null));
        r.IsValid.Should().BeFalse();
    }

    // Unknown-but-syntactically-valid provider names pass the validator and are
    // rejected later by ProviderProbeExecutorFactory → UnknownProviderException → HTTP 404.
    [Theory]
    [InlineData("openrouter")]
    [InlineData("deepseek")]
    [InlineData("ollama-local")]
    [InlineData("cohere")]
    public void Validator_NonEmptyProviderName_Passes(string name)
    {
        var v = new ProbeProviderCommandValidator();
        v.Validate(new ProbeProviderCommand(name, Guid.NewGuid(), null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyActorId_Fails()
    {
        var v = new ProbeProviderCommandValidator();
        var r = v.Validate(new ProbeProviderCommand("openrouter", Guid.Empty, null));
        r.IsValid.Should().BeFalse();
    }
}
