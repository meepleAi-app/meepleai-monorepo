# 📐 MEEPLEAI: Complete Product Specification

## Executive Summary

**Product**: MeepleAI - AI-Powered Board Game Assistant & Game Master
**Target**: Casual players (20-65, nerd demographic) with hardcore gamer tier
**Business Model**: Freemium with premium features
**Primary Use Case**: Learn rules, arbitrate disputes, manage game sessions
**MVP Timeline**: Q1-Q2 2025
**Platform**: Web PWA (mobile-optimized, offline nice-to-have)

---

## 🗺️ SITE MAP COMPLETO

```
MeepleAI App
│
├── 🏠 HOME (/)
│   ├── Welcome Banner (first visit)
│   ├── "Come funziona MeepleAI" (collapsible)
│   ├── Quick Tutorial (interactive, skippable)
│   ├── Recent Activity Feed
│   │   ├── Latest games played
│   │   ├── Recent chat sessions
│   │   └── Friend activity (future)
│   └── Quick Actions
│       ├── Start new game
│       ├── Continue last session
│       └── Browse game library
│
├── 🎮 GIOCHI (/games)
│   │
│   ├── 🔍 TAB: Ricerca & Catalogo (/games/search)
│   │   ├── Search Bar (autocomplete)
│   │   │   ├── By game name
│   │   │   ├── By publisher
│   │   │   └── By tags/mechanics
│   │   │
│   │   ├── Filter Panel (left sidebar)
│   │   │   ├── Source
│   │   │   │   ├── ☑️ Official catalog
│   │   │   │   ├── ☑️ User uploads
│   │   │   │   └── ☑️ BGG imported
│   │   │   ├── Complexity (1-5)
│   │   │   ├── Players (1-10+)
│   │   │   ├── Duration (15min-4h+)
│   │   │   └── Mechanics (tag cloud)
│   │   │
│   │   ├── Sort Options
│   │   │   ├── ⭐ Favorites first (default)
│   │   │   ├── 🔤 Alphabetical
│   │   │   ├── 📊 Rating (BGG)
│   │   │   ├── 👥 Users in library
│   │   │   └── 📅 Recently added
│   │   │
│   │   ├── Game Card Grid
│   │   │   ├── Cover image
│   │   │   ├── Title + Publisher
│   │   │   ├── Quick info (players, time, complexity)
│   │   │   ├── ⭐ Favorite toggle
│   │   │   ├── 📚 "Add to Library" button
│   │   │   └── 👥 N users have this
│   │   │
│   │   └── [➕ Add New Game] Button (top-right)
│   │       → Modal: Upload PDF
│   │           ├── File upload (drag & drop)
│   │           ├── Game name (auto-detect from PDF?)
│   │           ├── ☑️ "I own this game" confirmation
│   │           ├── Optional metadata (publisher, year, etc)
│   │           └── [Upload & Process]
│   │
│   ├── 📚 TAB: Mia Libreria (/games/library)
│   │   ├── Filter: All / Favorites / Custom uploads
│   │   ├── Game List (same card design as search)
│   │   └── Bulk actions (future: export, share)
│   │
│   └── GAME DETAIL PAGE (/games/:gameId)
│       │
│       ├── Header
│       │   ├── Cover image + Title
│       │   ├── Quick stats (players, time, complexity, BGG rating)
│       │   ├── ⭐ Favorite toggle
│       │   ├── 🔗 Share game
│       │   └── Actions
│       │       ├── [💬 Start Chat]
│       │       ├── [🎲 New Game Session]
│       │       └── [...] More (edit, delete if custom)
│       │
│       ├── 📖 TAB: Regolamento
│       │   ├── PDF Viewer (integrated, zoomable)
│       │   │   ├── Page navigation
│       │   │   ├── Search in PDF
│       │   │   ├── Download original
│       │   │   └── Print-friendly mode
│       │   ├── AI-Generated Setup Guide (if available)
│       │   └── Quick Reference Cards (extracted tables/diagrams)
│       │
│       ├── 🏠 TAB: House Rules
│       │   ├── Community house rules (upvote system)
│       │   ├── My house rules (private notes)
│       │   │   ├── Rich text editor
│       │   │   ├── Attach images/diagrams
│       │   │   └── Share with friends
│       │   └── [+ Add House Rule]
│       │
│       ├── 🎲 TAB: Partite
│       │   ├── Current Session (if active)
│       │   │   ├── Game state visualization
│       │   │   ├── Turn tracker
│       │   │   ├── Players list (human + AI)
│       │   │   ├── [Continue Game]
│       │   │   └── [End Game]
│       │   │
│       │   ├── Game History
│       │   │   ├── Past sessions list
│       │   │   │   ├── Date, duration
│       │   │   │   ├── Players & scores
│       │   │   │   ├── Winner highlight
│       │   │   │   └── [View Details]
│       │   │   │
│       │   │   └── Session Detail Modal
│       │   │       ├── Complete move log
│       │   │       ├── Chat transcript
│       │   │       ├── Statistics (turns, duration per player)
│       │   │       └── [Export Session]
│       │   │
│       │   └── [🎲 New Game Session] Button
│       │       → Modal: Setup Game
│       │           ├── Number of players (human)
│       │           ├── Add AI players (if game supports)
│       │           │   ├── Select difficulty
│       │           │   └── Assign color/position
│       │           ├── Game variants (if any)
│       │           ├── Starting player
│       │           └── [Start Game]
│       │
│       └── ℹ️ TAB: Info Gioco
│           ├── Description (BGG synopsis)
│           ├── Publisher & Designer info
│           ├── Mechanics & Categories
│           ├── Recommended age & player count
│           ├── Community ratings & reviews
│           ├── Related games (similar mechanics)
│           └── BGG link (external)
│
├── 💬 CHAT (/chat)
│   │
│   ├── Chat Sidebar (left, 25% width)
│   │   ├── [+ New Chat] Button
│   │   ├── Filter: All / Game-specific / Global
│   │   └── Chat History List
│   │       ├── Chat title (auto-generated from first message)
│   │       ├── Game badge (if game-specific)
│   │       ├── Last message preview
│   │       ├── Timestamp
│   │       └── [...] Menu (rename, delete, export)
│   │
│   ├── Chat Main Area (center, 75% width)
│   │   │
│   │   ├── Chat Header
│   │   │   ├── Chat title (editable)
│   │   │   ├── Game context (if applicable)
│   │   │   ├── 🔗 Share chat
│   │   │   └── [...] Options
│   │   │       ├── Export to PDF/Markdown
│   │   │       ├── Clear history
│   │   │       └── Settings (model, temperature)
│   │   │
│   │   ├── Message Thread
│   │   │   ├── User messages (right-aligned, blue)
│   │   │   └── AI messages (left-aligned, gray)
│   │   │       ├── Text response
│   │   │       ├── 📄 PDF citations (page N, clickable)
│   │   │       ├── 🖼️ Diagram references (inline images)
│   │   │       ├── 💡 Suggested follow-up questions
│   │   │       └── Actions
│   │   │           ├── 👍 👎 Feedback
│   │   │           ├── 📋 Copy
│   │   │           └── 🔗 Share message
│   │   │
│   │   └── Input Area (bottom)
│   │       ├── Text input (multi-line, markdown support)
│   │       ├── 📎 Attach image (board photo for CV)
│   │       ├── 🎤 Voice input (future)
│   │       ├── Context selector
│   │       │   ├── 🌍 Global (any topic)
│   │       │   └── 🎮 Game: [Dropdown to select game]
│   │       └── [Send] Button
│   │
│   └── NEW CHAT MODAL
│       ├── Chat type
│       │   ├── ○ Global (any board game topic)
│       │   └── ○ Game-specific
│       │       └── [Select Game Dropdown]
│       ├── Starting prompt template (optional)
│       └── [Create Chat]
│
├── 🤖 AGENTI (/agents)
│   │
│   ├── Agent Selection (top section)
│   │   ├── Agent Type Tabs
│   │   │   ├── 🎭 Game Masters (default)
│   │   │   ├── 🤖 AI Players
│   │   │   └── ⚙️ Workflow Agents
│   │   │
│   │   ├── Game Masters Tab
│   │   │   ├── "Select a game to start"
│   │   │   ├── Game Selector (dropdown, autocomplete)
│   │   │   └── Actions
│   │   │       ├── [💬 Start Chat] → redirects to /chat with game context
│   │   │       └── [🎲 Start Game Session] → game setup modal
│   │   │
│   │   ├── AI Players Tab
│   │   │   ├── Filter: Available for game [Dropdown]
│   │   │   ├── AI Player Cards
│   │   │   │   ├── Avatar + Name
│   │   │   │   ├── Difficulty level
│   │   │   │   ├── Playstyle (aggressive, defensive, balanced)
│   │   │   │   ├── Win rate % (if played before)
│   │   │   │   └── [Configure] button
│   │   │   └── Note: "Available only for games with full components definition"
│   │   │
│   │   └── Workflow Agents Tab
│   │       ├── Template Library
│   │       │   ├── 📧 Email reminders (game night)
│   │       │   ├── 📅 Calendar integration (schedule sessions)
│   │       │   ├── 📊 Stats export (weekly game recap)
│   │       │   └── 🔔 Discord/Slack notifications
│   │       ├── My Workflows (if user created any)
│   │       └── [+ Create Workflow] → n8n template editor
│   │
│   ├── Active Sessions (middle section)
│   │   ├── Title: "Active Game Sessions"
│   │   ├── Session Cards
│   │   │   ├── Game name + cover
│   │   │   ├── Players (human + AI)
│   │   │   ├── Turn: [Current player]
│   │   │   ├── Duration: [Time elapsed]
│   │   │   └── [Continue Game] button
│   │   └── Empty state: "No active sessions. Start a new game!"
│   │
│   └── History & Stats (bottom section)
│       ├── Recent Sessions (last 10)
│       │   ├── Date, game, result
│       │   └── [View Full History] → links to /games/:id/partite
│       │
│       └── Agent Performance (future)
│           ├── AI win rates by game
│           ├── Most used agents
│           └── Accuracy metrics (rule arbitration)
│
├── ⚙️ IMPOSTAZIONI (/settings)
│   │
│   ├── Sidebar Navigation (left)
│   │   ├── 👤 Account
│   │   ├── 🎨 Preferences
│   │   ├── 🔒 Privacy
│   │   └── 🛠️ Advanced
│   │
│   ├── 👤 ACCOUNT (/settings/account)
│   │   ├── Profile Section
│   │   │   ├── Avatar upload (drag & drop)
│   │   │   ├── Display name (editable)
│   │   │   ├── Username (if system uses)
│   │   │   └── [Save Changes]
│   │   │
│   │   ├── Email & Password
│   │   │   ├── Current email (display)
│   │   │   ├── [Change Email] → verification flow
│   │   │   ├── [Change Password] → old + new password
│   │   │   └── Last password change: [date]
│   │   │
│   │   ├── Two-Factor Authentication
│   │   │   ├── Status: ⚫ Disabled / 🟢 Enabled
│   │   │   ├── [Enable 2FA] → QR code + backup codes
│   │   │   ├── [Disable 2FA] → requires password + code
│   │   │   └── [Regenerate Backup Codes]
│   │   │
│   │   ├── Connected Accounts (OAuth)
│   │   │   ├── Google: 🟢 Connected / [Link Account]
│   │   │   ├── Discord: 🟢 Connected / [Link Account]
│   │   │   └── GitHub: 🟢 Connected / [Link Account]
│   │   │
│   │   └── Danger Zone
│   │       ├── [Delete Account] → confirmation modal
│   │       └── Note: "This action is permanent"
│   │
│   ├── 🎨 PREFERENCES (/settings/preferences)
│   │   ├── Language
│   │   │   ├── ○ Italiano (default)
│   │   │   └── ○ English
│   │   │
│   │   ├── Theme
│   │   │   ├── ○ Light
│   │   │   ├── ○ Dark (default)
│   │   │   └── ○ Auto (system preference)
│   │   │
│   │   ├── Notifications
│   │   │   ├── Email Notifications
│   │   │   │   ├── ☑️ Game invites
│   │   │   │   ├── ☑️ Chat mentions
│   │   │   │   ├── ☑️ Weekly digest
│   │   │   │   └── ☐ Marketing emails
│   │   │   │
│   │   │   └── Push Notifications (PWA)
│   │   │       ├── ☑️ Turn reminders
│   │   │       ├── ☑️ Game invites
│   │   │       └── ☐ Friend activity
│   │   │
│   │   ├── Chat Preferences
│   │   │   ├── Default model: [Dropdown: GPT-4, Claude, Gemini]
│   │   │   ├── Response style
│   │   │   │   ├── ○ Concise
│   │   │   │   ├── ○ Detailed (default)
│   │   │   │   └── ○ Expert
│   │   │   └── ☑️ Show PDF citations
│   │   │
│   │   └── [Save Preferences]
│   │
│   ├── 🔒 PRIVACY (/settings/privacy)
│   │   ├── Profile Visibility
│   │   │   ├── ○ Public (anyone can see)
│   │   │   ├── ○ Friends only (future)
│   │   │   └── ○ Private (default)
│   │   │
│   │   ├── Activity Sharing
│   │   │   ├── ☑️ Share game library
│   │   │   ├── ☑️ Share play statistics
│   │   │   └── ☐ Share chat history (anonymized)
│   │   │
│   │   ├── Data Retention
│   │   │   ├── Chat history: Keep for [Dropdown: 30/90/365 days / Forever]
│   │   │   ├── Game sessions: Keep for [Dropdown: 90/365 days / Forever]
│   │   │   └── [Clear Old Data Now] button
│   │   │
│   │   └── [Save Privacy Settings]
│   │
│   └── 🛠️ ADVANCED (/settings/advanced)
│       ├── API Keys (for developers)
│       │   ├── Your API Keys (list)
│       │   │   ├── Key name, created date, last used
│       │   │   ├── [Regenerate] [Delete]
│       │   │   └── Usage stats (requests, quota)
│       │   ├── [+ Create New API Key]
│       │   └── API Documentation link → external docs
│       │
│       ├── Developer Mode
│       │   ├── ☑️ Enable developer mode
│       │   │   → Shows debug info, API logs, performance metrics
│       │   └── Note: "For testing and debugging only"
│       │
│       ├── Data Export (GDPR compliance)
│       │   ├── [Export All My Data] → generates ZIP
│       │   │   ├── Profile data (JSON)
│       │   │   ├── Game library (JSON)
│       │   │   ├── Chat history (Markdown)
│       │   │   ├── Game sessions (JSON)
│       │   │   └── Uploaded PDFs (originals)
│       │   └── Last export: [date] / Never
│       │
│       └── Experimental Features (future)
│           ├── ☐ Beta: Computer Vision board recognition
│           ├── ☐ Beta: Voice commands
│           └── ☐ Alpha: Multiplayer real-time (coming soon)
│
└── 🔑 AUTHENTICATION FLOWS
    │
    ├── /register
    │   ├── Email + Password form
    │   ├── ☑️ Accept Terms & Privacy
    │   ├── [Sign Up] → sends verification email (disabled in dev)
    │   └── "Already have account?" → /login
    │
    ├── /login
    │   ├── Email + Password form
    │   ├── [Login] button
    │   ├── OR: Social login buttons
    │   │   ├── [Continue with Google]
    │   │   ├── [Continue with Discord]
    │   │   └── [Continue with GitHub]
    │   ├── "Forgot password?" → /forgot-password
    │   └── "Don't have account?" → /register
    │
    ├── /forgot-password
    │   ├── Email input
    │   ├── [Send Reset Link]
    │   └── → Email with reset token
    │
    ├── /reset-password/:token
    │   ├── New password input
    │   ├── Confirm password
    │   └── [Reset Password]
    │
    ├── /verify-email/:token (disabled in dev)
    │   ├── Auto-verify on load
    │   └── → Redirect to /login with success message
    │
    ├── /2fa/setup
    │   ├── QR code (TOTP)
    │   ├── Manual entry code (fallback)
    │   ├── [Verify Code] input
    │   ├── Backup codes (display once)
    │   └── [Enable 2FA]
    │
    └── /2fa/verify
        ├── Enter 6-digit code
        ├── "Use backup code" link
        └── [Verify]
```

