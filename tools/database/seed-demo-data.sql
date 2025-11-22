-- Seed Demo Data for MeepleAI
-- Users: admin@meepleai.dev, editor@meepleai.dev, user@meepleai.dev (all password: Demo123!)
-- Games: tic-tac-toe, chess
-- Generated: 2025-11-05

-- Password hash for "Demo123!" (PBKDF2, 210k iterations)
-- Admin: v1.210000.pccAUuTn3Wmb42A84/iUAA==.MJMqJeHauENiCaXwOrjpBl7+ghmjcQT/e+IjXATnon8=
-- Editor: v1.210000.uSiRkC8CXopSLjNPKYMmGA==.7141Vjqa3XautblxusA8BD06lzY7If5bM/jdjutBgbw=
-- User: v1.210000.RG2uECM2+wXDfyLmB6sgVA==.lyiEgcPrY3kZP3N6wKEY9MUEYmwetidHsLc5K/RpV8Y=
-- Regenerate with: dotnet run --project apps/api/GenerateHashes Demo123!

-- Insert demo users
INSERT INTO users ("Id", "Email", "DisplayName", "PasswordHash", "Role", "IsTwoFactorEnabled", "TotpSecretEncrypted", "TwoFactorEnabledAt", "CreatedAt")
VALUES
    ('demo-admin-001', 'admin@meepleai.dev', 'Demo Admin', 'v1.210000.pccAUuTn3Wmb42A84/iUAA==.MJMqJeHauENiCaXwOrjpBl7+ghmjcQT/e+IjXATnon8=', 'admin', false, NULL, NULL, NOW()),
    ('demo-editor-001', 'editor@meepleai.dev', 'Demo Editor', 'v1.210000.uSiRkC8CXopSLjNPKYMmGA==.7141Vjqa3XautblxusA8BD06lzY7If5bM/jdjutBgbw=', 'editor', false, NULL, NULL, NOW()),
    ('demo-user-001', 'user@meepleai.dev', 'Demo User', 'v1.210000.RG2uECM2+wXDfyLmB6sgVA==.lyiEgcPrY3kZP3N6wKEY9MUEYmwetidHsLc5K/RpV8Y=', 'user', false, NULL, NULL, NOW())
ON CONFLICT ("Id") DO NOTHING;

-- Insert demo games
INSERT INTO games ("Id", "Name", "CreatedAt")
VALUES
    ('tic-tac-toe', 'Tic-Tac-Toe', NOW()),
    ('chess', 'Chess', NOW())
ON CONFLICT ("Id") DO NOTHING;

-- Insert demo rule specs
INSERT INTO rule_specs ("Id", "GameId", "Version", "CreatedAt", "CreatedByUserId")
VALUES
    (gen_random_uuid(), 'tic-tac-toe', 'v1.0', NOW(), 'demo-admin-001'),
    (gen_random_uuid(), 'chess', 'v1.0', NOW(), 'demo-admin-001')
ON CONFLICT ("GameId", "Version") DO NOTHING;

-- Insert demo agents
INSERT INTO agents ("Id", "GameId", "Name", "Kind", "CreatedAt")
VALUES
    ('agent-ttt-explain', 'tic-tac-toe', 'Tic-Tac-Toe Explainer', 'explain', NOW()),
    ('agent-ttt-qa', 'tic-tac-toe', 'Tic-Tac-Toe Q&A', 'qa', NOW()),
    ('agent-chess-explain', 'chess', 'Chess Explainer', 'explain', NOW()),
    ('agent-chess-qa', 'chess', 'Chess Q&A', 'qa', NOW())
ON CONFLICT ("Id") DO NOTHING;

-- Verify seed data
SELECT 'Users' as entity, COUNT(*) as count FROM users WHERE "Email" LIKE '%@meepleai.dev'
UNION ALL
SELECT 'Games', COUNT(*) FROM games WHERE "Id" IN ('tic-tac-toe', 'chess')
UNION ALL
SELECT 'RuleSpecs', COUNT(*) FROM rule_specs WHERE "GameId" IN ('tic-tac-toe', 'chess')
UNION ALL
SELECT 'Agents', COUNT(*) FROM agents WHERE "GameId" IN ('tic-tac-toe', 'chess');
