using System.Reflection;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #3838 — <c>security_audit_logs</c> recorded only logins, though <see cref="AuditEventType"/> declared password
/// changes, resets, OAuth links, role changes, the admin 2FA override and bulk session revocation. The vocabulary was
/// there and nobody called it: a constant with no emitter is a security event that silently never reaches the log,
/// and nothing failed. This test fails on such a constant.
/// </summary>
/// <remarks>
/// <b>Text scan.</b> A constant is emitted when some file under <c>apps/api/src/Api</c> other than
/// <c>AuditEventType.cs</c> mentions <c>AuditEventType.&lt;Name&gt;</c>, qualified or not. It proves a call site exists,
/// not that it runs: the per-handler tests and the logout integration test cover that.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "3838")]
public sealed class SecurityAuditEventTypesAreEmittedArchitectureTests
{
    [Fact]
    public void EveryDeclaredSecurityAuditEventType_HasAnEmitter()
    {
        var constants = typeof(AuditEventType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => f.Name)
            .ToList();

        var sources = Directory.EnumerateFiles(LocateApiSrc(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith($"{Path.DirectorySeparatorChar}AuditEventType.cs", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();

        var neverEmitted = constants
            .Where(name => !sources.Any(text => text.Contains($"AuditEventType.{name}", StringComparison.Ordinal)))
            .ToList();

        constants.Should().Contain(["LoginSuccess", "Logout", "RoleChanged"], "the scan must see the constants it was written for");
        neverEmitted.Should().BeEmpty(
            "a declared security audit event type nobody logs is a security event that never reaches security_audit_logs");
    }

    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // In a git worktree `.git` is a file, not a directory: accepting both stops the climb at the right root.
        while (dir is not null)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                break;
            }

            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the tests run inside the repository");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}
