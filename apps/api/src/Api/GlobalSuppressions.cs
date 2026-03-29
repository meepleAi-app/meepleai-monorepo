using System.Diagnostics.CodeAnalysis;

// S2325: Methods and properties that don't access instance data should be static
[assembly: SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Refactoring candidate for future cleanup")]

// CA1822: Mark members as static
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Refactoring candidate for future cleanup")]

// CA1848: Use the LoggerMessage delegates
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "High volume, low impact. Requires pervasive refactoring.")]

// CA1031: Do not catch general exception types
// Kept as a global suppression because catch(Exception) is used across ~425 files at legitimate
// service/infrastructure boundaries: health checks, background tasks, middleware, and event handlers
// where the caller must receive a result or the process must continue regardless of exception type.
// Files that are clearly boundary points already carry local #pragma warning disable CA1031 comments
// (244 files as of this writing). The remaining sites are tracked for incremental improvement.
// Do NOT use this global as license to swallow exceptions silently — every catch site must log or re-throw.
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentional at service/infrastructure/middleware boundaries. ~244 sites have local pragmas; remainder tracked for incremental improvement. See GlobalSuppressions.cs comment.")]

// CA1305: Specify IFormatProvider
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Local application context — all string formatting is user-facing UI text or internal logging, not culture-sensitive data exchange.")]

// CA2016: Forward the 'CancellationToken' parameter to methods
// Kept as global suppression because CancellationToken propagation gaps exist across hundreds of
// async call sites in services, repositories, and event handlers. Fixing all sites requires a
// dedicated async-reliability pass to avoid unintentional behavioral changes in long-running tasks.
// New code should always forward CancellationToken where available.
[assembly: SuppressMessage("Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "Hundreds of sites require a dedicated async-reliability pass. New code should always forward CancellationToken. See GlobalSuppressions.cs comment.")]

// CA1711: Identifiers should not have incorrect suffix
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain-driven naming uses suffixes like 'Exception', 'Collection', 'Repository' that match DDD conventions but trigger this rule.")]

// CA1861: Prefer 'static readonly' fields over constant array arguments
[assembly: SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Migrations are executed once and do not require this optimization", Scope = "namespaceanddescendants", Target = "~N:Api.Migrations")]

// CA1062: Validate arguments of public methods
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Auto-generated migration code - EF Core guarantees non-null parameters", Scope = "namespaceanddescendants", Target = "~N:Api.Migrations")]

// MA0048: File name must match type name - EF Core migrations have timestamps in filenames but not class names
[assembly: SuppressMessage("Design", "MA0048:File name must match type name", Justification = "EF Core auto-generates migration class names without file timestamp prefix", Scope = "namespaceanddescendants", Target = "~N:Api.Infrastructure.Migrations")]

// MA0048: File name must match type name - CQRS pattern: Commands/Queries paired with DTOs/Handlers in same file
[assembly: SuppressMessage("Design", "MA0048:File name must match type name", Justification = "CQRS pattern groups related types (Command+Handler, Query+DTO) in single files for cohesion", Scope = "namespaceanddescendants", Target = "~N:Api.BoundedContexts")]
