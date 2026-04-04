# Dashboard "Il Tavolo" — Design Spec

**Date**: 2026-03-23
**Status**: Approved
**Scope**: `/dashboard` page — rewrite completo

## Vision

Pagina post-login dell'utente. Layout 2 colonne: Tavolo (main) + Sidebar (280px). Tema warm/glassmorphism attuale migliorato. Componenti MeepleCard reali con CoverOverlay 4-corner e ManaLinkFooter.

## Decisions Log

| Decision | Choice |
|----------|--------|
| Scope | Solo `/dashboard` |
| Sections | Tutte (sessioni con empty state per Alpha) |
| Theme | Warm/glassmorphism attuale |
| Layout | 2 colonne: Tavolo SX + Sidebar DX (280px) |
| Column order | Hero → Stats → Sessions → Games → Agents |
| Sidebar style | 3 sezioni separate: Agenti + Chat + Attività |
| Game cards | MeepleCard Grid + CoverOverlay 4-corner + ManaLinkFooter |
| Hero (session active) | Banner con gioco, durata, CTA "Riprendi" |
| Hero (no session) | Saluto time-aware + data |
| Implementation | Rewrite completo (approach B) |

## Routing & Navigation

### Post-Login Landing

`/dashboard` è la prima pagina dopo il login. Modifiche necessarie:

| File | Modifica |
|------|----------|
| `components/auth/AuthModal.tsx` (line 58) | `redirectTo = '/dashboard'` (era `/library`) |
| `components/auth/AuthModal.tsx` (lines 111, 159) | Verificare che il redirect default punti a `/dashboard` |

Nota: Gli admin continuano a essere redirected a `/admin` tramite `isAdminRole()`.

### Navbar Integration

Il nav attuale ha 4 tab: `home | library | play | chat`. La dashboard diventa il tab `home`.

| File | Modifica |
|------|----------|
| `components/layout/UserShell/UserDesktopSidebar.tsx` | Tab `home` → href `/dashboard` |
| `components/layout/UserShell/UserTabBar.tsx` | Tab `home` → href `/dashboard` |
| `lib/stores/navStore.ts` | Aggiungere mapping `home` → `/dashboard` |
| `hooks/useNavigation.ts` | Aggiungere `dashboard` a `SEGMENT_LABELS` per breadcrumbs |

### Route

Creare `apps/web/src/app/(authenticated)/dashboard/page.tsx` come nuova route.

## Component Architecture

```
/dashboard (page.tsx) — server component
└── DashboardClient — client component
    ├── <main> Tavolo (flex-1)
    │   ├── HeroZone
    │   ├── QuickStats
    │   ├── ActiveSessions
    │   ├── RecentGames
    │   └── YourAgents
    └── <aside> Sidebar (w-[280px])
        ├── RecentAgentsSidebar
        ├── RecentChatsSidebar
        └── RecentActivitySidebar
```

## Files

### Create

| File | Purpose |
|------|---------|
| `apps/web/src/app/(authenticated)/dashboard/page.tsx` | Route entry (server component) |
| `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx` | Client orchestrator |
| `apps/web/src/components/dashboard/v2/HeroZone.tsx` | Hero banner (contextual) |
| `apps/web/src/components/dashboard/v2/QuickStats.tsx` | 4 KPI stat cards |
| `apps/web/src/components/dashboard/v2/ActiveSessions.tsx` | Session cards or empty state |
| `apps/web/src/components/dashboard/v2/RecentGames.tsx` | Game MeepleCard grid |
| `apps/web/src/components/dashboard/v2/YourAgents.tsx` | Agent MeepleCard grid + CTA |
| `apps/web/src/components/dashboard/v2/RecentAgentsSidebar.tsx` | Sidebar agents list |
| `apps/web/src/components/dashboard/v2/RecentChatsSidebar.tsx` | Sidebar chat threads |
| `apps/web/src/components/dashboard/v2/RecentActivitySidebar.tsx` | Sidebar activity timeline |
| `apps/web/src/components/dashboard/v2/index.ts` | Barrel export |

### Remove (after migration)

| File | Reason |
|------|--------|
| `apps/web/src/app/(authenticated)/gaming-hub-client.tsx` | Replaced by dashboard-client.tsx |
| Old dashboard components no longer referenced | Cleanup |

## Section Specs

### 1. HeroZone

**Props**: `userName: string`, `activeSession?: ActiveSessionDto`

**Logic**:
- `activeSession` exists → Banner: game name, duration, players, CTA "Riprendi" → navigate to session
- No session → "Buongiorno/Buon pomeriggio/Buonasera, {name}" + date string

**Style**:
- `background: linear-gradient(135deg, rgba(210,105,30,0.06), rgba(210,105,30,0.02))`
- `border: 1px solid rgba(210,105,30,0.10)`
- `border-radius: 16px`, `padding: 24px 28px`, `backdrop-filter: blur(8px)`
- Title: `font-quicksand font-bold text-2xl`
- Date: `text-muted-foreground text-sm`

