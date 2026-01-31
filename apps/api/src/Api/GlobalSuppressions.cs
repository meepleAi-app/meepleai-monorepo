using System.Diagnostics.CodeAnalysis;

// S2325: Methods and properties that don't access instance data should be static
[assembly: SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Refactoring candidate for future cleanup")]

// CA1822: Mark members as static
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Refactoring candidate for future cleanup")]

// CA1848: Use the LoggerMessage delegates
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "High volume, low impact. Requires pervasive refactoring.")]



// CA1031: Do not catch general exception types
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Legacy error handling pattern.")]

// CA1305: Specify IFormatProvider
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Local application context.")]

// CA2016: Forward the 'CancellationToken' parameter to methods
[assembly: SuppressMessage("Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "To be addressed in async reliability pass.")]

// CA1711: Identifiers should not have incorrect suffix
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Legacy naming conventions.")]

// CA1861: Prefer 'static readonly' fields over constant array arguments
[assembly: SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Migrations are executed once and do not require this optimization", Scope = "namespaceanddescendants", Target = "~N:Api.Migrations")]

// CA1062: Validate arguments of public methods
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Auto-generated migration code - EF Core guarantees non-null parameters", Scope = "namespaceanddescendants", Target = "~N:Api.Migrations")]

// MA0048: File name must match type name - EF Core migrations have timestamps in filenames but not class names
[assembly: SuppressMessage("Design", "MA0048:File name must match type name", Justification = "EF Core auto-generates migration class names without file timestamp prefix", Scope = "namespaceanddescendants", Target = "~N:Api.Infrastructure.Migrations")]

// MA0048: File name must match type name - CQRS pattern: Commands/Queries paired with DTOs/Handlers in same file
[assembly: SuppressMessage("Design", "MA0048:File name must match type name", Justification = "CQRS pattern groups related types (Command+Handler, Query+DTO) in single files for cohesion", Scope = "namespaceanddescendants", Target = "~N:Api.BoundedContexts")]