---

## 🔄 USER FLOW DIAGRAMS

### Flow 1: New User Onboarding
```
START: User visits meepleai.com
│
├─→ [Register] → Fill email/password → Confirm → (Skip email verification in dev)
│   │
│   └─→ [Login] → Authenticated
│
└─→ [Login with OAuth] → Select provider (Google/Discord/GitHub) → OAuth flow → Authenticated
    │
    ↓
[First Login] → HOME page
    │
    ├─→ Welcome banner: "Benvenuto su MeepleAI!"
    ├─→ "Come funziona" section (collapsible, expanded first time)
    │   ├─ "1. Aggiungi giochi alla tua libreria"
    │   ├─ "2. Chatta con AI per imparare regole"
    │   ├─ "3. Gestisci partite con AI Game Master"
    │   └─ [Start Quick Tutorial] button
    │
    ├─→ [Skip Tutorial] → Collapsed welcome banner
    │
    └─→ [Start Quick Tutorial] → Interactive walkthrough (5 steps)
        ├─ Step 1: "Add your first game" → highlights /games tab
        ├─ Step 2: "Search or upload" → shows search + upload button
        ├─ Step 3: "Start a chat" → highlights chat button on game
        ├─ Step 4: "Ask about rules" → sample chat interaction
        └─ Step 5: "Start a game session" → shows game setup modal
            │
            └─→ [Finish Tutorial] → Dismiss welcome banner
                │
                └─→ Normal HOME view (tutorial can be re-triggered from help icon)
```

