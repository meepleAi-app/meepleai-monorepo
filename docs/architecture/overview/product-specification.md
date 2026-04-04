# MeepleAI Product Specification

**AI board game assistant: RAG, multi-agent, living docs**

---

## Executive Summary

| Aspect | Detail |
|--------|--------|
| **Product** | MeepleAI - AI-Powered Board Game Assistant & Game Master |
| **Target** | Casual players (20-65, nerd demographic) + hardcore tier |
| **Business Model** | Freemium with premium features |
| **Primary Use** | Learn rules, arbitrate disputes, manage sessions |
| **Platform** | Web PWA (mobile-optimized, offline nice-to-have) |
| **MVP Timeline** | Q1-Q2 2025 |

---

## Site Map

```
MeepleAI
├── 🏠 HOME (/)
│   ├── Welcome banner (first visit, collapsible)
│   ├── Recent activity feed, Quick actions
│   └── Quick tutorial (interactive, skippable)
│
├── 🎮 GIOCHI (/games)
│   ├── [Ricerca & Catalogo] Tab
│   │   ├── Search (autocomplete: name, publisher, tags)
│   │   ├── Filters: Source, Complexity (1-5), Players, Duration, Mechanics
│   │   ├── Sort: Favorites, Alphabetical, Rating, Users, Date
│   │   ├── Game cards (cover, info, favorite toggle, add to library)
│   │   └── [➕ Add New Game] → Upload PDF modal
│   ├── [Mia Libreria] Tab
│   │   └── Filter: All / Favorites / Custom uploads
│   └── GAME DETAIL (/games/:gameId)
│       ├── Header: Cover, stats, favorite, actions (Start Chat, New Session)
│       ├── [📖 Regolamento] Tab: PDF viewer, AI setup guide, quick reference
│       ├── [🏠 House Rules] Tab: Community rules (upvote), private notes
│       ├── [🎲 Partite] Tab: Current session, game history, new session modal
│       └── [ℹ️ Info Gioco] Tab: Description, mechanics, designer, BGG link
│
├── 💬 CHAT (/chat)
│   ├── Sidebar (25%): [+ New Chat], filter, history list
│   ├── Main (75%): Header, message thread, input area
│   │   ├── Messages: User (right, blue), AI (left, gray)
│   │   ├── Citations: PDF pages (clickable), diagrams, suggested follow-ups
│   │   └── Input: Multi-line, attach image, context selector, voice (future)
│   └── New Chat Modal: Type (Global|Game-specific), starting prompt
│
├── 🤖 AGENTI (/agents)
│   ├── Agent Type Tabs
│   │   ├── [🎭 Game Masters]: Game selector, Start Chat, Start Session
│   │   ├── [🤖 AI Players]: Filter by game, difficulty, playstyle, win rate
│   │   └── [⚙️ Workflow]: Templates (email, calendar, stats, notifications), n8n editor
│   ├── Active Sessions: Current games, players, turn, duration
│   └── History & Stats: Recent sessions, agent performance (future)
│
├── ⚙️ IMPOSTAZIONI (/settings)
│   ├── [👤 Account]: Profile, email/password, 2FA, OAuth, delete account
│   ├── [🎨 Preferences]: Language (IT/EN), theme (light/dark/auto), notifications, chat prefs
│   ├── [🔒 Privacy]: Visibility, activity sharing, data retention
│   └── [🛠️ Advanced]: API keys, developer mode, data export (GDPR), experimental
│
└── 🔑 AUTH
    ├── /register, /login, /forgot-password, /reset-password/:token
    ├── /verify-email/:token (disabled in dev)
    ├── /2fa/setup, /2fa/verify
    └── OAuth: Google, Discord, GitHub
```

---

## User Flows

### Flow 1: New User Onboarding
```
Visit site → [Register] or [OAuth] → Authenticated → HOME
→ Welcome banner → [Start Tutorial] or [Skip]
→ Tutorial (5 steps): Add game → Search → Start chat → Ask rules → Start session
→ Normal HOME view (tutorial re-triggerable from help)
```

### Flow 2: Play Complete Game
```
/games → Search "Catan" → Click card → /games/catan
→ [Regolamento] Tab → View PDF, learn rules
→ [🎲 New Game Session] → Setup modal (players, AI, variant)
→ Game Session Screen: Left (board state), Center (actions), Right (chat with GM)
→ Turn sequence: Player action → AI validates → Update state → Repeat
→ End game → Victory modal → Session saved to history
```

### Flow 3: Learning New Game
```
/games → Search "Wingspan" → /games/wingspan
→ [Info Gioco]: Read description, mechanics
→ [Regolamento]: PDF viewer, AI setup guide, quick reference cards
→ [💬 Start Chat]: Ask questions while learning
→ [🎲 Start Practice Game]: Guided tutorial mode
→ Complete first round → Ready to play!
```

### Flow 4: Upload Custom Game
```
/games → [➕ Add New Game] → Upload modal
→ Drop PDF → Auto-detect name/metadata → Ownership confirmation
→ [Upload & Process] → Progress (upload, extract, embed, index)
→ Success → Game in "Mia Libreria" (private, custom badge)
→ Full features: Chat, sessions, house rules
```

