using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4401: every request sent through MediatR has a handler.
/// <para>
/// The game import saga sent <c>IndexDocumentCommand</c>, which had a command, a validator, and no
/// handler. MediatR only finds out at <c>Send</c>, and the saga sent it inside a <c>Task.Run</c>:
/// the error went to the log ("Step 3 async FAILED") and the wizard showed the indexing as
/// "pending" forever. Nothing in the build or the tests could see it.
/// </para>
/// <para>
/// A request without a handler is dead code that looks alive: it compiles, it validates, and it
/// fails only when it is sent. This test makes the absence visible where it is cheap to fix.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4401")]
public sealed class MediatRRequestHandlerCoverageArchitectureTests
{
    [Fact]
    public void EveryRequest_HasAHandler()
    {
        var types = typeof(Program).Assembly.GetTypes();

        var handled = types
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                         i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))
            .Select(i => i.GetGenericArguments()[0])
            .Where(r => !r.IsGenericParameter)
            .ToHashSet();

        var unhandled = types
            .Where(t => t is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false })
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !handled.Contains(t))
            .Select(t => t.FullName)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Joined, so the failure lists every request: BeEmpty on the list shows only the first.
        string.Join(Environment.NewLine, unhandled).Should().BeEmpty(
            "MediatR throws at Send when a request has no handler, so a request without one fails " +
            "only at run time — and, sent from a background task, only in the log. Write the " +
            "handler, or delete the request and its validator.");
    }
}
