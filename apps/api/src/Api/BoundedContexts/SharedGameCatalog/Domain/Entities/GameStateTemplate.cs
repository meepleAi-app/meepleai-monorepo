using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a game state template that defines what state needs to be tracked for a game.
/// Templates are generated from rulebook analysis (AI) or created manually.
/// Only one active version per game is allowed.
/// </summary>
public sealed class GameStateTemplate : Entity<Guid>
{
    private Guid _id;
    private Guid _sharedGameId;
    private string _name = string.Empty;
    private JsonDocument? _schema;
    private string _version = string.Empty;
    private bool _isActive;
    private GenerationSource _source;
    private decimal? _confidenceScore;
    private readonly DateTime _generatedAt;
    private Guid _createdBy;

    /// <summary>
    /// Gets the unique identifier of this template.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the shared game this template belongs to.
    /// </summary>
    public Guid SharedGameId => _sharedGameId;

    /// <summary>
    /// Gets the name of this template.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the JSON Schema defining the game state structure.
    /// </summary>
    public JsonDocument? Schema => _schema;

    /// <summary>
    /// Gets the version string (e.g., "1.0", "2.1").
    /// </summary>
    public string Version => _version;

    /// <summary>
    /// Gets whether this is the active version for the game.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets the source of this template (AI or Manual).
    /// </summary>
    public GenerationSource Source => _source;

    /// <summary>
    /// Gets the AI confidence score (0-1) for AI-generated templates.
    /// Null for manually created templates.
    /// </summary>
    public decimal? ConfidenceScore => _confidenceScore;

    /// <summary>
    /// Gets the timestamp when this template was generated/created.
    /// </summary>
    public DateTime GeneratedAt => _generatedAt;

    /// <summary>
    /// Gets the ID of the user who created this template.
    /// </summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private GameStateTemplate() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal GameStateTemplate(
        Guid id,
        Guid sharedGameId,
        string name,
        JsonDocument? schema,
        string version,
        bool isActive,
        GenerationSource source,
        decimal? confidenceScore,
        DateTime generatedAt,
        Guid createdBy) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _name = name;
        _schema = schema;
        _version = version;
        _isActive = isActive;
        _source = source;
        _confidenceScore = confidenceScore;
        _generatedAt = generatedAt;
        _createdBy = createdBy;
    }

    /// <summary>
    /// Creates a new AI-generated GameStateTemplate.
    /// </summary>
    public static GameStateTemplate CreateFromAI(
        Guid sharedGameId,
        string name,
        JsonDocument schema,
        Guid createdBy,
        decimal confidenceScore,
        string? version = null)
    {
        ValidateCreateParameters(sharedGameId, name, createdBy);

        if (confidenceScore < 0 || confidenceScore > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence score must be between 0 and 1");

        var templateVersion = version != null
            ? TemplateVersion.Create(version)
            : TemplateVersion.Default;

        return new GameStateTemplate(
            Guid.NewGuid(),
            sharedGameId,
            name,
            schema,
            templateVersion.Value,
            false, // Not active by default
            GenerationSource.AI,
            confidenceScore,
            DateTime.UtcNow,
            createdBy);
    }

    /// <summary>
    /// Creates a new manually created GameStateTemplate.
    /// </summary>
    public static GameStateTemplate CreateManual(
        Guid sharedGameId,
        string name,
        JsonDocument schema,
        Guid createdBy,
        string? version = null)
    {
        ValidateCreateParameters(sharedGameId, name, createdBy);

        var templateVersion = version != null
            ? TemplateVersion.Create(version)
            : TemplateVersion.Default;

        return new GameStateTemplate(
            Guid.NewGuid(),
            sharedGameId,
            name,
            schema,
            templateVersion.Value,
            false, // Not active by default
            GenerationSource.Manual,
            null, // No confidence score for manual templates
            DateTime.UtcNow,
            createdBy);
    }

    /// <summary>
    /// Sets this template as the active version.
    /// </summary>
    public void SetAsActive()
    {
        _isActive = true;
    }

    /// <summary>
    /// Deactivates this template version.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
    }

    /// <summary>
    /// Updates the schema of this template.
    /// Creates a new version based on the existing one.
    /// </summary>
    public void UpdateSchema(JsonDocument newSchema, string newVersion)
    {
        ArgumentNullException.ThrowIfNull(newSchema);

        var templateVersion = TemplateVersion.Create(newVersion);
        var currentVersion = TemplateVersion.Create(_version);

        if (!templateVersion.IsNewerThan(currentVersion))
            throw new InvalidOperationException($"New version {newVersion} must be greater than current version {_version}");

        _schema?.Dispose();
        _schema = newSchema;
        _version = templateVersion.Value;
        _source = GenerationSource.Manual; // Any manual edit changes source to Manual
        _confidenceScore = null; // Clear confidence score after manual edit
    }

    /// <summary>
    /// Updates the name of this template.
    /// </summary>
    public void UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));

        if (newName.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(newName));

        _name = newName.Trim();
    }

    /// <summary>
    /// Gets the schema as a raw JSON string.
    /// </summary>
    public string? GetSchemaAsString()
    {
        return _schema?.RootElement.GetRawText();
    }

    private static void ValidateCreateParameters(Guid sharedGameId, string name, Guid createdBy)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(name));

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));
    }
}
