using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Models;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style data-driven authorization tests for all admin endpoints.
///
/// Feature: Admin endpoint role-based access control
/// As a system administrator
/// I want to ensure only Admin role users can access admin endpoints
/// So that sensitive operations and data are protected
///
/// Note: This consolidates authorization tests to eliminate duplication.
/// Individual endpoint test files focus on happy-path behaviors.
/// </summary>
[Collection("Admin Endpoints")]
public class AdminAuthorizationTests : AdminTestFixture
{
    public AdminAuthorizationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Represents an admin endpoint to test for authorization.
    /// </summary>
    public sealed record AdminEndpointTestCase(
        string EndpointDescription,
        HttpMethod Method,
        string Path,
        Func<HttpContent?>? CreateContent = null);

    /// <summary>
    /// All admin endpoints that require authorization.
    /// Each endpoint is tested against both Editor and User roles.
    /// </summary>
    public static IEnumerable<object[]> AdminEndpoints => new[]
    {
        // Stats endpoints
        new object[]
        {
            new AdminEndpointTestCase(
                "GET /admin/stats",
                HttpMethod.Get,
                "/admin/stats")
        },

        // Request logs endpoints
        new object[]
        {
            new AdminEndpointTestCase(
                "GET /admin/requests",
                HttpMethod.Get,
                "/admin/requests")
        },

        // N8n configuration endpoints
        new object[]
        {
            new AdminEndpointTestCase(
                "POST /admin/n8n",
                HttpMethod.Post,
                "/admin/n8n",
                () => JsonContent.Create(new CreateN8nConfigRequest("Test", "https://n8n.test", "key", null)))
        },
        new object[]
        {
            new AdminEndpointTestCase(
                "GET /admin/n8n",
                HttpMethod.Get,
                "/admin/n8n")
        },
        new object[]
        {
            new AdminEndpointTestCase(
                "GET /admin/n8n/{id}",
                HttpMethod.Get,
                "/admin/n8n/test-id")
        },
        new object[]
        {
            new AdminEndpointTestCase(
                "PUT /admin/n8n/{id}",
                HttpMethod.Put,
                "/admin/n8n/test-id",
                () => JsonContent.Create(new UpdateN8nConfigRequest("Updated", "https://n8n.test", "key", null, true)))
        },
        new object[]
        {
            new AdminEndpointTestCase(
                "DELETE /admin/n8n/{id}",
                HttpMethod.Delete,
                "/admin/n8n/test-id")
        },
        new object[]
        {
            new AdminEndpointTestCase(
                "POST /admin/n8n/{id}/test",
                HttpMethod.Post,
                "/admin/n8n/test-id/test")
        }
    };

    /// <summary>
    /// Non-admin user roles that should be denied access.
    /// </summary>
    public static IEnumerable<object[]> NonAdminRoles => new[]
    {
        new object[] { "Editor" },
        new object[] { "User" }
    };

    /// <summary>
    /// Scenario: Non-admin user attempts to access admin endpoint
    ///   Given user is authenticated with Editor or User role
    ///   When user requests any admin endpoint
    ///   Then system returns HTTP 403 Forbidden
    /// </summary>
    [Theory]
    [MemberData(nameof(NonAdminRoleEndpointCombinations))]
    public async Task AdminEndpoint_WhenNonAdminRole_ReturnsForbidden(string role, AdminEndpointTestCase endpoint)
    {
        // Given: User authenticated with non-admin role
        using var nonAdminClient = Factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(nonAdminClient, email, role);

        // When: User attempts to access admin endpoint
        var request = new HttpRequestMessage(endpoint.Method, endpoint.Path);
        if (endpoint.CreateContent != null)
        {
            request.Content = endpoint.CreateContent();
        }
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        // Then: System denies access
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Anonymous user attempts to access admin endpoint
    ///   Given user is not authenticated
    ///   When user requests any admin endpoint
    ///   Then system returns HTTP 401 Unauthorized
    /// </summary>
    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AdminEndpoint_WhenNotAuthenticated_ReturnsUnauthorized(AdminEndpointTestCase endpoint)
    {
        // Given: User is not authenticated
        using var client = Factory.CreateHttpsClient();

        // When: Anonymous user attempts to access admin endpoint
        var request = new HttpRequestMessage(endpoint.Method, endpoint.Path);
        if (endpoint.CreateContent != null)
        {
            request.Content = endpoint.CreateContent();
        }

        var response = await client.SendAsync(request);

        // Then: System requires authentication
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Generates cartesian product of non-admin roles and admin endpoints.
    /// This creates test cases for every (role, endpoint) combination.
    /// </summary>
    public static IEnumerable<object[]> NonAdminRoleEndpointCombinations
    {
        get
        {
            foreach (var roleData in NonAdminRoles)
            {
                var role = (string)roleData[0];
                foreach (var endpointData in AdminEndpoints)
                {
                    var endpoint = (AdminEndpointTestCase)endpointData[0];
                    yield return new object[] { role, endpoint };
                }
            }
        }
    }
}
