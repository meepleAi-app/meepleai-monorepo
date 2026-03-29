using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure.Http;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Http;

public sealed class ServiceCallLoggingHandlerTests
{
    // -------------------------------------------------------------------------
    // Helper: fake inner handler that returns a predetermined response
    // -------------------------------------------------------------------------
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    // -------------------------------------------------------------------------
    // Factory helpers
    // -------------------------------------------------------------------------
    private static (ServiceCallLoggingHandler handler, Mock<IServiceCallLogRepository> repoMock)
        BuildHandler(HttpResponseMessage fakeResponse)
    {
        var repoMock = new Mock<IServiceCallLogRepository>();
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceCallLogEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(repoMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var inner = new FakeHttpHandler(fakeResponse);
        var loggingHandler = new ServiceCallLoggingHandler(
            serviceProvider, "TestService", NullLogger<ServiceCallLoggingHandler>.Instance)
        {
            InnerHandler = inner
        };

        return (loggingHandler, repoMock);
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task SendAsync_LogsSuccessfulCall()
    {
        // Arrange
        var fakeResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        var (handler, repoMock) = BuildHandler(fakeResponse);

        var httpClient = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://test-service/api/resource");

        // Act
        var response = await httpClient.SendAsync(request);

        // Allow the fire-and-forget Task.Run to complete
        await Task.Delay(200);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ServiceCallLogEntry>(e =>
                e.ServiceName == "TestService" &&
                e.IsSuccess &&
                e.HttpMethod == "GET"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task SendAsync_LogsFailedCall()
    {
        // Arrange
        var fakeResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        var (handler, repoMock) = BuildHandler(fakeResponse);

        var httpClient = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Post, "http://test-service/api/resource");

        // Act
        var response = await httpClient.SendAsync(request);

        // Allow the fire-and-forget Task.Run to complete
        await Task.Delay(200);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ServiceCallLogEntry>(e =>
                !e.IsSuccess &&
                e.StatusCode == 500),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
