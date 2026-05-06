# Page Catalog with Screenshots -- Design Spec

**Date**: 2026-03-22
**Status**: Approved
**Audience**: Stakeholder, utenti, team di sviluppo

## Objective

Documentare tutte le 209 pagine del frontend MeepleAI con screenshot automatici, producendo un sito HTML statico navigabile. Il sistema e' rieseguibile e incrementale.

## Architecture Overview

```
Filesystem scan ──► manifest.json ──► capture.ts (Playwright) ──► screenshots/
                                  ──► generate.ts ──► dist/index.html
```

Three decoupled components (all in `capture.ts`, route discovery is a module/phase within capture, not a separate script):
1. **Route Discovery** -- scans `apps/web/src/app/**/page.tsx`, builds manifest
2. **Capture Engine** -- Playwright crawls staging, captures screenshots
3. **HTML Generator** -- reads manifest + screenshots, outputs static site

## 1. Route Discovery & Manifest

### Source
All Next.js pages at `apps/web/src/app/**/page.tsx` (209 pages as of 2026-03-22).

### Route Groups (9)

| Group | Pages | Auth Required | Description |
|-------|-------|---------------|-------------|
| `public` | 25 | No | Landing, catalog, legal pages |
| `auth` | 10 | No | Login, register, verify flows |
| `authenticated` | 90 | Yes (user) | User-facing app features |
| `admin` | 75 | Yes (admin) | Admin dashboard |
| `chat` | 4 | Yes (user) | AI chat interface |
| `dev` | 2 | No | Component showcase |
| `docs` | 1 | No | RAG documentation |
| `join` | 1 | No | Invite acceptance |
| `offline` | 1 | No | Offline fallback |

### Manifest Schema (`docs/pages/manifest.json`)

```json
{
  "generatedAt": "ISO-8601 timestamp",
  "baseUrl": "https://meepleai.app",
  "stats": {
    "total": 209,
    "captured": 200,
    "errors": 9,
    "groups": 9
  },
  "pages": [
    {
      "id": "public-home",
      "route": "/",
      "group": "public",
      "title": "Home",
      "description": "Landing page principale",
      "screenshot": "screenshots/public/home.png",
      "requiresAuth": false,
      "requiresAdmin": false,
      "params": null,
      "status": "ok",
      "capturedAt": "ISO-8601 timestamp",
      "error": null
    }
  ]
}
```

### Dynamic Route Parameters

Pages with dynamic segments (`[id]`, `[gameId]`, etc.) require real IDs from staging. The capture script resolves these via API calls before crawling:

| Parameter | API Endpoint | Strategy |
|-----------|-------------|----------|
| `[gameId]` (shared) | `GET /api/v1/shared-games?limit=1` | First available |
| `[gameId]` (library) | `GET /api/v1/library?limit=1` | First in user library |
| `[privateGameId]` | `GET /api/v1/library/private?limit=1` | First private game |
| `[id]` (agent) | `GET /api/v1/agents?limit=1` | First agent |
| `[id]` (player) | `GET /api/v1/players?limit=1` | First player |
| `[id]` (session) | `GET /api/v1/sessions?limit=1` | First session |
| `[id]` (game-night) | `GET /api/v1/game-nights?limit=1` | First game night |
| `[threadId]` | `GET /api/v1/chat-threads?limit=1` | First chat thread |
| `[id]` (playlist) | `GET /api/v1/playlists?limit=1` | First playlist |
| `[token]` (shared playlist) | Derived from playlist | Share token |
| `[inviteToken]` | Skip or use test token | Best effort |
| `[id]` (admin entities) | Same as above per entity | First available |
| `[component]` (showcase) | Hardcoded known component | Static value |

If an API call fails or returns empty, the page is marked `"status": "skipped"` with reason.

**Parameter disambiguation**: The `[id]` segment is resolved based on the parent route path (e.g., `/agents/[id]` → agents API, `/players/[id]` → players API). The capture script matches the path segment immediately before `[id]` to select the correct API endpoint.

