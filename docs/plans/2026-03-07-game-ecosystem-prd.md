# PRD: MeepleAI Game Ecosystem Evolution

**Date**: 2026-03-07
**Status**: Draft
**Author**: Spec Panel (Wiegers, Adzic, Cockburn, Fowler, Nygard, Crispin)
**Stakeholders**: Product, Engineering, AI Team

---

## Executive Summary

This PRD defines 4 interconnected epics that evolve MeepleAI from a game catalog + AI assistant into a **complete board game lifecycle platform** — from publisher rule-testing through live multi-day play sessions with photo state capture.

### Strategic Decisions (Locked)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Publisher pricing | Free basic, premium analytics | Lower barrier to entry |
| Photo retention | 90 days post-session-completion | Balance cost vs. usefulness |
| Session architecture | Merge SessionTracking into LiveGameSession (future) | Reduce duplication |
| Publisher catalog access | Self-service for testing, admin gate for public | Trust but verify |
| Rulebook analysis model | Dedicated fine-tuned model | Higher quality, domain-specific |

---

## Phase 0: Session Photo Attachments

**Epic**: Session Board State Capture
**Priority**: P0 (highest — enables campaign play UX)
**Estimated Issues**: ~15
**Dependencies**: None (builds on existing LiveGameSession + IBlobStorageService)

### Problem Statement

Players of campaign/multi-session games (Gloomhaven, Risk Legacy, Pandemic Legacy) need to save physical board state between play sessions. Currently, LiveGameSession supports JSON state snapshots but no visual evidence. Players resort to taking photos on personal phones with no linkage to the session record.

### Requirements

#### Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| SP-001 | Players can upload photos during a session | Must | Max 5 photos/player/snapshot, JPEG/PNG, max 10MB each |
| SP-002 | Photos are linked to session snapshots | Must | Each photo has optional SnapshotIndex FK |
| SP-003 | Photos are categorized by type | Should | Types: PlayerArea, BoardState, CharacterSheet, ResourceInventory, Custom |
| SP-004 | Host can upload "main board" photos | Must | BoardState type, visible to all players |
| SP-005 | Thumbnails are auto-generated | Should | 300px resize on upload, stored alongside original |
| SP-006 | Photos display in session resume flow | Must | Gallery view with player attribution on Resume |
| SP-007 | Photos are auto-deleted 90 days after session completion | Must | Background job, S3 lifecycle policy |
| SP-008 | Players can add captions to photos | Could | Max 200 chars per photo |
| SP-009 | Photos accessible in session history | Must | Read-only gallery for completed sessions |
| SP-010 | Upload progress feedback | Should | XHR progress bar (same pattern as PDF upload) |

#### Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| SP-NFR-001 | Upload latency P95 | < 3s for 10MB photo on 50Mbps connection |
| SP-NFR-002 | Thumbnail generation | < 500ms server-side |
| SP-NFR-003 | Storage cost per session | < $0.05/month average (5 players x 3 photos x 5MB avg) |
| SP-NFR-004 | Gallery load time P95 | < 1s for 20 thumbnails |
| SP-NFR-005 | Concurrent uploads | Support 5 simultaneous uploads per session |

### Data Model

```
SessionAttachment (new entity)
├── Id: Guid (PK)
├── SessionId: Guid (FK → LiveGameSession, required, CASCADE)
├── SnapshotIndex: int? (links to specific SessionSnapshot)
├── PlayerId: Guid (FK → LiveSessionPlayer, required)
├── AttachmentType: enum (PlayerArea=0, BoardState=1, CharacterSheet=2, ResourceInventory=3, Custom=4)
├── BlobUrl: string (S3 URL, required, max 2048)
├── ThumbnailUrl: string? (S3 URL, max 2048)
├── Caption: string? (max 200)
├── ContentType: string (image/jpeg or image/png, required)
├── FileSizeBytes: long (min 1KB, max 10MB)
├── CreatedAt: DateTime
└── IsDeleted: bool (soft delete for retention cleanup)

Indexes:
├── IX_SessionAttachment_SessionId (clustered on SessionId for fast listing)
├── IX_SessionAttachment_PlayerId (for per-player queries)
└── IX_SessionAttachment_CreatedAt (for retention cleanup job)
```

