#!/bin/bash

# MeepleAI MVP - GitHub Issues Generation Script
# Creates issues for Sprint 1-5 based on product specification
# Prerequisites: gh CLI installed and authenticated

set -e

echo "🚀 Generating GitHub Issues for MeepleAI MVP (Sprint 1-5)"
echo "=========================================================="
echo ""

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ Error: GitHub CLI (gh) is not installed"
    echo "Install it from: https://cli.github.com/"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo "❌ Error: Not authenticated with GitHub CLI"
    echo "Run: gh auth login"
    exit 1
fi

echo "✅ GitHub CLI authenticated"
echo ""

# =============================================================================
# SPRINT 1: Authentication & Settings (2 weeks)
# =============================================================================

echo "📋 Creating Sprint 1 issues (Authentication & Settings)..."

gh issue create \
  --title "[SPRINT-1] OAuth Integration Complete" \
  --label "sprint-1,authentication,high-priority" \
  --milestone "MVP Sprint 1" \
  --body "## 🎯 Objective
Complete OAuth 2.0 integration for social login providers (Google, Discord, GitHub).

## 📋 Tasks
- [x] OAuth callback endpoints (AUTH-06 already implemented)
- [x] Token encryption with Data Protection API
- [ ] Add unit tests for OAuthService (target: 95% coverage)
- [ ] Add integration tests for OAuth flows
- [ ] Update frontend OAuth buttons UI
- [ ] Test Google OAuth flow end-to-end
- [ ] Test Discord OAuth flow end-to-end
- [ ] Test GitHub OAuth flow end-to-end
- [ ] Add E2E tests with Playwright

## 🧪 Testing Requirements
- Unit tests: 95%+ coverage for OAuthService
- Integration tests: All 3 providers (Google, Discord, GitHub)
- E2E tests: Complete login flow for each provider
- Security tests: CSRF protection validation

## ✅ Definition of Done
- All OAuth providers working in dev/staging
- 95%+ test coverage
- E2E tests passing in CI
- Documentation updated
- Code reviewed and approved

## 📚 References
- [OAuth Security Docs](../docs/security/oauth-security.md)
- [OAuth Setup Guide](../docs/guide/oauth-setup-guide.md)
- AUTH-06 implementation status"

gh issue create \
  --title "[SPRINT-1] 2FA/TOTP Management UI" \
  --label "sprint-1,authentication,medium-priority" \
  --milestone "MVP Sprint 1" \
  --body "## 🎯 Objective
Complete 2FA/TOTP frontend implementation for user enrollment and management.

## 📋 Tasks
- [x] Backend 2FA endpoints (AUTH-07 already implemented)
- [ ] Create /settings/security page with 2FA section
- [ ] Implement QR code display component
- [ ] Add backup codes display and regeneration UI
- [ ] Create 2FA verification flow during login
- [ ] Add temp session handling (5-min TTL)
- [ ] Implement rate limiting UI feedback (3 attempts/min)
- [ ] Add unit tests for 2FA components
- [ ] Add E2E tests for enrollment flow

## 🧪 Testing Requirements
- Unit tests: 90%+ for all 2FA components
- Integration tests: QR code generation, backup codes
- E2E tests: Complete enrollment + login with 2FA
- Security tests: Rate limiting, single-use codes

## ✅ Definition of Done
- Users can enroll in 2FA from settings
- QR code displayed correctly for TOTP apps
- Backup codes generated and downloadable
- Login flow works with 2FA enabled
- All tests passing with 90%+ coverage

## 📚 References
- AUTH-07 implementation
- [2FA Security Docs](../docs/security/2fa-security.md)"

gh issue create \
  --title "[SPRINT-1] Settings Pages - 4 Tabs Implementation" \
  --label "sprint-1,frontend,medium-priority" \
  --milestone "MVP Sprint 1" \
  --body "## 🎯 Objective
Implement all 4 settings tabs: Account, Preferences, Privacy, Advanced.

## 📋 Tasks

### Account Tab (/settings/account)
- [ ] Profile section (avatar upload, display name, username)
- [ ] Email & password change forms
- [ ] 2FA management integration
- [ ] OAuth account linking/unlinking UI
- [ ] Danger zone (delete account)

### Preferences Tab (/settings/preferences)
- [ ] Language selector (Italian, English)
- [ ] Theme selector (Light, Dark, Auto)
- [ ] Notification preferences (Email, Push)
- [ ] Chat preferences (model, response style)

### Privacy Tab (/settings/privacy)
- [ ] Profile visibility settings
- [ ] Activity sharing toggles
- [ ] Data retention configuration
- [ ] Clear old data button

### Advanced Tab (/settings/advanced)
- [ ] API key management CRUD
- [ ] Developer mode toggle
- [ ] Data export (GDPR compliance)
- [ ] Experimental features toggles

## 🧪 Testing Requirements
- Unit tests: 90%+ for all components
- Integration tests: Settings save/load
- E2E tests: Navigation between tabs
- Accessibility tests: WCAG 2.1 AA compliance

## ✅ Definition of Done
- All 4 tabs functional and styled
- Settings persist correctly
- Mobile-responsive design
- 90%+ test coverage
- Accessibility audit passing