---

## Feature Matrix

### MVP (Q1-Q2 2025) ✅ Must Have

| Category | Features |
|----------|----------|
| **Authentication** | Email/password • OAuth (Google/Discord/GitHub) • 2FA/TOTP • Password reset |
| **Game Library** | Admin/editor uploads • User custom PDFs (private) • Search/filter • Favorites • 4-tab detail pages |
| **PDF Processing** | Viewer • Text extraction (Docling) • Table recognition • Embeddings • Vector search (Qdrant) |
| **Chat RAG** | Global chat • Game-specific • Citations (page numbers) • Cross-session memory • Export |
| **Agents** | Game Master (rule arbitration) • Basic move validation (RuleSpec v2) |
| **Game Sessions** | Create (setup modal) • Track current • History • Basic state management |
| **Settings** | Profile • Email/password/2FA • OAuth linking • Preferences (lang, theme, notifications) • Privacy • API keys • GDPR export |
| **Infrastructure** | ASP.NET Core API • Next.js PWA • PostgreSQL + Qdrant + Redis • Docker Compose • Monitoring |

### V2 (Q3 2025) 🔵 High Priority

| Category | Features |
|----------|----------|
| **BGG Integration** | Search catalog • Import metadata • Link rulebook downloads • Sync ratings |
| **AI Players** | Opponent for supported games • Difficulty levels • Playstyle config • Win rate tracking |
| **Visual Game State** | Board visualization (2D) • Piece placement UI • Move highlighting • Drag-and-drop |
| **Enhanced Chat** | Image attachment (board photos) • Diagram extraction • Multi-LLM consensus • Voice input |
| **House Rules** | Community rules (upvote/downvote) • Share with friends • Apply to sessions |
| **Workflow Agents** | n8n templates • Email reminders • Calendar • Discord/Slack notifications |
| **Italian Optimization** | Italian embeddings • Terminology glossary (500+ terms) • Italian catalog (50+ titles) |

### V3 (Q4 2025) 🟣 Nice to Have

| Category | Features |
|----------|----------|
| **Tournament** | Swiss/Round Robin • Brackets • Leaderboards • Automated pairings |
| **Social** | Friends system • Invites • Activity feed • Public profiles |
| **Multiplayer** | Online sessions • Real-time board sync (WebSockets) • Turn notifications • Spectator mode |
| **Computer Vision** | Board photo recognition • Piece detection • State sync • Move suggestions |
| **Mobile App** | React Native iOS/Android • Offline mode • Camera integration • Push notifications |
| **Gamification** | Achievements • Badges & titles • XP & leveling • Weekly challenges |

### Future (2026+) 🔮 Vision

| Category | Features |
|----------|----------|
| **Advanced AI** | Adaptive AI (learns playstyle) • Multi-agent coordination • Strategic coaching |
| **Content Creation** | AI-generated variants • Procedural scenarios • Custom rule generation |
| **Publisher Tools** | B2B dashboard • Analytics • Playtest coordination • Errata distribution |
| **Marketplace** | Premium AI models (subscription) • Expert AI players (DLC) • Custom agent marketplace |

---

## Database Schema (Key Entities)

### Games & Library

```sql
games: id, name, slug, publisher, designer, year, bgg_id, bgg_rating, min_players, max_players,
       duration_min, duration_max, complexity(1-5), description, cover_image_url,
       source(official|user_upload|bgg_import), uploaded_by_user_id, is_public,
       supports_ai_players, has_components_definition

game_mechanics: id, name, description

game_mechanics_mapping: game_id, mechanic_id (junction)

user_game_library: id, user_id, game_id, is_favorite, added_at (unique user+game)
```

### Sessions & Tracking

```sql
game_sessions: id, game_id, created_by_user_id, session_type(practice|competitive|tutorial),
               variant, status(setup|active|paused|completed|abandoned), current_turn,
               current_player_index, game_state(JSONB), winner_user_id, started_at, ended_at

session_players: id, session_id, user_id(NULL if AI), player_type(human|ai_easy|ai_medium|ai_hard),
                 player_name, player_index, color, final_score, is_winner

session_moves: id, session_id, player_id, move_number, move_type, move_data(JSONB),
               state_before(JSONB), state_after(JSONB), is_valid, validation_notes
```

### Chat & Agents

```sql
chat_threads: id, user_id, title(auto-gen or user-edited), game_id, session_id,
              chat_type(global|game_specific|session), message_count, last_message_at

chat_logs: ... + thread_id(FK)

ai_agents: id, name, agent_type(game_master|ai_player|workflow), game_id, configuration(JSONB), is_active

workflow_templates: id, name, description, category(notification|scheduling|reporting),
                    n8n_workflow_json(JSONB), is_public, created_by_user_id

user_workflows: id, user_id, template_id, name, configuration(JSONB), is_active
```

### House Rules & Social

