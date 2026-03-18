namespace Api.Infrastructure.Entities;

/// <summary>
/// User entity - persistence model.
/// DDD-PHASE2: Converted to Guid IDs and string Role for domain alignment.
/// </summary>
public class UserEntity
{
    required public Guid Id { get; set; }
    required public string Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PasswordHash { get; set; } // Nullable for OAuth-only users
    public string Role { get; set; } = "user"; // DDD-PHASE2: Changed from enum to string
    public string Tier { get; set; } = "free"; // User subscription tier (free, normal, premium)
    public DateTime CreatedAt { get; set; }
    public bool IsDemoAccount { get; set; } // Issue #1663: Demo users for testing (user/editor@meepleai.dev)

    // User Preferences
    public string Language { get; set; } = "en";
    public bool EmailNotifications { get; set; } = true;
    public string Theme { get; set; } = "system";
    public int DataRetentionDays { get; set; } = 90;

    // Two-Factor Authentication
    public string? TotpSecretEncrypted { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public DateTime? TwoFactorEnabledAt { get; set; }

    // ISSUE-3071: Email Verification
    public bool EmailVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }

    // ISSUE-3672: Email Verification Grace Period
    public DateTime? VerificationGracePeriodEndsAt { get; set; }

    // ISSUE-2886: User Suspension
    public bool IsSuspended { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? SuspendReason { get; set; }

    // Epic #4068: User Account Status (Active/Suspended/Banned)
    public string Status { get; set; } = "Active";

    // ISSUE-3141: Gamification (Level/XP)
    public int Level { get; set; } = 1;
    public int ExperiencePoints { get; set; }

    // ISSUE-3339: Account Lockout
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    // D3: Contributor flag — contributors get premium tier access
    public bool IsContributor { get; set; }

    // Issue #124: Onboarding interests
    public List<string>? Interests { get; set; }

    // Profile & Onboarding
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime? OnboardingWizardSeenAt { get; set; }
    public DateTime? OnboardingDismissedAt { get; set; }

    // Issue #323: Onboarding completion tracking
    public bool OnboardingCompleted { get; set; }
    public bool OnboardingSkipped { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }

    // Admin Invitation Flow: tracks who invited this user and when the invitation expires
    public Guid? InvitedByUserId { get; set; }
    public DateTime? InvitationExpiresAt { get; set; }

    // Navigation properties
    public ICollection<UserSessionEntity> Sessions { get; set; } = new List<UserSessionEntity>();
    public ICollection<UserBackupCodeEntity> BackupCodes { get; set; } = new List<UserBackupCodeEntity>();
    public ICollection<OAuthAccountEntity> OAuthAccounts { get; set; } = new List<OAuthAccountEntity>();
}