### Flow 2: Play a Complete Game Session
```
START: User on /games page
│
├─→ Search/browse game → Find "Catan"
│   │
│   └─→ Click game card → /games/catan
│       │
│       ├─→ TAB: Regolamento → View PDF, learn rules
│       │
│       ├─→ TAB: Info Gioco → Read description, mechanics
│       │
│       └─→ [🎲 New Game Session] button (top-right or Partite tab)
│           │
│           └─→ MODAL: Setup Game
│               ├─ Number of human players: [Dropdown: 1-4]
│               ├─ Add AI players? (Catan supports AI)
│               │   ├─ AI Player 1: [Difficulty: Medium] [Color: Red]
│               │   └─ AI Player 2: [Difficulty: Hard] [Color: Blue]
│               ├─ Game variant: [○ Base game ○ Seafarers ○ Cities & Knights]
│               ├─ Starting player: [Random / Manual select]
│               └─ [Start Game]
│                   │
│                   └─→ Game Session Screen (new page or modal)
│                       │
│                       ├─ LEFT PANEL: Game State Visualization
│                       │   ├─ Board view (if available)
│                       │   ├─ Resources/cards per player
│                       │   ├─ Victory points tracker
│                       │   └─ Turn indicator: "Player 1's turn"
│                       │
│                       ├─ CENTER PANEL: Game Actions
│                       │   ├─ [Roll Dice] button (if applicable)
│                       │   ├─ [Trade] [Build] [End Turn] actions
│                       │   └─ Move history log
│                       │
│                       └─ RIGHT PANEL: Chat with Game Master
│                           ├─ Real-time rule assistance
│                           ├─ Move validation
│                           │   ├─ User: "Can I build a road here?"
│                           │   └─ AI: "Yes, you have adjacent settlement + resources"
│                           └─ Ambiguity resolution
│                               ├─ User: "What if we run out of road pieces?"
│                               └─ AI: "Use proxy markers (page 12 of rules)"
│
DURING GAME:
│
├─→ Turn sequence:
│   ├─ Player action → AI validates → Update state
│   ├─ AI player turn (if present) → AI decides move → Execute → Update state
│   └─ Repeat
│
├─→ Rule questions anytime → Chat with Game Master → Get answer with citation
│
└─→ Game ends → Victory condition detected
    │
    └─→ END GAME MODAL
        ├─ Winner: [Player Name] - [Victory Points]
        ├─ Final scores (all players)
        ├─ Game duration: [1h 23min]
        ├─ Move count: [87 moves]
        ├─ [View Full Log] → Detailed move history
        ├─ [Export Session] → Download JSON/PDF
        └─ [Play Again] / [Back to Game Page]
            │
            └─→ Session saved to /games/catan → TAB: Partite → History
```

