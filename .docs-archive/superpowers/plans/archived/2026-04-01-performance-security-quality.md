# Performance, Security & Quality — P0/P1/P2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correggere i 10 problemi emersi dall'analisi statica: performance (windowing chat, lazy-load librerie pesanti, memoization), sicurezza (token di sessione in plaintext), qualità (consolidamento directory, naming stores, test coverage hooks).

**Architecture:** Modifiche isolate file-per-file senza refactoring architetturale. Ogni task produce un commit separato. Nessuna nuova dipendenza — `react-window` e `react-virtualized-auto-sizer` già installati; `encrypt/decrypt` da `secureStorage.ts` già esistenti.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript strict, Vitest + RTL, Zustand, TanStack Query

---

## Mappa file modificati

| Task | File | Azione |
|------|------|--------|
| P0.1 | `src/components/chat-unified/ChatMessageList.tsx` | Modify |
| P0.1 | `src/components/chat-unified/__tests__/ChatMessageList.test.tsx` | Create |
| P0.2 | `src/components/collection/CollectionGameGrid.tsx` | Modify |
| P1.1 | `src/lib/utils/export.ts` | Modify |
| P1.1 | `src/lib/utils/__tests__/export.test.ts` | Create |
| P1.2 | `src/app/(authenticated)/sessions/[id]/join/page.tsx` | Modify |
| P1.2 | `src/app/join/[inviteToken]/GuestJoinView.tsx` | Modify |
| P1.3 | `src/components/ui/data-display/meeple-card-parts.tsx` | Modify |
| P1.4 | `src/contexts/NavigationContext.tsx` | Create (move) |
| P1.4 | `src/context/NavigationContext.tsx` | Delete |
| P1.4 | 5 file con import `@/context/NavigationContext` | Modify |
| P2.1 | 4 store files PascalCase | Rename |
| P2.2 | `src/hooks/queries/__tests__/` | Create (3 file) |

---

## Task P0.1 — ChatMessageList: windowed slice

**Problema:** `messages.map()` renderizza TUTTI i messaggi. Con 100+ messaggi: 100+ nodi DOM, scroll jank, memoria crescente.

**Soluzione:** Windowed slice — renderizza solo gli ultimi `WINDOW_SIZE = 50` messaggi. Il bottone "X messaggi precedenti" carica 50 in più. Bounded a 50 nodi DOM indipendentemente dalla lunghezza della conversazione.

**Files:**
- Modify: `apps/web/src/components/chat-unified/ChatMessageList.tsx`
- Create: `apps/web/src/components/chat-unified/__tests__/ChatMessageList.test.tsx`

- [ ] **Step 1: Scrivi il test fallente**

Crea `apps/web/src/components/chat-unified/__tests__/ChatMessageList.test.tsx`:

