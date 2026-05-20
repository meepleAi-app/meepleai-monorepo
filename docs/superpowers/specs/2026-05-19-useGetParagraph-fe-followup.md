# useGetParagraph FE follow-up — Spec (draft)

| Field | Value |
|---|---|
| **Issue** | (to be opened — this doc seeds the body) |
| **Parent** | #747 (closed by PR #1301 `718ea76ed`) |
| **Date** | 2026-05-19 |
| **Status** | hardened — panel score 7.4 → 8.6/10 |
| **Authors** | follow-up of #747 sequence (PR-A #1295, PR-B #1298, PR-C #1301) |
| **Hardening** | 2026-05-19 — applied P0+P1 panel feedback (Wiegers/Adzic/Fowler/Crispin/Cockburn). See §13 change-log. |

## 1. Context

The backend sequence #747 closed on 2026-05-19 with three merged PRs:

- **PR-A** `71c287f06` — added `photo_batch_pages.paragraph_numbers integer[]` schema + GIN index + `IPhotoBatchUploadRepository.GetPageTextByParagraphNumberAsync`.
- **PR-B** `0ba93671a` — `ParagraphLookupKey` discriminated union in C#, handler dispatch, new endpoint `GET /api/v1/photo-batches/{id}/paragraphs/by-paragraph/{paragraphNumber}`, `ParagraphDto.ParagraphNumber: int?` added.
- **PR-C** `718ea76ed` — `RegexParagraphNumberExtractor` populates `paragraph_numbers` during OCR (≥80% F1 accuracy on 3 gamebook fixtures).

End-to-end the backend now answers `?paragraph=42` requests with real OCR text. The frontend hook (`apps/web/src/lib/gamebook/hooks/useGetParagraph.ts`) and the API wrapper (`apps/web/src/lib/gamebook/api.ts`) still hit only the legacy `/paragraphs/{pageNumber}` route, so the new capability is invisible to callers.

