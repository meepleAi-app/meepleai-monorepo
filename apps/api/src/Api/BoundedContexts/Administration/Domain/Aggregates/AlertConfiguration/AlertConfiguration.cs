namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;

/// <summary>
/// AlertConfiguration Aggregate Root (Issue #921)
/// </summary>
public class AlertConfiguration
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

    public void UpdateValue(string configValue, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(configValue)) throw new ArgumentException("ConfigValue cannot be empty", nameof(configValue));
        ConfigValue = configValue;
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
