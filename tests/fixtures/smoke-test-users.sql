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
-- BCrypt hash regenerable with:
--   python -c "import bcrypt; print(bcrypt.hashpw(b'SmokeAaron1!!', bcrypt.gensalt(12)).decode())"
-- (committed once — DO NOT regenerate per run or integration tests will break;
--  the hash below was generated with Python bcrypt, cost factor 12)
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
    '$2b$12$67gcjKPZeNs3ZRyiR2u0duROna5jW0RAwSN2GWQOjjw3Bu0/vbmk6',
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
