using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Verifies that GetServiceHealthAsync correctly distinguishes between:
/// (a) an unknown service name (return Unhealthy — likely a bug or stale dashboard config), and
/// (b) a known optional provider with no active health check registration (return Healthy
/// with an explanatory description — this is intentional, not a failure).
///
/// Companion to PR #883: when conditional registration removes Unstructured/SmolDocling/Ollama
/// for a deselected provider, the admin dashboard must not show them as Unhealthy.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class InfrastructureHealthServiceTests
{
    private static InfrastructureHealthService CreateService(out Mock<HealthCheckService> healthCheckMock)
    {
        healthCheckMock = new Mock<HealthCheckService>();
        var logger = new Mock<ILogger<InfrastructureHealthService>>();
        // InfrastructureHealthService is internal: use the InternalsVisibleTo grant
        // declared in apps/api/src/Api/AssemblyInfo.cs for the test assembly.
        return new InfrastructureHealthService(healthCheckMock.Object, logger.Object);
    }

    private static HealthReport EmptyReport() => new(
        new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
        TimeSpan.Zero);

    [Theory]
    [InlineData("unstructured")]
    [InlineData("smoldocling")]
    [InlineData("ollama")]
    [InlineData("postgres")]
    public async Task Known_Service_Without_Health_Entry_Returns_Healthy_With_Not_Active_Description(string serviceName)
    {
        var service = CreateService(out var healthCheckMock);
        healthCheckMock
            .Setup(s => s.CheckHealthAsync(
                It.IsAny<Func<HealthCheckRegistration, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyReport());

        var result = await service.GetServiceHealthAsync(serviceName, CancellationToken.None);

        result.State.Should().Be(HealthState.Healthy,
            because: "a known optional provider with no registration is intentionally deselected, not broken");
        result.ErrorMessage.Should().Contain("not active");
        result.ServiceName.Should().Be(serviceName);
    }

    [Fact]
    public async Task Unknown_Service_Without_Health_Entry_Still_Returns_Unhealthy()
    {
        // Sanity check: the known-service short-circuit must not swallow genuine misconfigs
        // such as a stale dashboard pointing to a removed/renamed service.
        var service = CreateService(out var healthCheckMock);
        healthCheckMock
            .Setup(s => s.CheckHealthAsync(
                It.IsAny<Func<HealthCheckRegistration, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyReport());

        var result = await service.GetServiceHealthAsync("nonexistent-service", CancellationToken.None);

        result.State.Should().Be(HealthState.Unhealthy);
        result.ErrorMessage.Should().Contain("not configured");
    }
}
