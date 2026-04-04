using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Filters;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Filters;

/// <summary>
/// Tests for RequireFeatureFilter endpoint filter.
/// Issue #3674: Feature Flag Tier-Based Access Verification
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class RequireFeatureFilterTests
{
    private readonly Mock<IFeatureFlagService> _mockFeatureFlagService;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<RequireFeatureFilter>> _mockLogger;

    public RequireFeatureFilterTests()
    {
        _mockFeatureFlagService = new Mock<IFeatureFlagService>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<RequireFeatureFilter>>();
    }

    /// <summary>
    /// Test implementation of EndpointFilterInvocationContext for unit testing.
    /// DefaultEndpointFilterInvocationContext is internal to ASP.NET Core.
    /// </summary>
    private sealed class TestEndpointFilterInvocationContext : EndpointFilterInvocationContext
    {
        private readonly HttpContext _httpContext;

        public TestEndpointFilterInvocationContext(HttpContext httpContext)
        {
            _httpContext = httpContext;
        }

        public override HttpContext HttpContext => _httpContext;
        public override IList<object?> Arguments => new List<object?>();
        public override T GetArgument<T>(int index) => default!;
    }

    [Fact]
    public async Task InvokeAsync_WhenNoSession_Returns401()
    {
        // Arrange
        var filter = new RequireFeatureFilter("advanced_rag");
        var httpContext = CreateHttpContext(session: null);
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); };

        // Act
        var result = await filter.InvokeAsync(invocationContext, next);

        // Assert
        nextCalled.Should().BeFalse();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenUserHasAccess_CallsNext()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, UserTier.Premium, Role.User);
        var session = CreateSession(userId);

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockFeatureFlagService.Setup(f => f.CanAccessFeatureAsync(user, "advanced_rag"))
            .ReturnsAsync(true);

        var filter = new RequireFeatureFilter("advanced_rag");
        var httpContext = CreateHttpContext(session);
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); };

        // Act
        await filter.InvokeAsync(invocationContext, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenUserDeniedAccess_Returns403()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, UserTier.Free, Role.User);
        var session = CreateSession(userId);

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockFeatureFlagService.Setup(f => f.CanAccessFeatureAsync(user, "multi_agent"))
            .ReturnsAsync(false);

        var filter = new RequireFeatureFilter("multi_agent");
        var httpContext = CreateHttpContext(session);
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); };

        // Act
        var result = await filter.InvokeAsync(invocationContext, next);

        // Assert
        nextCalled.Should().BeFalse();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenUserNotFound_Returns401()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateSession(userId);

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var filter = new RequireFeatureFilter("advanced_rag");
        var httpContext = CreateHttpContext(session);
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Results.Ok());

        // Act
        var result = await filter.InvokeAsync(invocationContext, next);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithEmptyFeatureName_ThrowsArgumentException()
    {
        ((Action)(() => new RequireFeatureFilter(""))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullFeatureName_ThrowsArgumentNullException()
    {
        ((Action)(() => new RequireFeatureFilter(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_PremiumUserAccessingPremiumFeature_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, UserTier.Premium, Role.User);
        var session = CreateSession(userId);

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockFeatureFlagService.Setup(f => f.CanAccessFeatureAsync(user, "bulk_import"))
            .ReturnsAsync(true);

        var filter = new RequireFeatureFilter("bulk_import");
        var httpContext = CreateHttpContext(session);
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); };

        // Act
        await filter.InvokeAsync(invocationContext, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_FreeUserAccessingPremiumFeature_ReturnsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, UserTier.Free, Role.User);
        var session = CreateSession(userId);

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockFeatureFlagService.Setup(f => f.CanAccessFeatureAsync(user, "bulk_import"))
            .ReturnsAsync(false);

        var filter = new RequireFeatureFilter("bulk_import");
        var httpContext = CreateHttpContext(session);
        var invocationContext = new TestEndpointFilterInvocationContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Results.Ok());

        // Act
        var result = await filter.InvokeAsync(invocationContext, next);

        // Assert
        result.Should().NotBeNull();
    }

    #region Helper Methods

    private HttpContext CreateHttpContext(SessionStatusDto? session)
    {
        var httpContext = new DefaultHttpContext();

        var services = new ServiceCollection();
        services.AddSingleton(_mockFeatureFlagService.Object);
        services.AddSingleton(_mockUserRepository.Object);
        services.AddSingleton(_mockLogger.Object);
        httpContext.RequestServices = services.BuildServiceProvider();

        if (session != null)
        {
            httpContext.Items[nameof(SessionStatusDto)] = session;
        }

        return httpContext;
    }

    private static SessionStatusDto CreateSession(Guid userId)
    {
        return new SessionStatusDto(
            IsValid: true,
            User: new Api.BoundedContexts.Authentication.Application.DTOs.UserDto(
                Id: userId,
                Email: "test@example.com",
                DisplayName: "Test User",
                Role: "user",
                Tier: "free",
                CreatedAt: DateTime.UtcNow,
                IsTwoFactorEnabled: false,
                TwoFactorEnabledAt: null,
                Level: 1,
                ExperiencePoints: 0),
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            LastSeenAt: DateTime.UtcNow,
            SessionId: Guid.NewGuid()
        );
    }

    private static User CreateUser(Guid userId, UserTier tier, Role role)
    {
        return new User(
            id: userId,
            email: new Email("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Test123!"),
            role: role,
            tier: tier
        );
    }

    #endregion
}