### Flow 3: Learning a New Game
```
START: User wants to learn "Wingspan"
│
├─→ /games → TAB: Ricerca
│   │
│   ├─ Search: "wingspan" → Autocomplete suggestions
│   │
│   └─→ Click "Wingspan" card → /games/wingspan
│       │
│       ├─ OVERVIEW (Game Detail Header)
│       │   ├─ Cover art, title
│       │   ├─ Quick stats: 1-5 players, 40-70 min, complexity 3/5
│       │   ├─ BGG rating: 8.1/10
│       │   └─ "Strategy card game about birds"
│       │
│       ├─→ TAB: Info Gioco (start here for context)
│       │   ├─ Description: "You are bird enthusiasts..."
│       │   ├─ Mechanics: Card Drafting, Engine Building, Set Collection
│       │   ├─ Designer: Elizabeth Hargrave
│       │   ├─ Publisher: Stonemaier Games
│       │   └─ Related games: Terraforming Mars, Everdell
│       │
│       ├─→ TAB: Regolamento (dive into rules)
│       │   ├─ PDF Viewer (integrated)
│       │   │   ├─ Navigate pages
│       │   │   ├─ Search: "habitat" → Find all occurrences
│       │   │   └─ Zoom in on complex diagrams
│       │   │
│       │   ├─ AI-Generated Setup Guide (if available)
│       │   │   ├─ "Step 1: Distribute player mats"
│       │   │   ├─ "Step 2: Shuffle bird cards..."
│       │   │   └─ Interactive checklist
│       │   │
│       │   └─ Quick Reference Cards (extracted)
│       │       ├─ Turn structure flowchart
│       │       ├─ Food cost table
│       │       └─ End-of-round goals
│       │
│       ├─→ [💬 Start Chat] (ask questions while learning)
│       │   └─→ /chat (game-specific context: Wingspan)
│       │       ├─ User: "How do the brown powers work?"
│       │       ├─ AI: "Brown powers activate when any player... (page 8)" [citation]
│       │       ├─ User: "Can I activate multiple brown powers?"
│       │       └─ AI: "Yes, all applicable brown powers trigger..." [diagram]
│       │
│       └─→ [🎲 Start Practice Game] (guided playthrough)
│           │
│           └─→ MODAL: Setup Practice Game
│               ├─ Mode: ○ Solo practice ● Guided tutorial
│               ├─ AI opponent: [Difficulty: Easy] (for practice)
│               └─ [Start Tutorial]
│                   │
│                   └─→ Tutorial Mode (step-by-step)
│                       ├─ Step 1: "Place player mat → Click here"
│                       ├─ Step 2: "Draw starting hand → Click these cards"
│                       ├─ Game Master explains each action in real-time
│                       ├─ Undo allowed (forgiving mode)
│                       └─ Complete first round → "You've learned the basics!"
│                           │
│                           └─→ [Continue Game] or [End Tutorial]
│                               │
│                               └─→ Ready to play real game!
```

### Flow 4: Upload Custom Game
```
START: User has PDF of obscure game "Flamme Rouge"
│
├─→ /games → TAB: Ricerca → [➕ Add New Game] (top-right button)
│   │
│   └─→ MODAL: Upload New Game
│       │
│       ├─ FILE UPLOAD SECTION
│       │   ├─ Drag & drop area
│       │   │   └─ "Drop PDF rulebook here or click to browse"
│       │   ├─ File selected: "flamme_rouge_rules.pdf" (4.2 MB)
│       │   └─ Status: "Ready to upload"
│       │
│       ├─ GAME INFORMATION (auto-detect from PDF if possible)
│       │   ├─ Game name: [Auto-filled: "Flamme Rouge"] (editable)
│       │   ├─ Publisher: [Optional: "Stronghold Games"]
│       │   ├─ Year: [Optional: 2016]
│       │   ├─ Players: [Optional: 2-4]
│       │   └─ Duration: [Optional: 30-45 min]
│       │
│       ├─ OWNERSHIP CONFIRMATION (legal/ethical)
│       │   ├─ ☑️ "I confirm I own a physical copy of this game"
│       │   └─ Note: "MeepleAI respects intellectual property. Custom uploads are private to your account."
│       │
│       └─ [Upload & Process] button
│           │
│           └─→ UPLOAD IN PROGRESS
│               ├─ Progress bar: "Uploading PDF... 75%"
│               ├─ "Processing document... (this may take 30-60 seconds)"
│               │   ├─ Extract text
│               │   ├─ Generate embeddings
│               │   ├─ Detect tables/diagrams
│               │   └─ Build search index
│               │
│               └─→ SUCCESS
│                   ├─ "✅ Flamme Rouge added to your library!"
│                   ├─ [View Game] → /games/flamme-rouge-custom-:userId
│                   └─ [Stay Here] → Upload another game
│
│
RESULT: Game visible ONLY to uploader
│
├─→ /games → TAB: Mia Libreria
│   └─ "Flamme Rouge" card with badge: "📁 Custom Upload"
│
└─→ /games/flamme-rouge-custom-:userId
    ├─ All tabs available (Regolamento, House Rules, Partite, Info)
    ├─ Chat works (RAG on uploaded PDF)
    └─ Options: [Edit Info] [Re-upload PDF] [Delete Game]
```

### Flow 5: BGG Integration (Future)
```
START: User searches for "Gloomhaven" not in system
│
├─→ /games → TAB: Ricerca → Search: "gloomhaven"
│   │
│   └─→ No results in local catalog → Suggestion banner
│       │
│       ├─ "Game not found. Search BoardGameGeek?"
│       └─ [Search BGG] button
│           │
│           └─→ BGG API SEARCH
│               ├─ Query BGG API
│               ├─ Results: "Gloomhaven (2017)" + metadata
│               └─→ MODAL: Import from BGG
│                   ├─ Game info (from BGG)
│                   │   ├─ Title, publisher, year
│                   │   ├─ Cover image
│                   │   ├─ Description
│                   │   └─ Ratings, mechanics
│                   │
│                   ├─ Rulebook Source
│                   │   ├─ ○ BGG file downloads (if available)
│                   │   ├─ ○ Publisher website link
│                   │   └─ ● I'll upload manually
│                   │
│                   └─ [Import to MeepleAI]
│                       │
│                       └─→ Game added to catalog (partial data)
│                           ├─ Info tab: populated from BGG
│                           ├─ Regolamento tab: "Upload rulebook PDF" prompt
│                           └─ [Add to My Library] available
```

---

## 📊 FEATURE MATRIX: MVP vs V2 vs Future

### MVP (Q1-Q2 2025) - Must Have
```yaml
authentication:
  - ✅ Email/password registration & login
  - ✅ OAuth social login (Google/Discord/GitHub)
  - ✅ 2FA/TOTP support
  - ✅ Password reset flow

game_library:
  - ✅ Admin/editor game uploads
  - ✅ User custom PDF uploads (private)
  - ✅ Search & filter (name, mechanics, players)
  - ✅ Favorite games
  - ✅ Game detail pages (4 tabs: Regolamento, House Rules, Partite, Info)

pdf_processing:
  - ✅ PDF viewer integration
  - ✅ Text extraction (Docling)
  - ✅ Table recognition
  - ✅ Embedding generation
  - ✅ Vector search (Qdrant)

chat_rag:
  - ✅ Global chat (any board game topic)
  - ✅ Game-specific chat (context-aware)
  - ✅ RAG with citations (page numbers)
  - ✅ Cross-session memory
  - ✅ Chat history & export

agents:
  - ✅ Game Master (rule arbitration)
  - 🟡 Basic move validation (if game has RuleSpec v2)
  - ❌ AI Players (V2)
  - ❌ Workflow Agents (V2)

game_sessions:
  - ✅ Create game session (setup modal)
  - ✅ Track current session
  - ✅ Session history
  - ✅ Basic state management
  - 🟡 Simple board state (text-based)
  - ❌ Visual board representation (V2)

settings:
  - ✅ Profile management
  - ✅ Email/password change
  - ✅ 2FA management
  - ✅ OAuth account linking
  - ✅ Preferences (language, theme, notifications)
  - ✅ Privacy settings
  - ✅ API keys for developers
  - ✅ Data export (GDPR)

infrastructure:
  - ✅ ASP.NET Core API
  - ✅ Next.js frontend (PWA-ready)
  - ✅ PostgreSQL + Qdrant + Redis
  - ✅ Docker Compose dev environment
  - ✅ Basic monitoring (Prometheus, Grafana)
```