### API Endpoints

```
POST   /api/v1/live-sessions/{sessionId}/attachments
  - Multipart form: file + attachmentType + caption? + snapshotIndex?
  - Auth: Session participant only
  - Validation: File type (JPEG/PNG), size (< 10MB), count (< 5 per player per snapshot)
  - Returns: 201 Created with AttachmentDto

GET    /api/v1/live-sessions/{sessionId}/attachments
  - Query params: playerId?, snapshotIndex?, type?
  - Auth: Session participant or spectator
  - Returns: List<AttachmentDto> with thumbnail URLs

GET    /api/v1/live-sessions/{sessionId}/attachments/{attachmentId}
  - Auth: Session participant
  - Returns: AttachmentDto with pre-signed download URL (1h expiry)

DELETE /api/v1/live-sessions/{sessionId}/attachments/{attachmentId}
  - Auth: Photo owner or Host
  - Returns: 204 No Content
  - Note: Soft delete, S3 cleanup via background job
```

### Frontend Integration

```
New Components:
├── PhotoUploadButton.tsx (in ToolRail, opens upload modal)
├── PhotoUploadModal.tsx (camera/gallery picker, type selector, caption input, progress)
├── SessionPhotoGallery.tsx (grid of thumbnails, lightbox on click)
├── ResumePhotoReview.tsx (shown on session resume, grouped by player)
└── PhotoAttachmentCard.tsx (single photo with caption, type badge, player name)

Integration Points:
├── ToolRail: Add "Camera" tool button (opens PhotoUploadModal)
├── Session Pause flow: Prompt "Upload board photos before pausing?"
├── Session Resume flow: Show ResumePhotoReview before "All Ready" confirmation
├── Session History: PhotoGallery tab in completed session detail
└── SessionStore: Add attachments[] state, uploadPhoto() action
```

### Background Jobs

```
SessionAttachmentCleanupJob (daily, 3 AM UTC):
  1. Query: SELECT * FROM session_attachments sa
     JOIN live_game_sessions s ON sa.SessionId = s.Id
     WHERE s.Status = 'Completed'
     AND s.CompletedAt < NOW() - INTERVAL '90 days'
     AND sa.IsDeleted = false
  2. For each: Delete from S3 (original + thumbnail)
  3. Mark IsDeleted = true
  4. Log: "{count} attachments cleaned up for {sessionCount} sessions"
```

### Scenarios (Gherkin)

```gherkin
Scenario: Player uploads a photo of their game area during play
  Given a LiveGameSession "GHAVEN" is InProgress
  And I am an active player "Alice" in the session
  When I upload a JPEG photo (3.2MB) with type "PlayerArea" and caption "My character sheet turn 5"
  Then a SessionAttachment is created with BlobUrl pointing to S3
  And a 300px thumbnail is generated and stored
  And other players can see the thumbnail in the photo gallery
  And the attachment count for Alice in this session is 1

Scenario: Photo upload rejected when limit exceeded
  Given I already have 5 photos for the current snapshot
  When I try to upload a 6th photo
  Then I receive a 422 Unprocessable Entity with message "Maximum 5 photos per player per snapshot"

Scenario: Photos displayed on session resume
  Given session "GHAVEN" was Paused yesterday with 3 player photos and 1 board photo
  When the host clicks "Resume Session"
  Then a ResumePhotoReview screen shows:
    | Player  | Photos | Type          |
    | Alice   | 1      | PlayerArea    |
    | Bob     | 1      | PlayerArea    |
    | Charlie | 1      | CharacterSheet|
    | Host    | 1      | BoardState    |
  And each player confirms "Board restored" before session resumes

Scenario: Old photos cleaned up after 90 days
  Given session "GHAVEN" was completed 91 days ago
  And it has 8 photos totaling 35MB
  When the cleanup job runs
  Then all 8 S3 objects (originals + thumbnails) are deleted
  And the session_attachments rows are marked IsDeleted = true
  And the session record itself is preserved (only photos deleted)
```

