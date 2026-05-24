# Codebase Audit — MeepleAI ↔ Design Handoff

> Generato dallo **Step 1** di `QUICK_START.md` (equivalente prompt **0.1** di `BACKEND_PROMPTS.md`).
> Data: 2026-05-24 · Branch: `feature/issue-1458-d1-dashboard-deadcode-removal` · Scope: solo lettura (zero modifiche).
> Revisione `/sc:spec-panel` applicata: aggiunte § 14.5 (DoD) + § 15.5 (Sprint cross-ref) + § 9 patch EntityChip + § 16 errata.

## TL;DR

Il codebase MeepleAI è **molto più maturo del baseline assunto dal handoff**. Il pacchetto è scritto per un progetto _tabula-rasa_ (Vite + `main.tsx`, niente token, niente entity system, niente test harness). Nella realtà:

- ✅ **Token system canonical già allineato** al handoff (`admin-mockups/design_files/tokens.css` ≡ `admin-mockups/design_handoff/tokens.css` _byte-identico_, già importato come `design-tokens-canonical.css` post DS-15/DS-16 / 2026-05-12).
- ✅ **Entity system già a regime** con 9 colors + helper TypeScript (`entity-tokens.ts` → Tailwind classes), ESLint rule `local/no-hardcoded-color-utility` (`error`) che blocca ogni `bg-white`/`text-gray-*` regression.
- ✅ **Design system primitives già esistenti**: `MeepleCard`, `ConnectionBar`, `Drawer`, `Tabs`, `EntityChip`/`EntityCard`/`EntityPip`, `RecentsBar`, `StepIndicator`, `ConfidenceBadge`, `KbDocList`, family Citation*. Vedi mapping § 9.
- ✅ **API layer maturo**: 60+ clients modulari in `lib/api/clients/`, 80+ React Query hooks in `hooks/queries/`, types da OpenAPI BE (`openapi-zod-client` rigenera Zod schemas — TS client manualmente mantenuto post #1543).
- ✅ **Realtime SSE in produzione**: `lib/session-live/*`, `useTranslateSegmentSSE`, `useSessionStream`, `useSessionAgentChat`, parser SSE generico.
- ⚠️ **Stack DIFFERENZA**: Next.js 16 App Router (NON Vite/`main.tsx`/`index.html`) + Tailwind 4 + next-themes (NON `localStorage.setItem('mai-theme')`) + .NET 9 EF Core (NON Prisma).
- ❌ **Mancanti dal design system v2 attesi dal handoff**: `MobileBottomBar`, `HouseRuleDrawer`, `SuggestedQueriesRow`, `ChatHistoryTimeline`, `ConfirmModal`, `CitationExpandedPanel`. Esistono però primitives correlate da cui derivarle.

## 1. Struttura monorepo

```
D:\Repositories\meepleai-monorepo-frontend
├── apps/
│   ├── api/                 # Backend .NET 9
│   │   └── src/Api/
│   │       ├── BoundedContexts/    # 18 BCs DDD (vedi CLAUDE.md)
│   │       ├── Routing/            # Minimal API endpoints
│   │       ├── Infrastructure/     # EF Core, Storage, Cache
│   │       └── openapi.json        # OpenAPI spec committed (Hybrid Option C)
│   ├── web/                 # Frontend Next.js 16 App Router
│   │   └── src/
│   │       ├── app/                # Routes (page.tsx + layout.tsx)
│   │       ├── components/
│   │       │   ├── ui/             # Design system primitives
│   │       │   ├── features/       # Feature compositions (post DS-deversioning)
│   │       │   ├── layout/         # Shell, UserShell, mobile/drawer
│   │       │   └── admin/, chat-unified/, chat/, dashboard/, session/, library/, rag-dashboard/, gamebook/…
│   │       ├── lib/
│   │       │   ├── api/clients/    # 60+ modular API clients
│   │       │   ├── api/schemas/    # Zod schemas generated
│   │       │   ├── api/generated/  # openapi-zod-client output
│   │       │   ├── session-live/   # SSE handlers
│   │       │   ├── stores/         # Zustand stores
│   │       │   └── domain-hooks/   # Composite hooks (session, game-night)
│   │       ├── hooks/queries/      # 80+ TanStack Query hooks
│   │       ├── types/              # 22 domain types (auth, agent, pdf, badges, …)
│   │       └── styles/             # 8 CSS files
│   ├── embedding-service/   # Python: embeddings
│   ├── reranker-service/    # Python: reranking
│   ├── smoldocling-service/ # Python: PDF parsing (SmolDocling)
│   └── unstructured-service/# Python: PDF/docs (Unstructured)
├── admin-mockups/
│   ├── design_files/        # SOURCE OF TRUTH: tokens.css, components.css, sp*-*.jsx, sp*-*.html
│   └── design_handoff/      # THIS PACKAGE (mockup duplicato + guida d'integrazione)
├── infra/                   # docker-compose.yml, secrets/, monitoring/
├── tests/                   # Api.Tests (xUnit + Testcontainers)
├── docs/                    # Architecture, ADR, dev guides
└── .github/workflows/       # CI/CD
```

## 2. Stack tecnico

### Frontend (`apps/web/package.json`)

| Categoria | Tool / Lib | Versione | Note |
|---|---|---|---|
| **Framework** | `next` | `16.2.6` | App Router (Pages Router rimosso #1077) |
| | `react` + `react-dom` | `19.2.6` | Server Components attivi |
| **Lang** | `typescript` | `5.9.3` | `tsc --noEmit` via `pnpm typecheck` |
| **CSS** | `tailwindcss` + `@tailwindcss/postcss` | `^4.3.0` | v4 (CSS-first via `@theme`, no JS config per colori) |
| | `tailwind-merge`, `class-variance-authority`, `clsx` | — | Conventional patterns |
| | `tailwindcss-animate` | `^1.0.7` | Animations utility |
| | `next-themes` | `^0.4.6` | `data-theme="light\|dark"` attribute |
| **Data fetching** | `@tanstack/react-query` | `^5.100.11` | Primary (80+ hooks in `hooks/queries/`) |
| | `swr` | `^2.4.1` | Coexiste in alcuni componenti (legacy / specifici) |
| **Stato globale** | `zustand` | `^5.0.13` | 6+ stores (session, toolbox, banners…) |
| | `zundo` | `^2.3.0` | Undo/redo middleware |
| | `xstate` + `@xstate/react` | `^5.31.1` | State machines (composite flows) |
| **UI library** | `@radix-ui/react-*` | varie 1.x/2.x | 22 primitives (dialog, dropdown, popover, tooltip, …) |
| | `vaul` | `^1.1.2` | Mobile drawer |
| | `cmdk` | `^1.1.1` | Command palette |
| | `framer-motion` | `^12.40.0` | Animations |
| | `lucide-react` | `^0.577.0` | Icon set |
| | `sonner` | `^2.0.7` | Toast notifications |
| **Form** | `react-hook-form` + `@hookform/resolvers` | `^7.76.0` + `^5.4.0` | Validation via Zod |
| | `zod` | `^4.4.3` | Schema validation |
| | `@rjsf/*` | `^6.5.3` | JSON-schema forms (admin) |
| **Editor** | `@tiptap/react` + `starter-kit` + ext. | `^3.23.6` | Rich text editor |
| | `@monaco-editor/react` | `^4.7.0` | Prompt editor (lazy-loaded) |
| | `prismjs` | `^1.30.0` | Code syntax highlighting |
| **PDF** | `@pdf-viewer/react`, `react-pdf`, `pdfjs-dist` | varie | PDF viewer + workers |
| | `jspdf`, `html2canvas` | — | PDF export client-side |
| **Charts/Viz** | `recharts`, `chart.js` + `react-chartjs-2`, `d3` | — | Multi-engine viz |
| | `@xyflow/react` | `^12.10.2` | Node-graph (RAG dashboard builder) |
| **Realtime** | `@microsoft/signalr` | `^10.0.0` | WebSocket via SignalR |
| **i18n** | `react-intl` | `^10.1.9` | Italian primary |
| **Test** | `vitest` + `@vitest/ui` + `@vitest/coverage-v8` | `^3.2.4` | Unit |
| | `@testing-library/{react,jest-dom,user-event,dom}` | varie | RTL stack |
| | `@playwright/test` | `^1.60.0` | E2E |
| | `@axe-core/playwright`, `@axe-core/react`, `axe-core`, `jest-axe`, `vitest-axe` | varie | A11y testing |
| | `msw` | `^2.14.6` | Mock Service Worker (test/e2e) |
| **Lint/Format** | `eslint` + `eslint-config-next` | `9.39.1` + `16.2.6` | Flat config (`eslint.config.mjs`) |
| | `eslint-plugin-{react,react-hooks,jsx-a11y,security,no-unsanitized,unused-imports,import}` | varie | + 5 **custom local rules** (vedi § 6) |
| | `prettier`, `husky`, `lint-staged`, `@commitlint/cli` | varie | Pre-commit hooks |
| **Storybook** | `storybook` + `@storybook/nextjs` + `@storybook/addon-{a11y,docs,onboarding,themes}` | `10.4.1` | Storybook 10 |
| | `chromatic` | `^17.0.0` | Visual regression CI |
| **DX** | `tsx`, `cross-env`, `dotenv`, `dotenv-cli` | varie | Tooling |
| | `@next/bundle-analyzer` | `^16.2.6` | Bundle inspection |
| | `openapi-zod-client` | `^1.18.3` | OpenAPI → Zod schemas (BE-driven types) |
| **Custom ESLint** | `eslint-rules/no-incomplete-sanitization.js` | local | XSS prevention |
| | `eslint-rules/no-hardcoded-hex.js` | local | Token enforcement primitives ui/ |
| | `eslint-rules/no-inline-hsl-v2.js` | local | Inline HSL guard in features/ |
| | `eslint-rules/no-hardcoded-color-utility.js` | local | DS-15 neutral palette ban (mode: **error** since 2026-05-12) |
| | `eslint-rules/api-client-v1-prefix.js` | local | apiClient path prefix /api/v1/ guard (#1229) |

### Backend (`apps/api/src/Api/Api.csproj`)

| Categoria | Pacchetto | Versione |
|---|---|---|
| **Framework** | .NET | `net9.0` (`net9.0` target) |
| | `Microsoft.AspNetCore.OpenApi` | `9.0.11` |
| | `Scalar.AspNetCore` | `2.11.1` |
| **CQRS / Mediator** | `MediatR` | `14.0.0` |
| **Validation** | `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` | `12.1.1` |
| **Persistence** | `Microsoft.EntityFrameworkCore` + `.Design` | `9.0.11` |
| | `Npgsql.EntityFrameworkCore.PostgreSQL` | `9.0.4` |
| | `Npgsql` | `10.0.2` |
| | `Pgvector.EntityFrameworkCore` | `0.3.0` (RAG vector search) |
| **Cache** | `Microsoft.Extensions.Caching.Hybrid` | `10.0.0` (L1+L2) |
| | `Microsoft.Extensions.Caching.StackExchangeRedis` | `10.0.8` |
| | `StackExchange.Redis` | `2.10.1` |
| **Jobs** | `Quartz` + `Quartz.Extensions.Hosting` + `.Serialization.Json` | `3.13.1` (scheduler) |
| **Resilience** | `Microsoft.Extensions.Http.Polly`, `Polly.Core` | `10.0.8`, `8.6.6` |
| **Auth** | `System.IdentityModel.Tokens.Jwt` | `8.2.1` |
| | `Otp.NET` | `1.4.1` (2FA) |
| **Observability** | `Serilog` + `.AspNetCore` + `.Sinks.{Console,File,Seq}` + `.Enrichers.Environment` | varie |
| | `OpenTelemetry` + `.Instrumentation.{AspNetCore,Http,Runtime,EntityFrameworkCore}` + `.Exporter.Prometheus.AspNetCore` | varie |
| | `AspNetCore.HealthChecks.{NpgSql,Redis,Uris}` | `9.0.0` |
| **Storage** | `AWSSDK.S3` | `3.7.413` (R2/AWS/MinIO via factory `STORAGE_PROVIDER`) |
| **PDF / OCR** | `Docnet.Core`, `itext7` + bouncy-castle, `Tesseract` + `Tesseract.Drawing`, `QuestPDF`, `ClosedXML` | varie |
| **Imaging** | `SixLabors.ImageSharp`, `SkiaSharp`, `System.Drawing.Common` | varie |
| **Misc** | `Parquet.Net`+`Snappier`, `WebPush`, `YamlDotNet`, `ScottPlot`, `QRCoder`, `SlackNet`, `DotNetEnv` | varie |
| **Analyzers** | `Microsoft.CodeAnalysis.NetAnalyzers` | `10.0.100` |
| | `SonarAnalyzer.CSharp` | `10.16.1.129956` |
| | `Meziantou.Analyzer` | `2.0.257` |
| | `Api.Analyzers` (local — `NoPiiInLogAnalyzer` MAI001-003) | local |

### DB & infra (da `CLAUDE.md`)

- **PostgreSQL 16** + `pgvector` extension (semantic search)
- **Redis** (cache + sessions)
- Docker Compose multi-service (`infra/docker-compose.yml`)
- Snapshot/seed workflow per dev (`make dev-from-snapshot`)
- 18 BoundedContexts DDD (vedi `CLAUDE.md` § Architecture)

## 3. Entry-point + theming

`apps/web/src/app/layout.tsx` (RootLayout, Server Component):

- **Font caricamento via `next/font/google`**: `Quicksand` (display) + `Nunito` (body) come CSS variables `--font-quicksand` / `--font-nunito`. _Niente `<link rel="stylesheet">` manuale_ — il flusso del handoff (step 2) **non si applica come scritto**.
- ⚠️ **Bug noto**: `tailwind.config.js:17-18` referenzia `--font-inter` che **non è definita** in `layout.tsx` → fallback silente a sans-serif. Da risolvere fuori-scope.
- `JetBrains Mono` (`--f-mono` nei tokens) **non è caricato via `next/font`** → eventuali usi di `font-family: var(--f-mono)` cadono su `ui-monospace`/`SF Mono`/`monospace`. Da risolvere se serve.
- `<html lang="it" data-theme="light" suppressHydrationWarning>` — `next-themes` rewrite client-side; SSR hint = light (mockup cream `#f7f3ee` ✅).
- Import order (DS-16, 2026-05-12):
  1. `design-tokens-canonical.css` (single source of truth, mockup-faithful)
  2. `globals.css` (Tailwind v4 `@import` + `@theme` block + keyframes)
  3. `diff-viewer.css`, `agent-theme.css`, `agent-typography.css`, `agent-animations.css`
  4. `prismjs/themes/prism-tomorrow.css`
- **Token bridge layer rimosso** in DS-16 (codemod renamed all consumer references to canonical names).

## 4. CSS approach

```
apps/web/src/styles/
├── design-tokens-canonical.css  # CANONICAL (post DS-1, mockup-faithful, byte-identical to design_handoff/tokens.css)
├── design-tokens.css            # LEGACY (referenced by globals.css @import, kept for compat)
├── globals.css                  # Tailwind v4 layer + @theme block + keyframes
├── premium-gaming.css           # Domain-specific theme
├── agent-theme.css              # AI agent surface theming
├── agent-typography.css         # AI agent typography variants
├── agent-animations.css         # AI agent animations
└── diff-viewer.css              # Code diff viewer styling
```

**Entity colors** (`--c-game`, `--c-player`, `--c-session`, `--c-agent`, `--c-kb`, `--c-chat`, `--c-event`, `--c-toolkit`, `--c-tool`) e text variants (`--c-game-text`, `--c-kb-text`, `--c-toolkit-text`) sono in `design-tokens-canonical.css`. Coerenti 1:1 con il pacchetto handoff (verificato via `cmp`).

**Bridge legacy v1**: **rimosso** in DS-16. La mia memoria di sessione (`MEMORY.md`) ne tracciava ancora la presenza — il file `apps/web/src/app/layout.tsx:18-21` chiarisce che è stato eliminato post-codemod. Riferimento aggiornato in audit.

## 5. API layer

### Generazione

- OpenAPI BE espone `http://localhost:8080/openapi/v1.json` (in dev) + spec committed in `apps/api/src/Api/openapi.json` (Hybrid Option C).
- `pnpm generate:api` → `apps/web/scripts/generate-api-client.ts` → **solo Zod schemas** in `apps/web/src/lib/api/generated/`.
- TS client modulare **manualmente mantenuto** in `apps/web/src/lib/api/clients/` post NSwag-migration (Issue #1543).

### Clients (60+)

Tutti sotto `apps/web/src/lib/api/clients/`. Highlights pertinenti al handoff:

- **Auth**: `authClient.ts`
- **Games**: `gamesClient.ts`, `gameNightsClient.ts`, `gameNightSessionClient.ts`, `gameNightBggClient.ts`, `gameContributorsClient.ts`, `gameSessionsClient.ts`, `gameToolkitClient.ts`, `sharedGamesClient.ts`
- **Sessions**: `sessionsClient.ts`, `liveSessionsClient.ts`, `sessionInviteClient.ts`, `sessionTrackingClient.ts`, `sessionAgentClient.ts`, `sessionAttachmentsClient.ts`, `sessionSnapshotsClient.ts`, `sessionStatisticsClient.ts`, `chatSessionsClient.ts`
- **AI/Agents**: `agentsClient.ts`, `agentSessionsClient.ts`, `agentMemoryClient.ts`, `agentDocumentsClient.ts`, `chatClient.ts`, `sandboxClient.ts`, `ragExecutionClient.ts`, `aiUsageClient.ts`
- **KB / docs**: `knowledgeBaseClient.ts`, `documentsClient.ts`, `pdfClient.ts`
- **Library**: `libraryClient.ts`, `wishlistClient.ts`, `collectionsClient.ts`, `playRecordsClient.ts`
- **Admin**: `adminClient.ts`, `adminDashboardClient.ts`, `adminShareRequestsClient.ts`, `adminNotificationsClient.ts`, `adminSecretsClient.ts`, `stagingAllowlistClient.ts`
- **Other**: `notifications.ts`, `invitationsClient.ts`, `accessRequestsClient.ts`, `shareRequestsClient.ts`, `shareLinksClient.ts`, `featureFlagsClient.ts`, `configClient.ts`, `onboardingClient.ts`, `tierClient.ts`, `tierStrategyClient.ts`, `infrastructureClient.ts`, `rateLimitsClient.ts`, `budgetClient.ts`, `dashboardClient.ts`, `entityLinksClient.ts`, `toolkit.ts`, `toolboxClient.ts`, `testResultsClient.ts`, `contactClient.ts`, `bggClient.ts`, `badgesClient.ts`, `alerts.ts`, `emailVerificationClient.ts`

### Hooks queries (80+)

Tutti sotto `apps/web/src/hooks/queries/`. ESLint exemption per `no-non-null-assertion` (queryFn idiomatic pattern `id!` + `enabled: !!id`).

### Auth pattern

- **Session-based** primario (via cookie, Set-Cookie da BE preservato via app/api/[...path]/route.ts catch-all proxy — Issue #703)
- **JWT** disponibile (`System.IdentityModel.Tokens.Jwt`)
- **2FA** via `Otp.NET`
- **OAuth** flow (vedi `authClient.ts`)
- API guard: `local/api-client-v1-prefix` ESLint rule (`error`) richiede prefisso `/api/v1/` su tutti gli `apiClient.{get,post,put,patch,delete,head,options}` call.

## 6. ESLint rules attive (custom + boundary)

| Rule | Scope | Mode | Scopo |
|---|---|---|---|
| `local/no-incomplete-sanitization` | tutto src/ | `error` | CWE-116 |
| `local/no-hardcoded-color-utility` | `src/**/*.{ts,tsx}` | **`error`** (DS-15) | Vieta `bg-white`, `bg-slate-*`, `text-gray-*`, full neutral palette. ~`text-white` exempt se className ha bg colorato |
| `local/no-hardcoded-hex` | 27 `ui/<primitive>/**` dirs | `error` | Vieta hex hardcoded nelle primitives ex-v2 |
| `meepleai/no-inline-hsl-v2` | `src/components/features/**` | `error` | Vieta inline `hsl()/hsla()` con hue match entity color signature |
| `local/api-client-v1-prefix` | `src/hooks/queries/**`, `src/lib/api/clients/**` | `error` | Path apiClient deve iniziare con `/api/v1/` |
| `@typescript-eslint/no-explicit-any` | tutto TS | `error` | TS-001 |
| `@typescript-eslint/no-unused-vars` | tutto TS | `error` | `^_` prefix exempt |
| `react-hooks/exhaustive-deps` | tutto JSX | `error` | TS-002 |
| Security cluster | tutto JS/TS | mix `error`/`warn` | XSS, eval, regex DoS, timing attack, prototype pollution |
| Boundary `chat/shared` | `src/components/chat/shared/**` | `error` | Leaf module — no chat-unified/* né chat/panel/* imports |

→ **Implicazione per Step 2-3-4**: ogni file nuovo è soggetto a queste rules. Helpers o tokens "via hex" del handoff falliscono lint.

## 7. Realtime / SSE

| Capability | Implementazione | Path |
|---|---|---|
| **SSE generic parser** | TextDecoder + ReadableStream | `apps/web/src/lib/utils/sseParser.ts` |
| **Session live SSE** | Pipeline: parse → compose state → hook | `apps/web/src/lib/session-live/{parse-sse-event,compose-session-live-state,use-session-live-stream}.ts` |
| **Gamebook translate SSE** | Streaming traduzione segmenti | `apps/web/src/lib/gamebook/hooks/useTranslateSegmentSSE.ts` |
| **Session stream hook** | Composite SSE+state | `apps/web/src/lib/domain-hooks/useSessionStream.ts` |
| **Session agent chat** | SSE chat con citazioni | `apps/web/src/lib/domain-hooks/useSessionAgentChat.ts` |
| **Game chat (RAG)** | TanStack Query + SSE injection | `apps/web/src/hooks/queries/useGameChat.ts` (+ feature `game-chat`) |
| **WebSocket** | SignalR client (`@microsoft/signalr`) | da localizzare (probabile dashboard live / alerts) |

→ Lo **schema SSE è già esteso** (events parse + redux-like compose state); il handoff `5.2 KB SSE` può riusare `lib/utils/sseParser.ts` come base.

## 8. Test stack

- **Unit**: `vitest run` (target 85%+ FE). Configs separati: `vitest.config.ts` (default) + `vitest.admin.config.ts` (admin pages).
- **A11y unit**: `pnpm test:a11y` (vitest + `vitest-axe`).
- **E2E**: `playwright` con multi-shard (6 shards) + multi-config (default, visual-docs, staging).
- **A11y E2E**: `pnpm test:a11y:e2e` (axe + Playwright).
- **Visual regression**: `chromatic` via Storybook 10 (Visual Gate rimosso 2026-05-20 — mockup `e2e/visual-mockups/` retired).
- **MSW** per E2E mock.
- **Coverage**: V8 (`@vitest/coverage-v8`) + `@bgotink/playwright-coverage` per E2E.

## 9. Design system components: mapping handoff → codebase

### Componenti **richiesti dal handoff** (`QUICK_START.md` step 6 + `WIRING_GUIDE.md`)

| Componente handoff | Stato | Path canonico | Note |
|---|---|---|---|
| `ConnectionBar` | ✅ ESISTE | `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx` | + feature copy in `features/session-summary/` |
| `MeepleCard` | ✅ ESISTE | `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx` | Compound API (`compound.tsx`), skeleton, `tokens.ts`, `types.ts`, `index.ts` |
| `EntityChip` | ✅ ESISTE | `apps/web/src/components/ui/entity-chip/entity-chip.tsx` | Firma: `function EntityChip({ entity: EntityType, label: string, size?: 'sm' \| 'md' }): JSX.Element`. Usa `getEntityToken()` da `entity-tokens.ts`. Lower-case file name, no `index.ts` barrel — import diretto |
| `Drawer` (with cascadeNavigationStore) | ✅ ESISTE | `apps/web/src/components/ui/drawer/drawer.tsx` | + variant `extra-meeple-card/DrawerActionFooter.tsx`, + mobile `layout/mobile/drawer/DrawerContent.tsx`. Lib backing: `vaul`. _cascadeNavigationStore_ → verificare presenza store dedicato (non localizzato in questo audit, deferred) |
| `Tabs` (animated underline) | ✅ ESISTE | `apps/web/src/components/ui/navigation/tabs.tsx` | + variant `ui/detail-layout/tabs.tsx`. Lib backing: `@radix-ui/react-tabs` |
| `MobileBottomBar` | ❌ MANCANTE | _da creare_ | Esiste struttura mobile (`layout/mobile/`) ma niente `BottomBar` primitive |
| `RecentsBar` | ✅ ESISTE | `apps/web/src/components/layout/UserShell/RecentsBar.tsx` | Con test |

### Nuovi componenti v2 attesi dal handoff (sez. "Nuovi v2 emersi dai mock")

| Componente v2 | Stato | Path candidato / Equivalente |
|---|---|---|
| `HouseRuleDrawer` | ❌ MANCANTE (drawer dedicato) | Esistono: `library/game-table/HouseRulesSection.tsx`, `session/live/HouseRulesDisplay.tsx`. Da estrarre come Drawer-wrapped primitive |
| `ConfidenceBadge` | ✅ ESISTE (3 versioni) | `components/ui/feedback/ConfidenceBadge.tsx` (canonical) + `admin/rag/`, `features/game-chat/`, `features/gamebook/` |
| `SuggestedQueriesRow` | ❌ MANCANTE | _da creare_ |
| `ChatHistoryTimeline` | ❌ MANCANTE (cross-session) | Esiste `components/admin/rag/TimelineStep.tsx` (single-session). Da generalizzare se serve cross-session |
| `KbDocList` | ✅ ESISTE | `components/features/agent-detail/KbDocList.tsx` |
| `CitationExpandedPanel` | ⚠️ PARZIALE | Esiste family: `chat-unified/CitationBadge.tsx`, `CitationBlock.tsx`, `CitationSheet.tsx`, `chat/panel/CitationExpander.tsx`, `features/game-chat/{CitationModal,CitationPdfTab,CitationChip,CitationOwnershipUpsell}.tsx`. Verificare se uno copre il "panel" richiesto |
| `StepIndicator` | ✅ ESISTE (2 versioni) | `components/library/add-game-sheet/StepIndicator.tsx` + `features/gamebook/StepIndicator.tsx` |
| `ConfirmModal` | ❌ MANCANTE (wrapper) | Esistono: `components/ui/animations/ModalAnimations.tsx`, Radix `@radix-ui/react-alert-dialog`. Da creare wrapper |

### Helpers TypeScript

| Helper handoff | Stato | Path codebase | Differenza concettuale |
|---|---|---|---|
| `src/lib/entity-color.ts` con `entityColor(type, alpha?) → 'hsl(…)'` | ❌ MANCANTE (path letterale) | Equivalente: `apps/web/src/components/ui/entity-tokens.ts` | Codebase usa **classi Tailwind compile-time** (`bg-entity-game`, `text-entity-session/10`, ecc.) — non `hsl(...)` string runtime. Approcci complementari: il primo è per inline `style={{ color: ... }}`, il secondo per `className`. **Decidere**: aggiungere helper a `lib/` o estendere `entity-tokens.ts` esistente con HSL accessor |

## 10. Entity types (handoff prompt 0.2) vs codebase

Il prompt 0.2 propone di creare `src/types/entities.ts` con 14 types ricavati da `data.js`. Realtà:

- **Source of truth**: `apps/api/src/Api/openapi.json` (committed) → Zod schemas in `apps/web/src/lib/api/generated/` → DTO TypeScript inferiti dai schemi.
- **Domain minimal types**: `apps/web/src/types/domain.ts` ha `Game`, `Agent`, `Chat`, `ChatMessage` ma con campi **minimi** (es. `Game = { id, title, createdAt? }`). I metadati ricchi (publisher, year, players, weight, rating, cover, status, kbState, …) presenti in `data.js` vivono in DTO specifici dei singoli client (`gamesClient.ts`, `libraryClient.ts`, ecc.).
- **22 type files** in `apps/web/src/types/` coprono: `agent`, `api-key-filters`, `api`, `auth`, `badges`, `bgg`, `collection`, `domain`, `game-state`, `index`, `layout`, `linked-entity`, `pdf`, `permissions`, `quick-action`, `quick-question`, `search`, `tags`, `admin-dashboard`, `test-hooks.d`, `view-transitions.d`, `web-speech.d`.

→ **Step 4 letterale `src/types/entities.ts` dal `data.js` è inutile/dannoso**: deriva types da mock fake invece che dal contratto BE. Alternative valide:
1. **Skip Step 4** (i tipi già esistono via OpenAPI/Zod).
2. **Estendere `types/domain.ts`** con campi mockup-only (es. `coverGradient`, `kbState`, `badge`) come optional, marcati `// derived from data.js fake, NOT from BE DTO`.
3. **Generare `data.js` types** in un file separato e isolato (`types/mockup-extra.ts`) usato solo durante migration period.

## 11. Discrepanze rilevanti vs assunzioni del handoff

| Assunzione handoff | Realtà MeepleAI | Impatto su QUICK_START |
|---|---|---|
| Stack Vite / `main.tsx` / `index.html` | Next.js 16 App Router / `app/layout.tsx` | Step 2 step "Aggiungi font Google a index.html" — _non si applica_, già `next/font/google` in `layout.tsx` |
| `data-theme="light"` su `<html>` manuale + `localStorage.setItem('mai-theme')` | `next-themes` library + SSR hint + `data-theme="light\|dark"` rewrite client-side | Step 2 step "Aggiungi data-theme="light" su <html>" — _già fatto_ |
| `tokens.css` da copiare in `src/styles/tokens.css` | `design-tokens-canonical.css` già importato in `layout.tsx`, byte-identico al handoff | Step 2 — _no-op_, file già allineato |
| `components.css` da copiare in `src/styles/components.css` | `globals.css` Tailwind v4 con `@theme` + 8 file CSS dominio-specifici | Step 2 step — _ridondante_, va valutato cosa effettivamente serve (es. utility `.e-game`/`.e-ring-game`/`.mai-cb-scroll` se non già in Tailwind utilities) |
| Helper TypeScript `entityColor(type, alpha) → 'hsl()'` | `getEntityToken(type) → { bg/bgSoft/text/border classes }` Tailwind-based | Step 3 — _approccio diverso_. Decidere se aggiungere helper HSL runtime separato |
| Tipi entity dal `data.js` mock | Tipi da OpenAPI BE (60+ clients + Zod schemas) | Step 4 — _da skip o trasformare_ |
| Prisma schema → migration check (prompt 0.3) | EF Core migrations (.NET) → SQL via `dotnet ef migrations` | Step 5 — _riadattare a EF Core_ |
| `cascadeNavigationStore` (Drawer push) | Da localizzare (non emerso nell'audit attuale) | Verificare in Step 5/6 review se esiste; altrimenti potrebbe essere primitivo da creare |

## 12. Storybook & Visual regression

- **Storybook 10** + addons `a11y`, `docs`, `onboarding`, `themes`
- **Chromatic** integration: `pnpm chromatic` / `pnpm chromatic:ci`
- **Visual Gate REMOVED 2026-05-20** (Issue #1269 waiver) — l'intera suite `e2e/visual-conformity/`, `visual-migrated/`, `v2-states/`, `visual-mockups/` ritirata insieme ai 9 workflow CI. Replacement: review manuale designer su PR.

## 13. Cosa funziona già, cosa serve davvero

### ✅ Pezzi già in produzione (skip in Step 2-4)

- Token system canonical (DS-15/DS-16 complete)
- Font Quicksand + Nunito caricati via `next/font`
- 9 entity colors + entity-tokens helper Tailwind classes
- ESLint rules anti-regression (no-hardcoded-color-utility = `error`)
- 60+ API clients + 80+ React Query hooks
- SSE infrastructure (parser + composer + 4+ hook concreti)
- 7/7 primitives "esistenti" del handoff + 5/8 componenti v2

### ⚠️ Pezzi da chiarire/decidere prima di Step 2-4

1. **`lib/entity-color.ts` HSL-string runtime helper**: serve davvero? Quando? (es. inline styles dinamici per gradient cover game). Decision pending review § 9.
2. **`components.css` utility globali** (`.e-game`, `.e-tint-game`, `.e-ring-game`, `.mai-cb-scroll`): valutare quali servono come Tailwind utilities vs CSS pre-built. Se Tailwind v4 `@theme` block copre già il 100%, l'import diventa no-op.
3. **`cascadeNavigationStore`**: localizzare o creare. Pattern Zustand attivo in repo (6+ stores), facile da aggiungere.
4. **`MobileBottomBar`**: design quale flusso? Bottom-nav app-style con icone+label.
5. **`HouseRuleDrawer`**: estrarre `HouseRulesSection` + wrap in `Drawer` primitive.
6. **`SuggestedQueriesRow`**: chip horizontal-scroll con varianti.
7. **`ChatHistoryTimeline`**: cross-session timeline (riusare logica `TimelineStep` admin/rag).
8. **`ConfirmModal`**: wrapper Radix AlertDialog + animazioni `ModalAnimations`.

### ❌ Step QUICK_START letterali che **non vanno applicati come scritto**

| Step QUICK_START | Cosa dice | Cosa fare invece |
|---|---|---|
| Step 2 — copia `tokens.css` | Sovrascrivere `src/styles/tokens.css` | **No-op**: `design-tokens-canonical.css` già importa identico file. Skippare. |
| Step 2 — copia `components.css` | Sovrascrivere `src/styles/components.css` | Audit `components.css` handoff vs Tailwind utilities esistenti, importare solo deltas non coperti |
| Step 2 — `<link>` Google Fonts in `index.html` | Aggiungere manualmente in `index.html` | **No-op**: `next/font/google` già attivo in `layout.tsx`. Se serve `JetBrains Mono`, aggiungerlo via `next/font` qui. |
| Step 2 — `data-theme="light"` su `<html>` | Aggiungere manualmente | **No-op**: `layout.tsx:78` già lo fa con `suppressHydrationWarning` + next-themes |
| Step 2 — `localStorage.setItem('mai-theme')` init | JS init in entry point | **No-op**: `next-themes` ThemeProvider già lo gestisce |
| Step 3 — `src/lib/entity-color.ts` letterale | Creare file con `entityColor(type, alpha?) → 'hsl()'` | **Decidere**: aggiungere helper HSL accessor a `entity-tokens.ts` esistente OPPURE creare `lib/entity-color.ts` complementare focalizzato su inline styles |
| Step 4 — `src/types/entities.ts` da `data.js` | Estrarre 14 types da mockup JSON | **Skip**: i tipi sono in `lib/api/clients/*` + `types/domain.ts` + Zod schemas generated. Se servono mockup-only fields, file separato `types/mockup-extra.ts` |
| Step 5 — Prisma schema check | Confronto con `prisma/schema.prisma` | **Riadattare a EF Core**: confronto con `apps/api/src/Api/BoundedContexts/*/Domain/Entities/*.cs` + migrations folder |
| Step 6 — Audit componenti UI | Cerca i 7+8 componenti nel codebase | **Già fatto in questo audit § 9**. Generare SCREENS_MISSING.md da SCREENS.md per delta |

## 14. Pitfalls noti / riferimenti per Step 2-3-4

- **Issue #2567**: Endpoint flow obbligatorio DTOs → Queries → Commands → Validators → Handlers → Routing (BE side, rilevante per Step 5 quando si proporrà di aggiungere endpoint).
- **Issue #2568**: Mai `InvalidOperationException` (500); usare `ConflictException` (409) o `NotFoundException` (404).
- **Issue #1229 / PR fix**: Tutti gli apiClient call **devono** iniziare con `/api/v1/` (ESLint enforced).
- **Pattern P31** (memoria sessione): owner-preempt frequente — verificare `gh issue list --state open` prima di aprire issue/PR per drift fix.
- **Pattern P58 conformity-debt**: drift visivo singolo NON apre issue separato — accorpare in epic esistente.

## 14.5. Definition of Done per schermata (estratto da `WIRING_GUIDE.md`)

Una schermata è "fatta" quando tutti questi 9 punti sono soddisfatti (criterio di accettazione per ogni Step 7+):

- [ ] Render 1:1 con il mock (mobile + desktop)
- [ ] Tutti gli stati (default / loading / empty / error) presenti
- [ ] Dati arrivano da API reale (non mock locale)
- [ ] Eventi cablati (click, form submit, navigazione)
- [ ] Test unit + integration base (Vitest + RTL)
- [ ] Storybook o equivalente (Chromatic)
- [ ] `data-comment-anchor` preservati (se nei mock ci sono)
- [ ] Accessibility: focus visibile, aria-label, `prefers-reduced-motion`
- [ ] PR review passa (rispetta `eslint.config.mjs` + `typecheck` + `pnpm test`)

→ Solo dopo passi alla schermata successiva.

**Cross-link**: vedi `WIRING_GUIDE.md § Quando completare un'iterazione` per pattern aggiuntivi (estrazione sub-componenti, mapping data.js→useQuery hook, drawer cascade, ecc.).

## 15. Stato dei deliverable Step 1-6

| Step | Deliverable | Stato | Path |
|---|---|---|---|
| 1 | `design_handoff/CODEBASE_AUDIT.md` | ✅ generato + post-review | `admin-mockups/design_handoff/CODEBASE_AUDIT.md` |
| 2 | Import design system | ✅ **APPLICATO 2026-05-24** (sub-task 2a/2b done, 2c SKIP per decisione, 2d PASS) | `apps/web/src/app/layout.tsx` + `apps/web/tailwind.config.js` |
| 3 | `entity-color.ts` helper | ✅ **APPLICATO 2026-05-24** (3a confirmed, 3b JSDoc, 3c alias creato) | `apps/web/src/components/ui/entity-tokens.ts` + `apps/web/src/lib/entity-color.ts` |
| 4 | `types/entities.ts` da `data.js` | ✅ **SKIP per decisione 2026-05-24** | (nessuna modifica) — tipi BE-driven via OpenAPI Zod schemas |
| 5 | `design_handoff/SCHEMA_DIFF.md` | ✅ generato + post-review (spot-check 2 entity) | `admin-mockups/design_handoff/SCHEMA_DIFF.md` |
| 6 | `design_handoff/COMPONENTS_AUDIT.md` | ✅ generato + post-review (+ LibraryGameAgentShell + verdetti divergence) | `admin-mockups/design_handoff/COMPONENTS_AUDIT.md` |

**File toccati in implementazione Step 2-3** (verificato via `git status`):
- `M apps/web/src/app/layout.tsx` (sub-task 2a: import + setup JetBrains_Mono + className body)
- `M apps/web/tailwind.config.js` (sub-task 2b: 3 occorrenze `var(--font-inter)` → `var(--font-nunito)`)
- `M apps/web/src/components/ui/entity-tokens.ts` (sub-task 3b: JSDoc 11-righe sopra `getEntityToken`)
- `?? apps/web/src/lib/entity-color.ts` (sub-task 3c: nuovo file alias 25 righe)
- `?? admin-mockups/design_handoff/{CODEBASE_AUDIT,SCHEMA_DIFF,COMPONENTS_AUDIT}.md` (Step 1+5+6 deliverable)

**Baseline post Step 2-3**:
- `pnpm typecheck`: ✅ PASS (zero errors)
- `pnpm lint`: ✅ PASS (0 errors, 49 warnings — pre-existing, sotto baseline `--max-warnings=510`)
- `pnpm test`: non eseguito in questo round (richiede ~5+ min full suite); raccomandato eseguire prima del commit.

## 15.5. Cross-reference con Sprint roadmap di `SCREENS.md`

`SCREENS.md` propone **7 Sprint per 13 settimane** (71 schermate organizzate in 12 Fasi). Mapping verso ciò che è **già pronto** vs **da costruire** nel codebase:

| Sprint | Focus | Componenti chiave | Stato codebase |
|---|---|---|---|
| **Sprint 1** (sett 1-2) — Foundation | tokens + EntityChip + ConnectionBar + Dashboard + Library + Game Detail + Auth | tokens.css (✅ DS-15), EntityChip (✅), ConnectionBar (✅), MeepleCard (✅), Drawer (✅), Tabs (✅) | 🟢 **Foundation già pronta** — può saltare a Sprint 2 |
| **Sprint 2** (sett 3-4) — Game Nights ⭐ gold scenario | Wizard create, Detail (host/invited), Live, Summary, Notifications base | GameNightEvent BE (✅), Notification BE (✅), notifications client (✅) | 🟡 Backend ready; FE da costruire |
| **Sprint 3** (sett 5-6) — AI Agents | `/library/games/[id]/agent` SSE, HouseRuleDrawer, Agents index/detail, KB upload | useSessionAgentChat SSE (✅), AgentMemory BE (✅), KbDocList (✅) | 🟡 Manca: `LibraryGameAgentShell`, `HouseRuleDrawer`, `SuggestedQueriesRow`, `ChatHistoryTimeline` |
| **Sprint 4** (sett 7) — Sessions | `/sessions/[id]/live` WebSocket, Summary | session-live SSE (✅), SignalR (✅) | 🟡 FE da costruire |
| **Sprint 5** (sett 8-10) — Librogame | Index, search, photo upload, setup wizard, play session, encounter, glossary, translate, quota | Gamebook entities BE (✅ via SessionTracking), StepIndicator SP6 (✅), ConfidenceBadge SP6 (✅) | 🟡 Maggior parte FE da costruire |
| **Sprint 6** (sett 11-12) — Polish | Settings, Hub pages, Discover, Pre-auth | UserPreferences BE (✅) | 🟡 FE da costruire |
| **Sprint 7** (sett 13) — Edge cases | Error states + P2/P3 | — | 🟡 FE da costruire |

**Implicazione strategica**: **Sprint 1 può essere saltato** perché tutti gli ingredienti foundation esistono già. Si può partire direttamente da **Sprint 2 Game Nights** (gold scenario del progetto) — più valore prima.

→ Per ognuno, lista P0 schermate priority + endpoint backend attesi: vedi tabelle in `SCREENS.md § Fase 01-12`.

## 16. Errata corrige post-review `/sc:spec-panel`

| # | Versione precedente | Versione corretta | Origine |
|---|---|---|---|
| 1 | "Bridge legacy v1 ATTIVO" (memoria sessione obsoleta) | "**Token bridge rimosso in DS-16** — codemod renamed all consumer references to canonical names (vedi `layout.tsx:18-21`)" | Read `layout.tsx` confermato |
| 2 | `EntityChip` "Firma — Da aprire per leggere — non letto in audit per economy" | Firma completa: `function EntityChip({ entity: EntityType, label: string, size?: 'sm' \| 'md' })`, usa `getEntityToken()` | Read `entity-chip/entity-chip.tsx` |
| 3 | "60+ API clients" (approssimato) | "**64** API clients" (count effettivo da Glob `lib/api/clients/*.ts`) | Glob count |
| 4 | (omissione) | Aggiunta DoD 9-point § 14.5 + Sprint cross-ref § 15.5 | Read `WIRING_GUIDE.md` + `SCREENS.md` |
| 5 | (Step 2c TBD) | **Step 2c — `components.css` handoff: SKIP integrale** (decisione 2026-05-24). Le 9 classi sono ~80% mock-canvas (`.phone`/`.phone-sbar`/`.stage`/`.hub-nav`/`.cover-ph`) o già coperte da Tailwind+MeepleCard (`.btn`/`.card`/`.e-chip`/`.e-dot`). Nessuna utility importata in `src/styles/`. | Read `components.css` (133 righe) |
| 6 | (Step 4 TBD) | **Step 4 — types entity: SKIP** (decisione 2026-05-24). Tipi BE-driven via OpenAPI Zod (`lib/api/generated/`) + `types/domain.ts`. Nessuna estrazione da `data.js`. Campi UI-only (`Player.color`, `Game.coverGradient`, `Game.coverEmoji`) restano inline nei componenti che li usano (mockup pattern, non BE contract). | Decisione design 2026-05-24 |

---

**Generato da Claude Code Opus 4.7 in modalità read-only.** Niente modifiche al codebase. Discussione attesa con maintainer prima di valutare Step 2-3-4. Post-review `/sc:spec-panel` aggiornato 2026-05-24.