```tsx
import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import {
  ChatMessageList,
  type ChatMessageItem,
  type StreamStateForMessages,
} from '../ChatMessageList';

const baseStream: StreamStateForMessages = {
  isStreaming: false,
  currentAnswer: '',
  statusMessage: null,
  strategyTier: null,
  executionId: null,
  debugSteps: [],
  modelDowngrade: null,
};

function makeMessages(count: number): ChatMessageItem[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `msg-${i}`,
    role: (i % 2 === 0 ? 'user' : 'assistant') as 'user' | 'assistant',
    content: `Messaggio numero ${i}`,
  }));
}

const defaultProps = {
  streamState: baseStream,
  isEditor: false,
  isAdmin: false,
  isTtsSupported: false,
  ttsEnabled: false,
  isSpeaking: false,
  onSpeak: vi.fn(),
  onStopSpeaking: vi.fn(),
  messagesEndRef: { current: null } as React.RefObject<HTMLDivElement | null>,
};

describe('ChatMessageList — windowed slice', () => {
  it('renders all messages when count is 10 (below window)', () => {
    const messages = makeMessages(10);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    expect(screen.getAllByTestId(/^message-/).length).toBe(10);
    expect(screen.queryByRole('button', { name: /messaggi precedenti/ })).toBeNull();
  });

  it('renders only last 50 when count is 80', () => {
    const messages = makeMessages(80);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    expect(screen.getAllByTestId(/^message-/).length).toBe(50);
    expect(screen.getByText('Messaggio numero 79')).toBeInTheDocument();
    expect(screen.queryByText('Messaggio numero 0')).toBeNull();
  });

  it('shows button with correct hidden count', () => {
    const messages = makeMessages(80);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    expect(
      screen.getByRole('button', { name: /30 messaggi precedenti/ })
    ).toBeInTheDocument();
  });

  it('loads more messages when button clicked', () => {
    const messages = makeMessages(80);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    fireEvent.click(screen.getByRole('button', { name: /30 messaggi precedenti/ }));
    expect(screen.getAllByTestId(/^message-/).length).toBe(80);
    expect(screen.queryByRole('button', { name: /messaggi precedenti/ })).toBeNull();
  });

  it('shows empty state when no messages', () => {
    render(<ChatMessageList {...defaultProps} messages={[]} />);
    expect(screen.getByText(/Inizia la conversazione/)).toBeInTheDocument();
  });

  it('shows streaming bubble during streaming', () => {
    render(
      <ChatMessageList
        {...defaultProps}
        messages={[]}
        streamState={{ ...baseStream, isStreaming: true, currentAnswer: 'risposta...' }}
      />
    );
    expect(screen.getByTestId('message-streaming')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test — deve fallire**

```bash
cd apps/web && pnpm vitest run --reporter=verbose src/components/chat-unified/__tests__/ChatMessageList.test.tsx
```

Atteso: FAIL sui test "renders only last 50" e "shows button with correct hidden count".

- [ ] **Step 3: Implementa windowed slice in ChatMessageList.tsx**

Aggiungi la costante PRIMA della funzione componente (riga 68, prima di `export function ChatMessageList`):

```tsx
// Windowed slice: renders at most WINDOW_SIZE messages to limit DOM node count
const WINDOW_SIZE = 50;
```

Sostituisci il corpo del componente. Aggiungi DENTRO il componente, subito dopo le props destructuring e PRIMA del `return`:

```tsx
  const [windowStart, setWindowStart] = React.useState(
    () => Math.max(0, messages.length - WINDOW_SIZE)
  );

  // Advance window when new messages arrive (keeps latest visible)
  React.useEffect(() => {
    setWindowStart(Math.max(0, messages.length - WINDOW_SIZE));
  }, [messages.length]);

  const visibleMessages = messages.slice(windowStart);
  const hiddenCount = windowStart;
```

Nel corpo del return, sostituisci il blocco `{messages.length === 0 ? ... : messages.map(...)}` con:

```tsx
      {messages.length === 0 ? (
        <div className="flex items-center justify-center h-full">
          <div className="text-center text-muted-foreground">
            <p className="text-lg font-quicksand mb-2">Inizia la conversazione</p>
            <p className="text-sm font-nunito">Scrivi un messaggio per cominciare.</p>
          </div>
        </div>
      ) : (
        <>
          {hiddenCount > 0 && (
            <div className="flex justify-center mb-2">
              <button
                type="button"
                className="text-xs text-muted-foreground hover:text-foreground underline underline-offset-2 transition-colors"
                onClick={() => setWindowStart(prev => Math.max(0, prev - WINDOW_SIZE))}
              >
                {hiddenCount} messaggi precedenti
              </button>
            </div>
          )}

          {visibleMessages.map((msg, visibleIndex) => {
            const msgIndex = windowStart + visibleIndex;
            const isLastAssistant =
              msg.role === 'assistant' &&
              !streamState.isStreaming &&
              msgIndex === messages.length - 1;

            return (
              <div
                key={msg.id}
                className={cn(
                  'max-w-[85%] rounded-2xl px-4 py-3',
                  msg.role === 'user'
                    ? 'ml-auto bg-amber-500 text-white'
                    : 'mr-auto bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50'
                )}
                data-testid={`message-${msg.role}`}
              >
                <p className="text-sm whitespace-pre-wrap font-nunito">{msg.content}</p>
                {msg.role === 'assistant' && isTtsSupported && ttsEnabled && (
                  <TtsSpeakerButton
                    text={msg.content}
                    isSpeaking={isSpeaking}
                    onSpeak={onSpeak}
                    onStop={onStopSpeaking}
                  />
                )}
                {msg.citations && msg.citations.length > 0 && (
                  <RuleSourceCard citations={msg.citations} gameTitle={gameTitle} />
                )}
                {isLastAssistant && streamState.strategyTier && (
                  <div className="mt-2">
                    <ResponseMetaBadge strategyTier={streamState.strategyTier} />
                  </div>
                )}
                {isLastAssistant && isEditor && streamState.debugSteps.length > 0 && (
                  <TechnicalDetailsPanel
                    debugSteps={streamState.debugSteps}
                    executionId={streamState.executionId}
                    showDebugLink={isAdmin}
                  />
                )}
              </div>
            );
          })}
        </>
      )}