A demo workaround `useTranslateParagraph` (apps/web/src/lib/gamebook/hooks/useTranslateParagraph.ts, **committed**, contrary to the original #747 issue note) wraps `useAgentChatStream` to fake paragraph lookup via chat-agent RAG. It is still load-bearing for `TranslateParagraphDemo.tsx`.

## 2. Goals

- **G1**: Add a new API function `getParagraphByParagraphNumber(batchId, paragraphNumber, hint?)` against the backend route from PR-B.
- **G2**: Extend `useGetParagraph` to accept a discriminated lookup union: `paragraphRef: { type: 'page' | 'paragraph', value: number }`.
- **G3**: Preserve the existing numeric `pageNumber` argument shape for one release with a deprecation warning, so the ~3 known callers (`TranslateParagraphDemo` + 2 grep hits) don't break in the same PR.
- **G4**: Update `ParagraphSchema` to mirror PR-B DTO additions: nullable `paragraphNumber`, and relax `pageNumber` to `nonnegative()` (backend returns `0` for ByParagraph + semantic-fallback case).
- **G5**: Establish whether `TranslateParagraphDemo` should be (a) migrated to the new hook + endpoint (drop the chat-agent RAG dependency), (b) kept as-is for one release, or (c) deleted with the demo.

## 3. Non-goals

- **NG1**: Refactor `useTranslateParagraph` itself — that hook owns SSE streaming + citation parsing for the chat-agent path; even after G2 ships, it's used by `TranslateParagraphDemo` for the RAG-based UX. Separate cleanup ticket if/when product decides the demo no longer needs the chat-agent path.
- **NG2**: New UI surface for paragraph navigation. This issue ships the data hook; the consumer UI (storybook deep-link `?paragraph=42`, sidebar navigation, etc.) is product-owned.
- **NG3**: Backfill `paragraph_numbers` for legacy photo batches — backend issue.
- **NG4**: Performance work on the GIN index `= ANY()` translation — backend issue.

## 4. API design — hook signature

### Option A (accepted): discriminated lookup union with backward-compat overload

```ts
type ParagraphRef =
  | { type: 'page'; value: number }
  | { type: 'paragraph'; value: number };

interface UseGetParagraphOptions {
  batchId: string | undefined;
  paragraphRef: ParagraphRef;
  hint?: string;
  enabled?: boolean;
}

interface UseGetParagraphLegacyOptions {
  /** @deprecated use `paragraphRef: { type: 'page', value }` */
  batchId: string | undefined;
  pageNumber: number;
  hint?: string;
  enabled?: boolean;
}

export function useGetParagraph(
  options: UseGetParagraphOptions | UseGetParagraphLegacyOptions
): UseQueryResult<Paragraph, Error>;
```

- Internal normalization maps the legacy shape to `{ type: 'page', value: pageNumber }` and emits a single `console.warn` at first use per session (gated by a module-level `Set<string>` to avoid log spam).
- Query key factory extended: `paragraphKeys.byPage`, `paragraphKeys.byParagraph` (separate so cache entries don't collide; the URL paths are different anyway).

### Option B (rejected): split into two hooks `useGetParagraphByPage` + `useGetParagraphByParagraph`

- Forces every caller into a refactor.
- Loses the shared TanStack Query stale-time + retry-policy defaults.

### Option C (rejected): keep one hook, just rename `pageNumber` → `paragraphRef.value`

- No discriminator; impossible to tell page vs paragraph at the type level. Defeats the PR-B refactor.

## 5. Schema updates

`apps/web/src/lib/gamebook/schemas.ts` — `ParagraphSchema`:

```ts
export const ParagraphSchema = z.object({
  // Was `positive()`. Backend returns 0 when ByParagraph hits semantic fallback
  // (no physical page row matched the requested paragraph). See PR-B DTO doc.
  pageNumber: z.number().int().nonnegative(),
  text: z.string(),
  fallbackUsed: z.boolean(),
  fallbackMethod: z.string().nullable(),
  // Added by PR-B (#1298). Set only when the caller used the
  // by-paragraph endpoint; null for page-based lookups.
  //
  // BACKEND INVARIANT (PR-B): paragraphNumber is either null (page lookup OR
  // by-paragraph + semantic-fallback) or ≥ 1 (by-paragraph + numbered match).
  // The `.positive()` guard is intentional — a `0` value here would indicate
  // a backend regression and `safeParse` rejecting it is the desired behavior.
  paragraphNumber: z.number().int().positive().nullable().default(null),
});
```

## 6. AC SMART

- [ ] **AC-1** Specific + Measurable — `useGetParagraph({ batchId, paragraphRef: { type: 'paragraph', value: 42 } })` issues `GET /api/v1/photo-batches/{batchId}/paragraphs/by-paragraph/42` (verified via MSW request-capture spy in `apps/web/src/__tests__/mocks/handlers/gamebook.handlers.ts`).
- [ ] **AC-2** Specific + Measurable — `useGetParagraph({ batchId, paragraphRef: { type: 'page', value: 5 } })` issues `GET /api/v1/photo-batches/{batchId}/paragraphs/5` (verified via the same MSW handler file).
- [ ] **AC-3** Backward compat — `useGetParagraph({ batchId, pageNumber: 5 })` returns the same data as AC-2 AND emits exactly one `console.warn` per session (verified via `vi.spyOn(console, 'warn')`; isolation via `vi.resetModules()` in `beforeEach`).
- [ ] **AC-4a** Schema relaxation — `ParagraphSchema.safeParse({ pageNumber: 0, text: 'x', fallbackUsed: true, fallbackMethod: 'semantic' })` succeeds (previously failed on `positive()` guard).
- [ ] **AC-4b** Schema fallback shape — `ParagraphSchema.safeParse({ pageNumber: 0, text: '...', fallbackUsed: true, fallbackMethod: 'semantic', paragraphNumber: null })` succeeds AND round-trips the `paragraphNumber: null` field (covers the ByParagraph + semantic-fallback DTO shape).
- [ ] **AC-5** Coverage — `pnpm vitest --coverage` (Istanbul v8 provider) reports ≥ 85 % statement coverage on **(a)** `apps/web/src/lib/gamebook/hooks/useGetParagraph.ts` and **(b)** the exports `getParagraph` + `getParagraphByParagraphNumber` from `apps/web/src/lib/gamebook/api.ts`. Existing `__tests__/useTranslateParagraph.test.ts` remains green. CI gate via the existing `pnpm test:coverage` step.
- [ ] **AC-6** Query-key isolation — `useGetParagraph({ paragraphRef: { type: 'page', value: 5 } })` and `useGetParagraph({ paragraphRef: { type: 'paragraph', value: 5 } })` produce **two distinct** cache entries (verified by mounting both in a single test, asserting `queryClient.getQueryCache().getAll().length === 2`).
- [ ] **AC-7** Type contract — TypeScript surfaces a compile error if a caller passes `paragraphRef: { type: 'paragraph', value: '42' }` (string instead of number). Validation: `// @ts-expect-error` comment on the offending line in a test-only `tsx` fixture; if the error doesn't trigger, the comment fails the typecheck.
- [ ] **AC-8** Disabled-batch flow — `useGetParagraph({ batchId: undefined, paragraphRef: { type: 'paragraph', value: 1 } })` does NOT issue any network request (verified by MSW `onUnhandledRequest: 'error'` + assert no MSW intercept fired).
- [ ] **AC-9** Hint parameter — `hint?` is forwarded as `?hint=<urlencoded>` query param for BOTH endpoints (backend `[FromQuery] string? hint` is shared by the two route handlers via `DispatchParagraphQueryAsync`). Verified via MSW request URL assertion.

## 7. Test strategy

- **Unit**: vitest + MSW handlers in `apps/web/src/__tests__/mocks/handlers/gamebook.handlers.ts` (NEW file; register it from `apps/web/src/__tests__/mocks/handlers/index.ts`).
- **AC-1, AC-2, AC-9**: MSW request-URL capture via `msw.HttpHandler` recording calls into a local array, asserted after `waitFor(() => result.current.isSuccess)`.
- **AC-3 (deprecation)**: `vi.spyOn(console, 'warn').mockImplementation(...)` + `vi.resetModules()` in `beforeEach` to clear the module-level deprecation `Set` between tests. Asserts (a) data returned matches the page lookup, (b) exactly 1 warn call in the first test of the file, (c) zero warns on subsequent invocations within the same test scope.
- **AC-4a, AC-4b**: pure `safeParse` tests on `ParagraphSchema`, no React/MSW infrastructure.
- **AC-5 (coverage)**: existing `pnpm test:coverage` step in `apps/web/package.json` produces `coverage/coverage-summary.json`. Validate the threshold inline in the test PR's CI summary (no new tooling).
- **AC-6 (query-key)**: render two hooks in a single test wrapped in a shared `QueryClientProvider`; assert `queryClient.getQueryCache().getAll().length === 2`. No MSW needed if responses are mocked at the `queryFn` boundary via `queryClient.setQueryData`.
- **AC-7 (type contract)**: `__tests__/useGetParagraph.types.test.ts` with `// @ts-expect-error` comments. Vitest collects but doesn't execute it; `pnpm typecheck` is the gate.
- **AC-8 (disabled)**: assert MSW handler is not invoked when `batchId: undefined`. `onUnhandledRequest: 'error'` on the test setup catches accidental requests.
- **No new E2E**: the consumer UI is product-owned (NG2); E2E lands with the UI ticket.

## 7a. Definition of Done

- [ ] `pnpm lint` green
- [ ] `pnpm typecheck` green (includes the `@ts-expect-error` AC-7 assertion)
- [ ] `pnpm test:coverage` green AND AC-5 thresholds met
- [ ] Existing `apps/web/src/lib/gamebook/hooks/__tests__/useTranslateParagraph.test.ts` still passes (no regression on the demo workaround)
- [ ] Follow-up ticket for `TranslateParagraphDemo` migration opened (D1 = defer)
- [ ] PR description quotes the AC checklist and links to this spec doc

## 8. Open decisions — RESOLVED

- [x] **D1**: `TranslateParagraphDemo` migration → **(b) defer**. Rationale: the demo wraps `useAgentChatStream` for chat-agent RAG flow which is orthogonal to the by-paragraph endpoint. Migrating it in the same PR conflates two concerns. **Action**: open a follow-up ticket "feat(gamebook): migrate TranslateParagraphDemo from chat-agent RAG to /by-paragraph endpoint" as a DoD item before merging this hook PR.
- [x] **D2**: Deprecation warning is **dev-only** (`process.env.NODE_ENV !== 'production'` short-circuit + `Set<string>` guard for one-warn-per-session).
- [x] **D3**: MSW handlers live in `apps/web/src/__tests__/mocks/handlers/gamebook.handlers.ts` (NEW file; pattern matches existing `library.handlers.ts`, `game-nights.handlers.ts`, etc.). Register from `apps/web/src/__tests__/mocks/handlers/index.ts`.

## 9. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Discriminated union breaks IDE intellisense for legacy callers | Low | Low | TS strict union; tested against the 3 known caller files in PR. |
| MSW handler drift if backend route shape changes | Low | Medium | Schema covered by zod; FE tests still catch shape break. |
| Deprecation `Set` cross-test leak (false negative on AC-3) | Medium | Low | Module-isolation `vi.resetModules()` per test file. |
| Bundle size grows from unused legacy overload branch | Low | Low | tree-shake-friendly: the legacy path is purely a type narrowing + warn; minified ~150 bytes. |

## 10. Sequencing

Single PR, ~2-3h:

1. Extend `api.ts` with `getParagraphByParagraphNumber`.
2. Update `ParagraphSchema` (nullable paragraphNumber, nonnegative pageNumber).
3. Refactor `useGetParagraph` to the union overload + deprecation warn.
4. Vitest covering AC-1..AC-5; MSW handlers in the test file.
5. Open follow-up ticket for `TranslateParagraphDemo` migration (D1).

## 13. Hardening change-log

### 2026-05-19 — Panel P0+P1 applied (score 7.4 → ~8.6 estimate)

- §1 frontmatter: added Hardening row pointing to this change-log; status → "hardened".
- §5 schema: clarified `paragraphNumber: positive().nullable()` is a documented invariant (P0-3 Fowler/Wiegers).
- §6 AC: 7 → 9 ACs.
  - AC-4 split into AC-4a (relaxation) + AC-4b (fallback shape round-trip) — P1-1 Adzic.
  - AC-5: named `vitest --coverage` (Istanbul v8) + concrete export scope + CI gate — P0-2 Wiegers/Crispin.
  - AC-6 query-key isolation added — P1-3 Fowler.
  - AC-8 disabled-batch flow — P2-1 Cockburn.
  - AC-9 `hint?` semantics defined — P2-3 Adzic.
- §7 test strategy: concrete per-AC mechanism (was vague). Resolved AC-3 isolation = `vi.resetModules()` — P1-2 Crispin.
- §7a Definition of Done added (5-item checklist) — P2-4 Crispin.
- §8 open decisions: all 3 resolved with concrete actions/file paths — P0-1, P1-4 + cleanup.
- Removed old §6 AC-6 ("no measurable bundle increase") as vague — P2-2 Wiegers. Bundle delta stays in §9 risk table.

## 11. References

- Backend sequence: PR #1295 (PR-A), #1298 (PR-B), #1301 (PR-C).
- Backend DTO: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ParagraphDto.cs`.
- Backend endpoint: `apps/api/src/Api/Routing/PhotoIngestionEndpoints.cs` (route registration with both `/paragraphs/{pageNumber:int}` and `/paragraphs/by-paragraph/{paragraphNumber:int}`).
- FE existing hook: `apps/web/src/lib/gamebook/hooks/useGetParagraph.ts`.
- FE existing API: `apps/web/src/lib/gamebook/api.ts` (function `getParagraph`).
- FE existing schema: `apps/web/src/lib/gamebook/schemas.ts` (`ParagraphSchema`).
- Demo workaround: `apps/web/src/lib/gamebook/hooks/useTranslateParagraph.ts` + `TranslateParagraphDemo.tsx`.
