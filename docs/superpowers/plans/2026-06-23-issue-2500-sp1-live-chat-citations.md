# SP1 — #2500 Chat-RAG citazioni nella superficie live (wiring FE) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Far sì che, nella superficie live canonica (`SessionLiveView → ChatAgentPanel → LiveAgentChat`), le risposte dell'agente RAG mostrino le **citazioni** (documento + pagina + snippet), consumando ciò che il BE già produce e persiste.

**Architecture:** FE-only. Il BE è completo: `ChatWithSessionAgentCommandHandler` (KnowledgeBase) produce `CitationDto` copyright-tier-aware, li streamma in `StreamingComplete.Citations` e li persiste in `ChatMessage.CitationsJson`. Lo schema FE `CitationSchema` (con `copyrightTier`) esiste già. SP1 estrae le citazioni dove oggi vengono scartate, le mappa al modello di rendering, e monta `ChatCitationCard`. Inoltre **collega l'agente RAG** al pannello live (oggi usa la chat *sociale*), replicando il pattern già funzionante in `RulesExplainer.tsx`.

**Tech Stack:** React 19 / Next.js 16, Vitest + Testing Library, Zod schemas, SSE streaming via `useSessionAgentChat`.

## Global Constraints

- **Design system**: token semantici (`bg-card`, `text-foreground`, …); ESLint `local/no-hardcoded-color-utility` è **error**. Riusare `ChatCitationCard` esistente (già conforme).
- **Decisione SP0 (ADR-083 § Update 3)**: la chat live canonica È l'agente RAG (`/agent/chat` keyed su `LiveGameSession.Id`); il path non-canonico `AskSessionAgentCommandHandler` resta out-of-scope.
- **TDD**: ogni task red→green. FE test mockati (no LLM reale).
- **AC di riferimento** (spec `2026-06-23-epic-2501-fase2-live-session-feature-gaps.md`): AC-CHAT-0/1/2/3/4/5 + AC-CHAT-NULL.

---

## File Structure

| File | Responsabilità | Azione |
|---|---|---|
| `apps/web/src/lib/api/schemas/streaming.schemas.ts` | `CitationSchema` (esiste, `:45-58`) — riuso | invariato |
| `apps/web/src/lib/session-live/map-citation-to-chat-citation.ts` | **NUOVO** — mapper puro `CitationSchema → ChatCitation` (tier-aware) | Create |
| `apps/web/src/lib/domain-hooks/useSessionAgentChat.ts` | hook agente RAG: estrarre `citations` dall'evento `complete` (`:122-140`), aggiungerle all'assistant `ChatMessage` (`:16-21`, `:146-152`) | Modify |
| `apps/web/src/components/features/session-live/LiveAgentChat.tsx` | `ChatMessage` (`:34-41`) + campo `citations`; render `ChatCitationCard` (`:168-193`) | Modify |
| `apps/web/src/components/chat/panel/ChatCitationCard.tsx` | render citazione (esiste, `:4-69`) — riuso | invariato |
| `apps/web/src/components/features/session-live/ChatAgentPanel.tsx` | wiring agente RAG (oggi delega messages prop) | Modify |
| `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` | lancio agente (`agentSessionId`) + `gameContext` da `LiveSessionDto`; passa la chat agente RAG al pannello | Modify |

**Pattern di riferimento per il wiring agente** (Task 5): `apps/web/src/components/game-night/RulesExplainer.tsx:49,380` (già usa `useSessionAgentChat` con `agentSessionId` + store gameContext).

---

## Task 1: Mapper `CitationSchema → ChatCitation` (tier-aware)

**Files:**
- Create: `apps/web/src/lib/session-live/map-citation-to-chat-citation.ts`
- Test: `apps/web/src/lib/session-live/__tests__/map-citation-to-chat-citation.test.ts`

**Interfaces:**
- Consumes: `Citation` (z.infer di `CitationSchema`, `streaming.schemas.ts:45-58` — campi `documentId`, `source`, `page`/`pageNumber`, `text`/`snippet`, `copyrightTier: 'full'|'protected'`, `paraphrasedSnippet`).
- Produces: `mapCitationToChatCitation(c: Citation): ChatCitation | null` — `ChatCitation` = `{ documentName, pages: number[], excerpt, openUrl? }` (da `ChatCitationCard.tsx:4-9`).

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect } from 'vitest';
import { mapCitationToChatCitation } from '../map-citation-to-chat-citation';

