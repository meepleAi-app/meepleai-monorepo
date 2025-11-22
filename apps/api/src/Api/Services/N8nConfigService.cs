using System.Security.Cryptography;
using System.Text;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class N8nConfigService
{
    private readonly MeepleAiDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<N8nConfigService> _logger;
    private readonly TimeProvider _timeProvider;

    public N8nConfigService(
        MeepleAiDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<N8nConfigService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<List<N8nConfigDto>> GetConfigsAsync(CancellationToken ct)
    {
        var configs = await _db.N8nConfigs
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return configs.Select(c => new N8nConfigDto(
            c.Id.ToString(),
            c.Name,
            c.BaseUrl,
            c.WebhookUrl,
            c.IsActive,
            c.LastTestedAt,
            c.LastTestResult,
            c.CreatedAt,
            c.UpdatedAt
        )).ToList();
    }

    public async Task<N8nConfigDto?> GetConfigAsync(string configId, CancellationToken ct)
    {
        if (!Guid.TryParse(configId, out var guidId))
        {
            return null;
        }

        var config = await _db.N8nConfigs
            .FirstOrDefaultAsync(c => c.Id == guidId, ct);

        if (config == null)
        {
            return null;
        }

        return new N8nConfigDto(
            config.Id.ToString(),
            config.Name,
            config.BaseUrl,
            config.WebhookUrl,
            config.IsActive,
            config.LastTestedAt,
            config.LastTestResult,
            config.CreatedAt,
            config.UpdatedAt
        );
    }

    public async Task<N8nConfigDto> CreateConfigAsync(
        string userId,
        CreateN8nConfigRequest request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new ArgumentException("Invalid user ID format", nameof(userId));
        }

        var existingConfig = await _db.N8nConfigs
            .FirstOrDefaultAsync(c => c.Name == request.Name, ct);

        if (existingConfig != null)
        {
            throw new InvalidOperationException($"Configuration with name '{request.Name}' already exists");
        }

        var config = new N8nConfigEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            BaseUrl = request.BaseUrl.TrimEnd('/'),
            ApiKeyEncrypted = EncryptApiKey(request.ApiKey),
            WebhookUrl = request.WebhookUrl?.TrimEnd('/'),
            IsActive = true,
            CreatedByUserId = userGuid,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _db.N8nConfigs.Add(config);
        await _db.SaveChangesAsync(ct);

        return new N8nConfigDto(
            config.Id.ToString(),
            config.Name,
            config.BaseUrl,
            config.WebhookUrl,
            config.IsActive,
            config.LastTestedAt,
            config.LastTestResult,
            config.CreatedAt,
            config.UpdatedAt
        );
    }

    public async Task<N8nConfigDto> UpdateConfigAsync(
        string configId,
        UpdateN8nConfigRequest request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(configId, out var guidId))
        {
            throw new ArgumentException("Invalid config ID format", nameof(configId));
        }

        var config = await _db.N8nConfigs
            .FirstOrDefaultAsync(c => c.Id == guidId, ct);

        if (config == null)
        {
            throw new InvalidOperationException("Configuration not found");
        }

        if (request.Name != null && request.Name != config.Name)
        {
            var existingConfig = await _db.N8nConfigs
                .FirstOrDefaultAsync(c => c.Name == request.Name && c.Id != guidId, ct);

            if (existingConfig != null)
            {
                throw new InvalidOperationException($"Configuration with name '{request.Name}' already exists");
            }

            config.Name = request.Name;
        }

        if (request.BaseUrl != null)
        {
            config.BaseUrl = request.BaseUrl.TrimEnd('/');
        }

        if (request.ApiKey != null)
        {
            config.ApiKeyEncrypted = EncryptApiKey(request.ApiKey);
        }

        if (request.WebhookUrl != null)
        {
            config.WebhookUrl = request.WebhookUrl.TrimEnd('/');
        }

        if (request.IsActive.HasValue)
        {
            config.IsActive = request.IsActive.Value;
        }

        config.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);

        return new N8nConfigDto(
            config.Id.ToString(),
            config.Name,
            config.BaseUrl,
            config.WebhookUrl,
            config.IsActive,
            config.LastTestedAt,
            config.LastTestResult,
            config.CreatedAt,
            config.UpdatedAt
        );
    }

    public async Task<bool> DeleteConfigAsync(string configId, CancellationToken ct)
    {
        if (!Guid.TryParse(configId, out var guidId))
        {
            return false;
        }

        var config = await _db.N8nConfigs
            .FirstOrDefaultAsync(c => c.Id == guidId, ct);

        if (config == null)
        {
            return false;
        }

        _db.N8nConfigs.Remove(config);
        await _db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<N8nTestResult> TestConnectionAsync(string configId, CancellationToken ct)
    {
        if (!Guid.TryParse(configId, out var guidId))
        {
            throw new ArgumentException("Invalid config ID format", nameof(configId));
        }

        var config = await _db.N8nConfigs
            .FirstOrDefaultAsync(c => c.Id == guidId, ct);

        if (config == null)
        {
            throw new InvalidOperationException("Configuration not found");
        }

        var apiKey = DecryptApiKey(config.ApiKeyEncrypted);
#pragma warning disable CA2000 // HttpClient lifetime managed by IHttpClientFactory
        var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl}/api/v1/workflows");
            request.Headers.Add("X-N8N-API-KEY", apiKey);

            var startTime = _timeProvider.GetUtcNow().UtcDateTime;
            using var response = await httpClient.SendAsync(request, ct);
            var latency = (int)(_timeProvider.GetUtcNow().UtcDateTime - startTime).TotalMilliseconds;

            var success = response.IsSuccessStatusCode;
            var message = success
                ? $"Connection successful ({latency}ms)"
                : $"Connection failed: {response.StatusCode}";

            config.LastTestedAt = _timeProvider.GetUtcNow().UtcDateTime;
            config.LastTestResult = message;
            await _db.SaveChangesAsync(ct);

            return new N8nTestResult(success, message, latency);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - external API calls with result pattern
        // Service layer: Catches all exceptions to return domain result object
        // Connection test failures logged, returned as test result with error details
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test n8n connection for config {ConfigId}", configId);

            var message = $"Connection failed: {ex.Message}";
            config.LastTestedAt = _timeProvider.GetUtcNow().UtcDateTime;
            config.LastTestResult = message;
            await _db.SaveChangesAsync(ct);

            return new N8nTestResult(false, message, null);
        }
#pragma warning restore CA1031
    }

    private string EncryptApiKey(string apiKey)
    {
        var encryptionKey = GetEncryptionKey();
        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(apiKey);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    private string DecryptApiKey(string encryptedApiKey)
    {
        var encryptionKey = GetEncryptionKey();
        var fullCipher = Convert.FromBase64String(encryptedApiKey);

        using var aes = Aes.Create();
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

        return CryptographyHelper.ComputeSha256HashBytes(key);
    }
}