---

## Phase 1: Publisher Role & Test Campaigns

**Epic**: Board Game Publisher Portal
**Priority**: P1
**Estimated Issues**: ~25 (3 sub-phases)
**Dependencies**: Phase 0 (photos enable test session evidence)

### Problem Statement

Board game publishers and designers lack tools to validate their rulebooks before printing. Current process: print prototype, find playtesters, observe confusion, manually note issues, revise rulebook, repeat. This is slow (weeks per iteration), expensive, and subjective.

MeepleAI can offer **AI-powered rulebook analysis + structured playtesting** — a unique value proposition with no direct competitor.

### Actor Definition

```
Publisher (new role):
  - A company or individual designing/publishing board games
  - Registers with company info, verified by admin
  - Can: Create draft games, upload rulebooks, run test campaigns, view analytics
  - Cannot: Publish to public catalog directly (admin approval required)
  - Tier: Free (basic analysis) / Premium (advanced analytics, unlimited campaigns)
```

### Requirements

#### Phase 1A: Publisher Onboarding

| ID | Requirement | Priority |
|----|-------------|----------|
| PB-001 | User can request Publisher role upgrade | Must |
| PB-002 | Request includes: company name, website, description, contact email | Must |
| PB-003 | Admin reviews and approves/rejects publisher request | Must |
| PB-004 | Approved publisher sees "Publisher Dashboard" in nav | Must |
| PB-005 | Publisher can update company profile | Should |
| PB-006 | Publisher profile visible on published games | Could |

**Data Model — PublisherProfile:**
```
PublisherProfile (new entity in Administration BC)
├── Id: Guid (PK)
├── UserId: Guid (FK → User, unique, required)
├── CompanyName: string (required, max 200)
├── Website: string? (max 500, URL validated)
├── Description: string? (max 2000)
├── ContactEmail: string (required, email validated)
├── LogoUrl: string? (S3, max 2048)
├── VerificationStatus: enum (Pending=0, Approved=1, Rejected=2, Suspended=3)
├── VerifiedByAdminId: Guid?
├── VerifiedAt: DateTime?
├── RejectionReason: string? (max 500)
├── CreatedAt: DateTime
├── UpdatedAt: DateTime?
└── IsDeleted: bool (soft delete)
```

**API Endpoints:**
```
POST /api/v1/publishers/register
  Body: { companyName, website?, description?, contactEmail }
  Auth: Authenticated user without PublisherRole
  Returns: 201 Created with PublisherProfileDto

GET  /api/v1/publishers/profile
  Auth: PublisherRole
  Returns: PublisherProfileDto

PUT  /api/v1/publishers/profile
  Auth: PublisherRole
  Returns: 204 No Content

# Admin endpoints
GET  /api/v1/admin/publishers/pending
  Auth: AdminOnlyPolicy
  Returns: PagedResult<PublisherProfileDto>

POST /api/v1/admin/publishers/{id}/approve
  Auth: AdminOnlyPolicy
  Returns: 204 No Content (grants PublisherRole claim)

POST /api/v1/admin/publishers/{id}/reject
  Body: { reason }
  Auth: AdminOnlyPolicy
  Returns: 204 No Content
```

#### Phase 1B: Rulebook Analysis Pipeline

