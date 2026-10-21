namespace Api.SharedKernel.Constants;

/// <summary>
/// Chiavi di <c>system_configurations</c> che il prodotto scrive per conto proprio e che l'API admin delle
/// configurazioni non deve né creare, né modificare, né cancellare, né esportare (#4612).
/// </summary>
internal static class ReservedConfigurationKeys
{
    /// <summary>
    /// La riga (Environment <c>All</c>) il cui flag <c>BootstrapAdminCreated</c> rende single-use il claim del
    /// primo admin in <c>RegisterCommandHandler</c>. Chi la riscrive o la toglie riapre il claim a chiunque
    /// conosca il token di bootstrap.
    /// </summary>
    /// <remarks>
    /// Il claim cerca la riga con questa stringa esatta e con Environment <c>All</c>. Il confronto di
    /// <see cref="IsReserved"/> invece ignora maiuscole e Environment: protegge più del necessario, e va bene
    /// così, perché nessun altro ha motivo di scrivere questa chiave.
    /// </remarks>
    public const string BootstrapAdmin = "Authentication:BootstrapAdmin";

    /// <summary>
    /// <c>true</c> se <paramref name="key"/> è una chiave riservata. Maiuscole e spazi ai bordi non contano:
    /// <c>ConfigKey</c> toglie gli spazi, e il database non è il solo posto in cui una chiave viene confrontata.
    /// </summary>
    public static bool IsReserved(string? key) =>
        key is not null && string.Equals(key.Trim(), BootstrapAdmin, StringComparison.OrdinalIgnoreCase);
}
