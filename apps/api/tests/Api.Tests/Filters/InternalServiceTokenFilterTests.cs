using Api.Filters;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Filters;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class InternalServiceTokenFilterTests
{
    private static InternalServiceTokenFilter Filter(string? configured) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [InternalServiceTokenFilter.ConfigKey] = configured })
            .Build());

    private static async Task<int?> StatusOf(InternalServiceTokenFilter filter, string? header)
    {
        var httpContext = new DefaultHttpContext();
        if (header is not null)
        {
            httpContext.Request.Headers[InternalServiceTokenFilter.HeaderName] = header;
        }

        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok("passed")));
        return result is IStatusCodeHttpResult status ? status.StatusCode : null;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task InvokeAsync_TokenNotConfigured_Returns404(string? configured)
    {
        (await StatusOf(Filter(configured), "anything")).Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task InvokeAsync_MissingHeader_Returns401()
    {
        (await StatusOf(Filter("secret-1"), null)).Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("secret-10")]
    [InlineData("secret-2")]
    public async Task InvokeAsync_WrongToken_Returns403(string header)
    {
        (await StatusOf(Filter("secret-1"), header)).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_RightToken_Passes()
    {
        (await StatusOf(Filter("secret-1"), "secret-1")).Should().Be(StatusCodes.Status200OK);
    }
}