**Title and description derivation**: Titles are derived from the route path (e.g., `/admin/users` → "Users", `/library/games/[gameId]` → "Game Detail"). Descriptions are auto-generated from group + route context. Both fields are editable in `manifest.json` after first run.

## 2. Capture Engine (Playwright)

### Authentication

Credentials loaded automatically from `infra/secrets/staging/admin.secret`:

```typescript
function loadCredentials(): { email: string; password: string } {
  const secretPath = 'infra/secrets/staging/admin.secret';
  const content = fs.readFileSync(secretPath, 'utf-8');
  const env = Object.fromEntries(
    content.split('\n')
      .filter(l => l.includes('=') && !l.startsWith('#'))
      .map(l => { const [k, ...v] = l.split('='); return [k.trim(), v.join('=').trim()]; })
  );
  return { email: env.ADMIN_EMAIL, password: env.ADMIN_PASSWORD };
}
```

**Login flow**:
1. Navigate to `{baseUrl}/login`
2. Fill email + password fields
3. Click submit, wait for redirect
4. Save session state (`storageState`) to temp file
5. Reuse session state for all authenticated/admin pages

**Fallback**: If `admin.secret` not found, script exits with error message suggesting `make secrets-staging`.

### Capture Configuration

```typescript
interface CaptureConfig {
  baseUrl: string;                          // https://meepleai.app
  viewport: { width: 1440; height: 900 };  // Desktop standard
  waitStrategy: 'networkidle';              // Default wait
  waitTimeout: 15000;                       // Max ms per page
  delayAfterLoad: 1000;                     // ms for animations/transitions
  concurrency: 3;                           // Parallel browser contexts
  screenshotDir: string;                    // docs/pages/screenshots
  forceRecapture: boolean;                  // --force flag
}
```

### Execution Order

1. **Phase 0 -- Parameter resolution**: API calls to collect dynamic IDs
2. **Phase 1 -- Public pages** (25 + 10 auth + 2 dev + 1 docs + 1 join + 1 offline = 40): No auth needed
3. **Phase 2 -- Authenticated pages** (90 + 4 chat = 94): User session
4. **Phase 3 -- Admin pages** (75): Admin session (same credentials)

Within each phase, pages are captured with concurrency=3 (3 parallel browser contexts sharing the same session state).

### Error Handling

| Scenario | Behavior |
|----------|----------|
| Page returns 404 | Screenshot of 404 page, status: `"error"`, error: `"404"` |
| Page timeout (>15s) | Skip screenshot, status: `"timeout"` |
| JS error on page | Capture screenshot anyway, log error |
| API param resolution fails | status: `"skipped"`, error: `"no valid params"` |
| Login fails | Abort entire run with clear error |

### Incremental Runs

The manifest stores `capturedAt` per page. On re-run:
- If page route unchanged and screenshot exists: skip (unless `--force`)
- Pages with status `"timeout"` or `"error"`: retried on every run (only `"ok"` pages are skipped)
- If new pages detected (new `page.tsx` files): capture only new ones
- Deleted pages: remove from manifest and delete screenshot file

## 3. HTML Generator

### Output

Single file `docs/pages/dist/index.html` with inline CSS and JS. Zero external dependencies. Opens with double-click in any browser.

### Layout

```
┌─────────────────────────────────────────────────────────┐
│  MeepleAI Page Catalog                    [search box]  │
├──────────┬──────────────────────────────────────────────┤
│ Sidebar  │  Card Grid (3 cols desktop / 2 tablet / 1)  │
│          │  ┌────────┐ ┌────────┐ ┌────────┐           │
│ ● All    │  │ thumb  │ │ thumb  │ │ thumb  │           │
│ ○ Public │  │ title  │ │ title  │ │ title  │           │
│ ○ Auth   │  │ /route │ │ /route │ │ /route │           │
│ ○ User   │  │ badge  │ │ badge  │ │ badge  │           │
│ ○ Admin  │  └────────┘ └────────┘ └────────┘           │
│ ○ Chat   │                                              │
│ ○ Other  │  Showing 209 of 209 pages                   │
│          │                                              │
│ Stats    │                                              │
│ 209 tot  │                                              │
│ 200 ok   │                                              │
│ 9 err    │                                              │
├──────────┴──────────────────────────────────────────────┤
│  Generated: 2026-03-22  │  MeepleAI                     │
└─────────────────────────────────────────────────────────┘
```