### V2 (Q3 2025) - High Priority
```yaml
bgg_integration:
  - 🔵 Search BGG catalog
  - 🔵 Import game metadata
  - 🔵 Link to BGG rulebook downloads
  - 🔵 Sync ratings & reviews

ai_players:
  - 🔵 AI opponent for supported games
  - 🔵 Difficulty levels (Easy, Medium, Hard)
  - 🔵 Playstyle configuration
  - 🔵 Win rate tracking

visual_game_state:
  - 🔵 Board visualization (2D top-down)
  - 🔵 Piece placement UI
  - 🔵 Move highlighting
  - 🔵 Drag-and-drop interactions

enhanced_chat:
  - 🔵 Image attachment (board photos)
  - 🔵 Diagram extraction from PDF (inline display)
  - 🔵 Multi-LLM consensus (accuracy boost)
  - 🔵 Voice input (speech-to-text)

house_rules:
  - 🔵 Community house rules (upvote/downvote)
  - 🔵 Share house rules with friends
  - 🔵 Apply house rules to game sessions

workflow_agents:
  - 🔵 n8n workflow templates
  - 🔵 Email reminders (game night)
  - 🔵 Calendar integration
  - 🔵 Discord/Slack notifications

italian_optimization:
  - 🔵 Italian-specific embeddings
  - 🔵 Game terminology glossary (500+ terms)
  - 🔵 Italian game catalog (50+ titles)
  - 🔵 Partnership with Italian publishers
```

### V3 (Q4 2025) - Nice to Have
```yaml
tournament_management:
  - 🟣 Create tournaments (Swiss, Round Robin)
  - 🟣 Bracket visualization
  - 🟣 Leaderboards
  - 🟣 Automated pairings

social_features:
  - 🟣 Friends system
  - 🟣 Friend invites to games
  - 🟣 Activity feed
  - 🟣 Public profiles

multiplayer_realtime:
  - 🟣 Online multiplayer sessions
  - 🟣 Real-time board sync (WebSockets)
  - 🟣 Turn-based notifications
  - 🟣 Spectator mode

computer_vision:
  - 🟣 Board photo recognition
  - 🟣 Piece detection
  - 🟣 State sync from photo
  - 🟣 Move suggestion based on CV

mobile_app:
  - 🟣 React Native iOS/Android
  - 🟣 Offline mode (download rules)
  - 🟣 Camera integration (board scan)
  - 🟣 Push notifications

gamification:
  - 🟣 Achievements (games played, rules mastered)
  - 🟣 Badges & titles
  - 🟣 XP & leveling system
  - 🟣 Challenges (weekly quests)
```

### Future (2026+) - Vision
```yaml
advanced_ai:
  - 🔮 Adaptive AI (learns from your playstyle)
  - 🔮 Multi-agent coordination (team games)
  - 🔮 Strategic advice (coaching mode)

content_creation:
  - 🔮 AI-generated variants
  - 🔮 Procedural scenarios
  - 🔮 Custom rule generation (experimental)

publisher_tools:
  - 🔮 B2B dashboard
  - 🔮 Analytics (player engagement)
  - 🔮 Playtest coordination
  - 🔮 Errata distribution

marketplace:
  - 🔮 Premium AI models (subscription)
  - 🔮 Expert AI players (DLC)
  - 🔮 Custom agent marketplace
```

---

## 🗃️ DATABASE SCHEMA EVOLUTION

### New Entities Required

```sql
-- GAMES & LIBRARY
CREATE TABLE games (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(255) UNIQUE NOT NULL, -- for URLs
    publisher VARCHAR(255),
    designer VARCHAR(255),
    year INT,
    bgg_id INT UNIQUE, -- BoardGameGeek ID
    bgg_rating DECIMAL(3,2),
    min_players INT,
    max_players INT,
    duration_min INT, -- minutes
    duration_max INT,
    complexity INT, -- 1-5
    description TEXT,
    cover_image_url TEXT,
    source VARCHAR(50) NOT NULL, -- 'official', 'user_upload', 'bgg_import'
    uploaded_by_user_id UUID REFERENCES users(id), -- if user upload
    is_public BOOLEAN DEFAULT true, -- false for user custom uploads
    supports_ai_players BOOLEAN DEFAULT false,
    has_components_definition BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE game_mechanics (
    id UUID PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL, -- e.g., "Card Drafting"
    description TEXT
);

CREATE TABLE game_mechanics_mapping (
    game_id UUID REFERENCES games(id) ON DELETE CASCADE,
    mechanic_id UUID REFERENCES game_mechanics(id) ON DELETE CASCADE,
    PRIMARY KEY (game_id, mechanic_id)
);

CREATE TABLE user_game_library (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    game_id UUID REFERENCES games(id) ON DELETE CASCADE,
    is_favorite BOOLEAN DEFAULT false,
    added_at TIMESTAMP DEFAULT NOW(),
    UNIQUE(user_id, game_id)
);

-- GAME SESSIONS & TRACKING
CREATE TABLE game_sessions (
    id UUID PRIMARY KEY,
    game_id UUID REFERENCES games(id) ON DELETE SET NULL,
    created_by_user_id UUID REFERENCES users(id),
    session_type VARCHAR(50) NOT NULL, -- 'practice', 'competitive', 'tutorial'
    variant VARCHAR(100), -- game variant if applicable
    status VARCHAR(50) NOT NULL, -- 'setup', 'active', 'paused', 'completed', 'abandoned'
    current_turn INT DEFAULT 1,
    current_player_index INT DEFAULT 0,
    game_state JSONB, -- complete game state (board, resources, etc.)
    winner_user_id UUID REFERENCES users(id),
    started_at TIMESTAMP,
    ended_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE session_players (
    id UUID PRIMARY KEY,
    session_id UUID REFERENCES game_sessions(id) ON DELETE CASCADE,
    user_id UUID REFERENCES users(id), -- NULL if AI player
    player_type VARCHAR(50) NOT NULL, -- 'human', 'ai_easy', 'ai_medium', 'ai_hard'
    player_name VARCHAR(100) NOT NULL,
    player_index INT NOT NULL, -- turn order
    color VARCHAR(50), -- player color/faction
    final_score INT,
    is_winner BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE session_moves (
    id UUID PRIMARY KEY,
    session_id UUID REFERENCES game_sessions(id) ON DELETE CASCADE,
    player_id UUID REFERENCES session_players(id),
    move_number INT NOT NULL,
    move_type VARCHAR(100), -- 'roll_dice', 'place_piece', 'draw_card', etc.
    move_data JSONB, -- specific move details
    state_before JSONB, -- snapshot before move
    state_after JSONB, -- snapshot after move
    is_valid BOOLEAN DEFAULT true,
    validation_notes TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

-- CHAT ENHANCEMENTS
ALTER TABLE chat_logs ADD COLUMN game_id UUID REFERENCES games(id);
ALTER TABLE chat_logs ADD COLUMN session_id UUID REFERENCES game_sessions(id);
ALTER TABLE chat_logs ADD COLUMN chat_type VARCHAR(50) DEFAULT 'global'; -- 'global', 'game_specific', 'session'

CREATE TABLE chat_threads (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    title VARCHAR(255), -- auto-generated or user-edited
    game_id UUID REFERENCES games(id) ON DELETE SET NULL,
    session_id UUID REFERENCES game_sessions(id) ON DELETE SET NULL,
    chat_type VARCHAR(50) NOT NULL, -- 'global', 'game_specific', 'session'
    message_count INT DEFAULT 0,
    last_message_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

ALTER TABLE chat_logs ADD COLUMN thread_id UUID REFERENCES chat_threads(id) ON DELETE CASCADE;

-- AGENTS & WORKFLOWS
CREATE TABLE ai_agents (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    agent_type VARCHAR(50) NOT NULL, -- 'game_master', 'ai_player', 'workflow'
    game_id UUID REFERENCES games(id), -- if game-specific
    configuration JSONB, -- difficulty, playstyle, etc.
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE workflow_templates (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    category VARCHAR(50), -- 'notification', 'scheduling', 'reporting'
    n8n_workflow_json JSONB, -- n8n workflow definition
    is_public BOOLEAN DEFAULT false, -- community templates
    created_by_user_id UUID REFERENCES users(id),
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE user_workflows (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    template_id UUID REFERENCES workflow_templates(id),
    name VARCHAR(100) NOT NULL,
    configuration JSONB, -- user-specific settings
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT NOW()
);

-- HOUSE RULES
CREATE TABLE house_rules (
    id UUID PRIMARY KEY,
    game_id UUID REFERENCES games(id) ON DELETE CASCADE,
    created_by_user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    title VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    is_public BOOLEAN DEFAULT false, -- share with community
    upvotes INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE house_rule_votes (
    id UUID PRIMARY KEY,
    house_rule_id UUID REFERENCES house_rules(id) ON DELETE CASCADE,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    vote INT CHECK (vote IN (-1, 1)), -- downvote or upvote
    created_at TIMESTAMP DEFAULT NOW(),
    UNIQUE(house_rule_id, user_id)
);

-- SOCIAL FEATURES (Future)
CREATE TABLE user_relationships (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    friend_id UUID REFERENCES users(id) ON DELETE CASCADE,
    status VARCHAR(50) NOT NULL, -- 'pending', 'accepted', 'blocked'
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    CHECK (user_id != friend_id),
    UNIQUE(user_id, friend_id)
);

CREATE TABLE activity_feed (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    activity_type VARCHAR(50) NOT NULL, -- 'game_played', 'game_added', 'achievement_unlocked'
    activity_data JSONB,
    is_public BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT NOW()
);

-- INDEXES FOR PERFORMANCE
CREATE INDEX idx_games_slug ON games(slug);
CREATE INDEX idx_games_bgg_id ON games(bgg_id);
CREATE INDEX idx_games_source ON games(source);
CREATE INDEX idx_games_is_public ON games(is_public);

CREATE INDEX idx_user_game_library_user_id ON user_game_library(user_id);
CREATE INDEX idx_user_game_library_game_id ON user_game_library(game_id);
CREATE INDEX idx_user_game_library_favorite ON user_game_library(is_favorite);

CREATE INDEX idx_game_sessions_user_id ON game_sessions(created_by_user_id);
CREATE INDEX idx_game_sessions_game_id ON game_sessions(game_id);
CREATE INDEX idx_game_sessions_status ON game_sessions(status);

CREATE INDEX idx_chat_threads_user_id ON chat_threads(user_id);
CREATE INDEX idx_chat_threads_game_id ON chat_threads(game_id);
CREATE INDEX idx_chat_logs_thread_id ON chat_logs(thread_id);

CREATE INDEX idx_house_rules_game_id ON house_rules(game_id);
CREATE INDEX idx_house_rules_public ON house_rules(is_public);
```