| ID | Requirement | Priority |
|----|-------------|----------|
| PB-010 | Publisher creates draft game in SharedGameCatalog | Must |
| PB-011 | Publisher uploads rulebook PDF (uses existing DocumentProcessing) | Must |
| PB-012 | System auto-generates RulebookAnalysis with quality metrics | Must |
| PB-013 | Analysis includes: clarity, completeness, consistency, predicted FAQs | Must |
| PB-014 | Publisher views analysis dashboard | Must |
| PB-015 | System auto-generates KB cards from rulebook | Must |
| PB-016 | System auto-creates Tutor agent for the game | Should |
| PB-017 | Publisher can upload revised rulebook versions | Must |
| PB-018 | System provides version comparison (v1 vs v2) | Should |

**Extended RulebookAnalysis (augment existing entity):**
```
RulebookAnalysis (extend existing)
├── ... existing fields ...
├── ClarityScore: decimal (0.0-10.0, fine-tuned model output)
├── CompletenessScore: decimal (0.0-10.0)
├── ConsistencyScore: decimal (0.0-10.0)
├── OverallScore: decimal (computed average)
├── AmbiguousRuleCount: int
├── AmbiguousRules: jsonb (array of { section, text, suggestion })
├── PredictedFaqCount: int
├── PredictedFaqs: jsonb (array of { question, answer, ruleReference })
├── ConflictCount: int
├── Conflicts: jsonb (array of { rule1, rule2, description })
├── VersionNumber: int (1, 2, 3... for version tracking)
├── PreviousVersionId: Guid? (FK to previous RulebookAnalysis)
├── AnalysisModelVersion: string (fine-tuned model identifier)
└── AnalysisDurationMs: int (processing time)
```

**Analysis Pipeline Flow:**
```
Publisher uploads PDF
  → DocumentProcessing: 3-stage extraction (Unstructured → SmolDocling → Docnet)
  → KnowledgeBase: Chunking + embedding + Qdrant indexing
  → RulebookAnalyzer (NEW service):
      1. Send extracted text to fine-tuned model
      2. Model returns structured analysis JSON:
         - Per-section clarity scores
         - Ambiguous rules with suggestions
         - Predicted FAQ questions with answers
         - Rule conflicts (contradictory statements)
         - Completeness gaps (missing setup/scoring/edge-cases)
      3. Store as RulebookAnalysis entity
      4. Auto-generate QuickQuestion entries from predicted FAQs
      5. Auto-create KB cards if not exist
  → Notification: SSE event to publisher dashboard
```

**API Endpoints:**
```
POST /api/v1/publishers/games/{gameId}/analyze
  Auth: PublisherRole + game owner
  Triggers: Async analysis pipeline
  Returns: 202 Accepted with analysisId

GET  /api/v1/publishers/games/{gameId}/analysis
  Auth: PublisherRole + game owner
  Returns: RulebookAnalysisDto (latest version)

GET  /api/v1/publishers/games/{gameId}/analysis/versions
  Auth: PublisherRole + game owner
  Returns: List<RulebookAnalysisSummaryDto>

GET  /api/v1/publishers/games/{gameId}/analysis/{versionNumber}
  Auth: PublisherRole + game owner
  Returns: RulebookAnalysisDto

GET  /api/v1/publishers/games/{gameId}/analysis/compare?v1={n}&v2={m}
  Auth: PublisherRole + game owner
  Returns: RulebookComparisonDto (diff between versions)
```

#### Phase 1C: Test Campaigns

| ID | Requirement | Priority |
|----|-------------|----------|
| PB-020 | Publisher creates test campaign for a game | Must |
| PB-021 | Campaign generates unique invite code | Must |
| PB-022 | Testers join via invite code (creates LiveGameSession) | Must |
| PB-023 | During play, testers can flag "rule confusion" moments | Must |
| PB-024 | Flags capture: rule section, game context (turn, phase), description | Must |
| PB-025 | Testers can rate rule clarity per section (1-5) | Should |
| PB-026 | Post-session: feedback report auto-generated | Must |
| PB-027 | Publisher views aggregate feedback across campaigns | Must |
| PB-028 | Publisher can export feedback report as PDF | Could |

