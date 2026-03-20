using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Factory for creating admin users with correct authentication cookies for integration tests.
/// </summary>
/// <remarks>
/// <para><strong>Problem Solved:</strong></para>
/// <para>
/// Standard user registration creates cookies with the default "User" role.
/// Manually updating the role in the database doesn't update the cookie, causing
/// 403 Forbidden errors when admin-only endpoints are accessed.
/// </para>
///
/// <para><strong>Solution:</strong></para>
/// <para>
/// 1. Register user via API (gets cookie with "User" role)
/// 2. Update role to "Admin" in database
/// 3. Re-authenticate to get new cookie with "Admin" role
/// </para>
///
/// <para><strong>Usage:</strong></para>
/// <code>
/// // In test setup
/// var (adminUser, adminCookie) = await AdminUserFactory.CreateAdminWithCookieAsync(
///     _factory,
///     _client
/// );
///
/// // Use cookie for admin endpoints
/// _client.DefaultRequestHeaders.Clear();
/// _client.DefaultRequestHeaders.Add("Cookie", adminCookie);
/// var response = await _client.PostAsJsonAsync("/api/v1/agents", request);
/// </code>
/// </remarks>
internal static class AdminUserFactory
{
    /// <summary>
    /// Creates an admin user and returns both the user ID and a valid authentication cookie.
    /// </summary>
    /// <param name="factory">WebApplicationFactory for accessing services</param>
    /// <param name="client">HttpClient for making API requests</param>
    /// <param name="email">Optional custom email (defaults to unique test email)</param>
    /// <param name="password">Optional custom password (defaults to "Admin123!")</param>
    /// <param name="displayName">Optional custom display name (defaults to "Admin User")</param>
    /// <returns>Tuple of (User ID, Authentication cookie string)</returns>
    public static async Task<(Guid userId, string cookie)>
        CreateAdminWithCookieAsync(
            WebApplicationFactory<Program> factory,
            HttpClient client,
            string? email = null,
            string? password = null,
            string? displayName = null)
    {
        var adminEmail = email ?? $"admin-{Guid.NewGuid():N}@test.com";
        var adminPassword = password ?? "Admin123!";
        var adminDisplayName = displayName ?? "Admin User";

        // Step 1: Register user via API (gets session cookie)
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = adminEmail,
            Password = adminPassword,
            DisplayName = adminDisplayName
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminCookie = registerResponse.Headers.GetValues("Set-Cookie").First();

        // Step 2: Update role to "Admin" directly in DB
        // Pattern from AgentTypologyEndpointsSmokeTests (passes with this approach)
        Guid adminUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var adminUser = await dbContext.Users.FirstAsync(u => u.Email == adminEmail);
            adminUser.Role = "Admin"; // Uppercase
            adminUserId = adminUser.Id;
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
        }

        // Step 3: NO re-login needed!
        // The middleware reads role from DB on each request, not from cookie
        // Cookie contains only session ID, role is fetched fresh from DB

        return (adminUserId, adminCookie);
    }

    /// <summary>
    /// Creates an admin user and returns only the authentication cookie.
    /// </summary>
    /// <param name="factory">WebApplicationFactory for accessing services</param>
    /// <param name="client">HttpClient for making API requests</param>
    /// <returns>Authentication cookie string</returns>
    public static async Task<string> CreateAdminCookieAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client)
    {
        var (_, cookie) = await CreateAdminWithCookieAsync(factory, client);
        return cookie;
    }

    /// <summary>
    /// Creates multiple admin users for batch testing.
    /// </summary>
    /// <param name="factory">WebApplicationFactory for accessing services</param>
    /// <param name="client">HttpClient for making API requests</param>
    /// <param name="count">Number of admin users to create</param>
    /// <returns>List of tuples containing (User ID, Authentication cookie)</returns>
    public static async Task<List<(Guid userId, string cookie)>>
        CreateAdminUsersAsync(
            WebApplicationFactory<Program> factory,
            HttpClient client,
            int count)
    {
        var admins = new List<(Guid, string)>();

        for (int i = 0; i < count; i++)
        {
            var admin = await CreateAdminWithCookieAsync(factory, client);
            admins.Add(admin);
        }

        return admins;
    }
}