---

## 🎨 WIREFRAME SPECIFICATIONS

### Page Layout Template (All Pages)
```
┌─────────────────────────────────────────────────────────────┐
│ HEADER (60px height, sticky)                                │
│ ┌─────────────┬──────────────────────────┬────────────────┐│
│ │ Logo + Name │   Global Search Bar      │ User Avatar  ▼││
│ │ MeepleAI    │   🔍 "Search games..."  │ Notifications  ││
│ └─────────────┴──────────────────────────┴────────────────┘│
└─────────────────────────────────────────────────────────────┘
┌───────────┬─────────────────────────────────────────────────┐
│ SIDEBAR   │ MAIN CONTENT AREA                               │
│ (240px)   │ (flex, responsive)                              │
│           │                                                 │
│ 🏠 Home    │                                                 │
│ 🎮 Giochi  │                                                 │
│ 💬 Chat    │                                                 │
│ 🤖 Agenti  │                                                 │
│ ⚙️ Settings│                                                 │
│ ─────────  │                                                 │
│ 🚪 Logout  │                                                 │
│           │                                                 │
│ (bottom)  │                                                 │
│ ❓ Help    │                                                 │
│ 📘 Docs    │                                                 │
└───────────┴─────────────────────────────────────────────────┘
```

### Wireframe 1: Home Page
```
┌──────────────────────────────────────────────────────────────┐
│ 🏠 HOME                                                      │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ 👋 Benvenuto su MeepleAI, [User Name]!                  ││
│ │                                                          ││
│ │ Come funziona MeepleAI (⬇️ Click to expand)             ││
│ │ ┌────────────────────────────────────────────────────┐  ││
│ │ │ 1️⃣ Aggiungi giochi alla tua libreria              │  ││
│ │ │ 2️⃣ Chatta con AI per imparare regole              │  ││
│ │ │ 3️⃣ Gestisci partite con AI Game Master            │  ││
│ │ │                                                    │  ││
│ │ │ [Start Quick Tutorial] [Maybe Later]              │  ││
│ │ └────────────────────────────────────────────────────┘  ││
│ └──────────────────────────────────────────────────────────┘│
│                                                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 📊 Your Activity                                        │ │
│ │ ─────────────────────────────────────────────────────── │ │
│ │ Games in Library: 12     Active Sessions: 1            │ │
│ │ Chats Started: 45        Hours Played: 23.5            │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 🕐 Recent Activity                                      │ │
│ │ ─────────────────────────────────────────────────────── │ │
│ │ • 2h ago: Chat about Wingspan rules                    │ │
│ │ • 1d ago: Completed Catan game session                 │ │
│ │ • 3d ago: Added Ticket to Ride to library              │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ ⚡ Quick Actions                                        │ │
│ │ ─────────────────────────────────────────────────────── │ │
│ │ [🎮 Browse Games]  [💬 New Chat]  [🎲 Start Game]     │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 🔥 Continue Where You Left Off                          │ │
│ │ ─────────────────────────────────────────────────────── │ │
│ │ ┌──────────────────────────────────────────────────┐   │ │
│ │ │ Catan Session                                    │   │ │
│ │ │ Turn 12/25 • Your turn                          │   │ │
│ │ │ [Continue Game]                                  │   │ │
│ │ └──────────────────────────────────────────────────┘   │ │
│ └─────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

### Wireframe 2: Games - Ricerca Tab
```
┌──────────────────────────────────────────────────────────────┐
│ 🎮 GIOCHI                            [➕ Add New Game]        │
├──────────────────────────────────────────────────────────────┤
│ [Ricerca] | Mia Libreria                                     │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ 🔍 Search: [___________________] 🔎                      ││
│ └──────────────────────────────────────────────────────────┘│
│                                                              │
│ ┌─────────────┬────────────────────────────────────────────┐│
│ │ FILTERS     │  GAME RESULTS                              ││
│ │             │                                            ││
│ │ Source      │  Sort by: [⭐ Favorites] ▼                ││
│ │ ☑️ Official │  ──────────────────────────────────────── ││
│ │ ☑️ User     │                                            ││
│ │ ☑️ BGG      │  ⭐ FAVORITES                              ││
│ │             │  ┌───────────────────────────────────────┐││
│ │ Players     │  │ ┌─────┐ Catan                         │││
│ │ ○ 1-2       │  │ │ img │ 3-4 players • 60-120 min     │││
│ │ ○ 3-4       │  │ └─────┘ Strategy, Trading           │││
│ │ ● Any       │  │ ⭐ [Remove from Favorites]            │││
│ │             │  │ 📚 In Library • 👥 1,234 users       │││
│ │ Duration    │  └───────────────────────────────────────┘││
│ │ ○ <30min    │                                            ││
│ │ ○ 30-60     │  ┌───────────────────────────────────────┐││
│ │ ● Any       │  │ ┌─────┐ Wingspan                      │││
│ │             │  │ │ img │ 1-5 players • 40-70 min      │││
│ │ Complexity  │  │ └─────┘ Card Drafting, Engine Build  │││
│ │ ─────       │  │ ⭐ [Remove from Favorites]            │││
│ │ 1 ●●●●● 5   │  │ 📚 In Library • 👥 987 users         │││
│ │ [1────●─5]  │  └───────────────────────────────────────┘││
│ │             │                                            ││
│ │ Mechanics   │  ALPHABETICAL (A-Z)                        ││
│ │ ☐ Deck      │  ┌───────────────────────────────────────┐││
│ │   Building  │  │ ┌─────┐ 7 Wonders                     │││
│ │ ☐ Worker    │  │ │ img │ 2-7 players • 30 min         │││
│ │   Placement │  │ └─────┘ Card Drafting, Set Collect.  │││
│ │             │  │ ☆ [Add to Favorites]                  │││
│ └─────────────┤  │ [📚 Add to Library] • 👥 543 users   │││
│               │  └───────────────────────────────────────┘││
│               │                                            ││
│               │  ┌───────────────────────────────────────┐││
│               │  │ ┌─────┐ Azul                          │││
│               │  │ │ img │ 2-4 players • 30-45 min      │││
│               │  │ └─────┘ Pattern Building, Drafting   │││
│               │  │ ☆ [Add to Favorites]                  │││
│               │  │ [📚 Add to Library] • 👥 321 users   │││
│               │  └───────────────────────────────────────┘││
│               │                                            ││
│               │  [Load More Games...]                      ││
│               │                                            ││
└───────────────┴────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────┘
```

### Wireframe 3: Game Detail Page
```
┌──────────────────────────────────────────────────────────────┐
│ 🎮 GIOCHI > Catan                                            │
├──────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ ┌────────┐                                               │ │
│ │ │        │  CATAN                                        │ │
│ │ │ Cover  │  Klaus Teuber • Kosmos (1995)                │ │
│ │ │ Image  │  👥 3-4 players • ⏱️ 60-120 min • 🎯 3/5    │ │
│ │ │        │  ⭐ 7.2/10 BGG                                │ │
│ │ └────────┘                                               │ │
│ │                                                          │ │
│ │  ⭐ [Favorite]  🔗 [Share]  [...More]                   │ │
│ │                                                          │ │
│ │  [💬 Start Chat]  [🎲 New Game Session]                │ │
│ └──────────────────────────────────────────────────────────┘ │
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ [📖 Regolamento] | House Rules | Partite | Info Gioco   │ │
│ ├──────────────────────────────────────────────────────────┤ │
│ │                                                          │ │
│ │ PDF VIEWER (Integrated, 70% width)                       │ │
│ │ ┌────────────────────────────────────────────────────┐  │ │
│ │ │                                                    │  │ │
│ │ │  [PDF Content Rendered Here]                       │  │ │
│ │ │                                                    │  │ │
│ │ │  - Page navigation                                 │  │ │
│ │ │  - Zoom controls                                   │  │ │
│ │ │  - Search in PDF                                   │  │ │
│ │ │  - Download original                               │  │ │
│ │ │                                                    │  │ │
│ │ └────────────────────────────────────────────────────┘  │ │
│ │                                                          │ │
│ │ ┌────────────────────────────────────────────────────┐  │ │
│ │ │ 🤖 AI-Generated Setup Guide                        │  │ │
│ │ │ ─────────────────────────────────────────────────  │  │ │
│ │ │ ☑️ Step 1: Lay out hexagonal tiles                 │  │ │
│ │ │ ☐ Step 2: Place number tokens                      │  │ │
│ │ │ ☐ Step 3: Distribute player pieces...             │  │ │
│ │ └────────────────────────────────────────────────────┘  │ │
│ │                                                          │ │
│ │ ┌────────────────────────────────────────────────────┐  │ │
│ │ │ 📋 Quick Reference Cards                           │  │ │
│ │ │ ─────────────────────────────────────────────────  │  │ │
│ │ │ • Turn Structure [View]                            │  │ │
│ │ │ • Resource Trading [View]                          │  │ │
│ │ │ • Development Cards [View]                         │  │ │
│ │ └────────────────────────────────────────────────────┘  │ │
│ └──────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