**Data Model — TestCampaign:**
```
TestCampaign (new entity in SharedGameCatalog BC)
├── Id: Guid (PK)
├── PublisherId: Guid (FK → PublisherProfile, required)
├── SharedGameId: Guid (FK → SharedGame, required)
├── RulebookAnalysisId: Guid? (FK → RulebookAnalysis)
├── Title: string (required, max 200)
├── Description: string? (max 2000)
├── InviteCode: string (unique, 8-char alphanumeric)
├── MaxTesters: int (default 20, max 100)
├── Status: enum (Draft=0, Active=1, Completed=2, Archived=3)
├── CreatedAt: DateTime
├── ActivatedAt: DateTime?
├── CompletedAt: DateTime?
└── IsDeleted: bool

TestSessionFeedback (new entity)
├── Id: Guid (PK)
├── CampaignId: Guid (FK → TestCampaign, required)
├── SessionId: Guid (FK → LiveGameSession, required)
├── PlayerId: Guid (required)
├── FeedbackType: enum (RuleConfusion=0, RuleClarity=1, Suggestion=2, Bug=3)
├── RuleReference: string? (section/page reference, max 200)
├── Content: string (required, max 2000)
├── ClarityRating: int? (1-5)
├── GameContext: jsonb? ({ turnIndex, phaseIndex, phaseName })
├── CreatedAt: DateTime
└── IsDeleted: bool
```

**API Endpoints:**
```
POST /api/v1/publishers/campaigns
  Body: { sharedGameId, title, description?, maxTesters? }
  Auth: PublisherRole
  Returns: 201 Created with TestCampaignDto (includes inviteCode)

GET  /api/v1/publishers/campaigns
  Auth: PublisherRole
  Returns: List<TestCampaignDto>

GET  /api/v1/publishers/campaigns/{id}
  Auth: PublisherRole + campaign owner
  Returns: TestCampaignDetailDto (with session list + feedback summary)

POST /api/v1/publishers/campaigns/{id}/activate
  Auth: PublisherRole + campaign owner
  Returns: 204 No Content

POST /api/v1/publishers/campaigns/{id}/complete
  Auth: PublisherRole + campaign owner
  Returns: 204 No Content

GET  /api/v1/publishers/campaigns/{id}/report
  Auth: PublisherRole + campaign owner
  Returns: CampaignReportDto (aggregate feedback analysis)

# Tester endpoints
POST /api/v1/test-sessions/{sessionId}/feedback
  Body: { feedbackType, ruleReference?, content, clarityRating?, gameContext? }
  Auth: Session participant
  Returns: 201 Created

GET  /api/v1/test-campaigns/join/{inviteCode}
  Auth: Authenticated user
  Returns: TestCampaignJoinDto (game info, campaign details)
```

### Frontend — Publisher Dashboard

```
New Route Group: /publisher
├── /publisher/dashboard         (overview: games, campaigns, analytics)
├── /publisher/games             (draft games list)
├── /publisher/games/{id}        (game detail with analysis tabs)
├── /publisher/games/{id}/analysis (rulebook analysis results)
├── /publisher/games/{id}/versions (version comparison)
├── /publisher/campaigns         (campaign list)
├── /publisher/campaigns/{id}    (campaign detail + feedback)
├── /publisher/campaigns/{id}/report (aggregate feedback report)
└── /publisher/profile           (company profile editor)

Key Components:
├── RulebookAnalysisDashboard.tsx (scores, charts, ambiguous rules list)
├── RuleConfusionHeatmap.tsx (sections colored by confusion frequency)
├── VersionComparisonView.tsx (side-by-side clarity score changes)
├── TestCampaignCard.tsx (campaign status, tester count, session count)
├── FeedbackTimeline.tsx (chronological feedback from all sessions)
├── CampaignReportView.tsx (aggregate analysis, top issues, recommendations)
└── TesterFeedbackButton.tsx (in-session: "Flag Rule Confusion" FAB)
```

---

