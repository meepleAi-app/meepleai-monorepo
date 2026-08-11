using System.Security.Claims;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection;
using Api.Tests.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Issue #3472 — regression guard for authorization policies referenced by endpoints
/// but never registered.
///
/// <para>
/// <c>GameToolkitRoutes</c> and <c>SharedGameCatalogAdminEndpoints</c> reference
/// <c>"AdminPolicy"</c>, and <c>SharedGameCatalogAdminShareRequestEndpoints</c> references
/// <c>"EditorOnlyPolicy"</c>, via <c>.RequireAuthorization("...")</c>. Neither name was
/// registered by <see cref="SharedGameCatalogServiceExtensions.AddSharedGameCatalogPolicies"/>
/// (the prod registration wired at <c>Program.cs</c>), so the ASP.NET authorization middleware
/// threw <c>InvalidOperationException: The AuthorizationPolicy named 'X' was not found</c> at
/// request time → HTTP 500 on RemoveRag, the GameToolkit review routes and the bulk
/// approve/reject share-request endpoints.
/// </para>
///
/// These tests invoke the SAME production registration method and assert the two policies are
/// resolvable and enforce the intended role tiers (aligned with the existing equivalents
/// <c>AdminOnlyPolicy</c> = SuperAdmin+Admin and <c>AdminOrEditorPolicy</c> = SuperAdmin+Admin+Editor).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.Security)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3472")]
public sealed class SharedGameCatalogAuthorizationPolicyRegistrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAuthorizationPolicyProvider _policyProvider;

    public SharedGameCatalogAuthorizationPolicyRegistrationTests()
    {
        var services = new ServiceCollection();

        // Invoke the REAL production registration (Program.cs calls this) so the test guards
        // the actual wiring, not a re-declared copy.
        services.AddSharedGameCatalogPolicies();
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _authorizationService = _serviceProvider.GetRequiredService<IAuthorizationService>();
        _policyProvider = _serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
    }

    [Fact]
    public async Task AddSharedGameCatalogPolicies_Registers_AdminPolicy()
    {
        var policy = await _policyProvider.GetPolicyAsync("AdminPolicy");

        policy.Should().NotBeNull(
            "endpoints reference \"AdminPolicy\" via RequireAuthorization and an unregistered policy 500s at request time");
    }

    [Fact]
    public async Task AddSharedGameCatalogPolicies_Registers_EditorOnlyPolicy()
    {
        var policy = await _policyProvider.GetPolicyAsync("EditorOnlyPolicy");

        policy.Should().NotBeNull(
            "the bulk approve/reject share-request endpoints reference \"EditorOnlyPolicy\" via RequireAuthorization");
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Admin")]
    public async Task AdminPolicy_Grants_AdminTierRoles(string role)
    {
        var result = await _authorizationService.AuthorizeAsync(
            CreatePrincipalWithRole(role), resource: null, "AdminPolicy");

        result.Succeeded.Should().BeTrue($"'{role}' is an admin-tier role and must satisfy AdminPolicy");
    }

    [Theory]
    [InlineData("Editor")]
    [InlineData("User")]
    public async Task AdminPolicy_Denies_NonAdminRoles(string role)
    {
        var result = await _authorizationService.AuthorizeAsync(
            CreatePrincipalWithRole(role), resource: null, "AdminPolicy");

        result.Succeeded.Should().BeFalse($"'{role}' is below the admin tier and must NOT satisfy AdminPolicy");
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Admin")]
    [InlineData("Editor")]
    public async Task EditorOnlyPolicy_Grants_EditorTierAndAbove(string role)
    {
        var result = await _authorizationService.AuthorizeAsync(
            CreatePrincipalWithRole(role), resource: null, "EditorOnlyPolicy");

        result.Succeeded.Should().BeTrue(
            $"'{role}' is editor-tier or above and must satisfy EditorOnlyPolicy (admins are never below editors)");
    }

    [Fact]
    public async Task EditorOnlyPolicy_Denies_PlainUser()
    {
        var result = await _authorizationService.AuthorizeAsync(
            CreatePrincipalWithRole("User"), resource: null, "EditorOnlyPolicy");

        result.Succeeded.Should().BeFalse("a plain 'User' must NOT satisfy EditorOnlyPolicy");
    }

    [Theory]
    [InlineData("AdminOnlyPolicy")]
    [InlineData("AdminOrEditorPolicy")]
    public async Task AddSharedGameCatalogPolicies_Keeps_ExistingPolicies(string policyName)
    {
        // Non-regression: the pre-existing policies registered by the same method must remain.
        var policy = await _policyProvider.GetPolicyAsync(policyName);

        policy.Should().NotBeNull($"'{policyName}' was already registered and must not regress");
    }

    private static ClaimsPrincipal CreatePrincipalWithRole(string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, "test@example.com"),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        return new ClaimsPrincipal(identity);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
