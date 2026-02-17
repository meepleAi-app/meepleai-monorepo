-- Gaming Hub Dashboard - Seed Data Script
-- Issue #4576
--
-- Creates test data for Gaming Hub Dashboard development
--
-- Usage: pwsh scripts/seed-dashboard-data.ps1 -UseDocker

\echo '🎲 Gaming Hub Dashboard - Data Seeder'
\echo ''

-- =====================================================================
-- STEP 1: CREATE TEST USERS
-- =====================================================================

\echo '👥 Seeding users...'

DO $$
DECLARE
    v_admin_hash TEXT;
BEGIN
    -- Get admin password hash to reuse for test users
    SELECT "PasswordHash" INTO v_admin_hash FROM users WHERE "Email" = 'admin@meepleai.dev' LIMIT 1;

    IF v_admin_hash IS NULL THEN
        RAISE EXCEPTION 'Admin user not found. Run migrations first.';
    END IF;

    -- Marco Rossi (Free)
    IF NOT EXISTS (SELECT 1 FROM users WHERE "Email" = 'marco.test@meeple.ai') THEN
        INSERT INTO users ("Id", "Email", "DisplayName", "Role", "Tier", "EmailVerified", "PasswordHash", "IsDemoAccount", "IsSuspended", "CreatedAt")
        VALUES (gen_random_uuid(), 'marco.test@meeple.ai', 'Marco Rossi', 'User', 'Free', true, v_admin_hash, true, false, NOW() - INTERVAL '6 months');
        RAISE NOTICE '   ✅ Marco Rossi (Free)';
    END IF;

    -- Sara Bianchi (Pro)
    IF NOT EXISTS (SELECT 1 FROM users WHERE "Email" = 'sara.test@meeple.ai') THEN
        INSERT INTO users ("Id", "Email", "DisplayName", "Role", "Tier", "EmailVerified", "PasswordHash", "IsDemoAccount", "IsSuspended", "CreatedAt")
        VALUES (gen_random_uuid(), 'sara.test@meeple.ai', 'Sara Bianchi', 'User', 'Pro', true, v_admin_hash, true, false, NOW() - INTERVAL '3 months');
        RAISE NOTICE '   ✅ Sara Bianchi (Pro)';
    END IF;

    -- Luca Verdi (Enterprise)
    IF NOT EXISTS (SELECT 1 FROM users WHERE "Email" = 'luca.test@meeple.ai') THEN
        INSERT INTO users ("Id", "Email", "DisplayName", "Role", "Tier", "EmailVerified", "PasswordHash", "IsDemoAccount", "IsSuspended", "CreatedAt")
        VALUES (gen_random_uuid(), 'luca.test@meeple.ai', 'Luca Verdi', 'User', 'Enterprise', true, v_admin_hash, true, false, NOW() - INTERVAL '12 months');
        RAISE NOTICE '   ✅ Luca Verdi (Enterprise)';
    END IF;
END $$;

-- =====================================================================
-- STEP 2: CREATE SHARED GAMES
-- =====================================================================

\echo '🎲 Seeding shared games...'

DO $$
DECLARE
    v_admin_id UUID;