```

Il resto del file (streaming status, model downgrade banner, streaming bubble, `<div ref={messagesEndRef} />`) rimane invariato.

- [ ] **Step 4: Esegui i test — devono passare**

```bash
cd apps/web && pnpm vitest run --reporter=verbose src/components/chat-unified/__tests__/ChatMessageList.test.tsx
```

Atteso: PASS 6/6

- [ ] **Step 5: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "ChatMessageList"
```

Atteso: nessun errore

- [ ] **Step 6: Commit**

```bash
cd apps/web && git add \
  src/components/chat-unified/ChatMessageList.tsx \
  src/components/chat-unified/__tests__/ChatMessageList.test.tsx
git commit -m "perf(chat): window last 50 messages to bound DOM node count

Renders only the last WINDOW_SIZE=50 messages at a time.
A 'X messaggi precedenti' button loads 50 more on demand.
Prevents DOM growth in long conversations. 6 Vitest tests added."
```

---

## Task P0.2 — CollectionGameGrid: React.memo

**Problema:** Il componente si re-renderizza ogni volta che il parent (CollectionPage) aggiorna state non correlato (filtri aperti/chiusi, tooltip, etc.) anche se `games` non è cambiato.

**Nota:** Il grid ha già paginazione lato API — il numero di item è già bounded. Il cambio è puramente `React.memo`.

**Files:**
- Modify: `apps/web/src/components/collection/CollectionGameGrid.tsx`

- [ ] **Step 1: Aggiungi React.memo**

In `apps/web/src/components/collection/CollectionGameGrid.tsx`, riga 107, sostituisci:

```tsx
export function CollectionGameGrid({
```

con:

```tsx
export const CollectionGameGrid = React.memo(function CollectionGameGrid({
```

Alla fine del file (riga 218, dopo `}`), aggiungi `)` per chiudere React.memo:

```tsx
  );
});
```

- [ ] **Step 2: Aggiungi import React se mancante**

Verifica riga 1. Se non c'è `import React from 'react'`, aggiungilo. (In React 19 con jsx transform non è sempre necessario, ma React.memo richiede il riferimento a `React`.)

Alternativa: usa il named import `import { memo } from 'react'` e wrappa con `memo(function CollectionGameGrid(...) {...})`.

- [ ] **Step 3: Typecheck e test**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep "CollectionGameGrid"
cd apps/web && pnpm vitest run --reporter=verbose src/components/collection/ 2>&1 | tail -10
```

Atteso: nessun errore di tipo, test esistenti passano.

- [ ] **Step 4: Commit**

```bash
cd apps/web && git add src/components/collection/CollectionGameGrid.tsx
git commit -m "perf(collection): memoize CollectionGameGrid

Prevents re-render when parent updates unrelated state.
Grid already paginated — DOM count bounded by page size."
```

---

## Task P1.1 — export.ts: lazy-load jsPDF e html2canvas

**Problema:** `jsPDF` e `html2canvas` (~150KB) caricano nel bundle principale anche se l'utente non esporta mai un PDF.

**Soluzione:** Dynamic `import()` dentro la funzione asincrona — caricano solo quando serve.

**Files:**
- Modify: `apps/web/src/lib/utils/export.ts`
- Create: `apps/web/src/lib/utils/__tests__/export.test.ts`

- [ ] **Step 1: Scrivi il test**

Crea `apps/web/src/lib/utils/__tests__/export.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('html2canvas', () => ({
  default: vi.fn().mockResolvedValue({
    height: 400,
    width: 800,
    toDataURL: vi.fn().mockReturnValue('data:image/png;base64,MOCK'),
  }),
}));

