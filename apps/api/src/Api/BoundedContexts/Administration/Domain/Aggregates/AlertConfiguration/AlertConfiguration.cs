namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;

/// <summary>
/// AlertConfiguration Aggregate Root (Issue #921)
/// </summary>
internal class AlertConfiguration
{
    public string ConfigKey { get; private set; }
    public string ConfigValue { get; private set; }
    public ConfigCategory Category { get; private set; }
    public bool IsEncrypted { get; private set; }
    public string? Description { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; }

    public Guid Id { get; private set; }

    private AlertConfiguration(Guid id, string configKey, string configValue, ConfigCategory category, string updatedBy)
    {
        Id = id;
        ConfigKey = configKey;
        ConfigValue = configValue;
        Category = category;
        IsEncrypted = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public static AlertConfiguration Create(string configKey, string configValue, ConfigCategory category, string updatedBy, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(configKey)) throw new ArgumentException("ConfigKey cannot be empty", nameof(configKey));
        if (string.IsNullOrWhiteSpace(configValue)) throw new ArgumentException("ConfigValue cannot be empty", nameof(configValue));

        var config = new AlertConfiguration(Guid.NewGuid(), configKey, configValue, category, updatedBy)
        {
            Description = description
        };
        return config;
    }

    /// <summary>
    /// Ricostituisce una configurazione letta dal database, così com'è (#4552).
    /// </summary>
    /// <remarks>
    /// Solo per il repository. <see cref="Create"/> è la factory di una configurazione nuova: genera l'id, la data
    /// ad "adesso" e la dichiara non cifrata. Usata per ricostruire una riga, dava a ogni lettura un id che nel
    /// database non esiste: la modifica di una chiave esistente cercava la riga per quell'id e falliva sempre, e
    /// la lettura riportava data e <see cref="IsEncrypted"/> sbagliati. Nessuna validazione: la riga è già stata
    /// scritta, e un valore salvato non diventa illeggibile perché la regola di una configurazione nuova cambia.
    /// </remarks>
    internal static AlertConfiguration Reconstitute(
        Guid id,
        string configKey,
        string configValue,
        ConfigCategory category,
        bool isEncrypted,
        string? description,
        DateTime updatedAt,
        string updatedBy) => new(id, configKey, configValue, category, updatedBy)
        {
            IsEncrypted = isEncrypted,
            Description = description,
            UpdatedAt = updatedAt,
        };

    /// <summary>
    /// Sostituisce il valore con uno in chiaro. Per un valore cifrato c'è <see cref="Encrypt"/>.
    /// </summary>
    /// <remarks>
    /// Il flag torna a <c>false</c> (#4552): il valore che arriva qui non è cifrato, e una riga cifrata aggiornata
    /// così resterebbe altrimenti marcata cifrata con un valore in chiaro.
    /// </remarks>
    public void UpdateValue(string configValue, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(configValue)) throw new ArgumentException("ConfigValue cannot be empty", nameof(configValue));
        ConfigValue = configValue;
        IsEncrypted = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Encrypt(string encryptedValue, string updatedBy)
    {
        ConfigValue = encryptedValue;
        IsEncrypted = true;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Decrypt(string decryptedValue, string updatedBy)
    {
        ConfigValue = decryptedValue;
        IsEncrypted = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