## Phase 2: KB Pipeline Self-Service

**Epic**: Publisher Knowledge Base Self-Service
**Priority**: P1 (parallel with Phase 1B)
**Estimated Issues**: ~12
**Dependencies**: Phase 1B (RulebookAnalysis)

### Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| KB-001 | Auto-generate KB cards when publisher uploads rulebook | Must |
| KB-002 | Auto-create Tutor agent linked to game's KB cards | Must |
| KB-003 | Publisher can review and edit auto-generated FAQs | Should |
| KB-004 | Publisher can approve/reject individual FAQ suggestions | Should |
| KB-005 | Auto-FAQ generation uses fine-tuned model + RAG context | Must |
| KB-006 | Version comparison shows KB card changes between versions | Could |

### Integration Architecture

```
                    Publisher uploads PDF
                           │
                    ┌──────▼──────┐
                    │ Document    │
                    │ Processing  │ (3-stage extraction)
                    └──────┬──────┘
                           │
               ┌───────────┼───────────┐
               │           │           │
        ┌──────▼──────┐  ┌▼────────┐  ┌▼──────────────┐
        │ Knowledge   │  │Rulebook │  │ Auto-FAQ      │
        │ Base        │  │Analyzer │  │ Generator     │
        │ (embeddings)│  │(fine-   │  │ (RAG + model) │
        └──────┬──────┘  │tuned)   │  └──────┬────────┘
               │         └────┬────┘         │
               │              │              │
        ┌──────▼──────┐  ┌───▼───────┐  ┌───▼──────────┐
        │ KB Cards    │  │ Rulebook  │  │ Quick        │
        │ (Qdrant)    │  │ Analysis  │  │ Questions    │
        └─────────────┘  └───────────┘  └──────────────┘
               │
        ┌──────▼──────┐
        │ Auto-create │
        │ Tutor Agent │
        └─────────────┘
```

---

## Phase 3: Real-Time Session Sync (Future)

**Epic**: LiveGameSession Real-Time Multiplayer
**Priority**: P2
**Estimated Issues**: ~10
**Dependencies**: SessionTracking Phase 3 (SSE infrastructure)

### Architecture Decision: SSE First, SignalR Later

**Phase 3A: SSE (MVP)**
- Extend existing SSE infrastructure (used for chat streaming)
- Server pushes: score updates, turn advances, player join/leave, photo uploads
- Client polls for state on reconnect (HTTP fallback)
- Sufficient for turn-based games (not real-time action games)

**Phase 3B: SignalR (v2)**
- Bidirectional communication for whiteboard sync, instant updates
- SignalR hub: `/hubs/live-session`
- Groups by SessionCode
- Fallback to SSE for read-only spectators

### Session Architecture Merge Plan

```
Current: 2 systems
├── LiveGameSession (GameManagement) — rich, full-featured
└── Session (SessionTracking) — lightweight scorekeeper

Future: 1 unified system
└── LiveGameSession (GameManagement) — absorbs SessionTracking features
    ├── "Quick" mode: score-only (replaces SessionTracking)
    └── "Full" mode: toolkit, photos, AI agent, whiteboard

Migration:
1. Add "SessionMode" enum (Quick, Full) to LiveGameSession
2. Map SessionTracking endpoints to LiveGameSession handlers
3. Deprecate SessionTracking endpoints (6-month sunset)
4. Remove SessionTracking BC
```

---

## Fine-Tuned Rulebook Analysis Model

### Model Specification

| Aspect | Detail |
|--------|--------|
| **Base model** | TBD (candidates: Llama 3.1 8B, Mistral 7B, Phi-3) |
| **Training data** | Board game rulebooks (public domain) + annotated clarity labels |
| **Output format** | Structured JSON with scores and suggestions |
| **Deployment** | Dedicated service container alongside embedding-service |
| **Inference** | < 30s for a typical 20-page rulebook |
| **Fine-tuning approach** | LoRA on instruction-tuned base |

