# ADR-075 — Deep Link Versioning and Backward Compatibility

**Status**: Accepted — implemented (Option D `NotificationRoutes`) in #2996
**Date**: 2026-06-15 (ratified/implemented 2026-07-16)
**Deciders**: @badsworm
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 4 — US-INT-5 (notifications & deep links)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · `apps/web/next.config.js` (redirects block) · issue #897 (branch retirement + route consolidation)

> **Implementation note (2026-07-16, #2996).** Option D shipped. The `NotificationRoutes` static class
> lives at `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Constants/NotificationRoutes.cs`
> (note: `Constants/`, not `Application/Services/` as sketched in §Implementation Guidance — the
> cross-language drift gate `scripts/lint-cross-lang-constants.sh` globs `**/Constants/*Routes.cs`), with
> the FE twin at `apps/web/src/lib/constants/notification-routes.ts`. All notification deep-link literals
> in the ~35 event handlers/jobs that populate `Notification.Link` were migrated to the constants (the two
> genuinely dynamic sinks — admin manual notification and the n8n webhook — remain free-form by design).
> The `*Template` constants carry a literal `{id}` token substituted by builder methods, producing output
> byte-identical to the previous `$"…{guid}…"` interpolations. The short-term drift lint is now **active**
> (was a no-op until the first `*Routes` pair landed) and green; BE/FE unit tests (`NotificationRoutesTests.cs`,
> `notification-routes.test.ts`) pin each side to the same golden set.

## Context

MeepleAI's frontend uses Next.js App Router with a well-established `next.config.js` `redirects()` block for managing route renames. As of 2026-06-15, the redirect block contains **19+ permanent 301 redirect rules** handling at least three distinct historical route migrations:

- **Issue #5039** — User route consolidation: `/library/games/:id/:tab` → `/library/:id?tab=:tab` (7 rules).
- **Issue #871** — SP6 play routes: `/library/games/:id/play/**` → `/library/:id/play/**` (3 rules).
- **Issue #1672 / #1004** — Profile/settings consolidation: `/settings/notifications` → `/profile?tab=settings&section=notifications`, `/profile/settings/**` → `/profile?tab=settings`, `/settings` → `/profile?tab=settings`.
- **Discover hub** (Asse D P2, issue #2309): old `/discover` routes preserved as aliases of the new `/games?tab=discover` hub.

Additionally, the `Notification` aggregate has a `Link: string?` field (the deep link path, stored in the `notifications.link` column as a relative path like `/library/abc123/play/xyz`). **Notification deep links are stored at dispatch time** — a notification dispatched before a route migration retains the old path in the DB forever. If the old path is not redirected in `next.config.js`, clicking the notification opens a 404.

**Route group discipline** (memory: `route-group-audience-not-feature.md`): `(public)/(authenticated)/(admin)` segments define who accesses a route, not what feature it belongs to. The route `(authenticated)/games/[id]` and `(authenticated)/library/[id]` share the same authenticated audience group. Route group changes do not change the visible URL.

**BGG asset ban** (CLAUDE.md, issue #2123): `/discover` routes were affected by the BGG ban migration. The `next.config.js` redirect logic for discover routes is already in place.

**Issue #897 note**: `frontend-dev` and `backend-dev` branches were retired; all routes now target `main-dev`. This is a branch-level change, not a URL route change, and does not affect deep links.

The `Notification.Link` field is populated by event handlers at dispatch time (e.g. `GameNightPublishedNotificationHandler` sets `DeepLinkPath = $"/game-nights/{eventId}"`). These paths are stored in the `notifications` table and never updated post-dispatch. If the route `/game-nights/{id}` is later renamed to `/events/{id}`, all existing notification links become stale.

## Problem

The specific architectural question: **how should deep links embedded in notifications remain valid as the Next.js route structure evolves, without requiring retroactive DB updates to stored notification links?**

Sub-decisions:
1. Should deep link paths be versioned (`/v1/game-nights/:id` → `/v2/game-nights/:id`)?
2. Should redirect rules cover all stored link patterns, or only "shared" paths (those embedded in emails)?
3. Should notification deep links be stored as absolute paths or as semantic identifiers (`entity:type:id`) resolved at render time?

## Options Considered

### Option A — 301 Redirects in next.config.js (current pattern, formalised)

For every route rename that affects a deep-linked surface, add a `permanent: true` redirect entry in `next.config.js`. The rule maps the old URL to the new canonical URL. Existing notification `Link` values in the DB remain unchanged; the browser follows the redirect transparently.

**Pros**:
- Already the established pattern in the codebase (19+ redirect rules in production). No new infrastructure.
- Transparent to users: clicking a notification link follows the 301 redirect to the new canonical URL — the user sees the correct page.
- No DB migration: notification link values are preserved as-is.
- Next.js `redirects()` supports dynamic segments (`:id`, `:campaignId`, `*` wildcards) — complex route renames are expressible.
- SEO-safe: 301 is a permanent redirect that search engines honour.

**Cons**:
- Redirect table grows with each route migration. After many migrations, the table becomes a historical log of every route rename — maintenance overhead.
- Redirect chains: if `/old-1` → `/new-1` and later `/new-1` → `/new-2`, Next.js does not automatically collapse the chain. Each intermediate hop requires an explicit rule, or the chain adds latency for clients following multiple 301s.
- Does not cover email-embedded deep links if email contains a fully-qualified URL (`https://meepleai.app/game-nights/abc`) and the route rename happens after the email is sent — the 301 redirect handles this correctly, but the email content shows the old path in the link text.
- Requires a deploy to activate new redirects — zero-downtime concern if the route rename and redirect ship in the same PR (they must).

**Risks**: Low. The existing redirect infrastructure is tested and operational. Risk is mainly **missing a redirect** when a route is renamed — this leaves old notification links as 404s until discovered and fixed. A lint rule or test checking that all notification `DeepLinkPath` patterns have corresponding redirect coverage would mitigate this.

**Impact**: ~0 additional infrastructure. Operational discipline required: every route rename PR must include the corresponding redirect rule in `next.config.js`.

---

### Option B — Versioned URL Prefix (/v1/x → /v2/x)

All deep-linked notification URLs use a version prefix: `/v1/game-nights/:id`. When the route is redesigned, the new URL is `/v2/game-nights/:id`. The API version prefix disambiguates old and new clients.

**Pros**:
- Explicit versioning: clients that stored `/v1/` links always get the v1 route; new links use `/v2/`. No implicit redirect needed between versions.
- Clear contract: the version number signals which route schema applies.

**Cons**:
- This pattern is from REST API versioning, not frontend URL design. Next.js App Router routes are not versioned in the MeepleAI codebase — the existing canonical routes are `/library/:id`, `/game-nights/:id`, `/games/:id`, none of which have a `/v1/` prefix.
- Retroactively adding `/v1/` prefixes to all existing notification deep link paths would require a DB migration across all stored `notifications.link` values — high risk of data corruption if any path does not match expected patterns.
- Creates a confusing multi-version URL space: users see `/v1/game-nights/abc` in their browser address bar — unprofessional and confusing.
- Incompatible with Next.js App Router conventions and the existing route group discipline (memory: `route-group-audience-not-feature.md`).

**Risks**: High adoption friction. Not recommended.

**Impact**: ~5 days. DB migration + Next.js route group refactor + all notification handler updates. Out of scope.

---

### Option C — Catch-All Middleware with Route Alias Map

A Next.js `middleware.ts` intercepts all requests and consults a `routeAliasMap` (a JSON map of `{old: string, new: string}[]` loaded at middleware startup). On match, the middleware issues a `NextResponse.redirect()` to the new canonical URL.

**Pros**:
- Runtime-configurable: the alias map can be updated without a Next.js deploy (if loaded from a config API or environment variable at runtime).
- Handles complex pattern matching (regex, wildcard) not easily expressible in `next.config.js` static redirects.

**Cons**:
- Middleware runs on every request, including API routes and static assets — performance impact unless carefully filtered with `config.matcher`.
- `next.config.js` `redirects()` already runs at the CDN edge before middleware — adding a middleware layer for the same purpose duplicates the concern.
- The alias map is a new config artifact that must be maintained in sync with `next.config.js`. Two sources of truth for redirect logic.
- Not needed: the existing `next.config.js` redirect approach handles all current migration patterns without middleware.

**Risks**: Middleware edge cases (auth redirects, API routes, `_next` static) require careful matcher exclusions. Adds operational complexity without benefit over Option A.

**Impact**: ~2 days. New middleware file + route alias map management. Not recommended.

---

### Option D — Shared Deep Links Only (pragmatic scope, recommended)

Formalise a policy: **only notification deep link paths that are embedded in outbound emails** (i.e. "shared" links that cannot be retrieved from the DB to update) require a redirect rule in `next.config.js`. In-app notification links (stored in `notifications.link`) are **retroactively updatable** by a DB migration when a route is renamed, because they are not cached in email inboxes.

**Two-tier strategy**:
1. **Email-embedded links** (sent via Resend, stored in email body): always add a `permanent: true` redirect for any renamed route that was embedded in a sent email. These cannot be updated post-dispatch.
2. **In-app notification links** (`notifications.link` DB column): add a redirect rule (preferred, zero risk) OR run a targeted DB update (`UPDATE notifications SET link = replace(link, '/old-path/', '/new-path/')`) at migration time (for high-confidence, scope-limited updates).

**Additionally**: notification event handlers must use **relative path constants** (e.g. `NotificationRoutes.GameNight(eventId)` → `$"/game-nights/{eventId}"`) defined in a central `NotificationRoutes` static class. When a route changes, updating `NotificationRoutes` is the single-location change, and the compiler flags all usages. Old stored links are covered by a redirect rule or DB migration.

**Pros**:
- Pragmatic: distinguishes "links we can fix" (in-app, DB-updatable) from "links we cannot fix" (emails, already delivered).
- `NotificationRoutes` static class acts as a compile-time contract for deep link patterns — reduces ad-hoc string construction in handlers.
- Consistent with the existing redirect approach (Option A) for email links, while offering a DB migration escape hatch for in-app links.
- No new infrastructure.

**Cons**:
- The DB update approach for in-app links carries risk if the path pattern is not unique or if the `replace()` function matches unintended substrings. Must be scoped carefully (e.g. `WHERE link LIKE '/game-nights/%'` to limit scope).
- The `NotificationRoutes` static class is a new convention — existing handlers must be updated to use it (10+ event handlers reference deep link paths as inline strings today).

**Risks**: Low if the `NotificationRoutes` static class is introduced carefully. DB path updates are reversible (the old path can be restored from audit logs or a DB snapshot).

**Impact**: ~1.5 days. `NotificationRoutes` static class + handlers updated + redirect-rule discipline documented.

## Decision

**Adopt Option D**: two-tier deep link strategy with `NotificationRoutes` static class. Email-embedded links always get a `next.config.js` redirect on route rename. In-app notification links are covered by either a redirect rule (preferred) or a scoped DB update when the redirect would be impractical.

**Rationale**: Option A alone (pure redirect table growth) is viable but does not address the root cause — ad-hoc string construction in handlers creates a maintenance blind spot when routes change. Option D adds `NotificationRoutes` as a compile-time guard that makes route changes visible at the point of handler authoring, while keeping the redirect-rule discipline for email links. Options B and C are over-engineered for the current scale and team size.

## Consequences

**Positive**:
- `NotificationRoutes.GameNight(id)`, `NotificationRoutes.Library(id)`, `NotificationRoutes.Session(id)` etc. are the single place to update when routes change.
- Email deep links are guaranteed to redirect correctly via `next.config.js` entries — same guarantee as the existing 19+ redirect rules.
- In-app notification links can be surgically repaired by a DB migration when a route rename occurs — no stale 404 links accumulate.

**Negative**:
- Existing handlers use inline string construction (`$"/game-nights/{eventId}"`) — migrating them to `NotificationRoutes` is a refactor effort.
- Two-tier logic (redirect vs DB update) adds decision overhead when a route rename occurs: engineers must check whether the old path appears in email bodies or only in in-app notifications.

**Trade-offs**:
- The `next.config.js` redirect table will continue to grow with each route rename. This is an accepted trade-off: the table is the historical record of route aliases, and Next.js compiles it into the edge runtime efficiently. A comment lint rule or CI test that warns when the redirect count exceeds a threshold (e.g. 40 rules) can prompt periodic consolidation.

## Implementation Guidance

1. **`NotificationRoutes` static class**: create `apps/web/src/lib/constants/notification-routes.ts` (the FE file MUST live under `lib/constants/` — this is what `scripts/lint-cross-lang-constants.sh` globs; `lib/notifications/` would make the drift gate silently no-op) with typed path constructors:
   ```typescript
   export const NotificationRoutes = {
     gameNight: (id: string) => `/game-nights/${id}`,
     library: (id: string) => `/library/${id}`,
     session: (id: string) => `/sessions/${id}`,
     discover: () => `/games?tab=discover`,
     kbDetail: (id: string) => `/knowledge-base/${id}`,
   } as const;
   ```
   C# equivalent in `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Constants/NotificationRoutes.cs` (BE file MUST live under `**/Constants/` for the same lint glob):
   ```csharp
   internal static class NotificationRoutes
   {
       public static string GameNight(Guid id) => $"/game-nights/{id}";
       public static string Library(Guid id) => $"/library/{id}";
       public static string Session(Guid id) => $"/sessions/{id}";
       public static string KnowledgeBase(Guid id) => $"/knowledge-base/{id}";
   }
   ```

2. **Redirect rule discipline**: every PR that renames a route that is used in at least one notification handler must include a `next.config.js` redirect entry. The PR checklist in `CONTRIBUTING.md` should include: "If this PR renames a user-facing route, add a `permanent: true` redirect in `next.config.js`."

3. **Existing handler migration**: update the 5 highest-traffic notification event handlers to use `NotificationRoutes` in the same PR as the static class introduction: `GameNightPublishedNotificationHandler`, `PdfNotificationEventHandler`, `VectorDocumentReadyNotificationHandler`, `ShareRequestApprovedNotificationHandler`, `GameNightCancelledNotificationHandler`.

4. **DB update pattern**: when a route rename is small-scope and only affects in-app notifications (not emails), the `down migration` SQL:
   ```sql
   UPDATE notifications
   SET link = replace(link, '/old-route/', '/new-route/')
   WHERE link LIKE '/old-route/%';
   ```
   Include this in the EF Core migration's `migrationBuilder.Sql(...)` block.

## Rollback / Reversibility

`NotificationRoutes` is an additive refactor — removing it reverts handlers to inline string construction. `next.config.js` redirect rules can be removed if the old route is restored. DB `replace()` updates can be reversed by applying the inverse pattern. No breaking schema changes.

## References

- `next.config.js` redirects block — `apps/web/next.config.js:164-290` (19+ redirect rules)
- `Notification.Link` — `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/Notification.cs:17`
- `NotificationEntity.Link` — `apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationEntity.cs:62`
- `NotificationDispatcher.DeepLinkPath` — `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs:73`
- `GameNightPublishedNotificationHandler` (example handler with inline deep link path)
- Memory: `route-group-audience-not-feature.md` (audience vs feature routing discipline)
- Issue #897 (route consolidation precedent — `frontend-dev`/`backend-dev` retirement)

---

## Update 2026-06-16 — drift-detection mitigation (brainstorm #2383)

Per the 2026-06-16 brainstorm session on umbrella [#2383](https://github.com/meepleAi-app/meepleai-monorepo/issues/2383), the drift risk between the future BE `NotificationRoutes` C# class and FE `notification-routes` TypeScript module is addressed in two phases:

**Short-term (lands now)**: auto-discovery lint script in CI.
- Path: `scripts/lint-cross-lang-constants.sh`
- Convention: globs `apps/api/src/Api/**/Constants/*Routes.cs` (BE) and `apps/web/src/lib/constants/*-routes.ts` (FE). Pairs files by name (PascalCase ↔ kebab-case) and hash-compares the string-key sets.
- Behaviour: **no-op** until the first BE/FE pair lands (zero matches → exit 0). Fails fast on key drift once the pair exists. Wired into `ci.yml` after the BGG ToS compliance gate.
- Effort: ~30 minutes (script + CI step).

**Long-term (deferred, tracked separately)**: JSON single-source-of-truth + codegen.
- Source: `infra/contracts/notification-routes.json` (single canonical schema).
- Codegen: `quicktype.io` step in the build pipeline emits both `NotificationRoutes.cs` and `notification-routes.ts` from the JSON.
- Effort: ~3 days, triggered when the cross-language constants surface grows to 3+ pairs OR drift incidents are observed in production.

The short-term lint is sufficient for the foreseeable future given the slow growth of cross-language constants in the codebase. The long-term codegen is an availability fallback if/when the count of pairs makes manual sync error-prone.

Brainstorm session output: `docs/superpowers/specs/2026-06-16-adr-069-toolkit-suggestion-cache-design.md` (companion spec from the same session, captures the ADR-069 follow-up).