**Loading**: Skeleton shimmer matching hero dimensions.

**Error**: Falls back to greeting-only (no session banner) on API failure.

### 2. QuickStats

**Props**: `stats: UserStatsDto`

**4 cards**: Giochi totali | Partite/mese | Tempo/settimana | Preferiti

**Style**: StatCard glassmorphism:
- `background: rgba(255,255,255,0.75)`, `backdrop-filter: blur(12px)`
- `border: 1px solid rgba(200,180,160,0.20)`, `border-radius: 12px`
- `box-shadow: 0 2px 12px rgba(180,120,60,0.06)`
- Value: `font-quicksand font-bold text-[28px] text-primary`
- Label: `text-[11px] font-semibold uppercase tracking-wide text-muted-foreground`
- Hover: `translateY(-2px)` + `shadow-warm-md`

**Responsive**: `grid-cols-4` → `grid-cols-2` (mobile)

**Loading**: 4 skeleton cards (same size, pulsing `bg-[rgba(200,180,160,0.20)]`).

**Error**: Show "—" as value on API failure (silent degradation).

**API**: `GET /api/v1/users/me/stats`

### 3. ActiveSessions

**Props**: `sessions: SessionSummaryDto[]`

**Note**: In Alpha, SessionTracking is dormant — this section will show empty state. The current `GET /api/v1/sessions/recent` endpoint returns completed `PlayRecords` only (no status field in `SessionSummaryDto`). A future backend change will be needed to support live/active session filtering. For now, this section always renders the empty state.

**With sessions** (future): MeepleCard `entity="session"` grid with:
- `sessionStatus` badge
- `sessionPlayers` display
- Green accent border
- Click → navigate to session

**Empty state**:
- Dashed border box: `border: 1.5px dashed var(--border)`, `border-radius: 14px`
- Text: "Nessuna partita in corso"
- CTA: "Inizia a giocare →" in primary color

**Loading**: Skeleton placeholder matching empty state height.

**Error**: Silent — fall back to empty state on API failure.

**API**: `GET /api/v1/sessions/recent` (currently returns only completed sessions; active filtering TBD)

### 4. RecentGames

**Props**: `games: UserGameDto[]`, `limit: 6`

**MeepleCard Grid** `entity="game"` with:
- `coverLabels`: `[{ text: game.title, primary: true }]`
- `subtypeIcons`: Mechanic icons from BGG data — `[{ icon: '⚙️', tooltip: 'Engine Building' }]`
- `stateLabel`: `{ text: 'Giocato' | 'Nuovo' | 'Preferito', variant: 'success' | 'info' | 'warning' }`
- `linkedEntities`: Array of `LinkedEntityInfo` (`{ entityType: MeepleEntityType, count: number }`):
  - `{ entityType: 'agent', count: 1 }` — if game has agent
  - `{ entityType: 'kb', count: n }` — if game has indexed docs (count = number of docs)
  - `{ entityType: 'session', count: n }` — if game has sessions
  - `{ entityType: 'chatSession', count: n }` — if game has chat threads
  - Icons/colors derived automatically by ManaSymbol from `entityType`
- `metadata`: `[{ icon: Users, label: '3-4' }, { icon: Clock, label: '60-120min' }, { icon: Star, label: '7.2' }]`

**Header**: Section title "📚 Giochi Recenti" + link "Vedi tutti →" → `/library`

**Responsive**: `grid-cols-3` → `grid-cols-2` (tablet) → `grid-cols-1` (mobile)

**Loading**: MeepleCard skeleton grid (3 items) via `loading={true}` prop.

**Error**: Show "Impossibile caricare i giochi" toast + empty grid.

**API**: `GET /api/v1/users/me/games?pageSize=6&page=1&sort=lastPlayed`

### 5. YourAgents

**Props**: `agents: AgentSummaryDto[]`, `limit: 5`

**MeepleCard Grid** `entity="agent"` with:
- Avatar: ManaSymbol (⚡ with radial gradient + glow)
- `agentStatus`: `'active' | 'idle' | 'training' | 'error'` (matches `AgentStatus` type)
- `agentStats`: `{ invocationCount: number, lastExecutedAt?: Date | string, avgResponseTimeMs?: number }` (matches `AgentStats` type)
- Last card slot: CTA dashed card "Crea agente" → agent wizard

**Header**: Section title "🤖 I Tuoi Agenti" + link "Gestisci →" → `/agents`

**Responsive**: `grid-cols-3` → `grid-cols-2` (tablet) → `grid-cols-1` (mobile)

**Loading**: MeepleCard skeleton grid (3 items) via `loading={true}` prop.