### Wireframe 4: Chat Interface
```
┌──────────────────────────────────────────────────────────────┐
│ 💬 CHAT                                                      │
├──────────────────────────────────────────────────────────────┤
│ ┌─────────────┬────────────────────────────────────────────┐│
│ │ CHAT LIST   │ CHAT MAIN AREA                             ││
│ │ (25% width) │ (75% width)                                ││
│ │             │                                            ││
│ │ [+ New Chat]│ ┌──────────────────────────────────────┐  ││
│ │             │ │ Chat: Wingspan Rules                 │  ││
│ │ Filter:     │ │ 🎮 Context: Wingspan                 │  ││
│ │ [All] ▼     │ │ [...Options] 🔗 Share                │  ││
│ │             │ └──────────────────────────────────────┘  ││
│ │ ──────────  │                                            ││
│ │             │ ┌──────────────────────────────────────┐  ││
│ │ ● Wingspan  │ │ MESSAGE THREAD (scrollable)          │  ││
│ │   Rules     │ │                                      │  ││
│ │   🎮 5m ago  │ │ ┌──────────────────────────┐        │  ││
│ │             │ │ │ USER (right-aligned)    │        │  ││
│ │ ● Global    │ │ │ How do brown powers     │        │  ││
│ │   Chat      │ │ │ work in Wingspan?       │        │  ││
│ │   🌍 1h ago  │ │ └──────────────────────────┘        │  ││
│ │             │ │                                      │  ││
│ │ ● Catan     │ │ ┌──────────────────────────┐        │  ││
│ │   Strategy  │ │ │ AI (left-aligned, gray) │        │  ││
│ │   🎮 2d ago  │ │ │ Brown powers activate   │        │  ││
│ │             │ │ │ when ANY player takes   │        │  ││
│ │ [View All]  │ │ │ the action shown...     │        │  ││
│ │             │ │ │                         │        │  ││
│ │             │ │ │ 📄 Citation: Page 8     │        │  ││
│ │             │ │ │ 🖼️ [Show Diagram]       │        │  ││
│ │             │ │ │                         │        │  ││
│ │             │ │ │ 💡 Follow-up questions: │        │  ││
│ │             │ │ │ • When do pink powers?  │        │  ││
│ │             │ │ │ • Can I chain powers?   │        │  ││
│ │             │ │ │                         │        │  ││
│ │             │ │ │ 👍 👎 📋 🔗              │        │  ││
│ │             │ │ └──────────────────────────┘        │  ││
│ │             │ │                                      │  ││
│ │             │ └──────────────────────────────────────┘  ││
│ │             │                                            ││
│ │             │ ┌──────────────────────────────────────┐  ││
│ │             │ │ INPUT AREA                           │  ││
│ │             │ │ ┌────────────────────────────────┐   │  ││
│ │             │ │ │ Type your message...           │   │  ││
│ │             │ │ │ (markdown supported)           │   │  ││
│ │             │ │ └────────────────────────────────┘   │  ││
│ │             │ │                                      │  ││
│ │             │ │ 📎 Attach  Context: [Wingspan] ▼    │  ││
│ │             │ │                          [Send] →    │  ││
│ │             │ └──────────────────────────────────────┘  ││
│ └─────────────┴────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────┘
```