BEGIN
    SELECT "Id" INTO v_admin_id FROM users WHERE "Role" = 'Admin' LIMIT 1;

    IF v_admin_id IS NULL THEN
        -- Use first user if no admin
        SELECT "Id" INTO v_admin_id FROM users LIMIT 1;
    END IF;

    -- Catan
    IF NOT EXISTS (SELECT 1 FROM shared_games WHERE title = 'Catan' AND is_deleted = false) THEN
        INSERT INTO shared_games (id, title, description, min_players, max_players, playing_time_minutes, min_age, complexity_rating, average_rating, image_url, thumbnail_url, year_published, status, is_deleted, created_by, created_at)
        VALUES (gen_random_uuid(), 'Catan', 'Build settlements and trade resources', 3, 4, 90, 10, 3.0, 7.2, 'https://placehold.co/400x300/f97316/white?text=Catan', 'https://placehold.co/200x150/f97316/white?text=Catan', 2010, 1, false, v_admin_id, NOW());
    END IF;

    -- Azul
    IF NOT EXISTS (SELECT 1 FROM shared_games WHERE title = 'Azul' AND is_deleted = false) THEN
        INSERT INTO shared_games (id, title, description, min_players, max_players, playing_time_minutes, min_age, complexity_rating, average_rating, image_url, thumbnail_url, year_published, status, is_deleted, created_by, created_at)
        VALUES (gen_random_uuid(), 'Azul', 'Create beautiful tile patterns', 2, 4, 45, 8, 2.5, 7.8, 'https://placehold.co/400x300/f97316/white?text=Azul', 'https://placehold.co/200x150/f97316/white?text=Azul', 2017, 1, false, v_admin_id, NOW());
    END IF;

    -- Wingspan
    IF NOT EXISTS (SELECT 1 FROM shared_games WHERE title = 'Wingspan' AND is_deleted = false) THEN
        INSERT INTO shared_games (id, title, description, min_players, max_players, playing_time_minutes, min_age, complexity_rating, average_rating, image_url, thumbnail_url, year_published, status, is_deleted, created_by, created_at)
        VALUES (gen_random_uuid(), 'Wingspan', 'Bird collection engine builder', 1, 5, 75, 10, 2.8, 8.1, 'https://placehold.co/400x300/f97316/white?text=Wingspan', 'https://placehold.co/200x150/f97316/white?text=Wingspan', 2019, 1, false, v_admin_id, NOW());
    END IF;

    -- 7 Wonders
    IF NOT EXISTS (SELECT 1 FROM shared_games WHERE title = '7 Wonders' AND is_deleted = false) THEN
        INSERT INTO shared_games (id, title, description, min_players, max_players, playing_time_minutes, min_age, complexity_rating, average_rating, image_url, thumbnail_url, year_published, status, is_deleted, created_by, created_at)
        VALUES (gen_random_uuid(), '7 Wonders', 'Build your civilization', 2, 7, 30, 10, 2.7, 7.7, 'https://placehold.co/400x300/f97316/white?text=7+Wonders', 'https://placehold.co/200x150/f97316/white?text=7+Wonders', 2010, 1, false, v_admin_id, NOW());
    END IF;

    -- Ticket to Ride
    IF NOT EXISTS (SELECT 1 FROM shared_games WHERE title = 'Ticket to Ride' AND is_deleted = false) THEN
        INSERT INTO shared_games (id, title, description, min_players, max_players, playing_time_minutes, min_age, complexity_rating, average_rating, image_url, thumbnail_url, year_published, status, is_deleted, created_by, created_at)
        VALUES (gen_random_uuid(), 'Ticket to Ride', 'Build train routes across the map', 2, 5, 60, 8, 2.0, 7.4, 'https://placehold.co/400x300/f97316/white?text=Ticket+to+Ride', 'https://placehold.co/200x150/f97316/white?text=Ticket+to+Ride', 2004, 1, false, v_admin_id, NOW());
    END IF;

    -- Pandemic
    IF NOT EXISTS (SELECT 1 FROM shared_games WHERE title = 'Pandemic' AND is_deleted = false) THEN
        INSERT INTO shared_games (id, title, description, min_players, max_players, playing_time_minutes, min_age, complexity_rating, average_rating, image_url, thumbnail_url, year_published, status, is_deleted, created_by, created_at)
        VALUES (gen_random_uuid(), 'Pandemic', 'Save the world from diseases', 2, 4, 45, 8, 2.6, 7.6, 'https://placehold.co/400x300/f97316/white?text=Pandemic', 'https://placehold.co/200x150/f97316/white?text=Pandemic', 2008, 1, false, v_admin_id, NOW());
    END IF;

    RAISE NOTICE '   ✅ Created games';
END $$;

-- =====================================================================
-- STEP 3: CREATE PLAY RECORDS FOR MARCO
-- =====================================================================

\echo '📊 Seeding play records...'

DO $$
DECLARE
    v_user_id UUID;
    v_record_id UUID;
    v_player_marco UUID;
    v_player_alice UUID;
    v_player_bob UUID;
    v_player_charlie UUID;
