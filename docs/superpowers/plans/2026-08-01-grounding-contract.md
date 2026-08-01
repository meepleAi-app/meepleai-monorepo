# Grounding Contract Invariant (#3388) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or superpowers:executing-plans. Steps use `- [ ]`.

**Goal:** Make `groundingStatus` a non-nullable contract invariant emitted by BOTH in-session agent paths (text-RAG SSE + image/text multimodal REST), remove the fabricated `confidence=0.85`, and surface a per-modality FE disclaimer for every ungrounded answer.

**Architecture:** New `GroundingStatus` enum in `Api.SharedKernel`. Derived from citations (`Grounded` iff citations>0, else `Ungrounded`; `Partial` reserved for #3390, no producer now). No EF migration — re-derivable from persisted `CitationsJson`. Wire representation is **string** on both paths (REST enum→`JsonStringEnumConverter`→PascalCase; SSE emits the enum's `.ToString()` string to avoid the numeric-enum SSE regime).

**Tech Stack:** .NET 9 (MediatR, SSE), Next.js/React (Vitest), xUnit + FluentAssertions + Moq.

## Global Constraints

- Issue #3388; branch `feature/issue-3388-grounding-contract` (parent `main-dev`).
- `enum GroundingStatus { Grounded = 0, Partial = 1, Ungrounded = 2 }`, namespace `Api.SharedKernel.Domain.Enums` (mirror `UserAccountStatus.cs`: file-scoped namespace, XML `<summary>`, `public enum`, numeric values).
- Wire value is the enum NAME string: `"Grounded"` / `"Partial"` / `"Ungrounded"` (PascalCase) on BOTH paths. FE compares `=== 'Ungrounded'`.
- `Grounded` iff `citations.Count > 0` else `Ungrounded`. No `Partial` producer.
- Remove fabricated `confidence 0.85f` (`ChatCommandHandlers.cs:201`, `:224`) → `null`. The SSE path's `confidence` is real (`ComputeConfidence`) — leave it.
- No EF migration. CQRS unchanged. Commit after each task. Kill testhost before BE runs. FE tests: `cd apps/web && pnpm test <file>`.

---

### Task 1: `GroundingStatus` enum + REST path (AskSessionAgent)

**Files:**
- Create: `apps/api/src/Api/SharedKernel/Domain/Enums/GroundingStatus.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/ChatCommands.cs:45-51` (add param), `.../Commands/ChatCommandHandlers.cs:201,224,266` (null confidence + emit grounding)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Handlers/ChatCommandHandlerTests.cs:242`

**Interfaces:**
- Produces: `public enum GroundingStatus { Grounded=0, Partial=1, Ungrounded=2 }`; `AskSessionAgentResult(Guid MessageId, string Answer, string AgentType, float? Confidence, string? CitationsJson, GroundingStatus GroundingStatus)`.

- [ ] **Step 1 (RED):** In `ChatCommandHandlerTests.cs` (the `AskSessionAgentCommandHandler` test that today asserts `result.Confidence.Should().Be(0.85f)` at `:242`): change it to `result.Confidence.Should().BeNull()` AND add `result.GroundingStatus.Should().Be(GroundingStatus.Ungrounded)` (add `using Api.SharedKernel.Domain.Enums;`).
- [ ] **Step 2:** Run `dotnet test ...Api.Tests.csproj --filter "FullyQualifiedName~ChatCommandHandlerTests" --nologo -v minimal` → FAIL (still 0.85 / no GroundingStatus member).
- [ ] **Step 3 (GREEN):** Create `GroundingStatus.cs`. In `ChatCommands.cs` add the positional param `GroundingStatus GroundingStatus` to `AskSessionAgentResult` (+ `using Api.SharedKernel.Domain.Enums;`). In `ChatCommandHandlers.cs`: replace `confidence = 0.85f;` at `:201` and `:224` with `confidence = null;`. At the return (`:266`) compute grounding = `Ungrounded` (this path never has citations) and pass it: `new AskSessionAgentResult(agentMessage.Id, answer, agentType, agentMessage.Confidence, null, GroundingStatus.Ungrounded)`. Add the `using`.
- [ ] **Step 4:** Run the filter → PASS. Also `--filter "Category=Unit&BoundedContext=SessionTracking"` (chat subset) → green.
- [ ] **Step 5:** Commit `feat(ai): GroundingStatus enum + honest confidence on image path (#3388)`.

---

### Task 2: SSE RAG path emits server-side `groundingStatus`

**Files:**
- Modify: `apps/api/src/Api/Models/Contracts.cs:147-158` (add field to `StreamingComplete`), `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs:710-719`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentMetricsTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1 except the concept (SSE emits a **string**, not the enum, to stay wire-consistent).
- Produces: `StreamingComplete` gains a trailing `string GroundingStatus` (string, not the enum — SSE serializes enums numerically). Value `"Grounded"`/`"Ungrounded"`.

- [ ] **Step 1 (RED):** In `ChatWithSessionAgentMetricsTests.cs`, in the zero-citation test (~`:46`) drain the stream, capture the `StreamingComplete` from the terminal `Complete` event, and assert its `GroundingStatus == "Ungrounded"`; add a with-citations case asserting `"Grounded"`. (Mirror the existing `BuildHandler(resolvedCitations, ...)` + `await foreach` drain pattern.)
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~ChatWithSessionAgentMetricsTests"` → FAIL (no GroundingStatus member).
- [ ] **Step 3 (GREEN):** Add `string GroundingStatus` as a trailing param on `record StreamingComplete(...)` (`Contracts.cs`). At `ChatWithSessionAgentCommandHandler.cs:710-719` set it: `GroundingStatus: citationDtos.Count > 0 ? "Grounded" : "Ungrounded"`. (Keep the real `confidence` unchanged.)
- [ ] **Step 4:** Run the filter → PASS. `--filter "Category=Unit&BoundedContext=KnowledgeBase&FullyQualifiedName~ChatWithSessionAgent"` green.
- [ ] **Step 5:** Commit `feat(ai): SSE RAG path emits groundingStatus in StreamingComplete (#3388)`.

---

### Task 3: FE reads server `groundingStatus` on both paths

**Files:**
- Modify: `apps/web/src/lib/domain-hooks/useSessionAgentChat.ts:24-36,319-337,354-363`, `apps/web/src/components/features/session-live/LiveAgentChat.tsx:44-58`, `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx:1256-1269`
- Test: `apps/web/src/lib/domain-hooks/__tests__/useSessionAgentChat.test.ts`, `.../_components/__tests__/SessionLiveView.test.tsx`

**Interfaces:**
- Consumes: backend `groundingStatus` string (`"Grounded"|"Partial"|"Ungrounded"`) on SSE `StreamingComplete.data` and on the multipart JSON response.
- Produces: `ChatMessage.groundingStatus?: 'Grounded'|'Partial'|'Ungrounded'` on BOTH the hook interface (`useSessionAgentChat.ts:24`) and the component interface (`LiveAgentChat.tsx:44`). `isNonGrounded` becomes derived from `groundingStatus === 'Ungrounded'` (keep the field for the disclaimer condition).

- [ ] **Step 1 (RED):** In `useSessionAgentChat.test.ts`, extend the `sseComplete(...)` wire builder to include `groundingStatus: 'Ungrounded'` in the `data`, and assert the produced assistant `ChatMessage` has `groundingStatus === 'Ungrounded'` (and `isNonGrounded === true`). In `SessionLiveView.test.tsx` (image branch, near the `:450` disclaimer stub), assert the image-branch agent message carries grounding when the mocked `/chat/ask-agent` JSON returns `{ answer, groundingStatus: 'Ungrounded' }`.
- [ ] **Step 2:** Run `pnpm test src/lib/domain-hooks/__tests__/useSessionAgentChat.test.ts` (and the SessionLiveView test) → FAIL.
- [ ] **Step 3 (GREEN):** Add `groundingStatus?: 'Grounded'|'Partial'|'Ungrounded'` to both `ChatMessage` interfaces. In `useSessionAgentChat.ts` SSE_COMPLETE handler (`:319`) read `(event.data as {...; groundingStatus?: string}).groundingStatus` and set it on the assistant message (`:354-363`); derive `isNonGrounded = groundingStatus === 'Ungrounded'` (fallback to the old citations heuristic only if `groundingStatus` is absent, for resilience). In `SessionLiveView.tsx:1256` widen the cast to `{ answer?: string; confidence?: number; groundingStatus?: string }` and set `groundingStatus`/`isNonGrounded: json.groundingStatus === 'Ungrounded'` on the message object (`:1259-1268`).
- [ ] **Step 4:** Run both tests → PASS; `pnpm typecheck` clean.
- [ ] **Step 5:** Commit `feat(ai): FE reads server groundingStatus on both agent paths (#3388)`.

---

### Task 4: Per-modality disclaimer + remove fabricated confidence from UI

**Files:**
- Modify: `apps/web/src/components/features/session-live/LiveAgentChat.tsx:240-250`, `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/components/features/session-live/__tests__/LiveAgentChat.test.tsx`

**Interfaces:**
- Consumes: `ChatMessage.groundingStatus` (Task 3). Needs a way to know the modality — the image-branch messages already differ; add an optional `modality?: 'text' | 'image'` to the component `ChatMessage` (default text), set `'image'` on the image-branch message in `SessionLiveView.tsx`.

- [ ] **Step 1 (RED):** In `LiveAgentChat.test.tsx`, add a test: an ungrounded agent message with `modality: 'image'` renders the image-specific disclaimer copy (assert `data-slot="chat-nongrounded-disclaimer"` present and text = the new image key), and an ungrounded text message renders the existing copy. Add the two new i18n keys to the test's `INTL_MESSAGES`.
- [ ] **Step 2:** Run `pnpm test src/components/features/session-live/__tests__/LiveAgentChat.test.tsx` → FAIL.
- [ ] **Step 3 (GREEN):** Add i18n keys under `pages.sessionLive.chatAgent`: keep `nonGroundedDisclaimer` (text) and add `nonGroundedDisclaimerImage` = IT `"Ho risposto dalla foto e dalla mia conoscenza del gioco, non dal regolamento ufficiale — verifica sul manuale"`, EN `"I answered from the photo and my game knowledge, not the official rulebook — check the manual"`. In `LiveAgentChat.tsx:240-250` pick the key by `msg.modality === 'image' ? '...Image' : 'nonGroundedDisclaimer'`. Add `modality?: 'text'|'image'` to the component `ChatMessage` interface; in `SessionLiveView.tsx` set `modality: 'image'` on the image-branch message. Grep the file for any rendered `confidence` on the agent bubble and remove it if shown as a measured value (the image JSON `confidence` is now `null`; ensure nothing renders it as authoritative).
- [ ] **Step 4:** Run the test → PASS; `pnpm typecheck` + `pnpm lint` clean.
- [ ] **Step 5:** Commit `feat(ai): per-modality non-grounded disclaimer + drop fabricated confidence (#3388)`.

---

## Self-Review

- **Spec/DoD coverage:** groundingStatus non-nullable both paths → T1 (REST) + T2 (SSE). image→Ungrounded → T1. FE disclaimer both modalities → T3+T4. No fabricated confidence → T1 (BE null) + T4 (UI). Mode-parity test → T1 asserts `AskSessionAgentResult.GroundingStatus` non-null Ungrounded; T2 asserts `StreamingComplete.GroundingStatus` emitted — together they prove both contracts carry a non-nullable grounding signal for the same (non-grounded) question. Add a one-line comment in the T2 test referencing the parity intent.
- **Type consistency:** enum `GroundingStatus` (BE, T1) vs wire **string** `"Grounded"/"Ungrounded"` (SSE T2 + REST via converter) vs FE union `'Grounded'|'Partial'|'Ungrounded'` (T3). Casing PascalCase throughout — FE compares `=== 'Ungrounded'`.
- **Two-serialization-regime risk (documented):** SSE would render a C# enum numerically; T2 emits a `string` on `StreamingComplete` to match the REST string. Verify the SSE test asserts the string, not a number.
- **Out of scope:** routing the image path through retrieval (#3390); a `Partial` producer.
