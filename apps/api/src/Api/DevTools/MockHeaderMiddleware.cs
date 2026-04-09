using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Api.DevTools;

internal sealed class MockHeaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMockToggleReader _toggles;

    public MockHeaderMiddleware(RequestDelegate next, IMockToggleReader toggles)
    {
        _next = next;
        _toggles = toggles;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var mocked = _toggles.GetAll()
                .Where(kv => kv.Value)
                .Select(kv => kv.Key)
                .OrderBy(k => k, System.StringComparer.Ordinal)
                .ToList();
            var value = mocked.Count == 0 ? "backend-di:" : $"backend-di:{string.Join(",", mocked)}";
            context.Response.Headers.Append("X-Meeple-Mock", value);
            return Task.CompletedTask;
        });
        await _next(context).ConfigureAwait(false);
    }
}