```sql
house_rules: id, game_id, created_by_user_id, title, description, is_public, upvotes

house_rule_votes: id, house_rule_id, user_id, vote(-1|1) (unique rule+user)

user_relationships: id, user_id, friend_id, status(pending|accepted|blocked) (unique, no self)

activity_feed: id, user_id, activity_type(game_played|game_added|achievement_unlocked),
               activity_data(JSONB), is_public
```

**Indexes**: 15+ performance indexes (see full spec lines 1036-1055)

---

## Wireframes (Layout Templates)

### Standard Page Layout
```
┌─────────────────────────────────────────────────┐
│ HEADER (60px, sticky)                           │
│ Logo | Global Search | User Avatar ▼ | Notify  │
├────────┬────────────────────────────────────────┤
│ SIDEBAR│ MAIN CONTENT                           │
│ 240px  │ (flex, responsive)                     │
│        │                                        │
│ 🏠 Home │                                        │
│ 🎮 Giochi│                                       │
│ 💬 Chat │                                        │
│ 🤖 Agenti│                                       │
│ ⚙️ Settings│                                     │
│ ─────── │                                        │
│ 🚪 Logout│                                       │
│ ❓ Help │                                        │
└────────┴────────────────────────────────────────┘
```

### Home Page Components
- Welcome banner (first visit, expandable "Come funziona")
- Activity stats: Games in library, Active sessions, Chats, Hours played
- Recent activity feed (time-sorted)
- Quick actions: Browse Games, New Chat, Start Game
- Continue active session (if any)

### Games Page (Ricerca Tab)
- Search bar (autocomplete)
- Left sidebar: Filters (source, players, duration, complexity, mechanics)
- Main: Sort dropdown + Game card grid (cover, title, stats, favorite toggle, add to library, user count)

### Chat Interface
- Left (25%): Chat list (new button, filter, history with game badges)
- Right (75%): Header (title, context, share, export) + Message thread + Input (multi-line, attach, context selector)
- Messages: User (right, blue), AI (left, gray) with citations, diagrams, follow-ups

### Agents Page
- Tabs: Game Masters (game selector) | AI Players (difficulty, playstyle) | Workflow (n8n templates)
- Active sessions section
- Recent history + agent performance stats (future)

### Settings Page
- Sidebar nav: Account, Preferences, Privacy, Advanced
- Account: Profile, email/password, 2FA, OAuth, danger zone
- Preferences: Language, theme, notifications, chat prefs
- Privacy: Visibility, activity sharing, data retention
- Advanced: API keys, developer mode, GDPR export, experimental features

---

## Data Model (Per User)

```
User (avg):
├── Games in library: 3
│   ├── PDFs: 3 × 2.5 = 7.5 files
│   └── Storage: 37.5MB
├── Personal PDFs: 10% users
│   ├── Count: 3 PDFs
│   └── Storage: 15MB
├── Vector chunks: 156 chunks
│   └── Qdrant: 390KB
└── Total: 39MB storage + 390KB vectors
```

```
PDF (5MB avg):
├── Text: ~1MB raw
├── Chunks: 20 @ 250KB each
├── Embeddings: 384-dim vectors
└── Qdrant: 20 × 2.5KB = 50KB per PDF
```

---

## User Flows Summary

| Flow | Key Steps | Outcome |
|------|-----------|---------|
| **Onboarding** | Visit → Register/OAuth → Welcome → Tutorial (5 steps) | First game in library |
| **Play Game** | Search → Game detail → Setup modal → Session screen (board + actions + chat) → End game → History | Completed session saved |
| **Learn Game** | Search → Info tab → Regolamento tab (PDF + AI guide) → Chat with questions → Practice game (tutorial mode) | Ready to play |
| **Upload Custom** | Add Game button → Upload PDF → Auto-detect metadata → Ownership confirm → Process → Success | Private game in library |
| **BGG Import** | Search (not found) → Search BGG → Import modal → Add to catalog → Upload rulebook prompt | Game in catalog (partial data) |

---

## Development Roadmap

### Sprint Plan (MVP)

| Sprint | Duration | Focus |
|--------|----------|-------|
| Sprint 1 | 2 weeks | Authentication & Settings (OAuth, 2FA, 4 settings tabs, profile) |
| Sprint 2 | 2 weeks | Game Library Foundation (entity CRUD, PDF upload/Docling, search/filter UI) |
| Sprint 3 | 2 weeks | Chat Enhancement (thread management, game context, citation display) |
| Sprint 4 | 3 weeks | Game Sessions MVP (creation, setup modal, state tracking, history) |
| Sprint 5 | 2 weeks | Agents Foundation (Game Master integration, agent selection UI, move validation if RuleSpec v2 ready) |

---

## Related Documentation

- **Architecture**: [01-architecture/README.md](../README.md)
- **Bounded Contexts**: [bounded-contexts/](../../bounded-contexts/)
- **Testing Strategy**: [05-testing/](../../testing/)
- **Deployment**: [04-deployment/](../../deployment/)

---

*Version: 1.0*
*Last Updated: 2025-12-13*
*Status: Draft for Review*
