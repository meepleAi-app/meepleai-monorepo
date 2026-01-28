using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Test authentication handler for integration tests.
/// Allows bypassing real authentication in tests by providing
/// a configurable user identity via test headers.
///
/// Usage:
/// 1. Configure in WebApplicationFactory:
///    builder.ConfigureTestServices(services =>
///    {
///        services.AddAuthentication(TestAuthenticationHandler.SchemeName)
///            .AddScheme&lt;AuthenticationSchemeOptions, TestAuthenticationHandler&gt;(
///                TestAuthenticationHandler.SchemeName, options => { });
///    });
///
/// 2. Add test header to client:
///    client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
///    client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    public const string UserIdHeader = "X-Test-UserId";
    public const string RoleHeader = "X-Test-Role";
    public const string EmailHeader = "X-Test-Email";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for test headers
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues) ||
            string.IsNullOrEmpty(userIdValues.FirstOrDefault()))
        {
            return Task.FromResult(AuthenticateResult.Fail("No test user ID provided"));
        }

        var userId = userIdValues.FirstOrDefault()!;
        var role = Request.Headers.TryGetValue(RoleHeader, out var roleValues)
            ? roleValues.FirstOrDefault() ?? "user"
            : "user";
        var email = Request.Headers.TryGetValue(EmailHeader, out var emailValues)
            ? emailValues.FirstOrDefault() ?? $"test-{userId}@test.com"
            : $"test-{userId}@test.com";

        // Create claims for the test user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("sub", userId) // Standard JWT subject claim
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
