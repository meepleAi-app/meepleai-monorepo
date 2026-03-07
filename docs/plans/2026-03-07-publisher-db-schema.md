# Database Schema: Publisher Portal & Session Photos

**Date**: 2026-03-07
**Status**: Draft
**Related PRD**: `docs/plans/2026-03-07-game-ecosystem-prd.md`

---

## Phase 0: Session Photo Attachments

### Table: `session_attachments`

```sql
CREATE TABLE session_attachments (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id        UUID NOT NULL,
    snapshot_index    INT NULL,
    player_id         UUID NOT NULL,
    attachment_type   SMALLINT NOT NULL DEFAULT 0,
    blob_url          VARCHAR(2048) NOT NULL,
    thumbnail_url     VARCHAR(2048) NULL,
    caption           VARCHAR(200) NULL,
    content_type      VARCHAR(50) NOT NULL,
    file_size_bytes   BIGINT NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted        BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT fk_session_attachment_session
        FOREIGN KEY (session_id) REFERENCES live_game_sessions(id) ON DELETE CASCADE,

    CONSTRAINT ck_session_attachment_type
        CHECK (attachment_type BETWEEN 0 AND 4),

    CONSTRAINT ck_session_attachment_content_type
        CHECK (content_type IN ('image/jpeg', 'image/png')),

    CONSTRAINT ck_session_attachment_file_size
        CHECK (file_size_bytes BETWEEN 1024 AND 10485760)
);

-- Primary query: list photos for a session
CREATE INDEX ix_session_attachment_session
    ON session_attachments(session_id)
    WHERE is_deleted = false;

-- Query: photos by player in a session
CREATE INDEX ix_session_attachment_player
    ON session_attachments(session_id, player_id)
    WHERE is_deleted = false;

-- Query: photos for a specific snapshot
CREATE INDEX ix_session_attachment_snapshot
    ON session_attachments(session_id, snapshot_index)
    WHERE is_deleted = false AND snapshot_index IS NOT NULL;

-- Background job: find old photos to clean up
CREATE INDEX ix_session_attachment_cleanup
    ON session_attachments(created_at)
    WHERE is_deleted = false;

COMMENT ON TABLE session_attachments IS 'Photos of board state captured during live game sessions';
COMMENT ON COLUMN session_attachments.attachment_type IS '0=PlayerArea, 1=BoardState, 2=CharacterSheet, 3=ResourceInventory, 4=Custom';
COMMENT ON COLUMN session_attachments.snapshot_index IS 'Links to SessionSnapshot.SnapshotIndex within the same session';
```

### EF Core Configuration

```csharp
internal sealed class SessionAttachmentConfiguration : IEntityTypeConfiguration<SessionAttachment>
{
    public void Configure(EntityTypeBuilder<SessionAttachment> builder)
    {
        builder.ToTable("session_attachments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SessionId).IsRequired();
        builder.Property(e => e.PlayerId).IsRequired();
        builder.Property(e => e.AttachmentType).IsRequired();
        builder.Property(e => e.BlobUrl).IsRequired().HasMaxLength(2048);
        builder.Property(e => e.ThumbnailUrl).HasMaxLength(2048);
        builder.Property(e => e.Caption).HasMaxLength(200);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        // Soft delete filter
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Relationship
        builder.HasOne<LiveGameSession>()
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.SessionId)
            .HasFilter("is_deleted = false");

        builder.HasIndex(e => new { e.SessionId, e.PlayerId })
            .HasFilter("is_deleted = false");

        builder.HasIndex(e => new { e.SessionId, e.SnapshotIndex })
            .HasFilter("is_deleted = false AND snapshot_index IS NOT NULL");

        builder.HasIndex(e => e.CreatedAt)
            .HasFilter("is_deleted = false");
    }
}
```

---

## Phase 1A: Publisher Profile

### Table: `publisher_profiles`

```sql
CREATE TABLE publisher_profiles (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id               UUID NOT NULL UNIQUE,
    company_name          VARCHAR(200) NOT NULL,
    website               VARCHAR(500) NULL,
    description           TEXT NULL,
    contact_email         VARCHAR(320) NOT NULL,
    logo_url              VARCHAR(2048) NULL,
    verification_status   SMALLINT NOT NULL DEFAULT 0,
    verified_by_admin_id  UUID NULL,
    verified_at           TIMESTAMPTZ NULL,
    rejection_reason      VARCHAR(500) NULL,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ NULL,
    is_deleted            BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT fk_publisher_user
        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,

    CONSTRAINT fk_publisher_verifier
        FOREIGN KEY (verified_by_admin_id) REFERENCES users(id) ON DELETE SET NULL,

    CONSTRAINT ck_publisher_verification_status
        CHECK (verification_status BETWEEN 0 AND 3)
);

-- Admin query: pending publishers
CREATE INDEX ix_publisher_profiles_status
    ON publisher_profiles(verification_status)
    WHERE is_deleted = false;

-- Lookup by user
CREATE UNIQUE INDEX ix_publisher_profiles_user
    ON publisher_profiles(user_id)
    WHERE is_deleted = false;

COMMENT ON TABLE publisher_profiles IS 'Board game publisher/designer profiles with admin verification';
COMMENT ON COLUMN publisher_profiles.verification_status IS '0=Pending, 1=Approved, 2=Rejected, 3=Suspended';
```