const base = { source: 'Regolamento Azul', documentId: 'doc-1', pageNumber: 7, copyrightTier: 'full' as const };

describe('mapCitationToChatCitation', () => {
  it('full tier → excerpt from snippet/text', () => {
    const r = mapCitationToChatCitation({ ...base, snippet: 'Posiziona la plancia.' } as never);
    expect(r).toEqual({ documentName: 'Regolamento Azul', pages: [7], excerpt: 'Posiziona la plancia.' });
  });
  it('protected tier → excerpt from paraphrasedSnippet, never verbatim', () => {
    const r = mapCitationToChatCitation({ ...base, copyrightTier: 'protected', snippet: null, paraphrasedSnippet: 'Sintesi della regola.' } as never);
    expect(r?.excerpt).toBe('Sintesi della regola.');
  });
  it('protected with no paraphrase and no snippet → excerpt empty string is NOT produced (returns null)', () => {
    const r = mapCitationToChatCitation({ ...base, copyrightTier: 'protected', snippet: null, paraphrasedSnippet: null } as never);
    expect(r).toBeNull();
  });
  it('uses page when pageNumber absent', () => {
    const r = mapCitationToChatCitation({ source: 'Doc', page: 3, copyrightTier: 'full', snippet: 'x' } as never);
    expect(r?.pages).toEqual([3]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails** — `pnpm vitest run src/lib/session-live/__tests__/map-citation-to-chat-citation.test.ts` → FAIL (modulo mancante).

- [ ] **Step 3: Write minimal implementation**

```ts
import type { z } from 'zod';
import type { CitationSchema } from '@/lib/api/schemas/streaming.schemas';
import type { ChatCitation } from '@/components/chat/panel/ChatCitationCard';

type Citation = z.infer<typeof CitationSchema>;

/**
 * Maps a streaming RAG citation to the ChatCitationCard model.
 * Tier-aware: 'full' uses the verbatim snippet/text; 'protected' uses the
 * paraphrasedSnippet only (never verbatim). Returns null when there is no
 * displayable excerpt (AC-CHAT-2/3: no empty-excerpt card).
 */
export function mapCitationToChatCitation(c: Citation): ChatCitation | null {
  const page = c.pageNumber ?? c.page ?? null;
  const excerpt =
    c.copyrightTier === 'protected'
      ? (c.paraphrasedSnippet ?? '').trim()
      : (c.snippet ?? c.text ?? '').trim();
  if (!excerpt) return null;
  return {
    documentName: c.source,
    pages: page != null ? [page] : [],
    excerpt,
    // AC-CHAT-5 (MVP): no anchored viewer → no deep-link CTA.
  };
}
```

- [ ] **Step 4: Run test to verify it passes** — same command → PASS.
- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat(session-live): #2500 citation→ChatCitation mapper (tier-aware)"`

---

## Task 2: `useSessionAgentChat` estrae le citazioni (oggi scartate)

**Files:**
- Modify: `apps/web/src/lib/domain-hooks/useSessionAgentChat.ts:16-21,122-140,146-152`
- Test: `apps/web/src/lib/domain-hooks/__tests__/useSessionAgentChat.test.ts` (esistente — estendere)

**Interfaces:**
- Consumes: mapper Task 1; payload SSE `complete` con campo `citations: Citation[]` (oggi il parsing tipa solo `{type, content?, threadId?}`).
- Produces: `ChatMessage.citations?: ChatCitation[]` sull'assistant message.

- [ ] **Step 1: Write the failing test** — aggiungere a `useSessionAgentChat.test.ts` un test che mocka un response SSE con un evento `data: {"type":"complete","threadId":"t1","citations":[{"source":"Reg","pageNumber":7,"copyrightTier":"full","snippet":"abc"}]}` e asserisce che l'ultimo assistant message abbia `citations` con `pages:[7]`, `excerpt:'abc'`. (Seguire il pattern di mock `fetch`/`ReadableStream` già presente nel file di test, righe ~20-67.)

- [ ] **Step 2: Run** → FAIL (`citations` undefined sull'assistant message).

- [ ] **Step 3: Implement** — in `useSessionAgentChat.ts`:
  1. `ChatMessage` (`:16-21`): aggiungere `citations?: ChatCitation[];` (import `ChatCitation` + `mapCitationToChatCitation` + `CitationSchema`).
  2. Aggiungere un ref `const citationsRef = useRef<ChatCitation[]>([]);` (reset a `[]` all'inizio di `ask`).
  3. Parsing `complete` (`:133`): tipizzare il payload con `citations?: unknown[]` e, quando presente, `citationsRef.current = (CitationSchema.array().safeParse(payload.citations).data ?? []).map(mapCitationToChatCitation).filter((x): x is ChatCitation => x !== null);`
  4. Assistant message (`:146-152`): `citations: citationsRef.current.length ? citationsRef.current : undefined`.

- [ ] **Step 4: Run** → PASS (+ test esistenti del file verdi).
- [ ] **Step 5: Commit** — `feat(session-live): #2500 extract RAG citations in useSessionAgentChat`

---

## Task 3: `LiveAgentChat` renderizza `ChatCitationCard`

**Files:**
- Modify: `apps/web/src/components/features/session-live/LiveAgentChat.tsx:34-41,168-193`
- Test: `apps/web/src/components/features/session-live/__tests__/LiveAgentChat.test.tsx` (esistente — estendere)

**Interfaces:**
- Consumes: `ChatMessage` con `citations?: ChatCitation[]`; `ChatCitationCard` (`:15-69`).
- Produces: render di N `ChatCitationCard` sotto il bubble dell'assistant.

- [ ] **Step 1: Write the failing test** — render `LiveAgentChat` con un messaggio assistant che ha `citations: [{ documentName:'Reg Azul', pages:[7], excerpt:'Posiziona' }]`; asserire `screen.getByText(/Reg Azul/)` e `/pag\. 7/`. Aggiungere un secondo test: messaggio senza citazioni → `queryByText(/pag\./)` è `null` (AC-CHAT-3).

- [ ] **Step 2: Run** → FAIL.

- [ ] **Step 3: Implement** — in `LiveAgentChat.tsx`:
  1. `ChatMessage` (`:34-41`): aggiungere `readonly citations?: readonly ChatCitation[];` (import `ChatCitation` + `ChatCitationCard`).
  2. Nel map dei messaggi (`:168-193`), dopo il bubble `{msg.content}`: `{msg.citations && msg.citations.length > 0 && (<div data-slot="chat-citations" className="mt-1 flex flex-col gap-1">{msg.citations.map((c, i) => <ChatCitationCard key={i} citation={c} />)}</div>)}`

- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `feat(session-live): #2500 render ChatCitationCard in LiveAgentChat`

---

## Task 4: Collegare l'agente RAG a `ChatAgentPanel`/`SessionLiveView` (AC-CHAT-0)

> Oggi `ChatAgentPanel` riceve `messages` (chat *sociale* da `actionLog`) + `onSendMessage` (POST `/game-sessions/{id}/chat`). SP0 impone che la chat live canonica sia l'**agente RAG**. Replicare il pattern di `RulesExplainer.tsx:380`.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx:492-508,881-893,1125-1137`
- (eventuale) Modify: `ChatAgentPanel.tsx` se serve passare `isLoading`/`streamingContent`.
- Test: `SessionLiveView.test.tsx` (esistente — estendere; il file ha già mock per gli hook).

**Interfaces:**
- Consumes: `useSessionAgentChat(gameSessionId, agentSessionId)` (Task 2); `agentSessionsClient` per ottenere `agentSessionId` (vedi `RulesExplainer.tsx` + `apps/web/src/lib/api/clients/agentSessionsClient.ts`); `gameContext` da `LiveSessionDto` (`gameId`, `gameName`, `players[].displayName`).
- Produces: il pannello agente mostra risposte RAG con citazioni.

- [ ] **Step 1 (discovery, da fare nell'esecuzione)**: leggere `RulesExplainer.tsx:360-420` + `agentSessionsClient.ts` per il modo esatto di ottenere `agentSessionId` (launch lazy on-first-message vs all'apertura del pannello) e per come `useSessionAgentChat` legge `useSessionStore` (decidere se popolare `useSessionStore` da `LiveSessionDto` o estendere il hook per accettare un `gameContext` esplicito — preferire l'iniezione esplicita per non accoppiare a un secondo store).

- [ ] **Step 2: Write the failing test** — in `SessionLiveView.test.tsx`, mockare `useSessionAgentChat` per restituire un assistant message con `citations`; asserire che `ChatAgentPanel` renderizzi la citazione (`/pag\. N/`). Verificare inoltre (AC-CHAT-0) che l'invio NON colpisca `/game-sessions/{id}/chat` (chat sociale) ma il path agente (`ask` del hook chiamato).

- [ ] **Step 3: Run** → FAIL.

- [ ] **Step 4: Implement** — in `SessionLiveView`: ottenere `agentSessionId` (launch via `agentSessionsClient`, lazy), costruire `gameContext` da `activeSession`/`LiveSessionDto`, usare `useSessionAgentChat(sessionId, agentSessionId)`; passare `messages` (del hook agente, con `citations`) + `onSendMessage = ask` a `ChatAgentPanel`. Mantenere la chat sociale (actionLog) separata se serve, ma il pannello AGENTE usa l'agente RAG. Gestire `AC-CHAT-NULL` (agente non ancora lanciato → pannello «Abilita assistente», nessun 404/NRE).

- [ ] **Step 5: Run** → PASS.
- [ ] **Step 6: Commit** — `feat(session-live): #2500 wire RAG agent into live ChatAgentPanel`

---

## Task 5: Persistenza — citazioni dopo reload (AC-CHAT-1)

**Files:**
- Modify: il loader della cronologia chat agente (cercare dove il FE carica `ChatThreadDto`/`ChatMessageDto.CitationsJson` — `ChatThreadDto.cs:27-43` lato BE; lato FE il client/schema dei messaggi storici). Mappare `CitationsJson` (string) → `Citation[]` → `ChatCitation[]` via mapper Task 1.
- Test: il messaggio assistant idratato dalla cronologia espone `citations`.

- [ ] **Step 1: Write the failing test** — dato un `ChatMessageDto` con `citationsJson` valorizzato, l'hook/loader produce un `ChatMessage` con `citations` mappate. → FAIL.
- [ ] **Step 2: Implement** — parse `JSON.parse(citationsJson)` → `CitationSchema.array().safeParse` → `.map(mapCitationToChatCitation).filter(Boolean)`.
- [ ] **Step 3: Run** → PASS.
- [ ] **Step 4: Commit** — `feat(session-live): #2500 hydrate citations from persisted CitationsJson on reload`

---

## Task 6: Edge cases + integration BE (deterministico, no LLM)

**Files:**
- Test FE: AC-CHAT-2 (protected → paraphrase, mai verbatim nel DOM), AC-CHAT-3 (non-grounded → nessuna card + disclaimer), AC-CHAT-NULL.
- Test BE (integration, Testcontainers): round-trip persistenza→trasmissione citazioni con `IEmbeddingRepository.SearchByVectorAsync` seed deterministico (riusare il pattern #2504, NO LLM). Asserire `citations[]` con `pageNumber>0` + persistenza dopo reload.

- [ ] **Step 1**: scrivere i test FE edge-case (sopra) → far fallire dove il comportamento manca → implementare i fix minimi (disclaimer non-grounded; guard agente-off) → PASS.
- [ ] **Step 2**: scrivere il test integration BE deterministico → PASS.
- [ ] **Step 3: Commit** — `test(session-live): #2500 citation edge cases + deterministic BE round-trip`

---

## Self-review (coverage AC)

| AC | Task |
|---|---|
| AC-CHAT-0 (chat live = agente RAG) | Task 4 |
| AC-CHAT-1 (page+snippet, persistenza reload) | Task 2/3/5 |
| AC-CHAT-2 (tier protetto → paraphrase) | Task 1/6 |
| AC-CHAT-3 (non-grounded → no card) | Task 1/3/6 |
| AC-CHAT-4 (mapping CitationDto→FE) | Task 1 |
| AC-CHAT-5 (deep-link MVP → CTA assente) | Task 1 (openUrl undefined) |
| AC-CHAT-NULL (agente off) | Task 4 |

**Rischio**: Task 1-3 e 5 a rischio basso (isolati, BE esiste). **Task 4 è il nodo** (wiring agente RAG nel pannello, dipendenze `agentSessionId`/`gameContext`) — replicare `RulesExplainer`. Out of scope SP1: SSE realignment (SP2), diary/media (SP3/4), companion Saga (SP0 residuo).
