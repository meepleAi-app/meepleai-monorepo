using System.Security.Cryptography;
using System.Text;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

public sealed record VersionHash(string Value)
{
    public static VersionHash Compute(IEnumerable<(Guid Id, string Statement, int ExpectedPage)> claims, IEnumerable<string> tagNames)
    {
        var sb = new StringBuilder();
        foreach (var c in claims.OrderBy(x => x.Id))
            sb.Append(c.Id).Append('|').Append(c.Statement).Append('|').Append(c.ExpectedPage).Append('\n');
        sb.Append("---\n");
        foreach (var t in tagNames.OrderBy(x => x, StringComparer.Ordinal))
            sb.Append(t).Append('\n');
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return new VersionHash(Convert.ToHexString(bytes).ToLowerInvariant());
    }
}