**Error**: Show "Impossibile caricare gli agenti" toast + CTA-only card.

**API**: `GET /api/v1/agents/recent` — limit 5

### 6. Sidebar — RecentAgentsSidebar

**Props**: `agents: AgentSummaryDto[]`, `limit: 3`

**Style**: Compact list items:
- Avatar: ManaSymbol mini (⚡, 30px), entity gradient background
- Name: `text-xs font-semibold`
- Timestamp: `text-[10px] text-muted-foreground`
- Status dot: 8px circle, green = ready
- Item bg: `rgba(255,255,255,0.5)`, `backdrop-filter: blur(4px)`, `border-radius: 10px`
- Hover: `rgba(255,255,255,0.75)` + `shadow-warm-sm`

**Loading**: 2 skeleton rows (30px avatar + text shimmer).

**Error**: Silent — hide section on failure.

**Section title**: "⚡ Agenti Recenti" in `hsl(38 92% 50%)` (agent color)

### 7. Sidebar — RecentChatsSidebar

**Props**: `threads: ChatThreadDto[]`, `limit: 4`

**Style**: Compact rows:
- Title: `text-xs font-semibold`
- Meta: `text-[10px] text-muted-foreground` — "{n} msg · con {agent} · {time}"
- Same bg/hover as agent sidebar items

**Section title**: "💬 Chat Recenti" in `hsl(220 80% 55%)` (chat color)

**Loading**: 3 skeleton rows (title + meta shimmer).

**Error**: Silent — hide section on failure.

**API**: `GET /api/v1/chat-threads/my` — limit 4, sort by last_message

### 8. Sidebar — RecentActivitySidebar

**Props**: `activities: ActivityTimelineDto[]`, `limit: 5`

**Style**: Minimal timeline:
- Mana symbol icon per event type: 📜 (KB), 🎲 (Game), ⚡ (Agent), ⏳ (Session)
- Entity name highlighted in entity color
- Timestamp: relative ("5 min fa", "ieri")
- `text-[11px] text-muted-foreground`
- Separator: `border-bottom: 1px solid rgba(200,180,160,0.12)`

**Section title**: "📋 Attività Recenti" in `text-muted-foreground`

**Loading**: 4 skeleton lines (icon + text shimmer).

**Error**: Silent — hide section on failure.

**API**: `GET /api/v1/dashboard/activity-timeline` — skip=0, take=5 (NOT `/api/v1/activity/timeline`)

## Data Flow

```
DashboardClient — fetches all data, passes as props to children
├── useDashboardStore() — Zustand (shared state)
│   ├── fetchStats()           → GET /api/v1/users/me/stats
│   │   └── consumed by: QuickStats
│   ├── fetchRecentSessions()  → GET /api/v1/sessions/recent
│   │   └── consumed by: ActiveSessions
│   └── fetchGames()           → GET /api/v1/users/me/games?pageSize=6&page=1&sort=lastPlayed
│       └── consumed by: RecentGames
├── dashboardClient (API client)
│   ├── getRecentAgents()      → GET /api/v1/agents/recent
│   │   └── consumed by: YourAgents + RecentAgentsSidebar (shared data)
│   └── getActivityTimeline()  → GET /api/v1/dashboard/activity-timeline (skip/take, max 100)
│       └── consumed by: RecentActivitySidebar
│       ⚠️ NOTE: Use /api/v1/dashboard/activity-timeline, NOT /api/v1/activity/timeline (different pagination contract)
└── chatClient (API client)
    └── getMyThreads()         → GET /api/v1/chat-threads/my
        └── consumed by: RecentChatsSidebar
```

No new backend endpoints needed. All APIs exist. The only future backend work is adding active session support to `SessionSummaryDto` (see Section 3).

## Responsive Breakpoints

| Viewport | Tavolo | Sidebar |
|----------|--------|---------|
| Desktop (>1024px) | Flex-1, grids 3 col | 280px fixed |
| Tablet (640-1024px) | Full width, grids 2 col | Collapses below tavolo |
| Mobile (<640px) | Full width, grids 1 col | Below tavolo |

## Design Tokens Reference

**Colors**: `hsl(25 95% 38%)` primary, entity colors from `meeple-card-styles.ts`

**Typography**: Quicksand 700 (headings), Inter 400-600 (body)

**Shadows**: `--shadow-warm-sm/md/lg/xl` from globals.css

**Glass**: `rgba(255,255,255,0.75)`, `backdrop-filter: blur(12px) saturate(180%)`

**Radius**: 16px cards, 12px stat cards, 14px empty states

**Texture**: `--texture-parchment` on MeepleCards

## Out of Scope

- Dark mode adjustments (follows existing system)
- Gamification (achievements, badges)
- Social features
- Notifiche push
- SSE real-time updates (can be added later)
- Personal Library / Public Library / Agent page (separate specs)
