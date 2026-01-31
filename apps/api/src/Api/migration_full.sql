CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE TABLE contributors (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        is_primary_contributor boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        modified_at timestamp with time zone,
        CONSTRAINT "PK_contributors" PRIMARY KEY (id),
        CONSTRAINT "FK_contributors_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE TABLE share_requests (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        source_game_id uuid NOT NULL,
        target_shared_game_id uuid,
        status integer NOT NULL,
        status_before_review integer,
        contribution_type integer NOT NULL,
        user_notes character varying(2000),
        admin_feedback character varying(2000),
        reviewing_admin_id uuid,
        review_started_at timestamp with time zone,
        review_lock_expires_at timestamp with time zone,
        resolved_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        modified_at timestamp with time zone,
        created_by uuid NOT NULL,
        modified_by uuid,
        row_version bytea,
        CONSTRAINT "PK_share_requests" PRIMARY KEY (id),
        CONSTRAINT "FK_share_requests_shared_games_source_game_id" FOREIGN KEY (source_game_id) REFERENCES shared_games (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_share_requests_shared_games_target_shared_game_id" FOREIGN KEY (target_shared_game_id) REFERENCES shared_games (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE TABLE contribution_records (
        id uuid NOT NULL,
        contributor_id uuid NOT NULL,
        type integer NOT NULL,
        description character varying(1000) NOT NULL,
        version integer NOT NULL,
        contributed_at timestamp with time zone NOT NULL,
        share_request_id uuid,
        document_ids jsonb,
        includes_game_data boolean NOT NULL,
        includes_metadata boolean NOT NULL,
        CONSTRAINT "PK_contribution_records" PRIMARY KEY (id),
        CONSTRAINT "FK_contribution_records_contributors_contributor_id" FOREIGN KEY (contributor_id) REFERENCES contributors (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE TABLE share_request_documents (
        id uuid NOT NULL,
        share_request_id uuid NOT NULL,
        document_id uuid NOT NULL,
        file_name character varying(255) NOT NULL,
        content_type character varying(100) NOT NULL,
        file_size bigint NOT NULL,
        attached_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_share_request_documents" PRIMARY KEY (id),
        CONSTRAINT "FK_share_request_documents_share_requests_share_request_id" FOREIGN KEY (share_request_id) REFERENCES share_requests (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_contribution_records_contributor_id ON contribution_records (contributor_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_contribution_records_contributor_version ON contribution_records (contributor_id, version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_contribution_records_share_request_id ON contribution_records (share_request_id) WHERE share_request_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_contributors_shared_game_id ON contributors (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_contributors_user_id ON contributors (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE UNIQUE INDEX ix_contributors_user_shared_game_unique ON contributors (user_id, shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_share_request_documents_document_id ON share_request_documents (document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE UNIQUE INDEX ix_share_request_documents_request_document ON share_request_documents (share_request_id, document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_share_request_documents_share_request_id ON share_request_documents (share_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_share_requests_review_lock_expires_at ON share_requests (review_lock_expires_at) WHERE review_lock_expires_at IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_share_requests_reviewing_admin_id ON share_requests (reviewing_admin_id) WHERE reviewing_admin_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_share_requests_source_game_id ON share_requests (source_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_share_requests_status ON share_requests (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX "IX_share_requests_target_shared_game_id" ON share_requests (target_shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_share_requests_user_id ON share_requests (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    CREATE INDEX ix_share_requests_user_source_status ON share_requests (user_id, source_game_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121181322_AddShareRequestAndContributorEntities_Issue2726') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260121181322_AddShareRequestAndContributorEntities_Issue2726', '9.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE TABLE badges (
        id uuid NOT NULL,
        code character varying(50) NOT NULL,
        name character varying(100) NOT NULL,
        description character varying(500) NOT NULL,
        icon_url character varying(500),
        tier integer NOT NULL,
        category integer NOT NULL,
        is_active boolean NOT NULL,
        display_order integer NOT NULL,
        requirement jsonb NOT NULL,
        created_at timestamp with time zone NOT NULL,
        modified_at timestamp with time zone,
        CONSTRAINT "PK_badges" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE TABLE user_badges (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        badge_id uuid NOT NULL,
        earned_at timestamp with time zone NOT NULL,
        triggering_share_request_id uuid,
        is_displayed boolean NOT NULL,
        revoked_at timestamp with time zone,
        revocation_reason character varying(500),
        CONSTRAINT "PK_user_badges" PRIMARY KEY (id),
        CONSTRAINT "FK_user_badges_badges_badge_id" FOREIGN KEY (badge_id) REFERENCES badges (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE INDEX ix_badges_category ON badges (category);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE UNIQUE INDEX ix_badges_code_unique ON badges (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE INDEX ix_badges_display_order ON badges (display_order);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE INDEX ix_badges_is_active ON badges (is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE INDEX ix_badges_tier ON badges (tier);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE INDEX ix_user_badges_badge_id ON user_badges (badge_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE INDEX ix_user_badges_revoked_at ON user_badges (revoked_at) WHERE revoked_at IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE INDEX ix_user_badges_triggering_share_request_id ON user_badges (triggering_share_request_id) WHERE triggering_share_request_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE UNIQUE INDEX ix_user_badges_user_badge_unique ON user_badges (user_id, badge_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    CREATE INDEX ix_user_badges_user_id ON user_badges (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121202807_AddBadgeAndUserBadgeEntities_Issue2731') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260121202807_AddBadgeAndUserBadgeEntities_Issue2731', '9.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121213027_AddSharedGameDocumentFieldsToPdfDocument') THEN
    ALTER TABLE pdf_documents ADD "ContributorId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121213027_AddSharedGameDocumentFieldsToPdfDocument') THEN
    ALTER TABLE pdf_documents ADD "SharedGameId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121213027_AddSharedGameDocumentFieldsToPdfDocument') THEN
    ALTER TABLE pdf_documents ADD "SourceDocumentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260121213027_AddSharedGameDocumentFieldsToPdfDocument') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260121213027_AddSharedGameDocumentFieldsToPdfDocument', '9.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122091703_AddRateLimitConfiguration') THEN
    CREATE TABLE "ShareRequestLimitConfigs" (
        "Id" uuid NOT NULL,
        "Tier" integer NOT NULL,
        "MaxPendingRequests" integer NOT NULL,
        "MaxRequestsPerMonth" integer NOT NULL,
        "CooldownAfterRejectionSeconds" bigint NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ShareRequestLimitConfigs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122091703_AddRateLimitConfiguration') THEN
    CREATE TABLE "UserRateLimitOverrides" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "MaxPendingRequests" integer,
        "MaxRequestsPerMonth" integer,
        "CooldownAfterRejectionSeconds" bigint,
        "ExpiresAt" timestamp with time zone,
        "Reason" text NOT NULL,
        "CreatedByAdminId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserRateLimitOverrides" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122091703_AddRateLimitConfiguration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260122091703_AddRateLimitConfiguration', '9.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE "UserRateLimitOverrides" DROP CONSTRAINT "PK_UserRateLimitOverrides";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE "ShareRequestLimitConfigs" DROP CONSTRAINT "PK_ShareRequestLimitConfigs";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE "UserRateLimitOverrides" RENAME TO user_rate_limit_overrides;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE "ShareRequestLimitConfigs" RENAME TO share_request_limit_configs;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "Reason" TO reason;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "Id" TO id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "UserId" TO user_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "UpdatedAt" TO updated_at;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "MaxRequestsPerMonth" TO max_requests_per_month;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "MaxPendingRequests" TO max_pending_requests;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "ExpiresAt" TO expires_at;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "CreatedByAdminId" TO created_by_admin_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "CreatedAt" TO created_at;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides RENAME COLUMN "CooldownAfterRejectionSeconds" TO cooldown_after_rejection_seconds;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs RENAME COLUMN "Tier" TO tier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs RENAME COLUMN "Id" TO id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs RENAME COLUMN "UpdatedAt" TO updated_at;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs RENAME COLUMN "MaxRequestsPerMonth" TO max_requests_per_month;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs RENAME COLUMN "MaxPendingRequests" TO max_pending_requests;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs RENAME COLUMN "IsActive" TO is_active;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs RENAME COLUMN "CreatedAt" TO created_at;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs RENAME COLUMN "CooldownAfterRejectionSeconds" TO cooldown_after_rejection_seconds;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides ALTER COLUMN reason TYPE character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides ADD CONSTRAINT "PK_user_rate_limit_overrides" PRIMARY KEY (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE share_request_limit_configs ADD CONSTRAINT "PK_share_request_limit_configs" PRIMARY KEY (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    CREATE INDEX ix_user_rate_limit_overrides_created_by_admin_id ON user_rate_limit_overrides (created_by_admin_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    CREATE INDEX ix_user_rate_limit_overrides_expires_at ON user_rate_limit_overrides (expires_at) WHERE expires_at IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    CREATE UNIQUE INDEX ix_user_rate_limit_overrides_user_id_active ON user_rate_limit_overrides (user_id) WHERE expires_at IS NULL OR expires_at > NOW();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    CREATE INDEX ix_share_request_limit_configs_is_active ON share_request_limit_configs (is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    CREATE UNIQUE INDEX ix_share_request_limit_configs_tier_unique_active ON share_request_limit_configs (tier) WHERE is_active = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides ADD CONSTRAINT "FK_user_rate_limit_overrides_users_created_by_admin_id" FOREIGN KEY (created_by_admin_id) REFERENCES users ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    ALTER TABLE user_rate_limit_overrides ADD CONSTRAINT "FK_user_rate_limit_overrides_users_user_id" FOREIGN KEY (user_id) REFERENCES users ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122093756_AddRateLimitForeignKeyConstraints') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260122093756_AddRateLimitForeignKeyConstraints', '9.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'SystemConfiguration') THEN
            CREATE SCHEMA "SystemConfiguration";
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE admin_reports (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        description character varying(1000) NOT NULL,
        template integer NOT NULL,
        format integer NOT NULL,
        parameters jsonb NOT NULL,
        schedule_expression character varying(100),
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        last_executed_at timestamp with time zone,
        created_by character varying(100) NOT NULL,
        email_recipients jsonb NOT NULL,
        CONSTRAINT "PK_admin_reports" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE agent_feedback (
        "Id" uuid NOT NULL,
        "MessageId" uuid NOT NULL,
        "Endpoint" character varying(32) NOT NULL,
        "GameId" uuid,
        "UserId" uuid NOT NULL,
        "Outcome" character varying(32) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_agent_feedback" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE ai_request_logs (
        "Id" uuid NOT NULL,
        "UserId" uuid,
        "ApiKeyId" uuid,
        "GameId" uuid,
        "Endpoint" character varying(32) NOT NULL,
        "Query" character varying(2048),
        "ResponseSnippet" character varying(1024),
        "LatencyMs" integer NOT NULL,
        "TokenCount" integer NOT NULL DEFAULT 0,
        "PromptTokens" integer NOT NULL DEFAULT 0,
        "CompletionTokens" integer NOT NULL DEFAULT 0,
        "Confidence" double precision,
        "Status" character varying(32) NOT NULL,
        "ErrorMessage" character varying(1024),
        "IpAddress" character varying(64),
        "UserAgent" character varying(256),
        "Model" character varying(128),
        "FinishReason" character varying(64),
        "CreatedAt" timestamp with time zone NOT NULL,
        "RagConfidence" double precision,
        "LlmConfidence" double precision,
        "CitationQuality" double precision,
        "OverallConfidence" double precision,
        "IsLowQuality" boolean NOT NULL,
        CONSTRAINT "PK_ai_request_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE "SystemConfiguration"."AiModelConfigurations" (
        "Id" uuid NOT NULL,
        "ModelId" character varying(200) NOT NULL,
        "DisplayName" character varying(200) NOT NULL,
        "Provider" character varying(50) NOT NULL,
        "Priority" integer NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "IsPrimary" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone,
        applicable_tier integer,
        environment_type integer NOT NULL DEFAULT 0,
        is_default_for_tier boolean NOT NULL DEFAULT FALSE,
        settings_json jsonb NOT NULL DEFAULT '{}',
        "PricingJson" jsonb NOT NULL DEFAULT '{}',
        usage_json jsonb NOT NULL DEFAULT '{}',
        CONSTRAINT "PK_AiModelConfigurations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE alert_configurations (
        id uuid NOT NULL,
        config_key character varying(200) NOT NULL,
        config_value character varying(4000) NOT NULL,
        category character varying(50) NOT NULL,
        is_encrypted boolean NOT NULL DEFAULT FALSE,
        description character varying(500),
        updated_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        updated_by character varying(200) NOT NULL,
        CONSTRAINT "PK_alert_configurations" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE alert_rules (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        alert_type character varying(100) NOT NULL,
        severity character varying(50) NOT NULL,
        description character varying(1000),
        threshold double precision NOT NULL,
        threshold_unit character varying(50) NOT NULL,
        duration_minutes integer NOT NULL,
        is_enabled boolean NOT NULL DEFAULT TRUE,
        metadata jsonb,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        updated_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        created_by character varying(200) NOT NULL,
        updated_by character varying(200) NOT NULL,
        CONSTRAINT "PK_alert_rules" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE alerts (
        id uuid NOT NULL,
        alert_type character varying(50) NOT NULL,
        severity character varying(20) NOT NULL,
        message text NOT NULL,
        metadata jsonb,
        triggered_at timestamp with time zone NOT NULL,
        resolved_at timestamp with time zone,
        is_active boolean NOT NULL,
        channel_sent jsonb,
        CONSTRAINT "PK_alerts" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE audit_logs (
        "Id" uuid NOT NULL,
        "UserId" uuid,
        "Action" character varying(64) NOT NULL,
        "Resource" character varying(128) NOT NULL,
        "ResourceId" character varying(64),
        "Result" character varying(32) NOT NULL,
        "Details" character varying(1024),
        "IpAddress" character varying(64),
        "UserAgent" character varying(256),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_audit_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE badges (
        id uuid NOT NULL,
        code character varying(50) NOT NULL,
        name character varying(100) NOT NULL,
        description character varying(500) NOT NULL,
        icon_url character varying(500),
        tier integer NOT NULL,
        category integer NOT NULL,
        is_active boolean NOT NULL,
        display_order integer NOT NULL,
        requirement jsonb NOT NULL,
        created_at timestamp with time zone NOT NULL,
        modified_at timestamp with time zone,
        CONSTRAINT "PK_badges" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE cache_stats (
        id integer GENERATED BY DEFAULT AS IDENTITY,
        game_id uuid NOT NULL,
        question_hash character varying(64) NOT NULL,
        hit_count bigint NOT NULL,
        miss_count bigint NOT NULL,
        created_at timestamp with time zone NOT NULL,
        last_hit_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_cache_stats" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_categories (
        id uuid NOT NULL,
        name character varying(100) NOT NULL,
        slug character varying(100) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        CONSTRAINT "PK_game_categories" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_designers (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        CONSTRAINT "PK_game_designers" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_mechanics (
        id uuid NOT NULL,
        name character varying(100) NOT NULL,
        slug character varying(100) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        CONSTRAINT "PK_game_mechanics" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_publishers (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        CONSTRAINT "PK_game_publishers" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE notifications (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        type character varying(50) NOT NULL,
        severity character varying(20) NOT NULL,
        title character varying(200) NOT NULL,
        message text NOT NULL,
        link character varying(500),
        metadata jsonb,
        is_read boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        read_at timestamp with time zone,
        CONSTRAINT "PK_notifications" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE prompt_evaluation_results (
        id uuid NOT NULL,
        template_id uuid NOT NULL,
        version_id uuid NOT NULL,
        dataset_id character varying(100) NOT NULL,
        executed_at timestamp with time zone NOT NULL,
        total_queries integer NOT NULL,
        accuracy double precision NOT NULL,
        relevance double precision NOT NULL,
        completeness double precision NOT NULL,
        clarity double precision NOT NULL,
        citation_quality double precision NOT NULL,
        passed boolean NOT NULL,
        summary character varying(500),
        query_results_json jsonb,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_prompt_evaluation_results" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE share_request_limit_configs (
        id uuid NOT NULL,
        tier integer NOT NULL,
        max_pending_requests integer NOT NULL,
        max_requests_per_month integer NOT NULL,
        cooldown_after_rejection_seconds bigint NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_share_request_limit_configs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE shared_games (
        id uuid NOT NULL,
        bgg_id integer,
        title character varying(500) NOT NULL,
        year_published integer NOT NULL,
        description text NOT NULL,
        min_players integer NOT NULL,
        max_players integer NOT NULL,
        playing_time_minutes integer NOT NULL,
        min_age integer NOT NULL,
        complexity_rating numeric(3,2),
        average_rating numeric(4,2),
        image_url character varying(1000) NOT NULL,
        thumbnail_url character varying(1000) NOT NULL,
        status integer NOT NULL DEFAULT 0,
        rules_content text,
        rules_language character varying(10),
        created_by uuid NOT NULL,
        modified_by uuid,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        modified_at timestamp with time zone,
        is_deleted boolean NOT NULL DEFAULT FALSE,
        CONSTRAINT "PK_shared_games" PRIMARY KEY (id),
        CONSTRAINT chk_shared_games_complexity CHECK (complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)),
        CONSTRAINT chk_shared_games_min_age CHECK (min_age >= 0),
        CONSTRAINT chk_shared_games_players CHECK (min_players > 0 AND max_players >= min_players),
        CONSTRAINT chk_shared_games_playing_time CHECK (playing_time_minutes > 0),
        CONSTRAINT chk_shared_games_rating CHECK (average_rating IS NULL OR (average_rating >= 1.0 AND average_rating <= 10.0)),
        CONSTRAINT chk_shared_games_year_published CHECK (year_published > 1900 AND year_published <= 2100)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "DisplayName" character varying(128),
        "PasswordHash" text,
        "Role" character varying(32) NOT NULL,
        "Tier" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "IsDemoAccount" boolean NOT NULL,
        "Language" character varying(10) NOT NULL DEFAULT 'en',
        "EmailNotifications" boolean NOT NULL DEFAULT TRUE,
        "Theme" character varying(20) NOT NULL DEFAULT 'system',
        "DataRetentionDays" integer NOT NULL DEFAULT 90,
        "TotpSecretEncrypted" character varying(512),
        "IsTwoFactorEnabled" boolean NOT NULL DEFAULT FALSE,
        "TwoFactorEnabledAt" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE validation_accuracy_baselines (
        id uuid NOT NULL,
        context character varying(200) NOT NULL,
        dataset_id character varying(100) NOT NULL,
        evaluation_id uuid,
        measured_at timestamp with time zone NOT NULL,
        true_positives integer NOT NULL,
        true_negatives integer NOT NULL,
        false_positives integer NOT NULL,
        false_negatives integer NOT NULL,
        total_cases integer NOT NULL,
        precision double precision NOT NULL,
        recall double precision NOT NULL,
        f1_score double precision NOT NULL,
        accuracy double precision NOT NULL,
        specificity double precision NOT NULL,
        matthews_correlation double precision NOT NULL,
        meets_baseline boolean NOT NULL,
        quality_level integer NOT NULL,
        summary character varying(500),
        recommendations_json jsonb,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        CONSTRAINT "PK_validation_accuracy_baselines" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE workflow_error_logs (
        id uuid NOT NULL,
        workflow_id character varying(255) NOT NULL,
        execution_id character varying(255) NOT NULL,
        error_message character varying(5000) NOT NULL,
        node_name character varying(255),
        retry_count integer NOT NULL DEFAULT 0,
        stack_trace character varying(10000),
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_workflow_error_logs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE admin_report_executions (
        id uuid NOT NULL,
        report_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        completed_at timestamp with time zone,
        status integer NOT NULL,
        error_message character varying(2000),
        output_path character varying(500),
        file_size_bytes bigint,
        duration_ms bigint,
        execution_metadata jsonb NOT NULL,
        CONSTRAINT "PK_admin_report_executions" PRIMARY KEY (id),
        CONSTRAINT "FK_admin_report_executions_admin_reports_report_id" FOREIGN KEY (report_id) REFERENCES admin_reports (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE user_badges (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        badge_id uuid NOT NULL,
        earned_at timestamp with time zone NOT NULL,
        triggering_share_request_id uuid,
        is_displayed boolean NOT NULL,
        revoked_at timestamp with time zone,
        revocation_reason character varying(500),
        CONSTRAINT "PK_user_badges" PRIMARY KEY (id),
        CONSTRAINT "FK_user_badges_badges_badge_id" FOREIGN KEY (badge_id) REFERENCES badges (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE contributors (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        is_primary_contributor boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        modified_at timestamp with time zone,
        CONSTRAINT "PK_contributors" PRIMARY KEY (id),
        CONSTRAINT "FK_contributors_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_errata (
        id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        description text NOT NULL,
        page_reference character varying(100) NOT NULL,
        published_date timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        CONSTRAINT "PK_game_errata" PRIMARY KEY (id),
        CONSTRAINT "FK_game_errata_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_faqs (
        id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        question character varying(500) NOT NULL,
        answer text NOT NULL,
        "order" integer NOT NULL DEFAULT 0,
        upvote_count integer NOT NULL DEFAULT 0,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_game_faqs" PRIMARY KEY (id),
        CONSTRAINT "FK_game_faqs_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_state_templates (
        id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        schema_json jsonb,
        version character varying(20) NOT NULL DEFAULT '1.0',
        is_active boolean NOT NULL DEFAULT FALSE,
        source integer NOT NULL DEFAULT 0,
        confidence_score numeric(5,4),
        generated_at timestamp with time zone NOT NULL,
        created_by uuid NOT NULL,
        CONSTRAINT "PK_game_state_templates" PRIMARY KEY (id),
        CONSTRAINT "FK_game_state_templates_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE games (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "Publisher" text,
        "YearPublished" integer,
        "MinPlayers" integer,
        "MaxPlayers" integer,
        "MinPlayTimeMinutes" integer,
        "MaxPlayTimeMinutes" integer,
        "BggId" integer,
        "BggMetadata" text,
        "IconUrl" text,
        "ImageUrl" text,
        "VersionType" text,
        "Language" text,
        "VersionNumber" text,
        "SharedGameId" uuid,
        CONSTRAINT "PK_games" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_games_shared_games_SharedGameId" FOREIGN KEY ("SharedGameId") REFERENCES shared_games (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE quick_questions (
        id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        text character varying(200) NOT NULL,
        emoji character varying(2) NOT NULL,
        category integer NOT NULL,
        display_order integer NOT NULL DEFAULT 0,
        is_generated boolean NOT NULL DEFAULT FALSE,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        is_active boolean NOT NULL DEFAULT TRUE,
        CONSTRAINT "PK_quick_questions" PRIMARY KEY (id),
        CONSTRAINT "FK_quick_questions_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE rulebook_analyses (
        id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        pdf_document_id uuid NOT NULL,
        game_title character varying(300) NOT NULL,
        summary text NOT NULL,
        key_mechanics_json jsonb NOT NULL DEFAULT '[]',
        victory_conditions_json jsonb,
        resources_json jsonb NOT NULL DEFAULT '[]',
        game_phases_json jsonb NOT NULL DEFAULT '[]',
        common_questions_json jsonb NOT NULL DEFAULT '[]',
        confidence_score numeric(5,4) NOT NULL,
        version character varying(20) NOT NULL DEFAULT '1.0',
        is_active boolean NOT NULL DEFAULT FALSE,
        source integer NOT NULL DEFAULT 0,
        analyzed_at timestamp with time zone NOT NULL,
        created_by uuid NOT NULL,
        CONSTRAINT "PK_rulebook_analyses" PRIMARY KEY (id),
        CONSTRAINT "FK_rulebook_analyses_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE share_requests (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        source_game_id uuid NOT NULL,
        target_shared_game_id uuid,
        status integer NOT NULL,
        status_before_review integer,
        contribution_type integer NOT NULL,
        user_notes character varying(2000),
        admin_feedback character varying(2000),
        reviewing_admin_id uuid,
        review_started_at timestamp with time zone,
        review_lock_expires_at timestamp with time zone,
        resolved_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        modified_at timestamp with time zone,
        created_by uuid NOT NULL,
        modified_by uuid,
        row_version bytea,
        CONSTRAINT "PK_share_requests" PRIMARY KEY (id),
        CONSTRAINT "FK_share_requests_shared_games_source_game_id" FOREIGN KEY (source_game_id) REFERENCES shared_games (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_share_requests_shared_games_target_shared_game_id" FOREIGN KEY (target_shared_game_id) REFERENCES shared_games (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE shared_game_categories (
        game_category_id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        CONSTRAINT "PK_shared_game_categories" PRIMARY KEY (game_category_id, shared_game_id),
        CONSTRAINT "FK_shared_game_categories_game_categories_game_category_id" FOREIGN KEY (game_category_id) REFERENCES game_categories (id) ON DELETE CASCADE,
        CONSTRAINT "FK_shared_game_categories_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE shared_game_delete_requests (
        id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        requested_by uuid NOT NULL,
        reason text NOT NULL,
        status integer NOT NULL DEFAULT 0,
        reviewed_by uuid,
        review_comment text,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        reviewed_at timestamp with time zone,
        CONSTRAINT "PK_shared_game_delete_requests" PRIMARY KEY (id),
        CONSTRAINT "FK_shared_game_delete_requests_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE shared_game_designers (
        game_designer_id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        CONSTRAINT "PK_shared_game_designers" PRIMARY KEY (game_designer_id, shared_game_id),
        CONSTRAINT "FK_shared_game_designers_game_designers_game_designer_id" FOREIGN KEY (game_designer_id) REFERENCES game_designers (id) ON DELETE CASCADE,
        CONSTRAINT "FK_shared_game_designers_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE shared_game_mechanics (
        game_mechanic_id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        CONSTRAINT "PK_shared_game_mechanics" PRIMARY KEY (game_mechanic_id, shared_game_id),
        CONSTRAINT "FK_shared_game_mechanics_game_mechanics_game_mechanic_id" FOREIGN KEY (game_mechanic_id) REFERENCES game_mechanics (id) ON DELETE CASCADE,
        CONSTRAINT "FK_shared_game_mechanics_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE shared_game_publishers (
        game_publisher_id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        CONSTRAINT "PK_shared_game_publishers" PRIMARY KEY (game_publisher_id, shared_game_id),
        CONSTRAINT "FK_shared_game_publishers_game_publishers_game_publisher_id" FOREIGN KEY (game_publisher_id) REFERENCES game_publishers (id) ON DELETE CASCADE,
        CONSTRAINT "FK_shared_game_publishers_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE api_keys (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "KeyName" character varying(128) NOT NULL,
        "KeyHash" character varying(256) NOT NULL,
        "KeyPrefix" character varying(16) NOT NULL,
        "Scopes" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "LastUsedAt" timestamp with time zone,
        "ExpiresAt" timestamp with time zone,
        "RevokedAt" timestamp with time zone,
        "RevokedBy" uuid,
        "Metadata" character varying(4096),
        "UsageCount" integer NOT NULL DEFAULT 0,
        CONSTRAINT "PK_api_keys" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_api_keys_users_RevokedBy" FOREIGN KEY ("RevokedBy") REFERENCES users ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_api_keys_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE library_share_links (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        share_token character varying(32) NOT NULL,
        privacy_level integer NOT NULL,
        include_notes boolean NOT NULL DEFAULT FALSE,
        created_at timestamp with time zone NOT NULL,
        expires_at timestamp with time zone,
        revoked_at timestamp with time zone,
        view_count integer NOT NULL DEFAULT 0,
        last_accessed_at timestamp with time zone,
        CONSTRAINT "PK_library_share_links" PRIMARY KEY (id),
        CONSTRAINT "FK_library_share_links_users_user_id" FOREIGN KEY (user_id) REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE llm_cost_logs (
        id uuid NOT NULL,
        user_id uuid,
        user_role character varying(50) NOT NULL,
        model_id character varying(100) NOT NULL,
        provider character varying(50) NOT NULL,
        prompt_tokens integer NOT NULL,
        completion_tokens integer NOT NULL,
        total_tokens integer NOT NULL,
        input_cost_usd numeric(18,6) NOT NULL,
        output_cost_usd numeric(18,6) NOT NULL,
        total_cost_usd numeric(18,6) NOT NULL,
        endpoint character varying(100) NOT NULL,
        success boolean NOT NULL,
        error_message character varying(500),
        latency_ms integer NOT NULL,
        ip_address character varying(45),
        user_agent character varying(500),
        created_at timestamp with time zone NOT NULL,
        request_date date NOT NULL,
        CONSTRAINT "PK_llm_cost_logs" PRIMARY KEY (id),
        CONSTRAINT "FK_llm_cost_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES users ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE n8n_configs (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "BaseUrl" character varying(512) NOT NULL,
        "ApiKeyEncrypted" character varying(512) NOT NULL,
        "WebhookUrl" character varying(512),
        "IsActive" boolean NOT NULL,
        "LastTestedAt" timestamp with time zone,
        "LastTestResult" character varying(512),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        CONSTRAINT "PK_n8n_configs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_n8n_configs_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE oauth_accounts (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Provider" character varying(20) NOT NULL,
        "ProviderUserId" character varying(255) NOT NULL,
        "AccessTokenEncrypted" text NOT NULL,
        "RefreshTokenEncrypted" text,
        "TokenExpiresAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_oauth_accounts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_oauth_accounts_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE password_reset_tokens (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TokenHash" character varying(256) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "IsUsed" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UsedAt" timestamp with time zone,
        CONSTRAINT "PK_password_reset_tokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_password_reset_tokens_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE prompt_templates (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "Description" character varying(512),
        "Category" character varying(64),
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_prompt_templates" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_prompt_templates_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE system_configurations (
        "Id" uuid NOT NULL,
        "Key" character varying(500) NOT NULL,
        "Value" text NOT NULL,
        "ValueType" character varying(50) NOT NULL,
        "Description" character varying(1000),
        "Category" character varying(100) NOT NULL,
        "IsActive" boolean NOT NULL,
        "RequiresRestart" boolean NOT NULL,
        "Environment" character varying(50) NOT NULL,
        "Version" integer NOT NULL,
        "PreviousValue" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "UpdatedByUserId" uuid,
        "LastToggledAt" timestamp with time zone,
        CONSTRAINT "PK_system_configurations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_system_configurations_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_system_configurations_users_UpdatedByUserId" FOREIGN KEY ("UpdatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE temp_sessions (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "IpAddress" character varying(64),
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "IsUsed" boolean NOT NULL DEFAULT FALSE,
        "UsedAt" timestamp with time zone,
        CONSTRAINT "PK_temp_sessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_temp_sessions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE used_totp_codes (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CodeHash" character varying(128) NOT NULL,
        "TimeStep" bigint NOT NULL,
        "UsedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_used_totp_codes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_used_totp_codes_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE user_backup_codes (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CodeHash" character varying(255) NOT NULL,
        "IsUsed" boolean NOT NULL DEFAULT FALSE,
        "UsedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_user_backup_codes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_user_backup_codes_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE user_library_entries (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "AddedAt" timestamp with time zone NOT NULL,
        "Notes" character varying(500),
        "IsFavorite" boolean NOT NULL DEFAULT FALSE,
        "CustomAgentConfigJson" jsonb,
        "CustomPdfUrl" character varying(2048),
        "CustomPdfUploadedAt" timestamp with time zone,
        "CustomPdfFileSizeBytes" bigint,
        "CustomPdfOriginalFileName" character varying(255),
        CONSTRAINT "PK_user_library_entries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_user_library_entries_shared_games_GameId" FOREIGN KEY ("GameId") REFERENCES shared_games (id) ON DELETE CASCADE,
        CONSTRAINT "FK_user_library_entries_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE user_rate_limit_overrides (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        max_pending_requests integer,
        max_requests_per_month integer,
        cooldown_after_rejection_seconds bigint,
        expires_at timestamp with time zone,
        reason character varying(500) NOT NULL,
        created_by_admin_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_user_rate_limit_overrides" PRIMARY KEY (id),
        CONSTRAINT "FK_user_rate_limit_overrides_users_created_by_admin_id" FOREIGN KEY (created_by_admin_id) REFERENCES users ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_user_rate_limit_overrides_users_user_id" FOREIGN KEY (user_id) REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE user_sessions (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "LastSeenAt" timestamp with time zone,
        "RevokedAt" timestamp with time zone,
        "UserAgent" character varying(256),
        "IpAddress" character varying(64),
        CONSTRAINT "PK_user_sessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_user_sessions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE contribution_records (
        id uuid NOT NULL,
        contributor_id uuid NOT NULL,
        type integer NOT NULL,
        description character varying(1000) NOT NULL,
        version integer NOT NULL,
        contributed_at timestamp with time zone NOT NULL,
        share_request_id uuid,
        document_ids jsonb,
        includes_game_data boolean NOT NULL,
        includes_metadata boolean NOT NULL,
        CONSTRAINT "PK_contribution_records" PRIMARY KEY (id),
        CONSTRAINT "FK_contribution_records_contributors_contributor_id" FOREIGN KEY (contributor_id) REFERENCES contributors (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE agents (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Type" character varying(50) NOT NULL,
        "StrategyName" character varying(100) NOT NULL,
        "StrategyParametersJson" jsonb NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "LastInvokedAt" timestamp with time zone,
        "InvocationCount" integer NOT NULL DEFAULT 0,
        "GameEntityId" uuid,
        CONSTRAINT "PK_agents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_agents_games_GameEntityId" FOREIGN KEY ("GameEntityId") REFERENCES games ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE "ChatThreads" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "GameId" uuid,
        "Title" text,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "LastMessageAt" timestamp with time zone NOT NULL,
        "MessagesJson" text NOT NULL,
        CONSTRAINT "PK_ChatThreads" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ChatThreads_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id"),
        CONSTRAINT "FK_ChatThreads_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE chunked_upload_sessions (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "FileName" character varying(256) NOT NULL,
        "TotalFileSize" bigint NOT NULL,
        "TotalChunks" integer NOT NULL,
        "ReceivedChunks" integer NOT NULL,
        "TempDirectory" character varying(512) NOT NULL,
        "Status" character varying(32) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "ErrorMessage" character varying(1024),
        "ReceivedChunkIndices" character varying(4096) NOT NULL,
        CONSTRAINT "PK_chunked_upload_sessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_chunked_upload_sessions_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_chunked_upload_sessions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE document_collections (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(1000),
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "DocumentsJson" text NOT NULL DEFAULT '[]',
        CONSTRAINT "PK_document_collections" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_document_collections_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_document_collections_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE "GameSessions" (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "Status" character varying(32) NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone,
        "WinnerName" character varying(256),
        "Notes" character varying(2000),
        "PlayersJson" text NOT NULL DEFAULT '[]',
        CONSTRAINT "PK_GameSessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GameSessions_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE rule_specs (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "Version" character varying(32) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedByUserId" uuid,
        "ParentVersionId" uuid,
        "MergedFromVersionIds" character varying(1024),
        "RowVersion" bytea,
        "GameEntityId" uuid,
        CONSTRAINT "PK_rule_specs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_rule_specs_games_GameEntityId" FOREIGN KEY ("GameEntityId") REFERENCES games ("Id"),
        CONSTRAINT "FK_rule_specs_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_rule_specs_rule_specs_ParentVersionId" FOREIGN KEY ("ParentVersionId") REFERENCES rule_specs ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_rule_specs_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE rulespec_comments (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "Version" character varying(32) NOT NULL,
        "AtomId" uuid,
        "UserId" uuid NOT NULL,
        "CommentText" character varying(2000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "LineNumber" integer,
        "LineContext" character varying(500),
        "ParentCommentId" uuid,
        "IsResolved" boolean NOT NULL DEFAULT FALSE,
        "ResolvedByUserId" uuid,
        "ResolvedAt" timestamp with time zone,
        "MentionedUserIds" character varying(1000) NOT NULL,
        CONSTRAINT "PK_rulespec_comments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_rulespec_comments_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_rulespec_comments_rulespec_comments_ParentCommentId" FOREIGN KEY ("ParentCommentId") REFERENCES rulespec_comments ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_rulespec_comments_users_ResolvedByUserId" FOREIGN KEY ("ResolvedByUserId") REFERENCES users ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_rulespec_comments_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE share_request_documents (
        id uuid NOT NULL,
        share_request_id uuid NOT NULL,
        document_id uuid NOT NULL,
        file_name character varying(255) NOT NULL,
        content_type character varying(100) NOT NULL,
        file_size bigint NOT NULL,
        attached_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_share_request_documents" PRIMARY KEY (id),
        CONSTRAINT "FK_share_request_documents_share_requests_share_request_id" FOREIGN KEY (share_request_id) REFERENCES share_requests (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE api_key_usage_logs (
        id uuid NOT NULL,
        key_id uuid NOT NULL,
        used_at timestamp with time zone NOT NULL,
        endpoint character varying(500) NOT NULL,
        ip_address character varying(45),
        user_agent character varying(500),
        http_method character varying(10),
        status_code integer,
        response_time_ms bigint,
        CONSTRAINT "PK_api_key_usage_logs" PRIMARY KEY (id),
        CONSTRAINT "FK_api_key_usage_logs_api_keys_key_id" FOREIGN KEY (key_id) REFERENCES api_keys ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE prompt_versions (
        "Id" uuid NOT NULL,
        "TemplateId" uuid NOT NULL,
        "VersionNumber" integer NOT NULL,
        "Content" text NOT NULL,
        "ChangeNotes" text,
        "IsActive" boolean NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ActivatedAt" timestamp with time zone,
        "ActivatedByUserId" uuid,
        "ActivationReason" text,
        "Metadata" character varying(4096),
        CONSTRAINT "PK_prompt_versions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_prompt_versions_prompt_templates_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES prompt_templates ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_prompt_versions_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE agent_configurations (
        id uuid NOT NULL,
        agent_id uuid NOT NULL,
        llm_provider integer NOT NULL,
        llm_model character varying(200) NOT NULL,
        agent_mode integer NOT NULL,
        selected_document_ids_json jsonb,
        temperature numeric(3,2) NOT NULL DEFAULT 0.7,
        max_tokens integer NOT NULL DEFAULT 4096,
        system_prompt_override character varying(5000),
        is_current boolean NOT NULL DEFAULT FALSE,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        created_by uuid NOT NULL,
        CONSTRAINT "PK_agent_configurations" PRIMARY KEY (id),
        CONSTRAINT ck_agent_configurations_max_tokens CHECK (max_tokens > 0 AND max_tokens <= 32000),
        CONSTRAINT ck_agent_configurations_temperature CHECK (temperature >= 0.0 AND temperature <= 2.0),
        CONSTRAINT "FK_agent_configurations_agents_agent_id" FOREIGN KEY (agent_id) REFERENCES agents ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE chats (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "AgentId" uuid NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "LastMessageAt" timestamp with time zone,
        CONSTRAINT "PK_chats" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_chats_agents_AgentId" FOREIGN KEY ("AgentId") REFERENCES agents ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_chats_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_chats_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE share_links (
        id uuid NOT NULL,
        thread_id uuid NOT NULL,
        creator_id uuid NOT NULL,
        role text NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        label character varying(200),
        access_count integer NOT NULL DEFAULT 0,
        last_accessed_at timestamp with time zone,
        CONSTRAINT "PK_share_links" PRIMARY KEY (id),
        CONSTRAINT "FK_share_links_ChatThreads_thread_id" FOREIGN KEY (thread_id) REFERENCES "ChatThreads" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_share_links_users_creator_id" FOREIGN KEY (creator_id) REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE chat_thread_collections (
        "Id" uuid NOT NULL,
        "ChatThreadId" uuid NOT NULL,
        "CollectionId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_chat_thread_collections" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_chat_thread_collections_ChatThreads_ChatThreadId" FOREIGN KEY ("ChatThreadId") REFERENCES "ChatThreads" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_chat_thread_collections_document_collections_CollectionId" FOREIGN KEY ("CollectionId") REFERENCES document_collections ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE pdf_documents (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "FileName" character varying(256) NOT NULL,
        "FilePath" character varying(1024) NOT NULL,
        "FileSizeBytes" bigint NOT NULL,
        "ContentType" character varying(128) NOT NULL,
        "UploadedByUserId" uuid NOT NULL,
        "UploadedAt" timestamp with time zone NOT NULL,
        "Metadata" character varying(2048),
        "ExtractedText" text,
        "ProcessingStatus" character varying(32) NOT NULL,
        "ProcessedAt" timestamp with time zone,
        "PageCount" integer,
        "CharacterCount" integer,
        "ProcessingError" character varying(1024),
        "ExtractedTables" character varying(8192),
        "ExtractedDiagrams" character varying(8192),
        "AtomicRules" character varying(8192),
        "TableCount" integer,
        "DiagramCount" integer,
        "AtomicRuleCount" integer,
        "ProcessingProgressJson" text,
        "Language" text NOT NULL,
        "CollectionId" uuid,
        "DocumentType" character varying(50) NOT NULL DEFAULT 'base',
        "SortOrder" integer NOT NULL DEFAULT 0,
        "IsPublic" boolean NOT NULL,
        "SharedGameId" uuid,
        "ContributorId" uuid,
        "SourceDocumentId" uuid,
        CONSTRAINT "PK_pdf_documents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_pdf_documents_document_collections_CollectionId" FOREIGN KEY ("CollectionId") REFERENCES document_collections ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_pdf_documents_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_pdf_documents_users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_session_states (
        id uuid NOT NULL,
        game_session_id uuid NOT NULL,
        template_id uuid NOT NULL,
        current_state_json jsonb NOT NULL,
        version integer NOT NULL DEFAULT 1,
        last_updated_at timestamp with time zone NOT NULL,
        last_updated_by character varying(255) NOT NULL,
        "RowVersion" bytea,
        CONSTRAINT "PK_game_session_states" PRIMARY KEY (id),
        CONSTRAINT "FK_game_session_states_GameSessions_game_session_id" FOREIGN KEY (game_session_id) REFERENCES "GameSessions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE rule_atoms (
        "Id" uuid NOT NULL,
        "RuleSpecId" uuid NOT NULL,
        "Key" character varying(32) NOT NULL,
        "Text" text NOT NULL,
        "Section" character varying(128),
        "PageNumber" integer,
        "LineNumber" integer,
        "SortOrder" integer NOT NULL,
        CONSTRAINT "PK_rule_atoms" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_rule_atoms_rule_specs_RuleSpecId" FOREIGN KEY ("RuleSpecId") REFERENCES rule_specs ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE prompt_audit_logs (
        "Id" uuid NOT NULL,
        "TemplateId" uuid NOT NULL,
        "VersionId" uuid,
        "Action" character varying(64) NOT NULL,
        "ChangedByUserId" uuid NOT NULL,
        "ChangedAt" timestamp with time zone NOT NULL,
        "Details" character varying(2048),
        CONSTRAINT "PK_prompt_audit_logs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_prompt_audit_logs_prompt_templates_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES prompt_templates ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_prompt_audit_logs_prompt_versions_VersionId" FOREIGN KEY ("VersionId") REFERENCES prompt_versions ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_prompt_audit_logs_users_ChangedByUserId" FOREIGN KEY ("ChangedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE chat_logs (
        "Id" uuid NOT NULL,
        "ChatId" uuid NOT NULL,
        "UserId" uuid,
        "Level" character varying(16) NOT NULL,
        "Message" text NOT NULL,
        "MetadataJson" character varying(2048),
        "SequenceNumber" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "DeletedAt" timestamp with time zone,
        "DeletedByUserId" uuid,
        "IsInvalidated" boolean NOT NULL DEFAULT FALSE,
        CONSTRAINT "PK_chat_logs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_chat_logs_chats_ChatId" FOREIGN KEY ("ChatId") REFERENCES chats ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_chat_logs_users_DeletedByUserId" FOREIGN KEY ("DeletedByUserId") REFERENCES users ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_chat_logs_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE shared_game_documents (
        id uuid NOT NULL,
        shared_game_id uuid NOT NULL,
        pdf_document_id uuid NOT NULL,
        document_type integer NOT NULL,
        version character varying(20) NOT NULL,
        is_active boolean NOT NULL DEFAULT FALSE,
        tags_json jsonb,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        created_by uuid NOT NULL,
        CONSTRAINT "PK_shared_game_documents" PRIMARY KEY (id),
        CONSTRAINT "FK_shared_game_documents_pdf_documents_pdf_document_id" FOREIGN KEY (pdf_document_id) REFERENCES pdf_documents ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_shared_game_documents_shared_games_shared_game_id" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE text_chunks (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "PdfDocumentId" uuid NOT NULL,
        "Content" text NOT NULL,
        "ChunkIndex" integer NOT NULL,
        "PageNumber" integer,
        "CharacterCount" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_text_chunks" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_text_chunks_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_text_chunks_pdf_documents_PdfDocumentId" FOREIGN KEY ("PdfDocumentId") REFERENCES pdf_documents ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE vector_documents (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "PdfDocumentId" uuid NOT NULL,
        "ChunkCount" integer NOT NULL,
        "TotalCharacters" integer NOT NULL,
        "IndexingStatus" character varying(32) NOT NULL,
        "IndexedAt" timestamp with time zone,
        "IndexingError" text,
        "EmbeddingModel" character varying(128) NOT NULL,
        "EmbeddingDimensions" integer NOT NULL,
        "Metadata" text,
        CONSTRAINT "PK_vector_documents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_vector_documents_games_GameId" FOREIGN KEY ("GameId") REFERENCES games ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_vector_documents_pdf_documents_PdfDocumentId" FOREIGN KEY ("PdfDocumentId") REFERENCES pdf_documents ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE TABLE game_state_snapshots (
        id uuid NOT NULL,
        session_state_id uuid NOT NULL,
        state_json jsonb NOT NULL,
        turn_number integer NOT NULL,
        description character varying(500) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by character varying(255) NOT NULL,
        CONSTRAINT "PK_game_state_snapshots" PRIMARY KEY (id),
        CONSTRAINT "FK_game_state_snapshots_game_session_states_session_state_id" FOREIGN KEY (session_state_id) REFERENCES game_session_states (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_admin_report_executions_report_id" ON admin_report_executions (report_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_agent_configurations_agent_id ON agent_configurations (agent_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_agent_configurations_current ON agent_configurations (agent_id, is_current) WHERE is_current = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_agent_feedback_Endpoint" ON agent_feedback ("Endpoint");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_agent_feedback_GameId" ON agent_feedback ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_agent_feedback_MessageId_UserId" ON agent_feedback ("MessageId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_agent_feedback_UserId" ON agent_feedback ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_agents_GameEntityId" ON agents ("GameEntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_agents_IsActive" ON agents ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_agents_LastInvokedAt" ON agents ("LastInvokedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_agents_Name" ON agents ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_agents_Type" ON agents ("Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_ai_request_logs_CreatedAt" ON ai_request_logs ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_ai_request_logs_Endpoint" ON ai_request_logs ("Endpoint");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_ai_request_logs_GameId" ON ai_request_logs ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_ai_request_logs_UserId" ON ai_request_logs ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_AiModelConfigurations_IsPrimary_IsActive" ON "SystemConfiguration"."AiModelConfigurations" ("IsPrimary", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_AiModelConfigurations_ModelId" ON "SystemConfiguration"."AiModelConfigurations" ("ModelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_AiModelConfigurations_Priority" ON "SystemConfiguration"."AiModelConfigurations" ("Priority");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_AiModelConfigurations_TierRouting" ON "SystemConfiguration"."AiModelConfigurations" (applicable_tier, environment_type, is_default_for_tier);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_alert_configurations_category ON alert_configurations (category);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_alert_configurations_config_key ON alert_configurations (config_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_alert_configurations_updated_at ON alert_configurations (updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_alert_rules_alert_type ON alert_rules (alert_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_alert_rules_created_at ON alert_rules (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_alert_rules_is_enabled ON alert_rules (is_enabled);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_alert_rules_name ON alert_rules (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_alerts_alert_type_triggered_at" ON alerts (alert_type, triggered_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_alerts_is_active" ON alerts (is_active) WHERE is_active = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_api_key_usage_logs_key_id ON api_key_usage_logs (key_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_api_key_usage_logs_key_id_used_at ON api_key_usage_logs (key_id, used_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_api_key_usage_logs_used_at ON api_key_usage_logs (used_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_api_keys_IsActive_ExpiresAt" ON api_keys ("IsActive", "ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_api_keys_KeyHash" ON api_keys ("KeyHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_api_keys_RevokedBy" ON api_keys ("RevokedBy");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_api_keys_UserId" ON api_keys ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_audit_logs_CreatedAt" ON audit_logs ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_audit_logs_UserId" ON audit_logs ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_badges_category ON badges (category);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_badges_code_unique ON badges (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_badges_display_order ON badges (display_order);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_badges_is_active ON badges (is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_badges_tier ON badges (tier);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_cache_stats_game_id" ON cache_stats (game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_cache_stats_question_hash" ON cache_stats (question_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX idx_chat_logs_chat_id_sequence_role ON chat_logs ("ChatId", "SequenceNumber", "Level");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX idx_chat_logs_deleted_at ON chat_logs ("DeletedAt") WHERE "DeletedAt" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX idx_chat_logs_user_id ON chat_logs ("UserId") WHERE "UserId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chat_logs_ChatId_CreatedAt" ON chat_logs ("ChatId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chat_logs_DeletedByUserId" ON chat_logs ("DeletedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chat_thread_collections_ChatThreadId" ON chat_thread_collections ("ChatThreadId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_chat_thread_collections_ChatThreadId_CollectionId" ON chat_thread_collections ("ChatThreadId", "CollectionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chat_thread_collections_CollectionId" ON chat_thread_collections ("CollectionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chats_AgentId" ON chats ("AgentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chats_GameId_StartedAt" ON chats ("GameId", "StartedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chats_UserId_LastMessageAt" ON chats ("UserId", "LastMessageAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_ChatThreads_GameId" ON "ChatThreads" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_ChatThreads_UserId" ON "ChatThreads" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chunked_upload_sessions_ExpiresAt" ON chunked_upload_sessions ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chunked_upload_sessions_GameId" ON chunked_upload_sessions ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chunked_upload_sessions_Status" ON chunked_upload_sessions ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chunked_upload_sessions_UserId" ON chunked_upload_sessions ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_chunked_upload_sessions_UserId_Status" ON chunked_upload_sessions ("UserId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_contribution_records_contributor_id ON contribution_records (contributor_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_contribution_records_contributor_version ON contribution_records (contributor_id, version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_contribution_records_share_request_id ON contribution_records (share_request_id) WHERE share_request_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_contributors_shared_game_id ON contributors (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_contributors_user_id ON contributors (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_contributors_user_shared_game_unique ON contributors (user_id, shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_document_collections_CreatedByUserId_CreatedAt" ON document_collections ("CreatedByUserId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_document_collections_GameId" ON document_collections ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_categories_name ON game_categories (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_categories_slug ON game_categories (slug);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_designers_name ON game_designers (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_errata_published_date ON game_errata (published_date DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_errata_shared_game_id ON game_errata (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_faqs_order ON game_faqs (shared_game_id, "order");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_faqs_shared_game_id ON game_faqs (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_mechanics_name ON game_mechanics (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_mechanics_slug ON game_mechanics (slug);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_publishers_name ON game_publishers (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_session_states_game_session_id ON game_session_states (game_session_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_session_states_last_updated_at ON game_session_states (last_updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_session_states_template_id ON game_session_states (template_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_state_snapshots_created_at ON game_state_snapshots (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_state_snapshots_session_state_id ON game_state_snapshots (session_state_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_state_snapshots_session_state_id_turn_number ON game_state_snapshots (session_state_id, turn_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_state_templates_shared_game_id ON game_state_templates (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_game_state_templates_shared_game_id_is_active ON game_state_templates (shared_game_id, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_game_state_templates_shared_game_id_version ON game_state_templates (shared_game_id, version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_games_Name" ON games ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_Games_SharedGameId" ON games ("SharedGameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_GameSessions_GameId" ON "GameSessions" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_GameSessions_StartedAt" ON "GameSessions" ("StartedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_GameSessions_Status_StartedAt" ON "GameSessions" ("Status", "StartedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_library_share_links_expires_at ON library_share_links (expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_library_share_links_privacy_level ON library_share_links (privacy_level);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_library_share_links_revoked_at ON library_share_links (revoked_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_library_share_links_share_token ON library_share_links (share_token);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_library_share_links_user_id ON library_share_links (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_llm_cost_logs_created_at ON llm_cost_logs (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_llm_cost_logs_provider_date ON llm_cost_logs (provider, request_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_llm_cost_logs_request_date ON llm_cost_logs (request_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_llm_cost_logs_role_date ON llm_cost_logs (user_role, request_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_llm_cost_logs_user_id ON llm_cost_logs (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_n8n_configs_CreatedByUserId" ON n8n_configs ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_n8n_configs_Name" ON n8n_configs ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_notifications_user_id_created_at" ON notifications (user_id, created_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_notifications_user_id_is_read" ON notifications (user_id, is_read);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_notifications_user_id_is_read_created_at" ON notifications (user_id, is_read, created_at) WHERE is_read = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_oauth_accounts_Provider" ON oauth_accounts ("Provider");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_oauth_accounts_Provider_ProviderUserId" ON oauth_accounts ("Provider", "ProviderUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_oauth_accounts_UserId" ON oauth_accounts ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_password_reset_tokens_ExpiresAt" ON password_reset_tokens ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_password_reset_tokens_TokenHash" ON password_reset_tokens ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_password_reset_tokens_UserId" ON password_reset_tokens ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_pdf_documents_CollectionId" ON pdf_documents ("CollectionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_pdf_documents_CollectionId_SortOrder" ON pdf_documents ("CollectionId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_pdf_documents_GameId_UploadedAt" ON pdf_documents ("GameId", "UploadedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_pdf_documents_UploadedByUserId" ON pdf_documents ("UploadedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_audit_logs_Action" ON prompt_audit_logs ("Action");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_audit_logs_ChangedAt" ON prompt_audit_logs ("ChangedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_audit_logs_ChangedByUserId" ON prompt_audit_logs ("ChangedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_audit_logs_TemplateId" ON prompt_audit_logs ("TemplateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_audit_logs_VersionId" ON prompt_audit_logs ("VersionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_evaluation_results_executed_at" ON prompt_evaluation_results (executed_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_evaluation_results_template_id" ON prompt_evaluation_results (template_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_evaluation_results_template_id_version_id_executed_at" ON prompt_evaluation_results (template_id, version_id, executed_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_evaluation_results_version_id" ON prompt_evaluation_results (version_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_templates_Category" ON prompt_templates ("Category");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_templates_CreatedAt" ON prompt_templates ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_templates_CreatedByUserId" ON prompt_templates ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_prompt_templates_Name" ON prompt_templates ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_versions_CreatedAt" ON prompt_versions ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_versions_CreatedByUserId" ON prompt_versions ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_prompt_versions_TemplateId_IsActive" ON prompt_versions ("TemplateId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_prompt_versions_TemplateId_VersionNumber" ON prompt_versions ("TemplateId", "VersionNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_quick_questions_active ON quick_questions (shared_game_id, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_quick_questions_order ON quick_questions (shared_game_id, display_order);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_quick_questions_shared_game_id ON quick_questions (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_rule_atoms_RuleSpecId_SortOrder" ON rule_atoms ("RuleSpecId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_rule_specs_CreatedByUserId" ON rule_specs ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_rule_specs_GameEntityId" ON rule_specs ("GameEntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_rule_specs_GameId_Version" ON rule_specs ("GameId", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_rule_specs_ParentVersionId" ON rule_specs ("ParentVersionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_rulebook_analyses_game_pdf_active ON rulebook_analyses (shared_game_id, pdf_document_id, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_rulebook_analyses_game_pdf_version ON rulebook_analyses (shared_game_id, pdf_document_id, version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_rulebook_analyses_pdf_document_id ON rulebook_analyses (pdf_document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_rulebook_analyses_shared_game_id ON rulebook_analyses (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX idx_rulespec_comments_game_version_line ON rulespec_comments ("GameId", "Version", "LineNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX idx_rulespec_comments_is_resolved ON rulespec_comments ("IsResolved");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX idx_rulespec_comments_parent_id ON rulespec_comments ("ParentCommentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX idx_rulespec_comments_user_id ON rulespec_comments ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_rulespec_comments_AtomId" ON rulespec_comments ("AtomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_rulespec_comments_GameId_Version" ON rulespec_comments ("GameId", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_rulespec_comments_ResolvedByUserId" ON rulespec_comments ("ResolvedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_links_creator_id ON share_links (creator_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_links_expires_at ON share_links (expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_links_revoked_at ON share_links (revoked_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_links_thread_id ON share_links (thread_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_request_documents_document_id ON share_request_documents (document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_share_request_documents_request_document ON share_request_documents (share_request_id, document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_request_documents_share_request_id ON share_request_documents (share_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_request_limit_configs_is_active ON share_request_limit_configs (is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_share_request_limit_configs_tier_unique_active ON share_request_limit_configs (tier) WHERE is_active = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_requests_review_lock_expires_at ON share_requests (review_lock_expires_at) WHERE review_lock_expires_at IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_requests_reviewing_admin_id ON share_requests (reviewing_admin_id) WHERE reviewing_admin_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_requests_source_game_id ON share_requests (source_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_requests_status ON share_requests (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_share_requests_target_shared_game_id" ON share_requests (target_shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_requests_user_id ON share_requests (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_share_requests_user_source_status ON share_requests (user_id, source_game_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_shared_game_categories_shared_game_id" ON shared_game_categories (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_delete_requests_created_at ON shared_game_delete_requests (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_delete_requests_shared_game_id ON shared_game_delete_requests (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_delete_requests_status ON shared_game_delete_requests (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_shared_game_designers_shared_game_id" ON shared_game_designers (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_shared_game_documents_active_version ON shared_game_documents (shared_game_id, document_type, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_shared_game_documents_pdf_document_id ON shared_game_documents (pdf_document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_shared_game_documents_shared_game_id ON shared_game_documents (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_shared_game_documents_version_unique ON shared_game_documents (shared_game_id, document_type, version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_shared_game_mechanics_shared_game_id" ON shared_game_mechanics (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_shared_game_publishers_shared_game_id" ON shared_game_publishers (shared_game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_shared_games_bgg_id ON shared_games (bgg_id) WHERE bgg_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_shared_games_status ON shared_games (status) WHERE is_deleted = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_shared_games_title ON shared_games (title) WHERE is_deleted = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_system_configurations_Category" ON system_configurations ("Category");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_system_configurations_CreatedByUserId" ON system_configurations ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_system_configurations_Environment" ON system_configurations ("Environment");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_system_configurations_IsActive" ON system_configurations ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_system_configurations_Key_Environment" ON system_configurations ("Key", "Environment");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_system_configurations_UpdatedAt" ON system_configurations ("UpdatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_system_configurations_UpdatedByUserId" ON system_configurations ("UpdatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_temp_sessions_ExpiresAt" ON temp_sessions ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_temp_sessions_TokenHash" ON temp_sessions ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_temp_sessions_UserId" ON temp_sessions ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_text_chunks_ChunkIndex" ON text_chunks ("ChunkIndex");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_text_chunks_GameId" ON text_chunks ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_text_chunks_PageNumber" ON text_chunks ("PageNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_text_chunks_PdfDocumentId" ON text_chunks ("PdfDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_used_totp_codes_expiry ON used_totp_codes ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_used_totp_codes_user_code_unique ON used_totp_codes ("UserId", "CodeHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_user_backup_codes_CodeHash" ON user_backup_codes ("CodeHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_user_backup_codes_UserId" ON user_backup_codes ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_user_backup_codes_UserId_IsUsed" ON user_backup_codes ("UserId", "IsUsed") WHERE "IsUsed" = FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_user_badges_badge_id ON user_badges (badge_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_user_badges_revoked_at ON user_badges (revoked_at) WHERE revoked_at IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_user_badges_triggering_share_request_id ON user_badges (triggering_share_request_id) WHERE triggering_share_request_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_user_badges_user_badge_unique ON user_badges (user_id, badge_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_user_badges_user_id ON user_badges (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_user_library_entries_GameId" ON user_library_entries ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_UserLibraryEntries_AddedAt" ON user_library_entries ("AddedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_UserLibraryEntries_CustomAgentConfigJson" ON user_library_entries USING gin ("CustomAgentConfigJson");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_UserLibraryEntries_UserId" ON user_library_entries ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_UserLibraryEntries_UserId_GameId" ON user_library_entries ("UserId", "GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_user_rate_limit_overrides_created_by_admin_id ON user_rate_limit_overrides (created_by_admin_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_user_rate_limit_overrides_expires_at ON user_rate_limit_overrides (expires_at) WHERE expires_at IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_user_rate_limit_overrides_user_id_active ON user_rate_limit_overrides (user_id) WHERE expires_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_user_sessions_TokenHash" ON user_sessions ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_user_sessions_UserId" ON user_sessions ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_validation_accuracy_baselines_accuracy ON validation_accuracy_baselines (accuracy);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_validation_accuracy_baselines_context ON validation_accuracy_baselines (context);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_validation_accuracy_baselines_dataset_id ON validation_accuracy_baselines (dataset_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_validation_accuracy_baselines_evaluation_id ON validation_accuracy_baselines (evaluation_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_validation_accuracy_baselines_measured_at ON validation_accuracy_baselines (measured_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX ix_validation_accuracy_baselines_meets_baseline ON validation_accuracy_baselines (meets_baseline);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_vector_documents_GameId" ON vector_documents ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_vector_documents_PdfDocumentId" ON vector_documents ("PdfDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_workflow_error_logs_created_at" ON workflow_error_logs (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_workflow_error_logs_execution_id" ON workflow_error_logs (execution_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    CREATE INDEX "IX_workflow_error_logs_workflow_id" ON workflow_error_logs (workflow_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260122181559_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260122181559_InitialCreate', '9.0.11');
    END IF;
END $EF$;
COMMIT;