### EF Core Configuration

```csharp
internal sealed class PublisherProfileConfiguration : IEntityTypeConfiguration<PublisherProfile>
{
    public void Configure(EntityTypeBuilder<PublisherProfile> builder)
    {
        builder.ToTable("publisher_profiles");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Website).HasMaxLength(500);
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.ContactEmail).IsRequired().HasMaxLength(320);
        builder.Property(e => e.LogoUrl).HasMaxLength(2048);
        builder.Property(e => e.VerificationStatus).IsRequired();
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<PublisherProfile>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId).IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(e => e.VerificationStatus)
            .HasFilter("is_deleted = false");
    }
}
```

---

## Phase 1B: Extended RulebookAnalysis

### ALTER existing `rulebook_analyses` table

```sql
-- Add new columns to existing rulebook_analyses table
ALTER TABLE rulebook_analyses
    ADD COLUMN clarity_score DECIMAL(4,2) NULL,
    ADD COLUMN completeness_score DECIMAL(4,2) NULL,
    ADD COLUMN consistency_score DECIMAL(4,2) NULL,
    ADD COLUMN overall_score DECIMAL(4,2) NULL,
    ADD COLUMN ambiguous_rule_count INT NOT NULL DEFAULT 0,
    ADD COLUMN ambiguous_rules JSONB NULL,
    ADD COLUMN predicted_faq_count INT NOT NULL DEFAULT 0,
    ADD COLUMN predicted_faqs JSONB NULL,
    ADD COLUMN conflict_count INT NOT NULL DEFAULT 0,
    ADD COLUMN conflicts JSONB NULL,
    ADD COLUMN completeness_gaps JSONB NULL,
    ADD COLUMN version_number INT NOT NULL DEFAULT 1,
    ADD COLUMN previous_version_id UUID NULL,
    ADD COLUMN analysis_model_version VARCHAR(100) NULL,
    ADD COLUMN analysis_duration_ms INT NULL;

-- FK for version chain
ALTER TABLE rulebook_analyses
    ADD CONSTRAINT fk_rulebook_analysis_previous
    FOREIGN KEY (previous_version_id) REFERENCES rulebook_analyses(id);

-- Index for version queries
CREATE INDEX ix_rulebook_analysis_game_version
    ON rulebook_analyses(shared_game_id, version_number DESC);

COMMENT ON COLUMN rulebook_analyses.clarity_score IS 'AI-assessed rule clarity (0.0-10.0)';
COMMENT ON COLUMN rulebook_analyses.ambiguous_rules IS 'JSON array of { section, text, problem, suggestion }';
COMMENT ON COLUMN rulebook_analyses.predicted_faqs IS 'JSON array of { question, answer, ruleReference, confidence }';
COMMENT ON COLUMN rulebook_analyses.conflicts IS 'JSON array of { rule1, rule2, issue }';
```

---

## Phase 1C: Test Campaigns

### Table: `test_campaigns`

```sql
CREATE TABLE test_campaigns (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    publisher_id            UUID NOT NULL,
    shared_game_id          UUID NOT NULL,
    rulebook_analysis_id    UUID NULL,
    title                   VARCHAR(200) NOT NULL,
    description             TEXT NULL,
    invite_code             VARCHAR(8) NOT NULL UNIQUE,
    max_testers             INT NOT NULL DEFAULT 20,
    status                  SMALLINT NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    activated_at            TIMESTAMPTZ NULL,
    completed_at            TIMESTAMPTZ NULL,
    is_deleted              BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT fk_campaign_publisher
        FOREIGN KEY (publisher_id) REFERENCES publisher_profiles(id) ON DELETE CASCADE,

    CONSTRAINT fk_campaign_game
        FOREIGN KEY (shared_game_id) REFERENCES shared_games(id) ON DELETE CASCADE,

    CONSTRAINT fk_campaign_analysis
        FOREIGN KEY (rulebook_analysis_id) REFERENCES rulebook_analyses(id) ON DELETE SET NULL,

    CONSTRAINT ck_campaign_status
        CHECK (status BETWEEN 0 AND 3),

    CONSTRAINT ck_campaign_max_testers
        CHECK (max_testers BETWEEN 1 AND 100)
);

CREATE UNIQUE INDEX ix_test_campaign_invite_code
    ON test_campaigns(invite_code)
    WHERE is_deleted = false;

CREATE INDEX ix_test_campaign_publisher
    ON test_campaigns(publisher_id)
    WHERE is_deleted = false;

CREATE INDEX ix_test_campaign_game
    ON test_campaigns(shared_game_id)
    WHERE is_deleted = false;

COMMENT ON TABLE test_campaigns IS 'Publisher-created testing campaigns for rulebook validation';
COMMENT ON COLUMN test_campaigns.status IS '0=Draft, 1=Active, 2=Completed, 3=Archived';
COMMENT ON COLUMN test_campaigns.invite_code IS '8-char alphanumeric code for testers to join';
```

