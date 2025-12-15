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
