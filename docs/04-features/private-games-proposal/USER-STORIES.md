# User Stories: Private Games & Catalog Proposal

## Epic: Private Game Management

### US-001: Search BGG as Regular User
**As a** registered user
**I want to** search for games on BoardGameGeek
**So that** I can find games not yet in the MeepleAI catalog

**Acceptance Criteria:**
- [ ] Search endpoint accessible to all authenticated users
- [ ] Results show game title, year, thumbnail, player count
- [ ] Results indicate if game already exists in SharedGames
- [ ] Rate limited to prevent abuse (10 req/min)

**Technical Notes:**
- New endpoint: `GET /api/v1/bgg/search`
- Reuse existing `IBggApiService`

---

### US-002: Add BGG Game as Private
**As a** user who found a game on BGG
**I want to** add it to my library even if it's not in the shared catalog
**So that** I can track my collection completely

**Acceptance Criteria:**
- [ ] Can add any BGG game to personal library
- [ ] Game metadata fetched automatically from BGG
- [ ] Entry appears in "My Library" with full details
- [ ] Counts toward library quota
- [ ] If game already in SharedGames, uses shared version automatically

**Technical Notes:**
- `AddPrivateGameToLibraryCommand` with BggId
- Auto-redirect logic to SharedGame

---

### US-003: Add Manual Private Game
**As a** user with a game not on BGG
**I want to** manually add it to my library
**So that** I can track indie games, prototypes, or custom games

**Acceptance Criteria:**
- [ ] Form with required fields: Name, Min Players, Max Players
- [ ] Optional fields: Year, Playing Time, Description, Image URL
- [ ] Entry appears in library with "Manual" source indicator
- [ ] Can upload custom image

**Technical Notes:**
- Same command, no BggId, validates required fields

---

### US-004: Upload PDF for Private Game
**As a** user with a private game
**I want to** upload the rulebook PDF
**So that** I can use AI chat for rules questions

**Acceptance Criteria:**
- [ ] PDF upload works same as SharedGame flow
- [ ] PDF linked to UserLibraryEntry (via existing PdfDocument)
- [ ] AI chat available after processing
- [ ] Storage counts toward user quota

**Technical Notes:**
- Extend existing PDF upload to support private games
- Link to UserLibraryEntry instead of SharedGame

---

### US-005: AI Chat for Private Game
**As a** user with a private game and uploaded PDF
**I want to** use AI chat to ask rules questions
**So that** I get the same experience as shared catalog games

**Acceptance Criteria:**
- [ ] Chat works identically to SharedGame chat
- [ ] RAG uses user's uploaded PDF
- [ ] Chat history persisted
- [ ] Works with multiple PDFs if uploaded

**Technical Notes:**
- Extend chat context resolution to check UserLibraryEntry

---

## Epic: Catalog Proposal System

### US-006: Propose Private Game to Catalog
**As a** user with a private game
**I want to** propose it for addition to the shared catalog
**So that** other users can benefit from it

**Acceptance Criteria:**
- [ ] "Propose to Catalog" button on private game detail
- [ ] Can add notes explaining why game should be added
- [ ] Can attach uploaded PDFs to proposal
- [ ] Confirmation before submission
- [ ] Cannot propose same game twice (while pending)

**Technical Notes:**
- `ProposePrivateGameToCatalogCommand`
- Creates ShareRequest with `ContributionType.NewGameProposal`

---

### US-007: Track Proposal Status
**As a** user who proposed a game
**I want to** see the status of my proposal
**So that** I know if it's being reviewed

**Acceptance Criteria:**
- [ ] Status visible: Pending, In Review, Approved, Rejected, Changes Requested
- [ ] Timestamp of last status change
- [ ] Reviewer notes visible (if any)
- [ ] Link to resulting SharedGame (if approved)

**Technical Notes:**
- Reuse existing ShareRequest status tracking

---

### US-008: Receive Proposal Notifications
**As a** user who proposed a game
**I want to** receive notifications about status changes
**So that** I don't need to constantly check

