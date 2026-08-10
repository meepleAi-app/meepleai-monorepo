using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;

/// <summary>Why a requested evaluation dataset path was refused.</summary>
internal enum EvalDatasetPathError
{
    /// <summary>No dataset root is configured: the subsystem is unavailable, not the request invalid.</summary>
    RootNotConfigured,

    /// <summary>Empty or whitespace.</summary>
    Empty,

    /// <summary>Absolute/rooted path, or a path that escapes the root (<c>..</c>, drive-qualified, UNC).</summary>
    OutsideRoot,

    /// <summary>Not a <c>.json</c> file.</summary>
    NotJson,
}

/// <summary>Outcome of resolving a caller-supplied dataset path against the configured root.</summary>
internal readonly record struct EvalDatasetPathResult(string? FullPath, EvalDatasetPathError? Error)
{
    public bool IsSuccess => Error is null;

    public static EvalDatasetPathResult Ok(string fullPath) => new(fullPath, null);

    public static EvalDatasetPathResult Fail(EvalDatasetPathError error) => new(null, error);
}

/// <summary>
/// Issue #3438 — resolves a caller-supplied evaluation dataset name against a fixed root, so the
/// admin eval endpoints can never read or write outside it.
/// </summary>
internal interface IEvalDatasetPathResolver
{
    /// <summary>Resolves a path for READING. The file must exist inside the root.</summary>
    EvalDatasetPathResult ResolveForRead(string? requestedPath);

    /// <summary>
    /// Resolves a path for WRITING. Identical containment rules, but the file need not exist yet —
    /// merge-labels legitimately targets a new file.
    /// </summary>
    EvalDatasetPathResult ResolveForWrite(string? requestedPath);
}

/// <summary>
/// Issue #3438 — sandboxes the evaluation dataset paths accepted by the admin eval endpoints.
///
/// <para>
/// Before this, <c>datasetPath</c> reached <c>File.ReadAllTextAsync</c> unfiltered and
/// <c>outputPath</c> reached the writer: an admin session — or a stolen one, or a CSRF — could read
/// any file the process could read (<c>appsettings.json</c>, mounted secrets) and, through
/// merge-labels, WRITE to an arbitrary path. Admin-gating narrows who can do it; it does not make
/// the capability acceptable.
/// </para>
/// <para>
/// <b>Fail-closed.</b> With no configured root every request is refused. The alternative — guessing
/// a default like the repo's <c>tests/evaluation-datasets/</c> — would hand back an implicit
/// sandbox that nobody chose and that silently differs per environment. Refusing until an operator
/// states where datasets live is the safe direction, and the issue's blocking question («where do
/// evaluation datasets live in production?») stops being a prerequisite: it becomes configuration.
/// </para>
/// <para>
/// Configuration key <c>Evaluation:DatasetRoot</c> (env <c>Evaluation__DatasetRoot</c>), with
/// <c>EVAL_DATASET_ROOT</c> accepted as a flat alias for parity with the other service URLs.
/// </para>
/// </summary>
internal sealed class EvalDatasetPathResolver : IEvalDatasetPathResolver
{
    internal const string RootConfigKey = "Evaluation:DatasetRoot";
    internal const string RootEnvAlias = "EVAL_DATASET_ROOT";

    private readonly string? _rootFullPath;

    public EvalDatasetPathResolver(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration[RootConfigKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = configuration[RootEnvAlias];
        }

        // Normalised once at construction: every later comparison is against a canonical, trailing
        // separator-terminated absolute path, so prefix matching cannot be fooled by a sibling
        // directory whose name merely starts with the root's ("/data/evalX" vs "/data/eval").
        _rootFullPath = string.IsNullOrWhiteSpace(configured)
            ? null
            : EnsureTrailingSeparator(Path.GetFullPath(configured));
    }

    public EvalDatasetPathResult ResolveForRead(string? requestedPath)
    {
        var resolved = Resolve(requestedPath);
        if (!resolved.IsSuccess)
        {
            return resolved;
        }

        // Existence is checked by the caller (404 vs 400 is an HTTP concern). Deliberately NOT
        // checked here: doing so would turn this resolver into a file-existence oracle for paths
        // it just refused, which is the leak the sandbox exists to prevent.
        return resolved;
    }

    public EvalDatasetPathResult ResolveForWrite(string? requestedPath) => Resolve(requestedPath);

    private EvalDatasetPathResult Resolve(string? requestedPath)
    {
        if (_rootFullPath is null)
        {
            return EvalDatasetPathResult.Fail(EvalDatasetPathError.RootNotConfigured);
        }

        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return EvalDatasetPathResult.Fail(EvalDatasetPathError.Empty);
        }

        // Rooted paths are refused outright rather than combined: Path.Combine(root, "/etc/passwd")
        // returns "/etc/passwd", silently discarding the root. On Windows this also covers
        // "C:\..." and UNC "\\server\share".
        if (Path.IsPathRooted(requestedPath))
        {
            return EvalDatasetPathResult.Fail(EvalDatasetPathError.OutsideRoot);
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(_rootFullPath, requestedPath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Malformed input (invalid characters, over-length) is a refusal, never a 500.
            return EvalDatasetPathResult.Fail(EvalDatasetPathError.OutsideRoot);
        }

        // Containment is decided AFTER normalisation, so "a/../../etc/passwd" is caught by where it
        // actually lands rather than by pattern-matching "..", which misses encodings and rejects
        // harmless names.
        if (!candidate.StartsWith(_rootFullPath, StringComparison.Ordinal))
        {
            return EvalDatasetPathResult.Fail(EvalDatasetPathError.OutsideRoot);
        }

        // Datasets are JSON. Narrowing the extension keeps the sandbox from doubling as a reader
        // for whatever else happens to sit under the root.
        if (!candidate.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return EvalDatasetPathResult.Fail(EvalDatasetPathError.NotJson);
        }

        return EvalDatasetPathResult.Ok(candidate);
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