BEGIN
    -- Get Marco's ID
    SELECT "Id" INTO v_user_id FROM users WHERE "Email" = 'marco.test@meeple.ai';

    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'Marco not found. Users might not have been created.';
    END IF;

    -- Skip if already has records
    IF EXISTS (SELECT 1 FROM play_records WHERE "CreatedByUserId" = v_user_id) THEN
        RAISE NOTICE '   ⏭️  Marco already has play records';
    ELSE
        -- Record 1: Catan (2 hours ago)
        v_record_id := gen_random_uuid();
        INSERT INTO play_records ("Id", "GameId", "GameName", "CreatedByUserId", "Visibility", "SessionDate", "StartTime", "EndTime", "Duration", "Status", "Notes", "Location", "ScoringConfigJson", "CreatedAt", "UpdatedAt")
        VALUES (v_record_id, NULL, 'Catan', v_user_id, 0, CURRENT_DATE, NOW() - INTERVAL '4 hours', NOW() - INTERVAL '2 hours 30 minutes', '01:30:00'::interval, 2, 'Great 4-player session!', 'Home', '{}'::jsonb, NOW() - INTERVAL '2 hours', NOW() - INTERVAL '2 hours');

        -- Players
        v_player_marco := gen_random_uuid();
        INSERT INTO record_players ("Id", "PlayRecordId", "UserId", "DisplayName") VALUES (v_player_marco, v_record_id, v_user_id, 'Marco Rossi');
        INSERT INTO record_scores ("Id", "RecordPlayerId", "Dimension", "Value", "Unit") VALUES (gen_random_uuid(), v_player_marco, 'Points', 10, 'pts');

        v_player_alice := gen_random_uuid();
        INSERT INTO record_players ("Id", "PlayRecordId", "UserId", "DisplayName") VALUES (v_player_alice, v_record_id, NULL, 'Alice');
        INSERT INTO record_scores ("Id", "RecordPlayerId", "Dimension", "Value", "Unit") VALUES (gen_random_uuid(), v_player_alice, 'Points', 8, 'pts');

        v_player_bob := gen_random_uuid();
        INSERT INTO record_players ("Id", "PlayRecordId", "UserId", "DisplayName") VALUES (v_player_bob, v_record_id, NULL, 'Bob');
        INSERT INTO record_scores ("Id", "RecordPlayerId", "Dimension", "Value", "Unit") VALUES (gen_random_uuid(), v_player_bob, 'Points', 9, 'pts');

        v_player_charlie := gen_random_uuid();
        INSERT INTO record_players ("Id", "PlayRecordId", "UserId", "DisplayName") VALUES (v_player_charlie, v_record_id, NULL, 'Charlie');
        INSERT INTO record_scores ("Id", "RecordPlayerId", "Dimension", "Value", "Unit") VALUES (gen_random_uuid(), v_player_charlie, 'Points', 7, 'pts');

        -- Record 2: Wingspan (yesterday)
        v_record_id := gen_random_uuid();
        INSERT INTO play_records ("Id", "GameId", "GameName", "CreatedByUserId", "Visibility", "SessionDate", "StartTime", "EndTime", "Duration", "Status", "Notes", "Location", "ScoringConfigJson", "CreatedAt", "UpdatedAt")
        VALUES (v_record_id, NULL, 'Wingspan', v_user_id, 0, CURRENT_DATE - 1, (CURRENT_DATE - 1) + TIME '18:30:00', (CURRENT_DATE - 1) + TIME '19:45:00', '01:15:00'::interval, 2, 'Relaxing solo game', 'Home', '{}'::jsonb, CURRENT_DATE - 1, CURRENT_DATE - 1);

        v_player_marco := gen_random_uuid();
        INSERT INTO record_players ("Id", "PlayRecordId", "UserId", "DisplayName") VALUES (v_player_marco, v_record_id, v_user_id, 'Marco Rossi');
        INSERT INTO record_scores ("Id", "RecordPlayerId", "Dimension", "Value", "Unit") VALUES (gen_random_uuid(), v_player_marco, 'Points', 87, 'pts');

        RAISE NOTICE '   ✅ Created 2 play records for Marco';
    END IF;
END $$;

\echo ''
\echo '✅ Seeding completed!'
\echo ''
\echo '📝 Test credentials:'
\echo '   Email: marco.test@meeple.ai (or sara.test / luca.test)'
\echo '   Password: [same as admin@meepleai.dev]'