### Wireframe 5: Agenti Page
```
┌──────────────────────────────────────────────────────────────┐
│ 🤖 AGENTI                                                    │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ AGENT TYPE SELECTION                                     ││
│ │ [🎭 Game Masters] | AI Players | Workflow Agents         ││
│ └──────────────────────────────────────────────────────────┘│
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ SELECT A GAME TO START                                   ││
│ │                                                          ││
│ │ Game: [Select game...                         ] ▼        ││
│ │                                                          ││
│ │ [💬 Start Chat]  [🎲 Start Game Session]                ││
│ └──────────────────────────────────────────────────────────┘│
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ ACTIVE GAME SESSIONS                                     ││
│ │ ──────────────────────────────────────────────────────   ││
│ │                                                          ││
│ │ ┌────────────────────────────────────────────────────┐  ││
│ │ │ 🎲 Catan                                           │  ││
│ │ │ Players: Alice (you), Bob, AI_Easy, AI_Medium     │  ││
│ │ │ Turn: Bob (turn 12/25)                            │  ││
│ │ │ Duration: 1h 15m                                  │  ││
│ │ │ [Continue Game]                                    │  ││
│ │ └────────────────────────────────────────────────────┘  ││
│ │                                                          ││
│ │ Empty state: "No active sessions. Start a new game!"   ││
│ └──────────────────────────────────────────────────────────┘│
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ RECENT SESSIONS                                          ││
│ │ ──────────────────────────────────────────────────────   ││
│ │ • 2h ago: Wingspan (Won by Alice) • [View Details]      ││
│ │ • 1d ago: Ticket to Ride (Won by Bob) • [View Details]  ││
│ │ • 3d ago: Azul (Won by AI_Hard) • [View Details]        ││
│ │                                                          ││
│ │ [View Full History]                                      ││
│ └──────────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────┘
```

### Wireframe 6: Settings Page
```
┌──────────────────────────────────────────────────────────────┐
│ ⚙️ IMPOSTAZIONI                                              │
├──────────────────────────────────────────────────────────────┤
│ ┌─────────────┬────────────────────────────────────────────┐│
│ │ SIDEBAR NAV │ SETTINGS CONTENT                           ││
│ │             │                                            ││
│ │ ● 👤 Account│ ┌──────────────────────────────────────┐  ││
│ │ ○ 🎨 Prefs  │ │ ACCOUNT SETTINGS                     │  ││
│ │ ○ 🔒 Privacy│ ├──────────────────────────────────────┤  ││
│ │ ○ 🛠️ Adv.   │ │                                      │  ││
│ │             │ │ Profile                              │  ││
│ │             │ │ ┌─────────────────────────────────┐  │  ││
│ │             │ │ │ [Avatar Upload]                 │  │  ││
│ │             │ │ │ Display Name: [Alice Smith___]  │  │  ││
│ │             │ │ │ Username: @alice_gamer          │  │  ││
│ │             │ │ │ [Save Changes]                  │  │  ││
│ │             │ │ └─────────────────────────────────┘  │  ││
│ │             │ │                                      │  ││
│ │             │ │ Email & Password                     │  ││
│ │             │ │ ┌─────────────────────────────────┐  │  ││
│ │             │ │ │ Email: alice@example.com        │  │  ││
│ │             │ │ │ [Change Email]                  │  │  ││
│ │             │ │ │ Password: ••••••••              │  │  ││
│ │             │ │ │ [Change Password]               │  │  ││
│ │             │ │ │ Last changed: 2024-12-15        │  │  ││
│ │             │ │ └─────────────────────────────────┘  │  ││
│ │             │ │                                      │  ││
│ │             │ │ Two-Factor Authentication            │  ││
│ │             │ │ ┌─────────────────────────────────┐  │  ││
│ │             │ │ │ Status: 🟢 Enabled              │  │  ││
│ │             │ │ │ [Disable 2FA] [Regen Codes]     │  │  ││
│ │             │ │ └─────────────────────────────────┘  │  ││
│ │             │ │                                      │  ││
│ │             │ │ Connected Accounts (OAuth)           │  ││
│ │             │ │ ┌─────────────────────────────────┐  │  ││
│ │             │ │ │ Google: 🟢 Connected [Unlink]  │  │  ││
│ │             │ │ │ Discord: ⚫ Not linked [Link]   │  │  ││
│ │             │ │ │ GitHub: 🟢 Connected [Unlink]  │  │  ││
│ │             │ │ └─────────────────────────────────┘  │  ││
│ │             │ │                                      │  ││
│ │             │ │ ⚠️ DANGER ZONE                       │  ││
│ │             │ │ ┌─────────────────────────────────┐  │  ││
│ │             │ │ │ [Delete Account]                │  │  ││
│ │             │ │ │ This action is permanent        │  │  ││
│ │             │ │ └─────────────────────────────────┘  │  ││
│ │             │ └──────────────────────────────────────┘  ││
│ └─────────────┴────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────┘
```

---

## 🎯 NEXT STEPS

### Immediate Actions (Week 1)
```bash
1. Review & approve specification
2. Create GitHub issues from feature matrix
3. Setup project boards (MVP / V2 / Future)
4. Begin database schema migrations
5. Design system components (Storybook?)
```

### Development Sprint Planning
```yaml
sprint_1: Authentication & Settings (2 weeks)
  - OAuth integration complete
  - Settings pages (4 tabs)
  - User profile management

sprint_2: Game Library Foundation (2 weeks)
  - Game entity & CRUD
  - PDF upload & processing (Docling)
  - Search & filter UI

sprint_3: Chat Enhancement (2 weeks)
  - Thread management
  - Game-specific context
  - PDF citations display

sprint_4: Game Sessions MVP (3 weeks)
  - Session creation & setup
  - Basic state tracking
  - History & statistics

sprint_5: Agents Foundation (2 weeks)
  - Game Master integration
  - Agent selection UI
  - Move validation (if RuleSpec v2 ready)
```

---

*Document Version: 1.0*
*Last Updated: 2025-12-13T10:59:23.970Z
*Status: Draft for Review*