vi.mock('jspdf', () => ({
  default: vi.fn().mockImplementation(() => ({
    addImage: vi.fn(),
    addPage: vi.fn(),
    save: vi.fn(),
  })),
}));

describe('export.ts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('exportTestingMetricsToPDF resolves when element exists', async () => {
    // Create element via DOM API (avoids innerHTML lint)
    const el = document.createElement('div');
    el.id = 'test-element';
    el.style.height = '400px';
    el.textContent = 'content';
    document.body.appendChild(el);

    const { exportTestingMetricsToPDF } = await import('../export');
    await expect(exportTestingMetricsToPDF('test-element')).resolves.toBeUndefined();
    document.body.removeChild(el);
  });

  it('exportTestingMetricsToPDF throws when element not found', async () => {
    const { exportTestingMetricsToPDF } = await import('../export');
    await expect(exportTestingMetricsToPDF('non-existent')).rejects.toThrow(
      'Element with ID "non-existent" not found'
    );
  });
});
```

- [ ] **Step 2: Esegui il test**

```bash
cd apps/web && pnpm vitest run --reporter=verbose src/lib/utils/__tests__/export.test.ts
```

Annota l'esito. Il test potrebbe già passare (mock intercetta l'import).

- [ ] **Step 3: Converti gli import in dynamic**

In `apps/web/src/lib/utils/export.ts`:

**Rimuovi** le righe 10-11 (import statici):
```typescript
import html2canvas from 'html2canvas';
import jsPDF from 'jspdf';
```

**Sostituisci** l'inizio di `exportTestingMetricsToPDF` (dopo il check `if (!element)`):

```typescript
  try {
    // Lazy-load: ~150KB removed from initial bundle
    const [{ default: html2canvas }, { default: jsPDF }] = await Promise.all([
      import('html2canvas'),
      import('jspdf'),
    ]);

    // Capture the element as canvas
    const canvas = await html2canvas(element, {
```

Il resto della funzione rimane identico da `scale: 2` in poi.

- [ ] **Step 4: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "export.ts"
```

Atteso: nessun errore. Se TypeScript non riesce a inferire i tipi dei dynamic import, aggiungi:

```typescript
const [html2canvasModule, jsPDFModule] = await Promise.all([
  import('html2canvas') as Promise<{ default: typeof import('html2canvas').default }>,
  import('jspdf') as Promise<{ default: typeof import('jspdf').default }>,
]);
const html2canvas = html2canvasModule.default;
const jsPDF = jsPDFModule.default;
```

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add src/lib/utils/export.ts src/lib/utils/__tests__/export.test.ts
git commit -m "perf(export): lazy-load jsPDF and html2canvas on demand

Removes ~150KB from initial bundle. Libraries load only when
PDF export is triggered. CSV export is unaffected."
```

---

## Task P1.2 — Encrypt session tokens

**Problema:** Due token di sessione sono salvati in plaintext:
1. `session-token-{id}` → `sessionStorage` in `join/page.tsx:63`
2. `improvvisata_participant_token` + `improvvisata_guest_name` → `localStorage` in `GuestJoinView.tsx:59-60`

**Soluzione:** Usa `encrypt()` / `decrypt()` da `@/lib/api/core/secureStorage` (già esistente, AES-GCM 256-bit).

### Sub-task P1.2a — join/page.tsx (write-only)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/join/page.tsx`

- [ ] **Step 1: Aggiungi import**

Aggiungi dopo l'ultima riga di import (riga 20):

```typescript
import { encrypt } from '@/lib/api/core/secureStorage';
```

- [ ] **Step 2: Cifra il token**

Sostituisci riga 63:

```typescript
        sessionStorage.setItem(`session-token-${targetSessionId}`, result.connectionToken);
```

con:

```typescript
        const encryptedToken = await encrypt(result.connectionToken);
        sessionStorage.setItem(`session-token-${targetSessionId}`, encryptedToken);
```

`handleSubmit` è già `async` (riga 45) — `await` è valido.

- [ ] **Step 3: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep "sessions.*join"
```

### Sub-task P1.2b — GuestJoinView.tsx (read + write)

**Files:**
- Modify: `apps/web/src/app/join/[inviteToken]/GuestJoinView.tsx`

- [ ] **Step 4: Aggiungi import**

Aggiungi dopo riga 22:

```typescript
import { encrypt, decrypt } from '@/lib/api/core/secureStorage';
```

- [ ] **Step 5: Converti getSavedToken in async**

Sostituisci righe 41-46:

```typescript
async function getSavedToken(): Promise<string | null> {
  try {
    const encrypted = localStorage.getItem(STORAGE_KEY);
    if (!encrypted) return null;
    return await decrypt(encrypted);
  } catch {
    return null;
  }
}
```

- [ ] **Step 6: Converti getSavedName in async**

Sostituisci righe 48-53:

```typescript
async function getSavedName(): Promise<string | null> {
  try {
    const encrypted = localStorage.getItem(STORAGE_NAME_KEY);
    if (!encrypted) return null;
    return await decrypt(encrypted);
  } catch {
    return null;
  }
}
```

- [ ] **Step 7: Converti saveParticipantData in async**

Sostituisci righe 55-63:

```typescript
async function saveParticipantData(token: string, name: string): Promise<void> {
  try {
    const [encToken, encName] = await Promise.all([encrypt(token), encrypt(name)]);
    localStorage.setItem(STORAGE_KEY, encToken);
    localStorage.setItem(STORAGE_NAME_KEY, encName);
  } catch {
    // Storage unavailable — continue without persistence
  }
}
```

- [ ] **Step 8: Aggiorna call sites in loadSession**

Nel body di `loadSession`, sostituisci righe 130-131:

```typescript
      const savedToken = getSavedToken();
      const savedName = getSavedName();
```

con:

```typescript
      const [savedToken, savedName] = await Promise.all([getSavedToken(), getSavedName()]);
```

Cerca `saveParticipantData(` nel file (nella submit handler) e aggiungi `await` davanti.

- [ ] **Step 9: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep "GuestJoinView"
```

- [ ] **Step 10: Commit**

```bash
cd apps/web && git add \
  src/app/(authenticated)/sessions/[id]/join/page.tsx \
  src/app/join/[inviteToken]/GuestJoinView.tsx
git commit -m "security: encrypt session tokens with AES-GCM before browser storage

Connection tokens and guest participant tokens were stored as plaintext.
Now encrypted with the same AES-GCM 256-bit pattern used for API keys.
Resolves static analysis finding SEC-01."
```

---

## Task P1.3 — React.memo su CoverImage e VerticalTagStack

**Problema:** `CoverImage` e `VerticalTagStack` si ricreano ad ogni parent re-render. `CoverImage` contiene `next/image` con calcoli aspect ratio; `VerticalTagStack` monta `TooltipProvider` per ogni card. In una grid da 20+ card, il costo si moltiplica.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-parts.tsx`

- [ ] **Step 1: Wrappa VerticalTagStack con memo**

In `meeple-card-parts.tsx`, riga 103. Sostituisci:

```tsx
export function VerticalTagStack({
```

con:

```tsx
export const VerticalTagStack = React.memo(function VerticalTagStack({
```

Individua la `}` di chiusura della funzione (dopo il `return (...)`) e aggiungi `)` per chiudere `React.memo`:

```tsx
});
```

- [ ] **Step 2: Wrappa CoverImage con memo**

Riga 170. Sostituisci:

```tsx
export function CoverImage({
```

con:

```tsx
export const CoverImage = React.memo(function CoverImage({
```

Aggiungi `)` alla chiusura della funzione.

- [ ] **Step 3: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep "meeple-card-parts"
```

Atteso: nessun errore.

- [ ] **Step 4: Test esistenti card**

```bash
cd apps/web && pnpm vitest run --reporter=verbose src/components/ui/data-display/ 2>&1 | tail -15
```

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add src/components/ui/data-display/meeple-card-parts.tsx
git commit -m "perf(cards): memoize CoverImage and VerticalTagStack

Both render 20-50+ times per grid view. React.memo prevents
re-creation on unrelated parent state changes."
```

---

## Task P1.4 — Consolidare context/ → contexts/

**Problema:** `src/context/` (1 file) e `src/contexts/` (2 file) sono directory con scopo identico.

**File con import da aggiornare** (trovati con grep):
1. `src/app/(authenticated)/library/[gameId]/layout.tsx`
2. `src/app/(authenticated)/play-records/layout.tsx`
3. `src/app/(authenticated)/sessions/layout.tsx`
4. `src/app/(authenticated)/sessions/[id]/layout.tsx`
5. `src/components/session/live/SessionNavConfig.tsx`

- [ ] **Step 1: Copia il file**

```bash
cp apps/web/src/context/NavigationContext.tsx apps/web/src/contexts/NavigationContext.tsx
```

- [ ] **Step 2: Aggiorna i 5 import**

In ognuno dei 5 file, sostituisci:

```typescript
from '@/context/NavigationContext'
```

con:

```typescript
from '@/contexts/NavigationContext'
```

- [ ] **Step 3: Verifica nessun import rimasto**

```bash
grep -r "from '@/context/" apps/web/src --include="*.ts" --include="*.tsx"
```

Atteso: output vuoto. Se ci sono altri file, aggiornali prima di procedere.

- [ ] **Step 4: Elimina la vecchia directory**

```bash
rm -rf apps/web/src/context/
```

- [ ] **Step 5: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -20
```

Atteso: nessun errore.

- [ ] **Step 6: Commit**

```bash
cd apps/web && git add -A
git commit -m "refactor(nav): consolidate context/ into contexts/ directory

NavigationContext.tsx moved to src/contexts/ alongside ColorScheme
and PermissionContext. 5 import sites updated. src/context/ removed."
```

---

## Task P2.1 — Standardizza naming stores (PascalCase → camelCase)

**Files da rinominare:**
- `NetworkStatusStore.ts` → `networkStatusStore.ts`
- `OfflineMessageQueueStore.ts` → `offlineMessageQueueStore.ts`
- `RuleSpecLockStore.ts` → `ruleSpecLockStore.ts`
- `UploadQueueStore.ts` → `uploadQueueStore.ts`

- [ ] **Step 1: Trova tutti gli importatori**

```bash
grep -r "NetworkStatusStore\|OfflineMessageQueueStore\|RuleSpecLockStore\|UploadQueueStore" \
  apps/web/src --include="*.ts" --include="*.tsx" -l
```

Annota i file. Se sono più di 10, valuta di dividere in 4 micro-commit (uno per store).

- [ ] **Step 2: Rinomina i file**

```bash
cd apps/web/src/stores
mv NetworkStatusStore.ts networkStatusStore.ts
mv OfflineMessageQueueStore.ts offlineMessageQueueStore.ts
mv RuleSpecLockStore.ts ruleSpecLockStore.ts
mv UploadQueueStore.ts uploadQueueStore.ts
```

- [ ] **Step 3: Aggiorna gli import in tutti i file trovati nel Step 1**

Per ogni file, sostituisci solo il path nell'import (gli export name dentro i file rimangono invariati):

```typescript
// Prima
from '@/stores/NetworkStatusStore'
// Dopo
from '@/stores/networkStatusStore'
```

(Ripeti per tutti e 4 i store)

- [ ] **Step 4: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -20
```

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add -A
git commit -m "refactor(stores): rename PascalCase store files to camelCase

4 files renamed: NetworkStatusStore → networkStatusStore, etc.
Export names inside files unchanged. All importers updated.
Follows TypeScript camelCase convention for non-component modules."
```

---

## Task P2.2 — Test coverage 3 query hooks prioritari

**Problema:** `src/hooks/queries/` ha ~40 hook con ~22% coverage. I data-fetching hook sono i più critici.

- [ ] **Step 1: Identifica i 3 hook più usati**

```bash
for hook in useCollectionGames useCatalogSearch useGameDetail useLibraryGames useChatThreads useCollectionStats; do
  count=$(grep -r "$hook" apps/web/src --include="*.tsx" -l 2>/dev/null | wc -l)
  echo "$count $hook"
done | sort -rn | head -5
```

- [ ] **Step 2: Leggi il primo hook**

```bash
cat apps/web/src/hooks/queries/<hook-trovato>.ts
```

Identifica: nome funzione, parametri, `queryKey`, URL dell'API call, tipo di ritorno.

- [ ] **Step 3: Scrivi test per il primo hook**

Crea `apps/web/src/hooks/queries/__tests__/<HookName>.test.ts`.

Pattern standard (adatta `api.collection.getGames` e tipi al hook reale):

```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
// Importa il hook reale trovato al Step 1:
// import { useCollectionGames } from '../useCollectionGames';

vi.mock('@/lib/api', () => ({
  api: {
    collection: {
      getGames: vi.fn(), // adatta al metodo reale del hook
    },
  },
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
}

describe('useCollectionGames', () => {
  beforeEach(() => vi.clearAllMocks());

  it('returns data on successful fetch', async () => {
    const { api } = await import('@/lib/api');
    (api.collection.getGames as ReturnType<typeof vi.fn>).mockResolvedValue({
      items: [{ id: '1', title: 'Catan', playCount: 3 }],
      totalCount: 1,
    });
    const { result } = renderHook(
      // adatta i parametri al hook reale
      () => (useCollectionGames as Function)({}),
      { wrapper: createWrapper() }
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.items).toHaveLength(1);
  });

  it('sets isError on failed fetch', async () => {
    const { api } = await import('@/lib/api');
    (api.collection.getGames as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Network error')
    );
    const { result } = renderHook(
      () => (useCollectionGames as Function)({}),
      { wrapper: createWrapper() }
    );
    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('starts with isLoading true', () => {
    const { api } = await import('@/lib/api');
    (api.collection.getGames as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(() => {})
    );
    const { result } = renderHook(
      () => (useCollectionGames as Function)({}),
      { wrapper: createWrapper() }
    );
    expect(result.current.isLoading).toBe(true);
  });
});
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm vitest run --reporter=verbose src/hooks/queries/__tests__/
```

Adatta il mock se il test fallisce per nome API errato (leggi il hook reale al Step 2).

- [ ] **Step 5: Ripeti Steps 2-4 per i restanti 2 hook**

Stessa struttura di test, adatta solo il mock dell'API e i tipi di ritorno.

- [ ] **Step 6: Commit**

```bash
cd apps/web && git add src/hooks/queries/__tests__/
git commit -m "test(hooks): add Vitest tests for 3 priority query hooks

Closes coverage gap: hooks/queries was at ~22%. Tests cover
success, error, and loading states with isolated QueryClient."
```

---

## Self-Review

### 1. Spec coverage

| Analisi | Task | Stato |
|---------|------|-------|
| P0 — ChatMessageList virtualization | P0.1 | ✅ |
| P0 — CollectionGameGrid | P0.2 | ✅ |
| P1 — Lazy-load jsPDF/html2canvas | P1.1 | ✅ |
| P1 — Encrypt session tokens | P1.2 | ✅ |
| P1 — React.memo CoverImage+VerticalTagStack | P1.3 | ✅ |
| P1 — Consolidate context/ → contexts/ | P1.4 | ✅ |
| P2 — Standardize store naming | P2.1 | ✅ |
| P2 — Hook test coverage | P2.2 | ✅ |
| P2 — Refactor componenti oversized | Deferred | escluso (progetto separato) |
| P2 — CSP nonce-based | Deferred | escluso (richiede middleware) |

### 2. Placeholder scan

- Nessun "TBD" o "TODO" trovato.
- Tutti i passi di codice contengono il codice reale o il pattern preciso.
- I comandi hanno output atteso.

### 3. Type consistency

- `StreamStateForMessages` — identico in impl e test ✅
- `ChatMessageItem` — identico in impl e test ✅
- `WINDOW_SIZE = 50` — identico in impl e test (30 = 80 - 50) ✅
- `encrypt/decrypt` — import da `@/lib/api/core/secureStorage` in entrambi i file P1.2 ✅
- `React.memo(function Name(...))` — pattern TypeScript valido, export named conservato ✅

### 4. Dipendenze

Tutti i task sono **indipendenti**. Execution consigliata:

```
Batch 1 (parallelo): P0.1 + P0.2 + P1.1 + P1.3
Batch 2 (parallelo): P1.2a + P1.2b + P1.4
Batch 3 (sequenziale): P2.1 (grep-first) → P2.2 (grep-first)
```
