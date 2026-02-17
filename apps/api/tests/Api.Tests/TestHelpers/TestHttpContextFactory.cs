using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Factory for creating mock IHttpContextAccessor instances for test purposes.
/// </summary>
internal static class TestHttpContextFactory
{
    /// <summary>
    /// Creates a mock IHttpContextAccessor with the specified user ID set in claims.
    /// </summary>
    public static Mock<IHttpContextAccessor> CreateMockHttpContextAccessor(Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var mock = new Mock<IHttpContextAccessor>();
        mock.Setup(x => x.HttpContext).Returns(httpContext);

        return mock;
    }
}
