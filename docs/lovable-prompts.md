# Lovable.dev Prompts - MeepleAI Frontend

> **Target Platform**: Lovable.dev
> **Framework**: React + TypeScript + Tailwind CSS
> **Created**: 2025-10-14
> **Purpose**: Progressive frontend development prompts

## 📋 Table of Contents

1. [Project Setup & Foundation](#project-setup--foundation)
2. [Authentication System](#authentication-system)
3. [Core UI Components](#core-ui-components)
4. [Chat Interface](#chat-interface)
5. [PDF Upload Wizard](#pdf-upload-wizard)
6. [RuleSpec Editor](#rulespec-editor)
7. [Admin Dashboard](#admin-dashboard)
8. [Advanced Features](#advanced-features)

---

## Project Setup & Foundation

### Prompt 1: Initialize MeepleAI Project

```
Create a new React + TypeScript + Tailwind CSS application called "MeepleAI" - an AI-powered board game rules assistant.

PROJECT STRUCTURE:
- Modern, clean design with a game-themed aesthetic
- Dark mode support (toggle in header)
- Responsive layout (mobile-first)
- Primary color: Blue (#0070f3)
- Secondary color: Green (#34a853)
- Accent color: Orange (#ff9800)

INITIAL PAGES:
1. Landing page (/) with hero section
2. Login/Register page (/auth)
3. Dashboard placeholder (/dashboard)

GLOBAL COMPONENTS:
- Header: Logo, navigation menu, user avatar dropdown, dark mode toggle
- Footer: Copyright, links to docs, GitHub
- Sidebar: Collapsible navigation with icons

TECH REQUIREMENTS:
- React 18+
- TypeScript (strict mode)
- Tailwind CSS with custom theme
- React Router v6
- Lucide React for icons
- shadcn/ui components library

HEADER NAVIGATION:
- Logo: "MeepleAI 🎲" (link to home)
- Links: Dashboard, Chat, Upload, Editor, Admin (show based on role)
- Right side: Dark mode toggle, User menu (logout)

Create a professional, modern UI with smooth animations and transitions.
```

---

### Prompt 2: API Client & Authentication Context

```
Create a robust API client and authentication system for MeepleAI.

API CLIENT (lib/api.ts):
- Base URL from environment variable: VITE_API_BASE (default: http://localhost:8080)
- HTTP methods: GET, POST, PUT, DELETE
- Automatic cookie handling (credentials: "include")
- Generic TypeScript types for type-safe requests
- Error handling with user-friendly messages
- Auto-redirect to /auth on 401 Unauthorized
- Retry logic for network errors (max 3 retries)
- Response headers: X-Correlation-Id, X-RateLimit-Remaining

EXAMPLE USAGE:
```typescript
import { api } from '@/lib/api';

// GET request
const games = await api.get<Game[]>('/games');

// POST request
const chat = await api.post<Chat>('/chats', { gameId: 'chess', agentId: 'qa' });

// PUT request
await api.put(`/games/${id}/rulespec`, ruleSpec);

// DELETE request
await api.delete(`/chats/${id}`);
```

AUTH CONTEXT (contexts/AuthContext.tsx):
- React Context for global auth state
- User type: { id, email, displayName, role }
- Roles: Admin, Editor, User
- Methods: login, register, logout, checkAuth
- Auto-load current user on mount (GET /auth/me)
- Persist user in context (no localStorage needed - cookie-based)

PROTECTED ROUTE COMPONENT:
- ProtectedRoute wrapper component
- Check auth state from context
- Redirect to /auth if not authenticated
- Show loading spinner while checking auth
- Optional role-based access (roleRequired prop)

HOOKS:
- useAuth(): Access auth context
- useRequireAuth(): Auto-redirect if not authenticated

Use Zustand or React Context API for state management. Implement proper TypeScript types for all API responses.
```

---

## Authentication System

### Prompt 3: Login & Registration Forms

```
Create beautiful, user-friendly login and registration forms for MeepleAI.

PAGE: /auth
- Tabs: "Login" and "Register" (toggle between them)
- Card layout with shadow and border
- Centered on page with background gradient

LOGIN FORM:
Fields:
- Email (required, email validation)
- Password (required, min 8 characters, show/hide toggle)

Buttons:
- "Login" (primary, full width)
- "Forgot password?" (link, below form)

API: POST /auth/login
Body: { email, password }
Response: { user: { id, email, displayName, role }, expiresAt }

REGISTER FORM:
Fields:
- Email (required, email validation)
- Password (required, min 8 characters, show/hide toggle, strength indicator)
- Display Name (optional)
- Role (select: User, Editor, Admin) - default: User

Buttons:
- "Create Account" (primary, full width)

API: POST /auth/register
Body: { email, password, displayName?, role }
Response: { user: { id, email, displayName, role }, expiresAt }

UX FEATURES:
- Loading state on submit (disable form + show spinner)
- Error messages below form (red alert)
- Success messages (green alert)
- Auto-redirect to /dashboard on success
- Field validation with real-time feedback
- Password strength meter for register
- Show/hide password toggle icon

DEMO USERS (display as info box):
- admin@meepleai.dev / Demo123! (Admin)
- editor@meepleai.dev / Demo123! (Editor)
- user@meepleai.dev / Demo123! (User)

Use shadcn/ui components: Card, Input, Button, Label, Alert, Tabs
```

---

### Prompt 4: User Profile & Settings

```
Create a user profile page with settings and session management.

PAGE: /profile
- Accessible from header user menu

SECTIONS:

1. PROFILE INFO (Card):
   - Avatar placeholder (first letter of display name)
   - Email (read-only)
   - Display Name (editable, save button)
   - Role badge (Admin/Editor/User with colored pill)
   - Account created date

2. ACTIVE SESSIONS (Card):
   - Table with columns: Device/Browser, IP Address, Last Active, Actions
   - Show current session (highlighted)
   - "Revoke" button for other sessions
   - API: GET /auth/sessions (future), DELETE /auth/sessions/{id}

3. SETTINGS (Card):
   - Dark mode toggle
   - Language selector (English, Italian) - future
   - Notifications preferences - future

4. DANGER ZONE (Card, red border):
   - "Change Password" button → modal
   - "Delete Account" button → confirmation modal

API ENDPOINTS:
- GET /auth/me - Current user
- PUT /auth/profile - Update profile
- POST /auth/change-password - Change password (future)

Use shadcn/ui: Card, Avatar, Badge, Table, Button, Dialog (for modals)
```

---

## Core UI Components

### Prompt 5: Shared Components Library

```
Create a library of reusable UI components for MeepleAI.

COMPONENTS TO BUILD:

1. GAME SELECTOR (components/GameSelector.tsx):
   - Dropdown with search
   - Show game icon/thumbnail + name
   - Load from API: GET /games
   - Props: value, onChange, disabled
   - Loading state skeleton

2. AGENT SELECTOR (components/AgentSelector.tsx):
   - Dropdown with agent types
   - Icon per agent type: QA (💬), Explain (📖), Setup (🎯)
   - Props: gameId, value, onChange, disabled
   - API: GET /games/{gameId}/agents
   - Disable if no gameId selected

3. LOADING SPINNER (components/LoadingSpinner.tsx):
   - Animated spinner with game dice icon
   - Sizes: sm, md, lg
   - Optional text below spinner
   - Use Lucide React icons

4. ERROR ALERT (components/ErrorAlert.tsx):
   - Red alert with error icon
   - Props: message, onClose
   - Auto-dismiss after 5 seconds (optional)

5. SUCCESS TOAST (components/SuccessToast.tsx):
   - Green toast notification
   - Appears top-right corner
   - Auto-dismiss after 3 seconds
   - Props: message

6. SNIPPET CARD (components/SnippetCard.tsx):
   - Card showing PDF snippet
   - Props: text, source, page, line
   - Truncate long text with "Show more"
   - Copy to clipboard button

7. EMPTY STATE (components/EmptyState.tsx):
   - Icon + message + optional action button
   - Props: icon, title, description, actionLabel, onAction

8. STATS CARD (components/StatsCard.tsx):
   - Card with large number + label
   - Optional trend indicator (up/down arrow)
   - Props: value, label, trend?, trendValue?

Use Tailwind CSS for styling. Make components fully responsive. Add hover effects and transitions.
```

---

### Prompt 6: Layout Components

```
Create layout components for consistent page structure.

1. DASHBOARD LAYOUT (layouts/DashboardLayout.tsx):
   - Sidebar on left (collapsible on mobile)
   - Main content area on right
   - Header on top
   - Props: children, title, breadcrumbs?

SIDEBAR ITEMS:
- Dashboard (Home icon)
- Chat (MessageSquare icon)
- Upload PDF (Upload icon)
- RuleSpec Editor (Edit icon)
- Admin (Shield icon) - only for Admin role
- Profile (User icon)
- Logout (LogOut icon)

SIDEBAR FEATURES:
- Active item highlighted
- Collapse/expand button (mobile)
- User avatar + name at bottom
- Role badge below name

2. PAGE HEADER (components/PageHeader.tsx):
   - Title (h1)
   - Optional breadcrumbs
   - Optional action buttons (right side)
   - Props: title, breadcrumbs?, actions?

3. CONTENT CONTAINER (components/ContentContainer.tsx):
   - Max width container with padding
   - Centered content
   - Props: children, maxWidth? (default: 1400px)

4. TWO COLUMN LAYOUT (components/TwoColumnLayout.tsx):
   - Responsive 2-column grid
   - Props: left, right, ratio? (default: 1:1)
   - Stack vertically on mobile

Use shadcn/ui Sidebar component. Implement smooth animations for sidebar collapse.
```

---

## Chat Interface

### Prompt 7: Chat Page - Basic Structure

```
Create the main chat interface page for MeepleAI.

PAGE: /chat
- Full height layout (no scroll on container)
- Three sections: Sidebar, Chat Area, Input

LAYOUT:
┌─────────────┬──────────────────────┐
│   Sidebar   │    Chat Area         │
│  (320px)    │   (flex-grow)        │
│             │                      │
│  - Filters  │  - Messages          │
│  - Chats    │  - Loading           │
│  - Create   │                      │
│             ├──────────────────────┤
│             │   Input Area         │
└─────────────┴──────────────────────┘

SIDEBAR (components/chat/ChatSidebar.tsx):
- Header: "Chats" title + collapse button
- Game selector dropdown
- Agent selector dropdown
- "New Chat" button (disabled if no game/agent selected)
- Chat list (scrollable):
  - Each item: Agent name, timestamp, preview
  - Active chat highlighted
  - Delete button on hover (trash icon)
- Loading skeleton when fetching chats

CHAT AREA (components/chat/ChatArea.tsx):
- Header:
  - Active chat name + game name
  - Back to home button (right)
- Messages container (scrollable):
  - User messages (right-aligned, blue background)
  - Assistant messages (left-aligned, gray background)
  - Snippets below assistant messages (collapsible cards)
  - Timestamps (small, gray text)
  - Loading indicator for new message
- Empty state: "Select a chat or create a new one"

INPUT AREA (components/chat/ChatInput.tsx):
- Text input (full width, multiline)
- Send button (disabled if empty or no chat selected)
- Character count (optional)
- Keyboard shortcut: Enter to send (Shift+Enter for new line)

STATE MANAGEMENT:
- selectedGameId: string | null
- selectedAgentId: string | null
- activeChatId: string | null
- chats: Chat[]
- messages: Message[]
- inputValue: string
- isLoading: boolean

Use shadcn/ui: ScrollArea, Separator, Textarea, Button
```

---

### Prompt 8: Chat Messages & Snippets

```
Implement chat messages with RAG snippets and feedback.

MESSAGE COMPONENT (components/chat/Message.tsx):
Types:
```typescript
type Message = {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  snippets?: Snippet[];
  feedback?: 'helpful' | 'not-helpful' | null;
  backendMessageId?: string;
};

type Snippet = {
  text: string;
  source: string;
  page?: number;
  line?: number;
};
```

USER MESSAGE:
- Right-aligned bubble (blue background)
- White text
- Max width 75%
- Rounded corners
- Small timestamp below

ASSISTANT MESSAGE:
- Left-aligned bubble (gray background)
- Black text
- Max width 75%
- Avatar icon (robot)
- Feedback buttons below:
  - 👍 Helpful (green when selected)
  - 👎 Not helpful (red when selected)
- Snippets section (if present)

SNIPPETS SECTION (components/chat/SnippetsList.tsx):
- Collapsible section: "Sources (3)" with chevron
- List of snippet cards:
  - Source name + page number (bold)
  - Text excerpt (truncated, "Show more" to expand)
  - Light border, subtle shadow
  - Copy button (clipboard icon)

FEEDBACK HANDLING:
- API: POST /agents/feedback
- Body: { messageId, endpoint: 'qa', outcome: 'helpful' | 'not-helpful' | null, userId, gameId }
- Toggle feedback (click again to remove)
- Optimistic update (instant UI change)
- Revert on API error

FEATURES:
- Markdown rendering for assistant messages
- Code blocks with syntax highlighting
- Auto-scroll to bottom on new message
- "Scroll to bottom" button when scrolled up
- Copy message button

Use react-markdown for rendering. Add smooth scroll animations.
```

---

### Prompt 9: Chat Creation & Management

```
Implement chat creation workflow and chat list management.

CHAT LIST COMPONENT (components/chat/ChatList.tsx):
- Fetch chats: GET /chats?gameId={gameId}
- Display format:
  ```
  🤖 QA Agent
  2 minutes ago
  "How many players..."
  ```
- Click to load chat history
- Delete button (trash icon, confirm modal)
- Active chat highlighted (blue border)
- Loading skeleton (3 items)
- Empty state: "No chats yet. Create one!"

CREATE CHAT FLOW:
1. User selects game → load agents
2. User selects agent → enable "New Chat" button
3. Click "New Chat" → API call
4. API: POST /chats, Body: { gameId, agentId }
5. Response: { id, gameId, agentId, gameName, agentName, startedAt, lastMessageAt }
6. Add chat to list + set as active
7. Clear messages + ready for input

LOAD CHAT HISTORY:
- API: GET /chats/{chatId}
- Response: { id, gameId, agentId, messages: [...] }
- Convert backend messages to frontend format:
  ```typescript
  const frontendMessage: Message = {
    id: `backend-${msg.id}`,
    role: msg.level === 'user' ? 'user' : 'assistant',
    content: msg.message,
    snippets: JSON.parse(msg.metadataJson || '{}').snippets,
    timestamp: new Date(msg.createdAt),
    backendMessageId: msg.id
  };
  ```

DELETE CHAT:
- Confirmation modal: "Delete this chat?"
- API: DELETE /chats/{chatId}
- Remove from list
- If active chat deleted → clear active chat

SEND MESSAGE FLOW:
1. Check if active chat exists
2. If no active chat → auto-create via POST /chats
3. API: POST /agents/qa, Body: { gameId, query, chatId }
4. Add user message to UI (optimistic)
5. Show loading indicator
6. Add assistant response + snippets to UI
7. Update chat lastMessageAt in list

ERROR HANDLING:
- Show error alert above chat input
- Don't clear user input on error
- Retry button for failed messages

Use optimistic updates for better UX. Implement auto-save draft messages.
```

---

## PDF Upload Wizard

### Prompt 10: Upload Wizard - Step 1 (Game Selection & Upload)

```
Create a multi-step wizard for PDF upload and processing.

PAGE: /upload
- Protected route (Admin/Editor only)
- Full-width wizard layout
- Progress indicator at top

STEP INDICATOR (components/wizard/StepIndicator.tsx):
- 4 steps: Upload → Parse → Review → Publish
- Show current step (blue), completed (green), upcoming (gray)
- Step numbers in circles
- Connecting lines between steps

STEP 1: UPLOAD PDF

GAME SELECTION (Card):
- Title: "Select or Create Game"
- Dropdown: Existing games
- "Confirm Selection" button → locks in game
- Confirmed game shown in readonly field (green border)

CREATE GAME (Card):
- Title: "Create New Game"
- Input: Game name
- "Create Game" button
- On success: auto-select + confirm new game

PDF UPLOAD (Card):
- Title: "Upload PDF Rulebook"
- File input: accept=".pdf"
- Drag & drop zone (dashed border, hover effect)
- Show selected file: name + size
- "Upload & Continue" button (disabled if no game confirmed or no file)

UPLOADED PDFS TABLE (Card):
- Title: "Previously Uploaded PDFs"
- Columns: File Name, Size, Uploaded, Status, Actions
- Status badges: Pending (gray), Processing (yellow), Completed (green), Failed (red)
- Actions: "View Log" button, "Retry Parse" button
- Empty state: "No PDFs uploaded yet"

APIs:
- GET /games - Load games list
- POST /games - Create game: { name }
- POST /ingest/pdf - Upload: FormData with file + gameId
- GET /games/{gameId}/pdfs - Load PDFs for game

STATE:
- selectedGameId: string | null
- confirmedGameId: string | null
- newGameName: string
- file: File | null
- uploading: boolean
- pdfs: PdfDocument[]

Use shadcn/ui: Card, Input, Button, Select, Progress, Table, Badge
```

---

### Prompt 11: Upload Wizard - Step 2 (Parse & Processing)

```
Implement PDF parsing step with real-time status polling.

STEP 2: PARSE PDF

PAGE STATE:
- Auto-advance from Step 1 after upload
- documentId from upload response
- Poll status every 2 seconds

STATUS DISPLAY (Card):
- Title: "Processing PDF"
- Document ID (small, monospace)
- Progress bar (0-100%):
  - Pending: 20%
  - Processing: 65%
  - Completed: 100%
  - Failed: 100% (red)
- Status text: "Pending..." / "Processing..." / "Completed!" / "Failed"
- Error message (if failed, red text)

PROGRESS STATES:
```typescript
type ProcessingStatus = 'pending' | 'processing' | 'completed' | 'failed';

const statusProgress = {
  pending: 20,
  processing: 65,
  completed: 100,
  failed: 100
};

const statusLabels = {
  pending: 'Waiting in queue...',
  processing: 'Extracting text from PDF...',
  completed: 'Processing complete!',
  failed: 'Processing failed'
};
```

POLLING LOGIC:
```typescript
useEffect(() => {
  if (step !== 'parse' || !documentId) return;

  const interval = setInterval(async () => {
    try {
      const response = await fetch(`/pdfs/${documentId}/text`);
      const data = await response.json();

      setProcessingStatus(data.processingStatus);
      setProcessingError(data.processingError);

      if (data.processingStatus === 'completed') {
        clearInterval(interval);
        // Auto-advance to review step
        setTimeout(() => handleParse(), 1000);
      } else if (data.processingStatus === 'failed') {
        clearInterval(interval);
      }
    } catch (error) {
      console.error('Status poll failed:', error);
    }
  }, 2000);

  return () => clearInterval(interval);
}, [step, documentId]);
```

ACTIONS:
- "Parse PDF" button (manual trigger if needed)
  - Disabled if status !== 'completed'
  - API: GET /games/{gameId}/rulespec
  - On success → advance to Step 3
- "Start Over" button
  - Reset wizard to Step 1
  - Clear all state

AUTO-ADVANCE:
- When status = 'completed' → wait 1s → call parse API → go to Step 3

Use animated progress bar. Show spinner during polling. Add pulse animation to "Processing" text.
```

---

### Prompt 12: Upload Wizard - Step 3 (Review RuleSpec)

```
Create RuleSpec review and editing interface for wizard.

STEP 3: REVIEW & EDIT RULESPEC

LAYOUT:
- Top: Game metadata card (readonly)
- Bottom: Rule atoms list (editable)

METADATA CARD:
- Game ID, Version, Total Rules count
- Read-only display with icons

RULE ATOMS LIST:
- Scrollable container
- Each rule atom in a card:
  ```
  ┌─────────────────────────────────┐
  │ Rule 1                   [Delete]│
  │ ─────────────────────────────── │
  │ Text (textarea, expandable)     │
  │ ─────────────────────────────── │
  │ Section | Page | Line (inputs)  │
  └─────────────────────────────────┘
  ```

RULE ATOM FIELDS:
- Text (textarea, min-height 60px, auto-expand)
- Section (input, optional)
- Page (input, optional, number)
- Line (input, optional, number)
- Delete button (top-right, red, confirm modal)

ACTIONS:
- "Add Rule" button (bottom)
  - Adds new empty rule atom
  - Auto-scroll to new rule
- "Publish RuleSpec" button (primary, large)
  - Validate: at least 1 rule with text
  - API: PUT /games/{gameId}/rulespec
  - Body: entire RuleSpec JSON
  - On success → advance to Step 4
- "Back" button → return to Step 2
- "Cancel" button (red) → confirm modal → reset wizard

VALIDATION:
- Real-time validation on text field
- Show error if text empty
- Disable publish if any rule invalid

STATE:
```typescript
type RuleAtom = {
  id: string;
  text: string;
  section?: string;
  page?: string;
  line?: string;
};

type RuleSpec = {
  gameId: string;
  version: string;
  createdAt: string;
  rules: RuleAtom[];
};
```

Use drag-and-drop library (dnd-kit) for reordering rules. Add "Duplicate" button for each rule.
```

---

### Prompt 13: Upload Wizard - Step 4 (Success & Actions)

```
Create success screen and post-publish actions.

STEP 4: PUBLISHED SUCCESSFULLY

LAYOUT:
- Large success icon (green checkmark in circle)
- Title: "RuleSpec Published Successfully! 🎉"
- Subtitle: "Your game rules for {gameName} are now available"
- Metadata card (readonly):
  - Game ID
  - Version
  - Total rules
  - Published at (timestamp)

ACTIONS (Button group):
1. "Import Another PDF" (primary, blue)
   - Reset wizard to Step 1
   - Clear all state
   - Keep same game selected

2. "Edit in RuleSpec Editor" (secondary, green)
   - Navigate to /editor?gameId={gameId}

3. "View in Chat" (secondary, purple)
   - Navigate to /chat with game pre-selected

4. "Go to Dashboard" (tertiary, gray)
   - Navigate to /dashboard

ADDITIONAL FEATURES:
- "Share" button → copy link to game
- "Download JSON" button → download RuleSpec as file
- Confetti animation on page load (react-confetti)

NEXT STEPS CARD:
- Title: "What's Next?"
- Checklist:
  - ✅ PDF uploaded and parsed
  - ✅ RuleSpec published
  - [ ] Index PDF for semantic search (link to action)
  - [ ] Test chat with new game (link to /chat)
  - [ ] Create more rule versions (link to /editor)

INDEX PDF ACTION:
- Button: "Index PDF for Search"
- API: POST /ingest/pdf/{pdfId}/index
- Show loading + progress
- On success: show success toast

Use framer-motion for success animation. Add social share buttons (optional).
```

---

## RuleSpec Editor

### Prompt 14: RuleSpec Editor - JSON Editor with Preview

```
Create a professional JSON editor for RuleSpec with live preview.

PAGE: /editor?gameId={gameId}
- Protected route (Admin/Editor only)
- Two-column layout (50/50 split, resizable)

LEFT COLUMN: JSON EDITOR
- Monaco Editor (or CodeMirror) with:
  - Syntax highlighting for JSON
  - Line numbers
  - Auto-indentation
  - Bracket matching
  - Error highlighting
  - Autocomplete (basic)

EDITOR TOOLBAR:
- "Undo" button (Ctrl+Z)
- "Redo" button (Ctrl+Y)
- "Format JSON" button (prettify)
- "Validate" button (manual trigger)
- "Save" button (primary, Ctrl+S)

VALIDATION:
- Real-time JSON validation
- Show errors below toolbar:
  - ✓ JSON Valid (green)
  - ✗ Syntax error at line 42 (red)
- Schema validation against RuleSpec v0:
  - Required fields: gameId, version, createdAt, rules
  - Rules array validation
  - Type checking

RIGHT COLUMN: LIVE PREVIEW
- Rendered view of RuleSpec
- Updates on every valid JSON change (debounced 300ms)

PREVIEW SECTIONS:
1. METADATA (Card):
   - Game ID
   - Version
   - Created At
   - Total Rules

2. RULES LIST (Cards):
   - Each rule in a card:
     - Rule number + ID (header)
     - Text (body, formatted)
     - Metadata (section, page, line) - small text
   - Alternating background colors
   - Collapsible rules (click to expand/collapse)

HEADER:
- Page title: "RuleSpec Editor - {gameName}"
- "Version History" link (top-right)
- "Back to Dashboard" link

APIs:
- GET /games/{gameId}/rulespec - Load current RuleSpec
- PUT /games/{gameId}/rulespec - Save changes

STATE:
- jsonContent: string
- isValid: boolean
- validationError: string | null
- isSaving: boolean
- lastSaved: Date | null

Install: npm install @monaco-editor/react

Use split-pane library for resizable columns. Add keyboard shortcuts (Ctrl+S for save).
```

---

### Prompt 15: Version History & Diff Viewer

```
Create version history page with diff viewer.

PAGE: /versions?gameId={gameId}
- Protected route (Admin/Editor only)

LAYOUT:
- Left sidebar: Version list (30%)
- Right panel: Version details + diff (70%)

VERSION LIST (Sidebar):
- Title: "Version History"
- Scrollable list of versions:
  ```
  v1.2.0              [Current]
  2025-10-14 15:30
  by admin@meepleai.dev
  ───────────────────────────
  v1.1.0
  2025-10-13 12:00
  by editor@meepleai.dev
  ───────────────────────────
  v1.0.0
  2025-10-12 10:00
  by admin@meepleai.dev
  ```
- Click version → load details
- "Current" badge on latest version
- "Compare" checkbox on each version

VERSION DETAILS (Panel):
- Tabs: "Details" | "JSON View" | "Compare"

DETAILS TAB:
- Metadata card
- Rules list (preview, expandable)
- "Edit in Editor" button
- "Restore this Version" button (if not current)

JSON VIEW TAB:
- Read-only Monaco editor
- Syntax highlighting
- "Copy JSON" button
- "Download JSON" button

COMPARE TAB:
- Shown when 2 versions selected via checkboxes
- Dropdown: "From" and "To" version selectors
- "Generate Diff" button
- Diff viewer (side-by-side or unified):
  - Left: old version
  - Right: new version
  - Highlighted changes:
    - Green: added lines
    - Red: deleted lines
    - Yellow: modified lines
- Change summary card:
  - Total changes count
  - Rules added: X
  - Rules modified: Y
  - Rules deleted: Z

APIs:
- GET /games/{gameId}/rulespec/history - List all versions
- GET /games/{gameId}/rulespec/versions/{version} - Get specific version
- GET /games/{gameId}/rulespec/diff?from={v1}&to={v2} - Generate diff

DIFF COMPONENT:
Use react-diff-viewer-continued library for diff display.

Add "Export Diff Report" button (PDF or Markdown).
```

---

## Admin Dashboard

### Prompt 16: Admin Dashboard - Overview & Stats

```
Create an admin dashboard for monitoring AI requests and usage.

PAGE: /admin
- Protected route (Admin only)
- Dashboard layout with cards and charts

HEADER:
- Title: "Admin Dashboard"
- Date range picker (default: Last 7 days)
- "Export Data" button (CSV)
- "Refresh" button

STATS CARDS (Grid, 4 columns):
1. Total Requests
   - Large number
   - Trend indicator (↑ 12% vs last week)

2. Average Latency
   - Value in ms
   - Trend indicator

3. Total Tokens
   - Large number with K/M suffix
   - Cost estimate (optional)

4. Success Rate
   - Percentage with progress ring
   - Color: green if >95%, yellow if >90%, red otherwise

ENDPOINT BREAKDOWN (Card):
- Title: "Requests by Endpoint"
- Bar chart or donut chart:
  - QA: X requests (blue)
  - Explain: Y requests (orange)
  - Setup: Z requests (purple)
  - Chess: W requests (green)

FEEDBACK SUMMARY (Card):
- Title: "User Feedback"
- Stats:
  - 👍 Helpful: X (green)
  - 👎 Not helpful: Y (red)
  - Total feedback: X + Y
- Feedback rate: (X+Y) / total requests
- Chart: helpful vs not helpful over time

RECENT ACTIVITY (Card):
- Title: "Recent Requests"
- Simplified table (last 10 requests):
  - Timestamp, Endpoint, User, Game, Latency, Status
- "View All" link → goes to request logs

APIs:
- GET /admin/stats?startDate={}&endDate={} - Aggregate stats
- GET /admin/requests?limit=10 - Recent requests

Use Recharts library for charts. Add real-time updates (polling every 30s).
```

---

### Prompt 17: Admin Dashboard - Request Logs

```
Create detailed request logs viewer with advanced filtering.

PAGE: /admin/logs
- Protected route (Admin only)
- Table layout with filters

FILTERS (Top bar):
- Search box: Filter by query text, user ID, game ID
- Endpoint dropdown: All | QA | Explain | Setup | Chess
- Status dropdown: All | Success | Error
- Date range picker
- "Reset Filters" button
- "Export CSV" button

REQUEST TABLE (Full width, scrollable):
Columns:
- Timestamp (sortable)
- Endpoint (badge with color)
- Game ID (truncated, tooltip on hover)
- User (email or ID)
- Query (truncated, click to expand modal)
- Latency (ms, color-coded: green <500, yellow <1000, red ≥1000)
- Tokens (Prompt + Completion = Total)
- Confidence (0-1, show as percentage)
- Model (e.g., "gpt-4")
- Status (badge: green for success, red for error)
- Actions:
  - "View Details" icon (eye)
  - "Copy" icon (clipboard)

PAGINATION:
- Show X-Y of Z results
- Page size selector: 10, 25, 50, 100
- Previous/Next buttons

REQUEST DETAILS MODAL:
- Trigger: Click "View Details" on row
- Tabs: Request | Response | Metadata

REQUEST TAB:
- Endpoint
- Timestamp
- User info
- IP address
- User agent
- Request body (JSON formatted)

RESPONSE TAB:
- Answer text (full)
- Snippets (if any)
- Token breakdown
- Confidence score

METADATA TAB:
- Model used
- Finish reason
- Latency breakdown (if available)
- Error message (if failed)

APIs:
- GET /admin/requests?limit=100&offset=0&endpoint=&userId=&gameId=&startDate=&endDate=

Use shadcn/ui: Table, Badge, Dialog, DateRangePicker, Pagination
```

---

### Prompt 18: Admin Dashboard - n8n Configuration

```
Create n8n workflow configuration management page.

PAGE: /admin/n8n
- Protected route (Admin only)

LAYOUT:
- List of configurations (left, 40%)
- Configuration details (right, 60%)

CONFIGURATION LIST:
- Add New Config button (top)
- Cards for each config:
  ```
  Production n8n        [Active]
  https://n8n.prod...
  Last tested: 2 min ago ✓
  ───────────────────────────
  [Edit] [Delete] [Test]
  ```
- Active badge (green)
- Last test result (✓ or ✗)

ADD/EDIT CONFIGURATION (Modal):
Fields:
- Name (required)
- Base URL (required, validated URL)
- API Key (password field, required)
- Webhook URL (optional, validated URL)
- Is Active (toggle)

Buttons:
- "Test Connection" (validate before save)
- "Save" (primary)
- "Cancel"

CONFIGURATION DETAILS (Panel):
When config selected from list:

TABS: Info | Webhooks | Logs

INFO TAB:
- Name
- Base URL (with "Open" link icon)
- API Key (masked: ••••••••, "Reveal" button)
- Webhook URL (with "Copy" button)
- Status (Active/Inactive toggle)
- Created at
- Last tested at
- Last test result (success/error message)

WEBHOOKS TAB:
- Title: "Available Webhooks"
- List of webhook endpoints:
  - POST /webhooks/n8n/{configId}/chess
  - POST /webhooks/n8n/{configId}/qa
  - POST /webhooks/n8n/{configId}/explain
- Copy button for each
- Test webhook button

LOGS TAB (Future):
- Webhook execution logs
- Request/response history

ACTIONS:
- "Test Connection" button
  - API: POST /admin/n8n/{configId}/test
  - Show loading spinner
  - Display result (toast)
- "Edit" button → open edit modal
- "Delete" button → confirmation dialog
  - API: DELETE /admin/n8n/{configId}

APIs:
- GET /admin/n8n - List configs
- GET /admin/n8n/{configId} - Get config details
- POST /admin/n8n - Create config
- PUT /admin/n8n/{configId} - Update config
- DELETE /admin/n8n/{configId} - Delete config
- POST /admin/n8n/{configId}/test - Test connection

Use masked input for API key. Add webhook payload examples (expandable code blocks).
```

---

## Advanced Features

### Prompt 19: Chess Chat Interface (CHESS-05)

```
Create specialized chess chat interface with chessboard visualization.

PAGE: /chess-chat
- Requires authentication
- Full-screen layout

LAYOUT:
┌────────────────┬──────────────────┐
│   Chessboard   │   Chat Panel     │
│   (50%)        │   (50%)          │
│                │                  │
│  - Board       │  - Messages      │
│  - Controls    │  - Snippets      │
│                │  - Input         │
└────────────────┴──────────────────┘

LEFT PANEL: CHESSBOARD
- Use react-chessboard library (npm install react-chessboard)
- Board orientation toggle (white/black)
- FEN position input (below board):
  - Text input (monospace)
  - "Load Position" button
  - "Reset to Start" button
  - "Copy FEN" button
- Position info card:
  - Current turn (White/Black)
  - Castling rights
  - En passant square

CHESSBOARD FEATURES:
- Draggable pieces (edit position mode)
- Legal move highlighting
- Last move highlighting
- Check/checkmate indicators
- Piece sprites (choose style: classic, modern, etc.)

RIGHT PANEL: CHAT
- Reuse chat components from /chat
- Agent: Chess Agent (fixed)
- Messages include:
  - Question + answer
  - Suggested moves (as chips/pills)
  - Position analysis (expandable card)
  - Sources from chess knowledge base

SUGGESTED MOVES:
- Display as clickable chips
- Click move → highlight on board
- Show move notation (e.g., "e4", "Nf3")

POSITION ANALYSIS CARD:
- Evaluation score (e.g., "+0.3")
- Key themes (list: "center control", "piece development")
- Tactical motifs (if any)

SAMPLE INTERACTION:
```
User types FEN: rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1
User clicks "Load Position" → board updates
User asks: "What are the best opening moves?"

Assistant replies:
"In the starting position, the most popular moves are:
e4, d4, Nf3, c4

These moves fight for control of the center..."

Suggested moves shown as chips:
[e4] [d4] [Nf3] [c4]

User clicks [e4] → board shows e4 highlighted
```

APIs:
- POST /agents/chess - Send question + FEN position
- GET /chess/search?q={query} - Search chess knowledge

Install: npm install react-chessboard chess.js

Use chess.js library for move validation and FEN parsing.
```

---

### Prompt 20: Dark Mode & Accessibility

```
Implement dark mode and accessibility features across the application.

DARK MODE:
- Use Tailwind CSS dark mode (class strategy)
- Toggle in header (sun/moon icon)
- Persist preference in localStorage
- Smooth transition animation (200ms)

THEME PROVIDER:
```typescript
// contexts/ThemeContext.tsx
type Theme = 'light' | 'dark';

const ThemeContext = createContext<{
  theme: Theme;
  toggleTheme: () => void;
}>({ theme: 'light', toggleTheme: () => {} });

export const ThemeProvider = ({ children }) => {
  const [theme, setTheme] = useState<Theme>('light');

  useEffect(() => {
    const saved = localStorage.getItem('theme') as Theme;
    if (saved) setTheme(saved);
  }, []);

  const toggleTheme = () => {
    const newTheme = theme === 'light' ? 'dark' : 'light';
    setTheme(newTheme);
    localStorage.setItem('theme', newTheme);
    document.documentElement.classList.toggle('dark');
  };

  return (
    <ThemeContext.Provider value={{ theme, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
};
```

DARK MODE COLORS:
Light:
- Background: white
- Foreground: black
- Border: #e5e7eb
- Card: #f9fafb

Dark:
- Background: #0f172a
- Foreground: white
- Border: #334155
- Card: #1e293b

ACCESSIBILITY (a11y):
- ARIA labels on all interactive elements
- Keyboard navigation (Tab, Enter, Escape)
- Focus indicators (visible outline)
- Skip to main content link
- Screen reader announcements for dynamic content
- Color contrast WCAG AA (4.5:1 for text)

KEYBOARD SHORTCUTS:
- Ctrl/Cmd + K: Search/Command palette (future)
- Ctrl/Cmd + D: Toggle dark mode
- Esc: Close modals
- Enter: Submit forms
- Tab: Navigate between fields
- Arrow keys: Navigate lists

FOCUS MANAGEMENT:
- Trap focus in modals
- Return focus to trigger element on close
- Skip navigation link (jump to main content)

SCREEN READER:
- Use semantic HTML (header, nav, main, aside, footer)
- ARIA landmarks
- Live regions for dynamic updates (aria-live)
- Descriptive button labels (avoid "Click here")

RUN AUDITS:
- Lighthouse accessibility audit (aim for 90+ score)
- Axe DevTools browser extension
- Manual keyboard navigation testing
- Test with screen reader (NVDA/JAWS)

Add toggle: "High Contrast Mode" (optional, for low vision users).
```

---

### Prompt 21: Progressive Web App (PWA)

```
Convert MeepleAI to a Progressive Web App.

PWA FEATURES:
1. Service Worker for offline caching
2. Install prompt for mobile/desktop
3. App manifest with icons
4. Offline fallback page

MANIFEST (public/manifest.json):
```json
{
  "name": "MeepleAI - Board Game Rules Assistant",
  "short_name": "MeepleAI",
  "description": "AI-powered assistant for board game rules",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#0f172a",
  "theme_color": "#0070f3",
  "orientation": "portrait-primary",
  "icons": [
    {
      "src": "/icons/icon-192.png",
      "sizes": "192x192",
      "type": "image/png",
      "purpose": "any maskable"
    },
    {
      "src": "/icons/icon-512.png",
      "sizes": "512x512",
      "type": "image/png",
      "purpose": "any maskable"
    }
  ]
}
```

SERVICE WORKER (public/sw.js):
- Cache static assets (JS, CSS, images)
- Cache API responses (with expiration)
- Network-first strategy for API calls
- Cache-first strategy for static assets
- Offline fallback page

INSTALL PROMPT:
- Show custom install banner (dismissible)
- Only show if not already installed
- Trigger on beforeinstallprompt event
- "Install App" button in header dropdown

OFFLINE DETECTION:
- Show banner when offline: "You're offline. Some features may be limited."
- Disable features that require network (chat, upload)
- Cache recent chats for offline viewing

PUSH NOTIFICATIONS (Optional):
- Ask permission after user interaction
- Notify when new messages in chat (if supported)
- Notify when PDF processing complete

WORKBOX (recommended):
Install: npm install workbox-webpack-plugin
Use Workbox for easier service worker management.

Use vite-plugin-pwa for Vite-based projects.
```

---

### Prompt 22: Error Boundaries & Error Pages

```
Implement comprehensive error handling and custom error pages.

ERROR BOUNDARY (components/ErrorBoundary.tsx):
```typescript
class ErrorBoundary extends React.Component<
  { children: React.ReactNode },
  { hasError: boolean; error: Error | null }
> {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error) {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    // Log to error tracking service (e.g., Sentry)
    console.error('Error caught by boundary:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <ErrorFallback
          error={this.state.error}
          resetError={() => this.setState({ hasError: false, error: null })}
        />
      );
    }

    return this.props.children;
  }
}
```

ERROR FALLBACK (components/ErrorFallback.tsx):
- Large error icon (red)
- Title: "Oops! Something went wrong"
- Error message (technical details, collapsible)
- "Try Again" button (reload page)
- "Go Home" button (navigate to /)
- "Report Issue" button (open GitHub issue with pre-filled template)

404 PAGE (pages/NotFound.tsx):
- Illustration: Lost robot or empty board
- Title: "Page Not Found"
- Subtitle: "The page you're looking for doesn't exist"
- "Go Home" button
- "Search" input (search games, optional)

403 FORBIDDEN PAGE (pages/Forbidden.tsx):
- Lock icon
- Title: "Access Denied"
- Message: "You don't have permission to access this page"
- "Go Back" button
- "Contact Admin" button

500 ERROR PAGE (pages/ServerError.tsx):
- Server error illustration
- Title: "Server Error"
- Message: "We're experiencing technical difficulties"
- "Retry" button
- "Status Page" link (if available)

NETWORK ERROR HANDLING:
- Detect offline state
- Show toast: "Connection lost. Retrying..."
- Auto-retry failed requests (exponential backoff)
- Queue mutations while offline (sync when back online)

API ERROR RESPONSES:
- 400 Bad Request: Show validation errors in form
- 401 Unauthorized: Redirect to /auth
- 403 Forbidden: Show forbidden page
- 404 Not Found: Show "Resource not found" message
- 429 Rate Limited: Show "Too many requests. Please wait X seconds"
- 500 Server Error: Show server error page
- Network error: Show "Connection failed" toast

Use react-error-boundary library for easier setup.
```

---

### Prompt 23: Performance Optimization

```
Implement performance optimizations for better UX.

CODE SPLITTING:
- Lazy load routes with React.lazy()
- Lazy load heavy components (Monaco Editor, Chessboard)
- Use React.Suspense with loading fallbacks

Example:
```typescript
const ChatPage = lazy(() => import('./pages/ChatPage'));
const AdminDashboard = lazy(() => import('./pages/AdminDashboard'));

<Suspense fallback={<LoadingSpinner />}>
  <Routes>
    <Route path="/chat" element={<ChatPage />} />
    <Route path="/admin" element={<AdminDashboard />} />
  </Routes>
</Suspense>
```

IMAGE OPTIMIZATION:
- Use WebP format with fallback
- Lazy load images (Intersection Observer)
- Responsive images (srcset)
- Compress images (TinyPNG)

MEMOIZATION:
- useMemo for expensive computations
- useCallback for event handlers
- React.memo for pure components

Example:
```typescript
const filteredRequests = useMemo(() => {
  return requests.filter(req =>
    req.query.toLowerCase().includes(filter.toLowerCase())
  );
}, [requests, filter]);

const handleSubmit = useCallback((e) => {
  e.preventDefault();
  // submit logic
}, [dependencies]);
```

DEBOUNCING & THROTTLING:
- Debounce search inputs (300ms)
- Throttle scroll handlers (100ms)
- Debounce window resize handlers (200ms)

Install: npm install lodash-es

VIRTUAL SCROLLING:
- Use for long lists (>100 items)
- Install: npm install react-window
- Apply to: Request logs table, chat message list, version history

PREFETCHING:
- Prefetch next page data (pagination)
- Prefetch on hover (navigation links)
- Preload critical resources

BUNDLE OPTIMIZATION:
- Tree shaking (remove unused code)
- Minification (production build)
- Gzip/Brotli compression
- Code splitting by route
- Analyze bundle size (webpack-bundle-analyzer)

CACHING:
- Use React Query for server state
- Cache API responses (stale-while-revalidate)
- Cache images in Service Worker

Install: npm install @tanstack/react-query

WEB VITALS:
- Monitor LCP (Largest Contentful Paint): <2.5s
- Monitor FID (First Input Delay): <100ms
- Monitor CLS (Cumulative Layout Shift): <0.1

Use Lighthouse for performance audits. Aim for 90+ score.
```

---

### Prompt 24: Testing Setup

```
Set up comprehensive testing infrastructure.

UNIT TESTS (Vitest + React Testing Library):
Install:
```bash
npm install -D vitest @testing-library/react @testing-library/jest-dom @testing-library/user-event
```

Config (vitest.config.ts):
```typescript
export default defineConfig({
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      exclude: ['node_modules/', 'src/test/'],
      thresholds: {
        lines: 80,
        functions: 80,
        branches: 80,
        statements: 80
      }
    }
  }
});
```

EXAMPLE COMPONENT TEST:
```typescript
// components/__tests__/GameSelector.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GameSelector } from '../GameSelector';

describe('GameSelector', () => {
  it('loads and displays games', async () => {
    render(<GameSelector value="" onChange={() => {}} />);

    await waitFor(() => {
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });
  });

  it('calls onChange when game selected', async () => {
    const handleChange = vi.fn();
    render(<GameSelector value="" onChange={handleChange} />);

    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(screen.getByText('Chess'));

    expect(handleChange).toHaveBeenCalledWith('demo-chess');
  });
});
```

E2E TESTS (Playwright):
Install:
```bash
npm install -D @playwright/test
npx playwright install
```

Config (playwright.config.ts):
```typescript
export default defineConfig({
  testDir: './e2e',
  use: {
    baseURL: 'http://localhost:5173',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  webServer: {
    command: 'npm run dev',
    port: 5173,
    reuseExistingServer: !process.env.CI
  }
});
```

EXAMPLE E2E TEST:
```typescript
// e2e/auth.spec.ts
import { test, expect } from '@playwright/test';

test('user can login and access chat', async ({ page }) => {
  await page.goto('/auth');

  await page.fill('input[name="email"]', 'user@meepleai.dev');
  await page.fill('input[name="password"]', 'Demo123!');
  await page.click('button[type="submit"]');

  await expect(page).toHaveURL('/dashboard');

  await page.click('a[href="/chat"]');
  await expect(page).toHaveURL('/chat');

  const heading = page.locator('h1');
  await expect(heading).toContainText('Chat');
});
```

INTEGRATION TESTS:
- Mock API calls with MSW (Mock Service Worker)
- Test user flows (login → chat → send message)
- Test error scenarios (network error, 401, 500)

API MOCKING (MSW):
Install: npm install -D msw

Setup:
```typescript
// src/test/mocks/handlers.ts
import { rest } from 'msw';

export const handlers = [
  rest.get('/games', (req, res, ctx) => {
    return res(ctx.json([
      { id: 'chess', name: 'Chess', createdAt: '2025-01-01' }
    ]));
  }),

  rest.post('/auth/login', (req, res, ctx) => {
    return res(ctx.json({
      user: { id: '1', email: 'test@example.com', role: 'User' },
      expiresAt: '2025-12-31'
    }));
  })
];
```

Run tests:
```bash
npm run test              # Unit tests
npm run test:coverage     # Coverage report
npm run test:e2e          # E2E tests
npm run test:e2e:ui       # E2E with UI
```

Add pre-commit hook to run tests (Husky).
```

---

## Implementation Strategy

### Phase 1: Foundation (Week 1-2)
1. Project Setup & Foundation (Prompts 1-2)
2. Authentication System (Prompts 3-4)
3. Core UI Components (Prompts 5-6)

### Phase 2: Core Features (Week 3-4)
4. Chat Interface (Prompts 7-9)
5. PDF Upload Wizard (Prompts 10-13)

### Phase 3: Advanced Features (Week 5-6)
6. RuleSpec Editor (Prompts 14-15)
7. Admin Dashboard (Prompts 16-18)

### Phase 4: Polish & Optimization (Week 7-8)
8. Chess Chat Interface (Prompt 19)
9. Dark Mode & Accessibility (Prompt 20)
10. PWA & Error Handling (Prompts 21-22)
11. Performance & Testing (Prompts 23-24)

---

## Best Practices for Lovable.dev

1. **Be Specific**: Include exact component names, file paths, and API endpoints
2. **Provide Context**: Explain the feature's purpose and user flow
3. **Show Examples**: Include TypeScript types and example API responses
4. **Reference Libraries**: Specify npm packages to use (shadcn/ui, Lucide icons)
5. **Describe UX**: Explain loading states, error handling, animations
6. **Iterate**: Start with basic version, then enhance in follow-up prompts
7. **Test**: Include testing requirements in prompts

## Notes

- Each prompt is designed to be self-contained but builds on previous work
- Adjust prompts based on Lovable.dev's capabilities and limitations
- Test each feature thoroughly before moving to the next
- Maintain consistent design language across all components
- Keep accessibility and performance in mind from the start

---

**Happy Building! 🎲**
