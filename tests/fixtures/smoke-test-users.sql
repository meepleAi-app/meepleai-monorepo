-- Smoke test user fixture — applied AFTER EF Core migrations.
--
-- Persona: smoke-aaron@meepleai.test (free-tier, Role=user)
-- Used by: tests/api-smoke/bruno-collection/* (Bruno collection runs against
--   running API; SG1-SG4 sub-issues #902-905 use this persona to validate
--   tier-quota gating in API smoke tests).
--
-- ⚠️  DO NOT confuse with badsworm@alice.it (Aaron, real superadmin in DB)
-- ⚠️  DO NOT confuse with smoke-user@meepleai.test (admin, used by nightly E2E)
--
-- PBKDF2-HMAC-SHA256 hash (matches PasswordHashingService exactly).
-- Format: v1.<iterations>.<base64Salt>.<base64Hash>
-- Parameters: 210 000 iterations | SHA-256 | 16-byte salt | 32-byte hash | UTF-8 password encoding
-- To regenerate, use a standalone C# console app with Rfc2898DeriveBytes.Pbkdf2:
--   var salt = RandomNumberGenerator.GetBytes(16);
--   var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes("SmokeAaron1!!"), salt, 210_000, HashAlgorithmName.SHA256, 32);
--   Console.WriteLine("v1.210000." + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash));
-- (committed once — DO NOT regenerate per run or integration tests will break)
--
-- UUID note: "Id" column is PostgreSQL uuid type (strict RFC-4122).
-- We use a memorable but valid v4 UUID: version nibble = 4, variant nibble = 8.
-- 00000000-0000-4000-8000-000000005a01 (last segment encodes "smoke-aaron" as hex shorthand)
--
-- Idempotent: ON CONFLICT ("Email") DO NOTHING — safe to apply multiple times.

INSERT INTO "users" (
    "Id",
    "Email",
    "DisplayName",
    "PasswordHash",
    "Role",
    "Tier",
    "CreatedAt",
    "IsDemoAccount",
    "Language",
    "EmailNotifications",
    "Theme",
    "DataRetentionDays",
    "IsTwoFactorEnabled",
    "EmailVerified",
    "EmailVerifiedAt",
    "IsSuspended",
    "Status",
    "Level",
    "ExperiencePoints",
    "FailedLoginAttempts",
    "IsContributor",
    "OnboardingCompleted",
    "OnboardingSkipped"
)
VALUES (
    '00000000-0000-4000-8000-000000005a01',
    'smoke-aaron@meepleai.test',
    'Smoke Aaron Free-Tier',
    'v1.210000.tHsjIM/Tr5TZr/5g06Cniw==.BItsiXLcDjIEvCl0VvzcvTjzAHpTrF1A7HROlLrqKJY=',
    'user',
    'free',
    NOW(),
    TRUE,
    'it',
    FALSE,
    'system',
    90,
    FALSE,
    TRUE,
    NOW(),
    FALSE,
    'Active',
    1,
    0,
    0,
    FALSE,
    TRUE,
    FALSE
)
ON CONFLICT ("Email") DO NOTHING;
