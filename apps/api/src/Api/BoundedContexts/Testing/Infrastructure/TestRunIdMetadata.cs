using System.Text.RegularExpressions;

namespace Api.BoundedContexts.Testing.Infrastructure;

public static partial class TestRunIdMetadata
{
    public const string ShadowPropertyName = "TestRunId";

    [GeneratedRegex(@"^e2e-[a-zA-Z0-9]{8,32}-\d{13}$", RegexOptions.Compiled, matchTimeoutMilliseconds: 500)]
    public static partial Regex Format();

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Format().IsMatch(value);
}