## 📚 References
- [Complete Specification](../claudedocs/meepleai_complete_specification.md) (lines 253-404)
- [Wireframe 6](../claudedocs/meepleai_complete_specification.md#wireframe-6-settings-page)"

gh issue create \
  --title "[SPRINT-1] User Profile Management Service" \
  --label "sprint-1,backend,medium-priority" \
  --milestone "MVP Sprint 1" \
  --body "## 🎯 Objective
Implement UserProfileService for profile CRUD operations.

## 📋 Tasks
- [ ] Create UserProfileService.cs
- [ ] Implement UpdateProfile endpoint
- [ ] Implement ChangeEmail endpoint (with verification)
- [ ] Implement ChangePassword endpoint (with validation)
- [ ] Add avatar upload to blob storage
- [ ] Implement DeleteAccount endpoint (soft delete)
- [ ] Add audit logging for profile changes
- [ ] Write unit tests (target: 95% coverage)
- [ ] Write integration tests with Testcontainers

## 🧪 Testing Requirements
- Unit tests: 95%+ coverage
- Integration tests: Profile CRUD with real database
- Edge cases: Invalid email, weak password
- Security tests: Authorization checks

## ✅ Definition of Done
- All profile operations working
- Avatar upload/delete functional
- Email verification flow complete
- 95%+ test coverage
- API documentation updated

## 📚 References
- Existing UserManagementService (ADMIN-01)
- AUTH-06, AUTH-07 implementations"

gh issue create \
  --title "[SPRINT-1] Unit Test Suite - Authentication Module" \
  --label "sprint-1,testing,high-priority" \
  --milestone "MVP Sprint 1" \
  --body "## 🎯 Objective
Achieve 95%+ unit test coverage for all authentication-related services.

## 📋 Tasks
- [ ] OAuthService unit tests (23 existing + add missing)
- [ ] TotpService unit tests (11 existing + add missing)
- [ ] TempSessionService unit tests (new)
- [ ] EncryptionService unit tests (10 existing + add missing)
- [ ] SessionManagementService unit tests (39 existing + refactor)
- [ ] ApiKeyAuthenticationService unit tests (21 existing + refactor)
- [ ] Add parameterized tests for edge cases
- [ ] Add performance benchmarks for critical paths

## 🧪 Testing Strategy
- Use xUnit + Moq + FluentAssertions
- Follow AAA pattern (Arrange-Act-Assert)
- Test behavior, not implementation
- Cover edge cases: null, empty, boundary values
- Target execution time: <2 minutes for full suite

## ✅ Definition of Done
- 95%+ line coverage for auth module
- 90%+ branch coverage
- All tests passing in <2 minutes
- Zero flaky tests
- Code coverage report in CI

## 📚 References
- [Test Automation Strategy](../claudedocs/test_automation_strategy_2025.md)
- Existing test files in apps/api/tests/Api.Tests/"

echo "✅ Sprint 1 issues created"
echo ""

# =============================================================================
# SPRINT 2: Game Library Foundation (2 weeks)
# =============================================================================

echo "📋 Creating Sprint 2 issues (Game Library Foundation)..."

gh issue create \
  --title "[SPRINT-2] Game Entity & Database Schema" \
  --label "sprint-2,backend,database,high-priority" \
  --milestone "MVP Sprint 2" \
  --body "## 🎯 Objective
Implement complete game entity model and database schema as per specification.

## 📋 Tasks
- [ ] Create \`games\` table with all fields
- [ ] Create \`game_mechanics\` table
- [ ] Create \`game_mechanics_mapping\` table
- [ ] Create \`user_game_library\` table
- [ ] Add indexes for performance (slug, bgg_id, source)
- [ ] Create EF Core entities (Game, GameMechanic, etc.)
- [ ] Write migration scripts
- [ ] Add seed data (10+ popular games)
- [ ] Write unit tests for entities
- [ ] Write integration tests for repositories

## 📊 Database Schema
\`\`\`sql
-- See meepleai_complete_specification.md lines 848-884
CREATE TABLE games (
  id UUID PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  slug VARCHAR(255) UNIQUE NOT NULL,
  -- ... (full schema in spec)
);
\`\`\`

## 🧪 Testing Requirements
- Unit tests: Entity validation logic
- Integration tests: CRUD operations with Testcontainers
- Migration tests: Up/down migrations
- Seed data tests: Verify correct data loaded

## ✅ Definition of Done
- All tables created with correct schema
- Migrations applied successfully
- Seed data loaded (10+ games)
- 90%+ test coverage
- Performance indexes verified

## 📚 References
- [Specification](../claudedocs/meepleai_complete_specification.md) lines 848-1055"

gh issue create \
  --title "[SPRINT-2] GameService CRUD Implementation" \
  --label "sprint-2,backend,high-priority" \
  --milestone "MVP Sprint 2" \
  --body "## 🎯 Objective
Implement GameService with full CRUD operations and search functionality.

## 📋 Tasks
- [ ] Create IGameService interface
- [ ] Implement GameService.cs
  - [ ] CreateGameAsync (admin/editor only)
  - [ ] UpdateGameAsync (admin/editor only)
  - [ ] DeleteGameAsync (soft delete)
  - [ ] GetGameByIdAsync
  - [ ] GetGameBySlugAsync
  - [ ] SearchGamesAsync (filters: name, mechanics, players, duration)
  - [ ] GetGameMechanicsAsync
- [ ] Add authorization checks (role-based)
- [ ] Implement search with EF Core queries
- [ ] Add caching for popular games (Redis)
- [ ] Write unit tests (target: 95% coverage)
- [ ] Write integration tests with Testcontainers

## 🧪 Testing Requirements
- Unit tests: 95%+ coverage, mocked dependencies
- Integration tests: Real database + Redis cache
- Authorization tests: Verify role checks
- Performance tests: Search <500ms for 1000+ games

## ✅ Definition of Done
- All CRUD operations working
- Search filters functional
- Authorization enforced
- Caching implemented
- 95%+ test coverage

## 📚 References
- Existing RuleSpecService pattern
- [HybridCache docs](../docs/technic/perf-05-hybridcache-implementation.md)"

gh issue create \
  --title "[SPRINT-2] PDF Upload & Processing Pipeline" \
  --label "sprint-2,backend,pdf,high-priority" \
  --milestone "MVP Sprint 2" \
  --body "## 🎯 Objective
Enhance existing PDF processing with user upload support and Docling integration.

## 📋 Tasks
- [ ] Add user upload endpoint (POST /api/v1/games/:id/upload-pdf)
- [ ] Implement ownership validation (user must own physical copy)
- [ ] Add PDF validation (size limits, format check)
- [ ] Integrate Docling for improved text extraction
- [ ] Add table extraction with TableFormer
- [ ] Implement multi-column layout detection
- [ ] Add diagram recognition with Vision API
- [ ] Store PDFs in blob storage (Azure/AWS S3)
- [ ] Generate embeddings for vector search
- [ ] Add background job for processing (Hangfire)
- [ ] Write unit tests (target: 90% coverage)
- [ ] Write integration tests with real PDFs

## 🧪 Testing Requirements
- Unit tests: 90%+ coverage for services
- Integration tests: End-to-end PDF processing
- Performance tests: <30s for 20-page rulebook
- Edge case tests: Corrupt PDFs, large files (50MB+)

## ✅ Definition of Done
- Users can upload custom game PDFs
- Docling extraction >95% accuracy
- Tables extracted correctly
- Embeddings generated and stored
- Background processing functional
- 90%+ test coverage

## 📚 References
- [Roadmap Phase 1](../claudedocs/roadmap_meepleai_evolution_2025.md) lines 90-135
- Existing PdfStorageService, PdfTextExtractionService"

gh issue create \
  --title "[SPRINT-2] Game Search & Filter UI" \
  --label "sprint-2,frontend,high-priority" \
  --milestone "MVP Sprint 2" \
  --body "## 🎯 Objective
Implement game search and filter UI with autocomplete and faceted search.

## 📋 Tasks
- [ ] Create /games page with tabs (Ricerca, Mia Libreria)
- [ ] Implement search bar with autocomplete
- [ ] Add filter sidebar
  - [ ] Source filters (Official, User uploads, BGG)
  - [ ] Players filter (1-2, 3-4, 5+)
  - [ ] Duration filter (<30min, 30-60, 60-120, 120+)
  - [ ] Complexity slider (1-5)
  - [ ] Mechanics tag cloud
- [ ] Implement sort options (Favorites, Alphabetical, Rating, etc.)
- [ ] Create game card grid component
- [ ] Add favorite toggle functionality
- [ ] Implement pagination (infinite scroll or pages)
- [ ] Add \"Add to Library\" button
- [ ] Write unit tests for all components
- [ ] Write E2E tests with Playwright

## 🧪 Testing Requirements
- Unit tests: 90%+ for search/filter components
- Integration tests: API calls with MSW
- E2E tests: Complete search flow
- Accessibility tests: Keyboard navigation, screen readers

## ✅ Definition of Done
- Search autocomplete working
- All filters functional
- Sort options working
- Game cards displayed correctly
- Favorite/library actions working
- 90%+ test coverage
- Accessible (WCAG 2.1 AA)

## 📚 References
- [Wireframe 2](../claudedocs/meepleai_complete_specification.md#wireframe-2-games---ricerca-tab)
- [Site Map](../claudedocs/meepleai_complete_specification.md) lines 32-77"

gh issue create \
  --title "[SPRINT-2] Game Detail Page - 4 Tabs" \
  --label "sprint-2,frontend,medium-priority" \
  --milestone "MVP Sprint 2" \
  --body "## 🎯 Objective
Implement game detail page with 4 tabs: Regolamento, House Rules, Partite, Info Gioco.

## 📋 Tasks

### Header Section
- [ ] Game cover image + title
- [ ] Quick stats (players, time, complexity, BGG rating)
- [ ] Favorite toggle button
- [ ] Share button
- [ ] Actions: Start Chat, New Game Session

### Tab 1: Regolamento
- [ ] PDF viewer integration (react-pdf or similar)
- [ ] Page navigation controls
- [ ] Search in PDF functionality
- [ ] Download original button
- [ ] AI-generated setup guide display
- [ ] Quick reference cards section

### Tab 2: House Rules
- [ ] Rich text editor (TipTap integration)
- [ ] Community house rules list (future: upvote system)
- [ ] My house rules (private notes)
- [ ] Share with friends button (future)

### Tab 3: Partite
- [ ] Current session display (if active)
- [ ] Game history list
- [ ] Session detail modal
- [ ] New game session button

### Tab 4: Info Gioco
- [ ] Description (BGG synopsis)
- [ ] Publisher & designer info
- [ ] Mechanics & categories tags
- [ ] Related games recommendations
- [ ] BGG external link

## 🧪 Testing Requirements
- Unit tests: 90%+ for all tab components
- E2E tests: Navigation between tabs
- PDF viewer tests: Loading, navigation
- Accessibility tests: Tab navigation, focus management

## ✅ Definition of Done
- All 4 tabs functional
- PDF viewer working correctly
- House rules editor functional
- Session history displayed
- Mobile-responsive design
- 90%+ test coverage

## 📚 References
- [Wireframe 3](../claudedocs/meepleai_complete_specification.md#wireframe-3-game-detail-page)
- [Flow 3](../claudedocs/meepleai_complete_specification.md#flow-3-learning-a-new-game)"

echo "✅ Sprint 2 issues created"
echo ""

# =============================================================================
# SPRINT 3: Chat Enhancement (2 weeks)
# =============================================================================

echo "📋 Creating Sprint 3 issues (Chat Enhancement)..."

gh issue create \
  --title "[SPRINT-3] Chat Thread Management" \
  --label "sprint-3,backend,chat,high-priority" \
  --milestone "MVP Sprint 3" \
  --body "## 🎯 Objective
Implement chat thread management with database persistence.

## 📋 Tasks
- [ ] Create \`chat_threads\` table migration
- [ ] Update \`chat_logs\` table (add thread_id, game_id, session_id)
- [ ] Implement ChatThreadService
  - [ ] CreateThreadAsync (global or game-specific)
  - [ ] GetThreadsAsync (filter by type, game)
  - [ ] UpdateThreadTitleAsync
  - [ ] DeleteThreadAsync
  - [ ] GetThreadMessagesAsync (pagination)
- [ ] Add auto-title generation (from first message)
- [ ] Implement thread archiving
- [ ] Add message count tracking
- [ ] Write unit tests (target: 95% coverage)
- [ ] Write integration tests with Testcontainers

## 🧪 Testing Requirements
- Unit tests: 95%+ coverage
- Integration tests: Thread CRUD with real database
- Concurrency tests: Multiple threads per user
- Performance tests: Pagination <200ms for 1000+ messages

## ✅ Definition of Done
- Thread management fully functional
- Auto-title generation working
- Message pagination performant
- 95%+ test coverage
- API documentation updated

## 📚 References
- [Database Schema](../claudedocs/meepleai_complete_specification.md) lines 940-959
- Existing ChatService pattern"

gh issue create \
  --title "[SPRINT-3] Game-Specific Chat Context" \
  --label "sprint-3,backend,rag,high-priority" \
  --milestone "MVP Sprint 3" \
  --body "## 🎯 Objective
Enhance RAG system to support game-specific context switching.

## 📋 Tasks
- [ ] Add game_id filter to RagService.SearchAsync
- [ ] Implement context switching in chat flow
- [ ] Add game-specific prompt engineering
- [ ] Optimize vector search for single-game queries
- [ ] Add game metadata to RAG context
- [ ] Implement fallback to global context if no results
- [ ] Add context indicator in responses
- [ ] Write unit tests (target: 90% coverage)
- [ ] Write integration tests with Qdrant

## 🧪 Testing Requirements
- Unit tests: 90%+ coverage
- Integration tests: Context switching accuracy
- RAG accuracy tests: >95% for game-specific queries
- Performance tests: <2s response time

## ✅ Definition of Done
- Game context switching functional
- Accuracy >95% for game queries
- Response time <2s p95
- 90%+ test coverage
- Context indicator in UI

## 📚 References
- Existing RagService implementation
- [RAG Evaluation Docs](../docs/ai-06-rag-evaluation.md)"

gh issue create \
  --title "[SPRINT-3] Chat UI with Thread Sidebar" \
  --label "sprint-3,frontend,high-priority" \
  --milestone "MVP Sprint 3" \
  --body "## 🎯 Objective
Implement chat interface with thread management sidebar.

## 📋 Tasks

### Left Sidebar (25% width)
- [ ] New Chat button
- [ ] Filter dropdown (All, Game-specific, Global)
- [ ] Chat history list
  - [ ] Auto-generated titles
  - [ ] Game badge (if game-specific)
  - [ ] Last message preview
  - [ ] Timestamp
  - [ ] Context menu (rename, delete, export)

### Main Chat Area (75% width)
- [ ] Chat header
  - [ ] Editable title
  - [ ] Game context badge
  - [ ] Share button
  - [ ] Options menu
- [ ] Message thread
  - [ ] User messages (right-aligned, blue)
  - [ ] AI messages (left-aligned, gray)
  - [ ] PDF citations (clickable page numbers)
  - [ ] Diagram references (inline images)
  - [ ] Suggested follow-up questions
  - [ ] Message actions (feedback, copy, share)
- [ ] Input area
  - [ ] Multi-line text input (markdown support)
  - [ ] Attach image button (future: CV)
  - [ ] Context selector (Global / Game dropdown)
  - [ ] Send button

## 🧪 Testing Requirements
- Unit tests: 90%+ for all components
- Integration tests: WebSocket messages with MSW
- E2E tests: Complete chat flow
- Accessibility tests: Focus management, keyboard nav

## ✅ Definition of Done
- Thread sidebar functional
- Message history displayed
- Context switching working
- Real-time updates via WebSocket
- 90%+ test coverage
- Mobile-responsive

## 📚 References
- [Wireframe 4](../claudedocs/meepleai_complete_specification.md#wireframe-4-chat-interface)
- [Flow 3](../claudedocs/meepleai_complete_specification.md#flow-3-learning-a-new-game)"

gh issue create \
  --title "[SPRINT-3] PDF Citation Display Enhancement" \
  --label "sprint-3,frontend,medium-priority" \
  --milestone "MVP Sprint 3" \
  --body "## 🎯 Objective
Enhance AI responses with clickable PDF citations and inline diagrams.

## 📋 Tasks
- [ ] Create Citation component (page number badge)
- [ ] Implement click handler to open PDF at specific page
- [ ] Add diagram extraction from PDF
- [ ] Display diagrams inline in chat messages
- [ ] Add image zoom modal
- [ ] Implement citation highlighting in PDF viewer
- [ ] Add \"Show in context\" feature
- [ ] Write unit tests for citation components
- [ ] Write E2E tests for citation flow

## 🧪 Testing Requirements
- Unit tests: 90%+ for citation components
- E2E tests: Click citation → PDF opens at correct page
- Visual tests: Diagram display and zoom
- Accessibility tests: Alt text for diagrams

## ✅ Definition of Done
- Citations clickable and functional
- PDF opens at correct page
- Diagrams displayed inline
- Zoom modal working
- 90%+ test coverage

## 📚 References
- Existing citation system in RagService
- [Flow 3](../claudedocs/meepleai_complete_specification.md#flow-3-learning-a-new-game) lines 557-563"

gh issue create \
  --title "[SPRINT-3] Chat Export Functionality" \
  --label "sprint-3,backend,medium-priority" \
  --milestone "MVP Sprint 3" \
  --body "## 🎯 Objective
Implement chat export to PDF and Markdown formats.

## 📋 Tasks
- [ ] Create ChatExportService
- [ ] Implement ExportToPdfAsync
  - [ ] Format messages with timestamps
  - [ ] Include game context
  - [ ] Add citations with page numbers
  - [ ] Generate PDF with styling
- [ ] Implement ExportToMarkdownAsync
  - [ ] Format as markdown document
  - [ ] Include metadata (game, date, duration)
  - [ ] Preserve code blocks and formatting
- [ ] Add export endpoint (GET /api/v1/chat/:id/export?format=pdf|md)
- [ ] Implement background job for large exports
- [ ] Write unit tests (target: 90% coverage)
- [ ] Write integration tests

## 🧪 Testing Requirements
- Unit tests: 90%+ coverage
- Integration tests: Export formats verified
- Performance tests: <10s for 1000-message thread
- Edge cases: Empty threads, special characters

## ✅ Definition of Done
- PDF export functional with styling
- Markdown export preserves formatting
- Background job for large exports
- 90%+ test coverage

## 📚 References
- Existing ChatExportService pattern"

echo "✅ Sprint 3 issues created"
echo ""

# =============================================================================
# SPRINT 4: Game Sessions MVP (3 weeks)
# =============================================================================

echo "📋 Creating Sprint 4 issues (Game Sessions MVP)..."

gh issue create \
  --title "[SPRINT-4] Game Session Entity & Database" \
  --label "sprint-4,backend,database,high-priority" \
  --milestone "MVP Sprint 4" \
  --body "## 🎯 Objective
Implement game session entity model and database schema.

## 📋 Tasks
- [ ] Create \`game_sessions\` table
- [ ] Create \`session_players\` table
- [ ] Create \`session_moves\` table
- [ ] Add indexes for performance
- [ ] Create EF Core entities (GameSession, SessionPlayer, SessionMove)
- [ ] Implement state management (JSONB for game_state)
- [ ] Add session status transitions (setup → active → completed)
- [ ] Write migration scripts
- [ ] Write unit tests for entities
- [ ] Write integration tests with Testcontainers

## 📊 Database Schema
\`\`\`sql
-- See meepleai_complete_specification.md lines 896-939
CREATE TABLE game_sessions (
  id UUID PRIMARY KEY,
  game_id UUID REFERENCES games(id),
  -- ... (full schema in spec)
);
\`\`\`

## 🧪 Testing Requirements
- Unit tests: Entity validation, state transitions
- Integration tests: Session CRUD operations
- Concurrency tests: Multiple sessions per user
- State consistency tests: JSONB integrity

## ✅ Definition of Done
- All tables created with correct schema
- State transitions working
- Indexes optimized for queries
- 90%+ test coverage

## 📚 References
- [Specification](../claudedocs/meepleai_complete_specification.md) lines 896-939"

gh issue create \
  --title "[SPRINT-4] GameSessionService Implementation" \
  --label "sprint-4,backend,high-priority" \
  --milestone "MVP Sprint 4" \
  --body "## 🎯 Objective
Implement GameSessionService with full session lifecycle management.

## 📋 Tasks
- [ ] Create IGameSessionService interface
- [ ] Implement GameSessionService.cs
  - [ ] CreateSessionAsync (with player setup)
  - [ ] GetSessionByIdAsync
  - [ ] GetActiveSessionsAsync (by user)
  - [ ] GetSessionHistoryAsync (pagination)
  - [ ] UpdateGameStateAsync
  - [ ] RecordMoveAsync
  - [ ] EndSessionAsync (calculate winner, final scores)
  - [ ] PauseSessionAsync / ResumeSessionAsync
  - [ ] AbandonSessionAsync
- [ ] Add WebSocket support for real-time updates
- [ ] Implement session state validation
- [ ] Add turn management logic
- [ ] Write unit tests (target: 95% coverage)
- [ ] Write integration tests with Testcontainers

## 🧪 Testing Requirements
- Unit tests: 95%+ coverage
- Integration tests: Full session lifecycle
- WebSocket tests: Real-time state updates
- Concurrency tests: Multiple users, same session

## ✅ Definition of Done
- All session operations working
- WebSocket updates functional
- State validation enforced
- 95%+ test coverage

## 📚 References
- Existing session management patterns"

gh issue create \
  --title "[SPRINT-4] Session Setup Modal & UI" \
  --label "sprint-4,frontend,high-priority" \
  --milestone "MVP Sprint 4" \
  --body "## 🎯 Objective
Implement game session setup modal and configuration UI.

## 📋 Tasks
- [ ] Create session setup modal component
  - [ ] Number of human players selector
  - [ ] Add AI players section (difficulty, color)
  - [ ] Game variant selector (if applicable)
  - [ ] Starting player selector
  - [ ] Start game button
- [ ] Add session configuration form validation
- [ ] Implement player management (add/remove)
- [ ] Add AI difficulty selector (Easy, Medium, Hard)
- [ ] Implement color/faction assignment
- [ ] Add session creation API call
- [ ] Write unit tests for modal components
- [ ] Write E2E tests for setup flow

## 🧪 Testing Requirements
- Unit tests: 90%+ for setup components
- E2E tests: Complete session creation flow
- Validation tests: Invalid configurations
- Accessibility tests: Modal focus management

## ✅ Definition of Done
- Setup modal fully functional
- Validation working correctly
- API integration complete
- 90%+ test coverage
- Accessible (WCAG 2.1 AA)

## 📚 References
- [Flow 2](../claudedocs/meepleai_complete_specification.md#flow-2-play-a-complete-game-session) lines 450-473"

gh issue create \
  --title "[SPRINT-4] Active Session Management UI" \
  --label "sprint-4,frontend,medium-priority" \
  --milestone "MVP Sprint 4" \
  --body "## 🎯 Objective
Implement active session display and management UI.

## 📋 Tasks

### Session Display Components
- [ ] Create SessionCard component
  - [ ] Game name + cover
  - [ ] Players list (human + AI)
  - [ ] Current turn indicator
  - [ ] Duration timer
  - [ ] Continue game button
- [ ] Implement session list view
- [ ] Add empty state (\"No active sessions\")

### Session Detail View
- [ ] Game state visualization
- [ ] Turn tracker
- [ ] Players list with scores
- [ ] Move history log
- [ ] Game actions panel
- [ ] Chat with Game Master (sidebar)

### Real-time Updates
- [ ] WebSocket connection setup
- [ ] State synchronization
- [ ] Turn notifications
- [ ] Move validation feedback

## 🧪 Testing Requirements
- Unit tests: 90%+ for session components
- WebSocket tests: State updates with MSW
- E2E tests: Complete session flow
- Performance tests: <100ms state update latency

## ✅ Definition of Done
- Active sessions displayed correctly
- Real-time updates working
- Session detail view functional
- 90%+ test coverage

## 📚 References
- [Flow 2](../claudedocs/meepleai_complete_specification.md#flow-2-play-a-complete-game-session) lines 474-515
- [Wireframe 5](../claudedocs/meepleai_complete_specification.md#wireframe-5-agenti-page)"

gh issue create \
  --title "[SPRINT-4] Session History & Statistics" \
  --label "sprint-4,frontend,medium-priority" \
  --milestone "MVP Sprint 4" \
  --body "## 🎯 Objective
Implement session history display and statistics tracking.

## 📋 Tasks
- [ ] Create session history list component
- [ ] Add session filters (by game, date, result)
- [ ] Implement session detail modal
  - [ ] Complete move log
  - [ ] Chat transcript
  - [ ] Statistics (turns, duration per player)
  - [ ] Winner highlight
- [ ] Add export session button (JSON/PDF)
- [ ] Implement pagination for history
- [ ] Create statistics dashboard
  - [ ] Games played count
  - [ ] Win rate per game
  - [ ] Average session duration
  - [ ] Favorite games
- [ ] Write unit tests for components
- [ ] Write E2E tests for history view

## 🧪 Testing Requirements
- Unit tests: 90%+ for history components
- E2E tests: View history, open details
- Performance tests: Pagination <500ms

## ✅ Definition of Done
- History list displayed correctly
- Session details accessible
- Export functionality working
- Statistics dashboard complete
- 90%+ test coverage

## 📚 References
- [Specification](../claudedocs/meepleai_complete_specification.md) lines 107-127"

echo "✅ Sprint 4 issues created"
echo ""

# =============================================================================
# SPRINT 5: Agents Foundation (2 weeks)
# =============================================================================

echo "📋 Creating Sprint 5 issues (Agents Foundation)..."

gh issue create \
  --title "[SPRINT-5] AI Agents Entity & Configuration" \
  --label "sprint-5,backend,ai,high-priority" \
  --milestone "MVP Sprint 5" \
  --body "## 🎯 Objective
Implement AI agents entity model and configuration system.

## 📋 Tasks
- [ ] Create \`ai_agents\` table
- [ ] Create EF Core entity (AiAgent)
- [ ] Implement agent types (game_master, ai_player, workflow)
- [ ] Add agent configuration (JSONB for settings)
- [ ] Create agent activation/deactivation logic
- [ ] Implement game-specific agent assignments
- [ ] Write migration scripts
- [ ] Write unit tests for entities
- [ ] Write integration tests with Testcontainers

## 📊 Database Schema
\`\`\`sql
-- See meepleai_complete_specification.md lines 961-969
CREATE TABLE ai_agents (
  id UUID PRIMARY KEY,
  name VARCHAR(100) NOT NULL,
  agent_type VARCHAR(50) NOT NULL,
  game_id UUID REFERENCES games(id),
  configuration JSONB,
  is_active BOOLEAN DEFAULT true,
  created_at TIMESTAMP DEFAULT NOW()
);
\`\`\`

## 🧪 Testing Requirements
- Unit tests: Entity validation, config parsing
- Integration tests: Agent CRUD operations
- Configuration tests: JSON schema validation

## ✅ Definition of Done
- Agent table created
- Configuration system working
- Agent types defined
- 90%+ test coverage

## 📚 References
- [Specification](../claudedocs/meepleai_complete_specification.md) lines 961-969"

gh issue create \
  --title "[SPRINT-5] Game Master Agent Integration" \
  --label "sprint-5,backend,ai,high-priority" \
  --milestone "MVP Sprint 5" \
  --body "## 🎯 Objective
Integrate Game Master agent for rule arbitration during sessions.

## 📋 Tasks
- [ ] Create GameMasterService
- [ ] Implement rule validation endpoint
- [ ] Add move legality checking (if RuleSpec v2 available)
- [ ] Integrate with RagService for rule lookup
- [ ] Add ambiguity detection logic
- [ ] Implement contextual help responses
- [ ] Add session state awareness
- [ ] Write unit tests (target: 95% coverage)
- [ ] Write integration tests with real sessions

## 🧪 Testing Requirements
- Unit tests: 95%+ coverage
- Integration tests: Game Master + RAG + Session
- Accuracy tests: >90% rule validation correctness
- Performance tests: <1s response time

## ✅ Definition of Done
- Game Master functional in sessions
- Rule validation working
- Contextual help accurate
- 95%+ test coverage
- <1s response time

## 📚 References
- Existing RagService, SetupGuideService
- [Flow 2](../claudedocs/meepleai_complete_specification.md#flow-2-play-a-complete-game-session) lines 486-493"

gh issue create \
  --title "[SPRINT-5] Agent Selection UI" \
  --label "sprint-5,frontend,medium-priority" \
  --milestone "MVP Sprint 5" \
  --body "## 🎯 Objective
Implement agent selection and configuration UI.

## 📋 Tasks

### Agent Selection Page (/agents)
- [ ] Create agent type tabs (Game Masters, AI Players, Workflow)
- [ ] Implement game selector dropdown
- [ ] Add \"Start Chat\" button (redirects to /chat)
- [ ] Add \"Start Game Session\" button (opens setup modal)

### Game Masters Tab
- [ ] Display available Game Master agents
- [ ] Show game-specific configurations
- [ ] Add agent description cards

### AI Players Tab (Phase 2)
- [ ] Filter by game support
- [ ] Display AI player cards
  - [ ] Avatar + name
  - [ ] Difficulty level
  - [ ] Playstyle (aggressive, defensive, balanced)
  - [ ] Win rate %
- [ ] Add configure button
- [ ] Note: \"Available for games with full components definition\"

### Active Sessions Section
- [ ] Display current active sessions
- [ ] Show session cards
- [ ] Add \"Continue Game\" buttons

## 🧪 Testing Requirements
- Unit tests: 90%+ for agent components
- E2E tests: Agent selection → session creation
- Accessibility tests: Keyboard navigation

## ✅ Definition of Done
- Agent selection UI functional
- Game Master integration working
- Session cards displayed
- 90%+ test coverage

## 📚 References
- [Wireframe 5](../claudedocs/meepleai_complete_specification.md#wireframe-5-agenti-page)
- [Site Map](../claudedocs/meepleai_complete_specification.md) lines 199-252"

gh issue create \
  --title "[SPRINT-5] Move Validation (RuleSpec v2 Integration)" \
  --label "sprint-5,backend,ai,medium-priority" \
  --milestone "MVP Sprint 5" \
  --body "## 🎯 Objective
Implement basic move validation using RuleSpec v2 (if available).

## 📋 Tasks
- [ ] Check RuleSpec v2 availability for game
- [ ] Implement MoveValidationService
- [ ] Add precondition checking
- [ ] Add postcondition validation
- [ ] Integrate with Prolog inference engine (optional)
- [ ] Add contradiction detection
- [ ] Implement fallback to RAG if no formal rules
- [ ] Write unit tests (target: 90% coverage)
- [ ] Write integration tests with sessions

## 🧪 Testing Requirements
- Unit tests: 90%+ coverage
- Integration tests: Validate moves in real sessions
- Accuracy tests: >98% for formalized rules
- Fallback tests: RAG validation when no RuleSpec

## ✅ Definition of Done
- Move validation functional
- RuleSpec v2 integration working
- Fallback to RAG implemented
- 90%+ test coverage
- >98% accuracy for formalized rules

## 📚 References
- [Roadmap Phase 1 Sprint 3](../claudedocs/roadmap_meepleai_evolution_2025.md) lines 203-270
- Existing RuleSpecService"

gh issue create \
  --title "[SPRINT-5] Integration Test Suite - Full Stack" \
  --label "sprint-5,testing,high-priority" \
  --milestone "MVP Sprint 5" \
  --body "## 🎯 Objective
Complete integration test coverage for MVP features.

## 📋 Tasks

### Backend Integration Tests
- [ ] Authentication flow tests (login, OAuth, 2FA)
- [ ] Game library tests (CRUD, search, upload)
- [ ] Chat thread tests (create, messages, context)
- [ ] Session management tests (create, play, end)
- [ ] Agent integration tests (Game Master, validation)

### Frontend Integration Tests
- [ ] API integration tests with MSW
- [ ] WebSocket tests (sessions, chat)
- [ ] State management tests (Redux/Context)
- [ ] Form submission tests (all forms)

### E2E Test Scenarios
- [ ] Complete user journey: Register → Add game → Chat → Play
- [ ] OAuth login flow (all 3 providers)
- [ ] Game upload → PDF processing → RAG query
- [ ] Session creation → Game Master interaction → End session

## 🧪 Testing Strategy
- Testcontainers for backend (Postgres, Qdrant, Redis)
- MSW for frontend API mocking
- Playwright for E2E (Chrome, Firefox, Safari)
- Parallel execution: 4 shards
- Target execution time: <10 minutes total

## ✅ Definition of Done
- 85%+ integration test coverage
- 80%+ E2E critical path coverage
- All tests passing in CI
- <10 minute CI execution time
- Zero flaky tests

## 📚 References
- [Test Automation Strategy](../claudedocs/test_automation_strategy_2025.md)
- Existing integration test patterns"

echo "✅ Sprint 5 issues created"
echo ""

# =============================================================================
# CROSS-SPRINT: Testing & CI/CD
# =============================================================================

echo "📋 Creating cross-sprint testing & CI/CD issues..."

gh issue create \
  --title "[CI/CD] GitHub Actions - Test Automation Pipeline" \
  --label "ci-cd,testing,high-priority" \
  --milestone "MVP Sprint 1" \
  --body "## 🎯 Objective
Setup complete GitHub Actions pipeline with parallel execution and coverage gates.

## 📋 Tasks
- [ ] Create test-automation.yml workflow
- [ ] Setup backend unit tests job (parallel)
- [ ] Setup backend integration tests job (parallel, Testcontainers)
- [ ] Setup frontend unit tests job (parallel)
- [ ] Setup E2E tests job (sequential, matrix: 3 browsers)
- [ ] Setup coverage gate job (90% threshold)
- [ ] Add test report generation job
- [ ] Configure Codecov integration
- [ ] Setup test result caching
- [ ] Add Slack/Discord notifications
- [ ] Implement selective test execution (changed files)
- [ ] Add test sharding (4 shards for E2E)
- [ ] Optimize for <10 minute total execution

## 🧪 Pipeline Architecture
\`\`\`yaml
jobs:
  backend-unit: (5 min, parallel)
  backend-integration: (10 min, parallel, services: postgres, qdrant)
  frontend-unit: (5 min, parallel)
  e2e-tests: (15 min, sequential, matrix: [chromium, firefox, webkit])
  coverage-gate: (depends on all, enforces 90%)
  test-report: (generates summary)
\`\`\`

## ✅ Definition of Done
- Complete pipeline functional
- Parallel execution working
- Coverage gates enforced (90%)
- Test reports generated
- <10 minute execution time (with parallel)
- Codecov integration complete

## 📚 References
- [Test Automation Strategy](../claudedocs/test_automation_strategy_2025.md)
- [CI/CD Best Practices Research](research findings)"

gh issue create \
  --title "[CI/CD] Coverage Reporting & Gates" \
  --label "ci-cd,testing,medium-priority" \
  --milestone "MVP Sprint 1" \
  --body "## 🎯 Objective
Setup Codecov integration with coverage gates and PR comments.

## 📋 Tasks
- [ ] Create .codecov.yml configuration
- [ ] Setup coverage thresholds (90% project, 80% patch)
- [ ] Configure flags for different test suites
  - [ ] backend-unit
  - [ ] backend-integration
  - [ ] frontend-unit
  - [ ] e2e-tests
- [ ] Add PR comment bot for coverage reports
- [ ] Setup coverage trend tracking
- [ ] Add coverage badges to README
- [ ] Configure carryforward for flaky CI
- [ ] Setup Codecov GitHub Action

## 📊 Coverage Targets
- Project: 90% minimum
- Patch: 80% minimum (new code)
- Precision: 2 decimal places
- Flags: Separate tracking for backend/frontend

## ✅ Definition of Done
- Codecov integrated
- Coverage gates enforced
- PR comments working
- Badges in README
- Trend tracking functional

## 📚 References
- [Test Automation Strategy](../claudedocs/test_automation_strategy_2025.md)
- Codecov documentation"

gh issue create \
  --title "[TESTING] Performance Test Suite" \
  --label "testing,performance,medium-priority" \
  --milestone "MVP Sprint 3" \
  --body "## 🎯 Objective
Implement performance test suite for critical API endpoints.

## 📋 Tasks
- [ ] Setup k6 for load testing
- [ ] Create performance test scenarios
  - [ ] RAG search: <2s p95, 1000 req/s
  - [ ] Chat message: <1s p95, 500 req/s
  - [ ] Game search: <500ms p95, 2000 req/s
  - [ ] Session update: <100ms p95, 1000 req/s
- [ ] Add database stress tests (concurrent queries)
- [ ] Add Redis cache hit rate tests
- [ ] Implement WebSocket load tests (1000+ concurrent)
- [ ] Setup performance benchmarking CI job
- [ ] Add performance regression detection
- [ ] Generate performance reports

## 🎯 Performance Targets
- RAG search: <2s p95
- API responses: <500ms p95
- WebSocket latency: <100ms
- Concurrent users: 1000+
- Database queries: <100ms p95

## ✅ Definition of Done
- Performance test suite complete
- All targets met in staging
- CI job running nightly
- Regression detection working
- Reports generated and archived

## 📚 References
- [Performance Optimization Docs](../docs/technic/performance-optimization-summary.md)"

echo "✅ CI/CD and testing issues created"
echo ""

echo "=========================================="
echo "✅ All GitHub issues created successfully!"
echo "=========================================="
echo ""
echo "📊 Summary:"
echo "  Sprint 1 (Auth & Settings): 5 issues"
echo "  Sprint 2 (Game Library): 5 issues"
echo "  Sprint 3 (Chat Enhancement): 5 issues"
echo "  Sprint 4 (Game Sessions): 5 issues"
echo "  Sprint 5 (Agents): 5 issues"
echo "  CI/CD & Testing: 3 issues"
echo "  ──────────────────────────────"
echo "  TOTAL: 28 issues"
echo ""
echo "🔗 View issues: gh issue list --milestone \"MVP Sprint 1\""
echo "🔗 View all milestones: gh issue list --milestone"
echo ""
echo "Next steps:"
echo "  1. Review issues on GitHub"
echo "  2. Assign team members"
echo "  3. Setup project board"
echo "  4. Start Sprint 1!"
