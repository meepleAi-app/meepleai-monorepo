using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;

namespace Api.Services;

public class N8nTemplateService
{
    private readonly MeepleAiDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<N8nTemplateService> _logger;
    private readonly string _templatesPath;

    public N8nTemplateService(
        MeepleAiDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<N8nTemplateService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        // Templates directory relative to project root
        var baseDir = Directory.GetCurrentDirectory();
        _templatesPath = Path.Combine(baseDir, "..", "..", "infra", "n8n", "templates");

        // Ensure templates directory exists
        if (!Directory.Exists(_templatesPath))
        {
            Directory.CreateDirectory(_templatesPath);
            _logger.LogInformation("Created templates directory at {Path}", _templatesPath);
        }
    }

    /// <summary>
    /// Get all available templates, optionally filtered by category
    /// </summary>
    public async Task<List<WorkflowTemplateDto>> GetTemplatesAsync(
        string? category = null,
        CancellationToken ct = default)
    {
        var templates = new List<WorkflowTemplateDto>();

        if (!Directory.Exists(_templatesPath))
        {
            _logger.LogWarning("Templates directory not found at {Path}", _templatesPath);
            return templates;
        }

        var templateFiles = Directory.GetFiles(_templatesPath, "*.json", SearchOption.TopDirectoryOnly);

        foreach (var file in templateFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var template = JsonSerializer.Deserialize<WorkflowTemplateFile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (template != null)
                {
                    // Filter by category if specified
                    if (category == null || template.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    {
                        templates.Add(new WorkflowTemplateDto(
                            template.Id,
                            template.Name,
                            template.Version,
                            template.Description,
                            template.Category,
                            template.Author,
                            template.Tags,
                            template.Icon,
                            template.Screenshot,
                            template.Documentation,
                            template.Parameters.Select(p => new TemplateParameterDto(
                                p.Name,
                                p.Type,
                                p.Label,
                                p.Description,
                                p.Required,
                                p.Default,
                                p.Options,
                                p.Sensitive
                            )).ToList()
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load template from {File}", file);
            }
        }

        return templates
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToList();
    }

    /// <summary>
    /// Get a specific template by ID with full workflow details
    /// </summary>
    public async Task<WorkflowTemplateDetailDto?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default)
    {
        var filePath = Path.Combine(_templatesPath, $"{templateId}.json");

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Template {TemplateId} not found at {Path}", templateId, filePath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var template = JsonSerializer.Deserialize<WorkflowTemplateFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (template == null)
            {
                return null;
            }

            return new WorkflowTemplateDetailDto(
                template.Id,
                template.Name,
                template.Version,
                template.Description,
                template.Category,
                template.Author,
                template.Tags,
                template.Icon,
                template.Screenshot,
                template.Documentation,
                template.Parameters.Select(p => new TemplateParameterDto(
                    p.Name,
                    p.Type,
                    p.Label,
                    p.Description,
                    p.Required,
                    p.Default,
                    p.Options,
                    p.Sensitive
                )).ToList(),
                template.Workflow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load template {TemplateId}", templateId);
            return null;
        }
    }

    /// <summary>
    /// Import a template into n8n by creating a new workflow with substituted parameters
    /// </summary>
    public async Task<ImportTemplateResponse> ImportTemplateAsync(
        string templateId,
        Dictionary<string, string> parameters,
        string userId,
        CancellationToken ct = default)
    {
        // Load template
        var template = await GetTemplateAsync(templateId, ct);
        if (template == null)
        {
            throw new InvalidOperationException($"Template '{templateId}' not found");
        }

        // Validate required parameters
        ValidateParameters(template.Parameters, parameters);

        // Substitute parameters in workflow JSON
        var workflowJson = SubstituteParameters(template.Workflow, parameters);

        // Get active n8n configuration
        var n8nConfig = await _db.N8nConfigs
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (n8nConfig == null)
        {
            throw new InvalidOperationException("No active n8n configuration found. Please configure n8n first.");
        }

        // Create workflow in n8n via REST API
        var workflowId = await CreateWorkflowInN8nAsync(
            n8nConfig,
            template.Name,
            workflowJson,
            ct);

        _logger.LogInformation(
            "Imported template {TemplateId} as workflow {WorkflowId} for user {UserId}",
            templateId,
            workflowId,
            userId);

        return new ImportTemplateResponse(
            workflowId,
            $"Template '{template.Name}' imported successfully as workflow '{workflowId}'"
        );
    }

    /// <summary>
    /// Validate template JSON structure
    /// </summary>
    public ValidateTemplateResponse ValidateTemplate(string templateJson)
    {
        var errors = new List<string>();

        try
        {
            var template = JsonSerializer.Deserialize<WorkflowTemplateFile>(
                templateJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (template == null)
            {
                errors.Add("Failed to deserialize template JSON");
                return new ValidateTemplateResponse(false, errors);
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(template.Id))
                errors.Add("Template ID is required");

            if (string.IsNullOrWhiteSpace(template.Name))
                errors.Add("Template name is required");

            if (string.IsNullOrWhiteSpace(template.Version))
                errors.Add("Template version is required");

            if (string.IsNullOrWhiteSpace(template.Description))
                errors.Add("Template description is required");

            if (string.IsNullOrWhiteSpace(template.Category))
                errors.Add("Template category is required");

            if (template.Workflow == null)
                errors.Add("Workflow definition is required");

            if (template.Parameters == null)
                errors.Add("Parameters array is required (can be empty)");

            // Validate workflow structure
            if (template.Workflow is JsonElement workflowElement)
            {
                if (!workflowElement.TryGetProperty("nodes", out _))
                    errors.Add("Workflow must have 'nodes' property");

                if (!workflowElement.TryGetProperty("connections", out _))
                    errors.Add("Workflow must have 'connections' property");
            }

            // Validate parameter definitions
            if (template.Parameters != null)
            {
                for (int i = 0; i < template.Parameters.Count; i++)
                {
                    var param = template.Parameters[i];
                    if (string.IsNullOrWhiteSpace(param.Name))
                        errors.Add($"Parameter [{i}] name is required");

                    if (string.IsNullOrWhiteSpace(param.Type))
                        errors.Add($"Parameter [{i}] type is required");

                    if (string.IsNullOrWhiteSpace(param.Label))
                        errors.Add($"Parameter [{i}] label is required");
                }
            }

            return new ValidateTemplateResponse(errors.Count == 0, errors.Count > 0 ? errors : null);
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return new ValidateTemplateResponse(false, errors);
        }
    }

    #region Private Helper Methods

    private void ValidateParameters(
        List<TemplateParameterDto> templateParams,
        Dictionary<string, string> providedParams)
    {
        var missingParams = new List<string>();

        foreach (var param in templateParams.Where(p => p.Required))
        {
            if (!providedParams.ContainsKey(param.Name) ||
                string.IsNullOrWhiteSpace(providedParams[param.Name]))
            {
                missingParams.Add(param.Name);
            }
        }

        if (missingParams.Any())
        {
            throw new InvalidOperationException(
                $"Missing required parameters: {string.Join(", ", missingParams)}");
        }

        // Validate parameter types (basic validation)
        foreach (var param in templateParams)
        {
            if (!providedParams.ContainsKey(param.Name))
                continue;

            var value = providedParams[param.Name];

            switch (param.Type.ToLower())
            {
                case "number":
                    if (!int.TryParse(value, out _) && !double.TryParse(value, out _))
                    {
                        throw new InvalidOperationException(
                            $"Parameter '{param.Name}' must be a number");
                    }
                    break;

                case "boolean":
                    if (!bool.TryParse(value, out _))
                    {
                        throw new InvalidOperationException(
                            $"Parameter '{param.Name}' must be true or false");
                    }
                    break;

                case "select":
                    if (param.Options != null && !param.Options.Contains(value))
                    {
                        throw new InvalidOperationException(
                            $"Parameter '{param.Name}' must be one of: {string.Join(", ", param.Options)}");
                    }
                    break;
            }
        }
    }

    private string SubstituteParameters(
        object workflow,
        Dictionary<string, string> parameters)
    {
        // Serialize workflow to JSON string
        var workflowJson = JsonSerializer.Serialize(workflow);

        // Apply parameter substitutions using regex to handle {{paramName}} placeholders
        foreach (var (key, value) in parameters)
        {
            var pattern = $@"\{{\{{\s*{Regex.Escape(key)}\s*\}}}}";
            workflowJson = Regex.Replace(workflowJson, pattern, value);
        }

        // Also apply defaults for any remaining placeholders
        // This handles parameters with default values that weren't explicitly provided
        var defaultPattern = @"\{\{(\w+)\}\}";
        var matches = Regex.Matches(workflowJson, defaultPattern);

        if (matches.Count > 0)
        {
            var unreplacedParams = matches.Select(m => m.Groups[1].Value).Distinct().ToList();
            _logger.LogWarning(
                "Template contains unreplaced parameters: {Parameters}. These will be left as placeholders.",
                string.Join(", ", unreplacedParams));
        }

        return workflowJson;
    }

    private async Task<string> CreateWorkflowInN8nAsync(
        N8nConfigEntity config,
        string workflowName,
        string workflowJson,
        CancellationToken ct)
    {
        var httpClient = _httpClientFactory.CreateClient();

        // Decrypt API key
        var apiKey = DecryptApiKey(config.ApiKeyEncrypted);

        // Parse workflow JSON to add metadata
        var workflowData = JsonSerializer.Deserialize<JsonElement>(workflowJson);

        // Create n8n workflow creation request
        var createRequest = new
        {
            name = $"{workflowName} (Imported)",
            nodes = workflowData.GetProperty("nodes"),
            connections = workflowData.GetProperty("connections"),
            settings = workflowData.TryGetProperty("settings", out var settings)
                ? settings
                : (object)new { },
            staticData = workflowData.TryGetProperty("staticData", out var staticData)
                ? staticData
                : (object)new { },
            active = false // Don't activate immediately
        };

        var requestJson = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/api/v1/workflows");
        request.Headers.Add("X-N8N-API-KEY", apiKey);
        request.Content = content;

        try
        {
            var response = await httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to create workflow in n8n. Status: {Status}, Response: {Response}",
                    response.StatusCode,
                    responseBody);
                throw new InvalidOperationException(
                    $"Failed to create workflow in n8n: {response.StatusCode} - {responseBody}");
            }

            var responseData = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var workflowId = responseData.GetProperty("id").GetString();

            if (string.IsNullOrEmpty(workflowId))
            {
                throw new InvalidOperationException("n8n API did not return a workflow ID");
            }

            _logger.LogInformation("Successfully created workflow {WorkflowId} in n8n", workflowId);
            return workflowId;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while creating workflow in n8n");
            throw new InvalidOperationException($"Failed to connect to n8n: {ex.Message}", ex);
        }
    }

    private string DecryptApiKey(string encryptedApiKey)
    {
        // Use the same decryption logic as N8nConfigService
        var encryptionKey = GetEncryptionKey();
        var fullCipher = Convert.FromBase64String(encryptedApiKey);

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = encryptionKey;

        var iv = new byte[aes.IV.Length];
        var cipher = new byte[fullCipher.Length - iv.Length];

        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private const string EncryptionKeyConfigName = "N8N_ENCRYPTION_KEY";
    private const string EncryptionKeyPlaceholder = "changeme-replace-with-32-byte-key-in-production";

    private byte[] GetEncryptionKey()
    {
        var key = _configuration[EncryptionKeyConfigName]?.Trim();

        if (string.IsNullOrWhiteSpace(key) || key == EncryptionKeyPlaceholder)
        {
            throw new InvalidOperationException(
                $"Missing or invalid n8n encryption key. Set the {EncryptionKeyConfigName} environment variable to a secure value.");
        }

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
    }

    #endregion

    #region Internal Template File Models

    // Internal model for deserializing template JSON files
    private class WorkflowTemplateFile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Author { get; set; } = "MeepleAI";
        public List<string> Tags { get; set; } = new();
        public string Icon { get; set; } = "📋";
        public string? Screenshot { get; set; }
        public string? Documentation { get; set; }
        public List<TemplateParameter> Parameters { get; set; } = new();
        public object Workflow { get; set; } = new();
    }

    private class TemplateParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Required { get; set; }
        public string? Default { get; set; }
        public List<string>? Options { get; set; }
        public bool Sensitive { get; set; }
    }

    #endregion
}