### Table: `test_session_feedback`

```sql
CREATE TABLE test_session_feedback (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    campaign_id       UUID NOT NULL,
    session_id        UUID NOT NULL,
    player_id         UUID NOT NULL,
    feedback_type     SMALLINT NOT NULL,
    rule_reference    VARCHAR(200) NULL,
    content           TEXT NOT NULL,
    clarity_rating    SMALLINT NULL,
    game_context      JSONB NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted        BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT fk_feedback_campaign
        FOREIGN KEY (campaign_id) REFERENCES test_campaigns(id) ON DELETE CASCADE,

    CONSTRAINT fk_feedback_session
        FOREIGN KEY (session_id) REFERENCES live_game_sessions(id) ON DELETE CASCADE,

    CONSTRAINT ck_feedback_type
        CHECK (feedback_type BETWEEN 0 AND 3),

    CONSTRAINT ck_feedback_clarity_rating
        CHECK (clarity_rating IS NULL OR clarity_rating BETWEEN 1 AND 5)
);

CREATE INDEX ix_feedback_campaign
    ON test_session_feedback(campaign_id)
    WHERE is_deleted = false;

CREATE INDEX ix_feedback_session
    ON test_session_feedback(session_id)
    WHERE is_deleted = false;

-- For aggregate queries: most confusing rules
CREATE INDEX ix_feedback_rule_reference
    ON test_session_feedback(campaign_id, rule_reference)
    WHERE is_deleted = false AND rule_reference IS NOT NULL;

COMMENT ON TABLE test_session_feedback IS 'Tester feedback during publisher test sessions';
COMMENT ON COLUMN test_session_feedback.feedback_type IS '0=RuleConfusion, 1=RuleClarity, 2=Suggestion, 3=Bug';
COMMENT ON COLUMN test_session_feedback.game_context IS '{ turnIndex, phaseIndex, phaseName }';
```

---

## Entity Relationship Diagram

```
users
  │
  ├──1:1──► publisher_profiles
  │              │
  │              ├──1:N──► test_campaigns
  │              │              │
  │              │              ├──1:N──► test_session_feedback
  │              │              │              │
  │              │              │              └──N:1──► live_game_sessions
  │              │              │
  │              │              └──N:1──► shared_games
  │              │                           │
  │              │                           └──1:N──► rulebook_analyses (extended)
  │              │
  │              └──verified_by──► users (admin)
  │
  └──N:1──► live_game_sessions
                 │
                 ├──1:N──► session_attachments (NEW)
                 │
                 ├──1:N──► session_snapshots
                 │
                 └──1:N──► live_session_players
```

---

## Migration Plan

### Migration 1: `AddSessionAttachments` (Phase 0)
- Create `session_attachments` table
- Create all indexes
- No data migration needed

### Migration 2: `AddPublisherProfiles` (Phase 1A)
- Create `publisher_profiles` table
- Add `PublisherPolicy` to authorization config
- No data migration needed

### Migration 3: `ExtendRulebookAnalysis` (Phase 1B)
- ALTER `rulebook_analyses` — add new columns
- All new columns nullable or have defaults → no data issues
- Backfill `version_number = 1` for existing records

### Migration 4: `AddTestCampaigns` (Phase 1C)
- Create `test_campaigns` table
- Create `test_session_feedback` table
- No data migration needed

### Migration Order
```
1. AddSessionAttachments       (Phase 0, independent)
2. AddPublisherProfiles        (Phase 1A, independent)
3. ExtendRulebookAnalysis      (Phase 1B, after Phase 1A)
4. AddTestCampaigns            (Phase 1C, after Phase 1A + 1B)
```

---

## Storage Estimates

### Session Photos (Phase 0)
```
Assumptions:
- 100 active sessions/month
- 3 snapshots/session average
- 4 players x 2 photos = 8 photos/snapshot
- 5MB average photo size

Monthly new storage: 100 x 3 x 8 x 5MB = 12 GB/month
With 90-day retention: ~36 GB max steady state
Thumbnail storage: ~1% of original = 360 MB
S3 cost at $0.023/GB: ~$0.83/month

Verdict: Negligible cost.
```

### Publisher Data (Phase 1)
```
Publisher profiles: ~1 KB each, negligible
Rulebook analyses: ~50 KB each (JSONB), ~100 per year = 5 MB
Test campaigns: ~2 KB each, negligible
Feedback entries: ~1 KB each, ~10,000/year = 10 MB
Total: < 20 MB/year

Verdict: Negligible.
```
