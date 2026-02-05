using System.Security.Cryptography;
using System.Text;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Represents a private note taken by a participant during a session.
/// Notes are encrypted at rest and can be revealed to other participants.
/// </summary>
public class SessionNote
{
    private static readonly byte[] DefaultKey = GenerateKey();
    private static readonly byte[] DefaultIv = GenerateIv();

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public string EncryptedContent { get; private set; } = string.Empty;
    public bool IsRevealed { get; private set; }
    public string? ObscuredText { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private SessionNote() { }

    /// <summary>
    /// Creates a new private session note.
    /// </summary>
    /// <param name="sessionId">The session this note belongs to.</param>
    /// <param name="participantId">The participant who owns the note.</param>
    /// <param name="content">The note content (will be encrypted).</param>
    /// <param name="obscuredText">Optional text to show when obscured.</param>
    /// <returns>A new SessionNote instance.</returns>
    public static SessionNote Create(
        Guid sessionId,
        Guid participantId,
        string content,
        string? obscuredText = null)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));
        }

        if (participantId == Guid.Empty)
        {
            throw new ArgumentException("Participant ID cannot be empty.", nameof(participantId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Note content cannot be empty.", nameof(content));
        }

        var now = DateTime.UtcNow;
        return new SessionNote
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParticipantId = participantId,
            EncryptedContent = Encrypt(content),
            IsRevealed = false,
            ObscuredText = obscuredText,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Reconstitutes a SessionNote from persistence.
    /// </summary>
    public static SessionNote Reconstitute(
        Guid id,
        Guid sessionId,
        Guid participantId,
        string encryptedContent,
        bool isRevealed,
        string? obscuredText,
        DateTime createdAt,
        DateTime updatedAt,
        bool isDeleted,
        DateTime? deletedAt)
    {
        return new SessionNote
        {
            Id = id,
            SessionId = sessionId,
            ParticipantId = participantId,
            EncryptedContent = encryptedContent,
            IsRevealed = isRevealed,
            ObscuredText = obscuredText,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
        };
    }

    /// <summary>
    /// Updates the note content.
    /// </summary>
    /// <param name="content">The new content (will be encrypted).</param>
    public void UpdateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Note content cannot be empty.", nameof(content));
        }

        EncryptedContent = Encrypt(content);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the obscured text (partial reveal text).
    /// </summary>
    /// <param name="obscuredText">The text to show when obscured.</param>
    public void UpdateObscuredText(string? obscuredText)
    {
        ObscuredText = obscuredText;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reveals the note to all participants.
    /// </summary>
    public void Reveal()
    {
        IsRevealed = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Hides the note from other participants.
    /// </summary>
    public void Hide()
    {
        IsRevealed = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the decrypted content. Only accessible to the owner or when revealed.
    /// </summary>
    /// <param name="requesterId">The ID of the user requesting the content.</param>
    /// <returns>The decrypted content or obscured text.</returns>
    public string GetContent(Guid requesterId)
    {
        if (requesterId == ParticipantId || IsRevealed)
        {
            return Decrypt(EncryptedContent);
        }

        return ObscuredText ?? "***";
    }

    /// <summary>
    /// Gets the decrypted content without access check (for owner use).
    /// </summary>
    public string GetDecryptedContent()
    {
        return Decrypt(EncryptedContent);
    }

    /// <summary>
    /// Checks if the given user can view the full content.
    /// </summary>
    public bool CanView(Guid requesterId)
    {
        return requesterId == ParticipantId || IsRevealed;
    }

    /// <summary>
    /// Soft deletes the note.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Encrypts content using AES.
    /// </summary>
    private static string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = DefaultKey;
        aes.IV = DefaultIv;

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(plainBytes, 0, plainBytes.Length);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decrypts content using AES.
    /// </summary>
    private static string Decrypt(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = DefaultKey;
        aes.IV = DefaultIv;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var cipherBytes = Convert.FromBase64String(cipherText);

        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);

        return reader.ReadToEnd();
    }

    private static byte[] GenerateKey()
    {
        // In production, this should come from configuration/secrets
        // Using a deterministic key for simplicity in this implementation
        return SHA256.HashData(Encoding.UTF8.GetBytes("MeepleAI-SessionNotes-EncryptionKey-v1"));
    }

    private static byte[] GenerateIv()
    {
        // In production, IV should be unique per note and stored alongside
        // Using SHA256 and taking first 16 bytes for AES IV (128 bits)
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("MeepleAI-Notes-IV-v1"));
        return hash[..16];
    }
}