### Training Data Requirements

```
Annotated dataset needed:
├── 500+ rulebooks (various complexity levels)
├── Per-section clarity labels (1-10 scale, 3 annotators)
├── Ambiguity annotations (specific passages + suggested rewrites)
├── Completeness checklist (setup, gameplay, scoring, variants, edge cases)
├── FAQ pairs (real player questions + rule-grounded answers)
└── Conflict annotations (pairs of contradictory rules)

Sources:
├── Public domain rulebooks (older games, out of copyright)
├── Publisher partnerships (permission to use for training)
├── BGG FAQ threads (question-answer mining)
└── Synthetic data (LLM-generated edge cases from existing rules)
```

### Analysis Output Schema

```json
{
  "overallScore": 7.8,
  "clarityScore": 7.2,
  "completenessScore": 8.5,
  "consistencyScore": 7.7,
  "sections": [
    {
      "title": "Setup",
      "pageRange": "3-5",
      "clarityScore": 8.1,
      "issues": []
    },
    {
      "title": "Turn Actions",
      "pageRange": "6-12",
      "clarityScore": 5.4,
      "issues": [
        {
          "type": "ambiguity",
          "text": "Players may trade resources with adjacent players",
          "problem": "\"Adjacent\" is undefined - does it mean physical seating or board position?",
          "suggestion": "Specify: \"Players may trade with any player whose piece occupies a connected territory\""
        }
      ]
    }
  ],
  "predictedFaqs": [
    {
      "question": "Can I trade with a player who is not next to me?",
      "answer": "Based on the rules, trading is limited to 'adjacent' players...",
      "ruleReference": "Section 3.2, page 7",
      "confidence": 0.85
    }
  ],
  "conflicts": [
    {
      "rule1": { "section": "Setup", "text": "Each player starts with 5 resources" },
      "rule2": { "section": "2-Player Variant", "text": "In a 2-player game, start with standard resources" },
      "issue": "\"Standard resources\" is ambiguous - does it mean 5 (base) or something different for 2-player?"
    }
  ],
  "completenessGaps": [
    "No rules for tie-breaking in final scoring",
    "Missing edge case: what happens when resource deck is exhausted"
  ]
}
```

---

## Cross-Phase Dependencies

```
Phase 0 (Photos)  ──────────────────────────────────────────► Standalone
     │
     │ enables test session evidence
     ▼
Phase 1A (Publisher Onboarding)  ────────────────────────────► Phase 1B
     │
Phase 1B (Rulebook Analysis)  ──► Phase 2 (KB Self-Service)
     │
Phase 1C (Test Campaigns)  ─────► Uses Phase 0 photos + Phase 1B analysis
     │
Phase 3 (Real-Time Sync)  ──────► Independent, enhances all sessions
```

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Session photo uploads/week | 100+ (month 3) | Analytics event tracking |
| Publisher registrations | 20+ (month 6) | Admin dashboard |
| Rulebook analyses completed | 50+ (month 6) | RulebookAnalysis count |
| Test campaigns created | 10+ (month 6) | TestCampaign count |
| Avg. rulebook score improvement (v1→v2) | +1.5 points | Version comparison |
| Session resume rate (paused→resumed) | > 60% | Session lifecycle tracking |
| Photo storage cost | < $50/month | S3 billing |

---

## Risk Register

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Fine-tuned model quality insufficient | Medium | High | Start with GPT-4/Claude analysis, fine-tune incrementally |
| Publishers upload copyrighted content | High | High | ToS, admin review gate, DMCA process |
| Photo storage costs spiral | Medium | Medium | 90-day retention, compression, thumbnail-only after 30 days |
| Low publisher adoption | Medium | Medium | Free tier, partner with indie publishers first |
| Session merge breaks existing features | Low | High | Feature flags, 6-month sunset, A/B testing |
| Real-time sync complexity | Medium | Medium | SSE first (simple), SignalR only if SSE insufficient |