**Acceptance Criteria:**
- [ ] Notification when proposal submitted (confirmation)
- [ ] Notification when admin starts review
- [ ] Notification when approved (with link to new SharedGame)
- [ ] Notification when rejected (with reason)
- [ ] Notification when changes requested (with details)

**Technical Notes:**
- New notification types in existing system

---

### US-009: Respond to Change Requests
**As a** user whose proposal needs changes
**I want to** update my proposal with requested changes
**So that** it can be re-reviewed

**Acceptance Criteria:**
- [ ] Can edit notes
- [ ] Can add/remove attached documents
- [ ] Can update game details (for manual games)
- [ ] Resubmit triggers new review cycle

**Technical Notes:**
- Extend existing ShareRequest "Changes Requested" flow

---

## Epic: Admin Review Workflow

### US-010: Review Game Proposals (Admin)
**As an** admin
**I want to** review proposed games
**So that** I can approve quality additions to the catalog

**Acceptance Criteria:**
- [ ] See all pending NewGameProposal requests
- [ ] View private game details and attached documents
- [ ] Approve → Creates SharedGame from private data
- [ ] Reject → Requires reason
- [ ] Request Changes → Requires details

**Technical Notes:**
- Extend existing ShareRequest admin review
- New handler to create SharedGame from proposal

---

### US-011: Merge BGG Data on Approval (Admin)
**As an** admin approving a BGG-sourced proposal
**I want to** fetch fresh BGG data
**So that** the SharedGame has complete metadata

**Acceptance Criteria:**
- [ ] Can fetch latest BGG data during approval
- [ ] Can choose to use proposer's data or fresh BGG data
- [ ] Designers, publishers, categories auto-created if needed
- [ ] Preview before final approval

**Technical Notes:**
- Reuse `ImportGameFromBggCommandHandler` logic

---

## Story Map

```
                    ┌─────────────────────────────────────────┐
                    │           USER JOURNEY                   │
                    └─────────────────────────────────────────┘
                                      │
        ┌─────────────────────────────┼─────────────────────────────┐
        │                             │                             │
        ▼                             ▼                             ▼
┌───────────────┐           ┌───────────────┐           ┌───────────────┐
│  DISCOVERY    │           │   LIBRARY     │           │   PROPOSAL    │
│               │           │   MGMT        │           │   FLOW        │
├───────────────┤           ├───────────────┤           ├───────────────┤
│ US-001        │           │ US-002        │           │ US-006        │
│ BGG Search    │──────────►│ Add BGG Game  │──────────►│ Propose Game  │
│               │           │               │           │               │
│               │           │ US-003        │           │ US-007        │
│               │           │ Add Manual    │           │ Track Status  │
│               │           │               │           │               │
│               │           │ US-004        │           │ US-008        │
│               │           │ Upload PDF    │           │ Notifications │
│               │           │               │           │               │
│               │           │ US-005        │           │ US-009        │
│               │           │ AI Chat       │           │ Respond       │
└───────────────┘           └───────────────┘           └───────────────┘
                                                                │
                                                                │
                                                                ▼
                                                    ┌───────────────┐
                                                    │  ADMIN        │
                                                    │  REVIEW       │
                                                    ├───────────────┤
                                                    │ US-010        │
                                                    │ Review        │
                                                    │               │
                                                    │ US-011        │
                                                    │ Merge BGG     │
                                                    └───────────────┘
```

## Priority Matrix

| Story | Priority | Effort | Dependencies |
|-------|----------|--------|--------------|
| US-001 | P0 | S | None |
| US-002 | P0 | M | US-001 |
| US-003 | P0 | S | None |
| US-004 | P1 | M | US-002/003 |
| US-005 | P1 | L | US-004 |
| US-006 | P1 | M | US-002/003 |
| US-007 | P1 | S | US-006 |
| US-008 | P2 | M | US-006 |
| US-009 | P2 | S | US-008 |
| US-010 | P1 | M | US-006 |
| US-011 | P2 | M | US-010 |

**Legend:**
- **P0**: MVP - Must have for initial release
- **P1**: High - Important for complete experience
- **P2**: Medium - Nice to have, can be added later
- **S/M/L**: Small/Medium/Large effort estimate