### Features

| Feature | Implementation |
|---------|---------------|
| **Group filter** | Sidebar radio buttons, JS filters cards by `data-group` |
| **Search** | Input field, JS filters cards matching title/route/description |
| **Card** | Thumbnail (300px wide), title, route path, colored group badge |
| **Lightbox** | Click card → fullscreen overlay, ESC/click-outside to close, arrow keys for prev/next |
| **Status badge** | Green dot (ok), red dot (error), yellow dot (timeout), grey dot (skipped) |
| **Counter** | "Showing X of Y pages" updates on filter/search |
| **Responsive** | CSS grid: 3 cols >1200px, 2 cols >768px, 1 col mobile |

### Group Badge Colors

| Group | Color | Hex |
|-------|-------|-----|
| public | Green | `#22c55e` |
| auth | Blue | `#3b82f6` |
| authenticated | Orange (brand) | `hsl(25,95%,38%)` |
| admin | Purple | `#a855f7` |
| chat | Teal | `#14b8a6` |
| dev | Grey | `#6b7280` |
| docs | Grey | `#6b7280` |
| join | Grey | `#6b7280` |
| offline | Grey | `#6b7280` |

## 4. Scripts & Configuration

### Config File (`docs/pages/config.ts`)

```typescript
export const config = {
  baseUrl: 'https://meepleai.app',
  secretsPath: 'infra/secrets/staging/admin.secret',
  outputDir: 'docs/pages/screenshots',
  distDir: 'docs/pages/dist',
  manifestPath: 'docs/pages/manifest.json',
  viewport: { width: 1440, height: 900 },
  pageTimeout: 15000,
  delayAfterLoad: 1000,
  concurrency: 3,
};
```

### NPM Scripts

Added to a dedicated `docs/pages/package.json` (keeps root clean):

```json
{
  "scripts": {
    "docs:capture": "tsx docs/pages/capture.ts",
    "docs:generate": "tsx docs/pages/generate.ts",
    "docs:pages": "npm run docs:capture && npm run docs:generate",
    "docs:serve": "npx serve docs/pages/dist"
  }
}
```

### Dependencies

```json
{
  "devDependencies": {
    "playwright": "^1.x",
    "tsx": "^4.x"
  }
}
```

No additional dependencies needed. `tsx` likely already present. Playwright may need `npx playwright install chromium`.

## 5. File Structure

```
docs/pages/
├── config.ts                    # Configuration
├── capture.ts                   # Playwright crawler
├── generate.ts                  # HTML static site generator
├── manifest.json                # Auto-generated page manifest
├── screenshots/                 # Auto-generated screenshots
│   ├── public/
│   │   ├── home.png
│   │   ├── about.png
│   │   └── ...
│   ├── auth/
│   │   ├── login.png
│   │   └── ...
│   ├── authenticated/
│   │   ├── dashboard.png
│   │   └── ...
│   ├── admin/
│   │   ├── overview.png
│   │   └── ...
│   ├── chat/
│   └── other/
└── dist/
    └── index.html               # Generated static site
```

## 6. Gitignore

Add to `.gitignore`:
```
docs/pages/screenshots/
docs/pages/dist/
docs/pages/manifest.json
```

Screenshots and generated output are build artifacts, not source. The manifest is regenerated on each run.

## Out of Scope

- Custom per-page descriptions (editable in manifest.json post-generation)
- Mobile/tablet viewport screenshots (desktop 1440x900 only)
- Specific UI states (open modals, filled forms, error states)
- CI/CD automated re-generation
- Multi-language screenshots
- Screenshot diffing / visual regression testing

## Usage

```bash
# First time setup
npx playwright install chromium

# Full run
npm run docs:pages

# View result
npm run docs:serve
# or: open docs/pages/dist/index.html

# Force recapture all screenshots
npm run docs:capture -- --force
npm run docs:generate
```

Estimated capture time: ~7-8 minutes (3 concurrent contexts, 209 pages).
