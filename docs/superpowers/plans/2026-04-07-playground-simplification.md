# RAG Playground Simplification & Spec-Panel Fixes

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove premature complexity (Compare Tab, Query Tester), consolidate into a single Chat Debug playground, and fix all issues identified by the spec-panel analysis.

**Architecture:** The playground page becomes a single-tab Chat Debug view with integrated parameter controls (model, temperature, strategy, topK). The Compare Tab and Query Tester are removed entirely. The `useDebugChatStream` hook gets a timeout guard and performance fix. The `adminAiClient.getProcessingQueue` parameter mismatch (already fixed) is included for completeness.

**Tech Stack:** Next.js 16 (App Router), React 19, TypeScript, Tailwind CSS, SSE streaming

**Branch:** `feature/playground-simplification` from `main-dev`

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| **Delete** | `apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx` | Dead code: Compare Tab |
| **Rewrite** | `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx` | Single Chat Debug view with parameter controls |
| **Modify** | `apps/web/src/hooks/useDebugChatStream.ts` | Add timeout, fix performance |
| **Modify** | `apps/web/src/components/admin/debug-chat/StrategySelectorBar.tsx` | Fix game endpoint, add model/temp controls |
| **Already fixed** | `apps/web/src/lib/api/clients/admin/adminAiClient.ts:570-571` | `status`→`statusFilter`, `search`→`searchText` |

---

### Task 1: Create branch and verify starting state

**Files:**
- None (git operations only)

- [ ] **Step 1: Create feature branch from main-dev**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git checkout main-dev && git pull
git checkout -b feature/playground-simplification
git config branch.feature/playground-simplification.parent main-dev
```

- [ ] **Step 2: Verify the playground page loads without errors**

```bash
cd apps/web && pnpm build 2>&1 | grep -E "error|Error" | head -20
```

Expected: No errors related to playground files.

- [ ] **Step 3: Commit the already-applied adminAiClient fix**

The fix at `adminAiClient.ts:570-571` changing `status`→`statusFilter` and `search`→`searchText` was already applied. Stage and commit it.

```bash
git add apps/web/src/lib/api/clients/admin/adminAiClient.ts
git commit -m "fix(admin): align getProcessingQueue params with backend (statusFilter, searchText)"
```

---

### Task 2: Delete Compare Tab

**Files:**
- Delete: `apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx` (remove import and tab)

- [ ] **Step 1: Delete the compare-tab file**

```bash
rm apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx
```

- [ ] **Step 2: Remove the import and Compare tab from page.tsx**

In `page.tsx`, remove:
```typescript
// Remove this import (line 41)
import { CompareTab } from './compare-tab';

// Remove the TAB_MAP entry for compare (line 71)
compare: 'compare',

// Remove the TabsTrigger for compare (line 598)
<TabsTrigger value="compare">Compare</TabsTrigger>

// Remove the TabsContent for compare (lines 609-611)
<TabsContent value="compare">
  <CompareTab />
</TabsContent>
```

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build 2>&1 | grep -E "error|Error" | head -20
```

Expected: No build errors.

- [ ] **Step 4: Commit**

```bash
git add -A apps/web/src/app/admin/(dashboard)/agents/playground/
git commit -m "refactor(playground): remove Compare Tab (premature complexity, dead code)"
```

---

### Task 3: Merge Query Tester into Chat Debug — remove Query Tester tab

The Query Tester is a broken duplicate (model/temp/agent params ignored). Instead of fixing it, we remove it and add the useful controls (model, temperature, topK) to the Chat Debug's StrategySelectorBar.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx` — remove QueryTesterTab, make ChatDebugTab the only content
- Modify: `apps/web/src/components/admin/debug-chat/StrategySelectorBar.tsx` — add model, temperature, topK controls

- [ ] **Step 1: Add model/temperature/topK props to StrategySelectorBar**

In `StrategySelectorBar.tsx`, update the interface and add controls:

```typescript
interface StrategySelectorBarProps {
  selectedGameId: string;
  onGameChange: (gameId: string) => void;
  selectedStrategy: string;
  onStrategyChange: (strategy: string) => void;
  // New parameter controls
  selectedModel: string;
  onModelChange: (model: string) => void;
  temperature: number;
  onTemperatureChange: (temp: number) => void;
  topK: number;
  onTopKChange: (topK: number) => void;
  // Existing
  onReExecute: () => void;
  isStreaming: boolean;
  hasLastQuery: boolean;
  showDebug: boolean;
  onToggleDebug: () => void;
  // Model list
  availableModels: { id: string; displayName: string; modelIdentifier: string }[];
  modelsLoading: boolean;
}
```

Add a second row in the bar for model/temperature/topK:

```tsx
{/* Second row: Model + Temperature + TopK */}
<div className="flex items-center gap-3 border-b px-4 py-2 bg-muted/20">
  <div className="flex items-center gap-2">
    <label className="text-xs font-medium text-muted-foreground shrink-0">Model</label>
    <Select value={selectedModel} onValueChange={onModelChange} disabled={modelsLoading}>
      <SelectTrigger className="h-8 text-xs w-[200px]">
        <SelectValue placeholder={modelsLoading ? 'Loading...' : 'Default'} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="__default__" className="text-xs">Default (agent config)</SelectItem>
        {availableModels.map(m => (
          <SelectItem key={m.id} value={m.modelIdentifier} className="text-xs">
            {m.displayName}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  </div>

  <div className="flex items-center gap-2">
    <label className="text-xs font-medium text-muted-foreground shrink-0">Temp</label>
    <input
      type="number"
      value={temperature}
      onChange={e => onTemperatureChange(Number(e.target.value))}
      min={0} max={2} step={0.1}
      className="h-8 w-[70px] rounded-md border bg-background px-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
    />
  </div>

  <div className="flex items-center gap-2">
    <label className="text-xs font-medium text-muted-foreground shrink-0">Top-K</label>
    <input
      type="number"
      value={topK}
      onChange={e => onTopKChange(Number(e.target.value))}
      min={1} max={20}
      className="h-8 w-[60px] rounded-md border bg-background px-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
    />
  </div>
</div>
```

- [ ] **Step 2: Fix the game endpoint in StrategySelectorBar**

Replace the hardcoded `/api/v1/games?pageSize=100` with the correct shared-games endpoint:

```typescript
// Old (line 73):
const response = await fetch(`${baseUrl}/api/v1/games?pageSize=100`, {

// New:
const response = await fetch(`${baseUrl}/api/v1/admin/shared-games?page=1&pageSize=100`, {
```

And update the field mapping (games may use `title` instead of `name`):

```typescript
// Old (line 84):
name: g.name || g.title || g.id,

// New:
name: g.title || g.name || g.id,
```

- [ ] **Step 3: Pass configOverride from ChatDebugTab to useDebugChatStream**

In `page.tsx`, update `ChatDebugTab` to use the new controls and pass them to `sendMessage`:

```typescript
// Add state for new controls
const [selectedModel, setSelectedModel] = useState('');
const [temperature, setTemperature] = useState(0.7);
const [topK, setTopK] = useState(5);

// Fetch AI models
const { data: aiModels, isLoading: modelsLoading } = useQuery({
  queryKey: ['admin', 'ai-models', 'active'],
  queryFn: () => _adminClient.getAiModels({ status: 'active' }),
  staleTime: 300_000,
});
```

Update `handleSend` to pass `configOverride`:

```typescript
const configOverride: DebugChatConfigOverride | undefined =
  (selectedModel && selectedModel !== '__default__') || temperature !== 0.7 || topK !== 5
    ? {
        model: selectedModel && selectedModel !== '__default__' ? selectedModel : undefined,
        temperature,
        topK,
      }
    : undefined;

sendMessage(selectedGameId, query, strategy, state.chatThreadId ?? undefined, configOverride);
```

Update `StrategySelectorBar` props:

```tsx
<StrategySelectorBar
  selectedGameId={selectedGameId}
  onGameChange={setSelectedGameId}
  selectedStrategy={selectedStrategy}
  onStrategyChange={setSelectedStrategy}
  selectedModel={selectedModel}
  onModelChange={setSelectedModel}
  temperature={temperature}
  onTemperatureChange={setTemperature}
  topK={topK}
  onTopKChange={setTopK}
  availableModels={(aiModels?.items ?? []).map(m => ({
    id: m.id,
    displayName: m.displayName,
    modelIdentifier: m.modelIdentifier,
  }))}
  modelsLoading={modelsLoading}
  onReExecute={handleReExecute}
  isStreaming={state.isStreaming}
  hasLastQuery={!!lastQueryRef.current}
  showDebug={showDebug}
  onToggleDebug={handleToggleDebug}
/>
```

- [ ] **Step 4: Remove QueryTesterTab and Tabs wrapper from page.tsx**

Remove the entire `QueryTesterTab` function (lines 76-333), the `STRATEGIES` constant (line 67), the `QueryResult` and `ChatMessage` types that are only used by QueryTesterTab, the `TAB_MAP`, the `Tabs`/`TabsList`/`TabsTrigger`/`TabsContent` wrapper, and the unused imports (`PlayIcon`, `Tabs*` components).

The page becomes:

```tsx
export default function PlaygroundPage() {
  return (
    <div className="space-y-6 p-4 lg:p-6">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          RAG Playground
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Debug chat con gli agenti RAG — streaming, pipeline debug e override parametri
        </p>
      </div>
      <ChatDebugTab />
    </div>
  );
}
```

Keep `ChatMessage` interface (used by ChatDebugTab). Remove `RagStrategy`, `QueryResult`, `STRATEGIES`, `TAB_MAP`.

- [ ] **Step 5: Clean up unused imports in page.tsx**

Remove: `PlayIcon`, `Tabs`, `TabsContent`, `TabsList`, `TabsTrigger`, `useSearchParams`.

Keep: `useState`, `useCallback`, `useRef`, `useQuery`, `SendIcon`, `Select*`, `useDebugChatStream`, `useLocalStorage`, `createAdminClient`, `HttpClient`, `cn`, `DebugTimeline`, `StrategySelectorBar`.

- [ ] **Step 6: Verify build**

```bash
cd apps/web && pnpm build 2>&1 | grep -E "error|Error" | head -20
```

Expected: No errors.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx
git add apps/web/src/components/admin/debug-chat/StrategySelectorBar.tsx
git commit -m "refactor(playground): consolidate into single Chat Debug view with model/temp/topK controls

Remove QueryTesterTab (broken: params not sent) and Compare Tab.
Add model, temperature, topK override controls to StrategySelectorBar.
Fix game endpoint: /api/v1/games → /api/v1/admin/shared-games."
```

---

### Task 4: Fix module-level HttpClient anti-pattern

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx`

- [ ] **Step 1: Replace module-level client with lazy initialization inside component**

Remove the module-level singletons:

```typescript
// DELETE these lines (38-39):
const _httpClient = new HttpClient();
const _adminClient = createAdminClient({ httpClient: _httpClient });
```

In `ChatDebugTab`, use `useMemo` to create a stable client reference:

```typescript
import { useMemo } from 'react';

function ChatDebugTab() {
  const adminClient = useMemo(() => {
    const httpClient = new HttpClient();
    return createAdminClient({ httpClient });
  }, []);

  // Replace _adminClient with adminClient in useQuery:
  const { data: aiModels, isLoading: modelsLoading } = useQuery({
    queryKey: ['admin', 'ai-models', 'active'],
    queryFn: () => adminClient.getAiModels({ status: 'active' }),
    staleTime: 300_000,
  });
  // ...
}
```

- [ ] **Step 2: Verify build**

```bash
cd apps/web && pnpm build 2>&1 | grep -E "error|Error" | head -20
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx
git commit -m "fix(playground): replace module-level HttpClient with useMemo in component"
```

---

### Task 5: Add timeout to useDebugChatStream

**Files:**
- Modify: `apps/web/src/hooks/useDebugChatStream.ts`

- [ ] **Step 1: Add stream timeout constant and AbortSignal.timeout**

At the top of the file, add:

```typescript
/** Maximum stream duration before auto-abort (2 minutes) */
const STREAM_TIMEOUT_MS = 120_000;
```

In the `sendMessage` function, modify the fetch call to use `AbortSignal.any` combining the manual abort and the timeout:

```typescript
// Old (line 173-174):
const abortController = new AbortController();
abortControllerRef.current = abortController;

// New:
const abortController = new AbortController();
abortControllerRef.current = abortController;

const timeoutSignal = AbortSignal.timeout(STREAM_TIMEOUT_MS);
const combinedSignal = AbortSignal.any([abortController.signal, timeoutSignal]);
```

Update the fetch call to use `combinedSignal`:

```typescript
// Old (line 191):
signal: abortController.signal,

// New:
signal: combinedSignal,
```

Update the error handler to distinguish timeout from manual abort:

```typescript
// Old (line 341):
if (error.name === 'AbortError') {
  setState(prev => ({ ...prev, isStreaming: false, statusMessage: null }));
  return;
}

// New:
if (error.name === 'AbortError') {
  setState(prev => ({ ...prev, isStreaming: false, statusMessage: null }));
  return;
}
if (error.name === 'TimeoutError') {
  const errorMsg = 'Stream timeout: nessuna risposta entro 2 minuti';
  setState(prev => ({
    ...prev,
    error: errorMsg,
    isStreaming: false,
    statusMessage: null,
  }));
  callbacksRef.current?.onError?.(errorMsg);
  return;
}
```

- [ ] **Step 2: Fix debugEvents performance — accumulate in ref, flush to state**

Replace the per-event setState with a ref-based accumulation:

```typescript
// Add a ref for accumulating debug events (near line 133):
const pendingDebugEventsRef = useRef<DebugEvent[]>([]);
const flushTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
```

Add a flush function:

```typescript
const flushDebugEvents = useCallback(() => {
  const pending = pendingDebugEventsRef.current;
  if (pending.length === 0) return;
  pendingDebugEventsRef.current = [];
  setState(prev => ({
    ...prev,
    debugEvents: [...prev.debugEvents, ...pending],
  }));
}, []);
```

In `sendMessage`, replace the debug event setState (lines 239-242) with:

```typescript
// Old:
setState(prev => ({
  ...prev,
  debugEvents: [...prev.debugEvents, debugEvent],
}));

// New:
pendingDebugEventsRef.current.push(debugEvent);
if (!flushTimerRef.current) {
  flushTimerRef.current = setTimeout(() => {
    flushTimerRef.current = null;
    flushDebugEvents();
  }, 50);
}
```

In the `reset` function, clear the pending events:

```typescript
const reset = useCallback(() => {
  stopStreaming();
  debugEventCounterRef.current = 0;
  pendingDebugEventsRef.current = [];
  if (flushTimerRef.current) {
    clearTimeout(flushTimerRef.current);
    flushTimerRef.current = null;
  }
  setState(INITIAL_STATE);
}, [stopStreaming, flushDebugEvents]);
```

Before the Complete event handler (line 281), flush any pending events:

```typescript
case StreamingEventType.Complete: {
  // Flush any pending debug events before completing
  if (flushTimerRef.current) {
    clearTimeout(flushTimerRef.current);
    flushTimerRef.current = null;
  }
  const pendingEvents = pendingDebugEventsRef.current;
  pendingDebugEventsRef.current = [];
  // ...existing complete logic, but merge pending events into final state:
  setState(prev => {
    const allDebugEvents = [...prev.debugEvents, ...pendingEvents];
    const finalState = {
      ...prev,
      debugEvents: allDebugEvents,
      totalTokens: data?.totalTokens || 0,
      // ... rest
    };
    // ...
  });
}
```

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build 2>&1 | grep -E "error|Error" | head -20
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/hooks/useDebugChatStream.ts
git commit -m "fix(playground): add 120s stream timeout + batch debug event updates

- AbortSignal.timeout prevents indefinite streaming
- Debug events batched via ref + 50ms flush timer (reduces re-renders)"
```

---

### Task 6: Unify RAG strategies as single source of truth

**Files:**
- Create: `apps/web/src/lib/constants/rag-strategies.ts`
- Modify: `apps/web/src/components/admin/debug-chat/StrategySelectorBar.tsx`

- [ ] **Step 1: Create shared strategies constant**

```typescript
// apps/web/src/lib/constants/rag-strategies.ts

/**
 * RAG strategies available in the playground.
 * Must match backend AgentStrategy value object names.
 */
export const RAG_STRATEGIES = [
  { value: '__default__', label: 'Default (auto)' },
  { value: 'HybridSearch', label: 'Hybrid Search' },
  { value: 'VectorOnly', label: 'Vector Only' },
  { value: 'KeywordOnly', label: 'Keyword Only' },
  { value: 'RetrievalOnly', label: 'Retrieval Only' },
  { value: 'SentenceWindowRAG', label: 'Sentence Window' },
  { value: 'ColBERTReranking', label: 'ColBERT Reranking' },
  { value: 'SingleModel', label: 'Single Model' },
  { value: 'IterativeRAG', label: 'Iterative RAG' },
  { value: 'MultiModel', label: 'Multi-Model' },
] as const;

export type RagStrategyValue = (typeof RAG_STRATEGIES)[number]['value'];
```

- [ ] **Step 2: Update StrategySelectorBar to use shared constant**

```typescript
// Replace local RAG_STRATEGIES (lines 29-37) with:
import { RAG_STRATEGIES } from '@/lib/constants/rag-strategies';
```

Remove the local `RAG_STRATEGIES` array.

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build 2>&1 | grep -E "error|Error" | head -20
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/constants/rag-strategies.ts
git add apps/web/src/components/admin/debug-chat/StrategySelectorBar.tsx
git commit -m "refactor(playground): unify RAG strategies into single source of truth"
```

---

### Task 7: Add data-testid attributes for testability

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx`
- Modify: `apps/web/src/components/admin/debug-chat/StrategySelectorBar.tsx`

- [ ] **Step 1: Add testids to playground page**

Add `data-testid` to key elements in `ChatDebugTab`:

```tsx
// Chat input textarea
<textarea data-testid="playground-chat-input" ... />

// Send button
<button data-testid="playground-send-btn" ... />

// Stop button
<button data-testid="playground-stop-btn" ... />

// Chat messages container
<div data-testid="playground-chat-messages" className="flex-1 overflow-y-auto ...">

// Error display
<div data-testid="playground-error" className="border-t border-destructive/20 ...">
```

- [ ] **Step 2: Add testids to StrategySelectorBar**

```tsx
// Game selector
<Select data-testid="playground-game-select" ...>

// Strategy selector
<Select data-testid="playground-strategy-select" ...>

// Re-execute button
<button data-testid="playground-reexecute-btn" ...>

// Debug toggle
<button data-testid="playground-debug-toggle" ...>
```

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build 2>&1 | grep -E "error|Error" | head -20
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx
git add apps/web/src/components/admin/debug-chat/StrategySelectorBar.tsx
git commit -m "test(playground): add data-testid attributes for E2E testability"
```

---

### Task 8: Mobile-friendly debug panel

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx`

- [ ] **Step 1: Make debug panel visible on mobile as collapsible section below chat**

Replace the current `hidden lg:flex lg:flex-col` with a responsive layout:

```tsx
{/* Debug Timeline Panel */}
{showDebug && (
  <>
    {/* Desktop: side panel */}
    <div className="min-h-0 hidden lg:flex lg:flex-col">
      <DebugTimeline events={state.debugEvents} isStreaming={state.isStreaming} />
    </div>
    {/* Mobile: bottom panel */}
    <div className="lg:hidden border-t max-h-[40vh] overflow-y-auto">
      <DebugTimeline events={state.debugEvents} isStreaming={state.isStreaming} />
    </div>
  </>
)}
```

Update the grid layout to not split on mobile:

```tsx
// Old:
<div className={cn('flex-1 grid min-h-0', showDebug ? 'grid-cols-1 lg:grid-cols-[55%_45%]' : 'grid-cols-1')}>

// New:
<div className={cn('flex-1 flex flex-col lg:grid min-h-0', showDebug ? 'lg:grid-cols-[55%_45%]' : '')}>
```

- [ ] **Step 2: Verify build**

```bash
cd apps/web && pnpm build 2>&1 | grep -E "error|Error" | head -20
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx
git commit -m "fix(playground): make debug panel accessible on mobile (bottom panel)"
```

---

### Task 9: Final verification and PR

**Files:**
- None (verification and git operations)

- [ ] **Step 1: Full build verification**

```bash
cd apps/web && pnpm build
```

Expected: Clean build with no errors.

- [ ] **Step 2: Verify the page renders correctly**

Open `http://localhost:3000/admin/agents/playground` and verify:
- Single view (no tabs)
- Game selector loads shared games
- Model/Temperature/TopK controls visible in toolbar
- Chat sends messages with config overrides
- Debug panel toggles on/off
- Debug panel visible on mobile as bottom section
- Streaming stops after 2 minutes if no response

- [ ] **Step 3: Run frontend tests**

```bash
cd apps/web && pnpm test --passWithNoTests 2>&1 | tail -20
```

- [ ] **Step 4: Create PR to main-dev**

```bash
git push -u origin feature/playground-simplification
```

Create PR with title: `refactor(playground): simplify RAG playground — remove premature complexity`

PR body should reference the spec-panel findings:
- Removed Compare Tab (dead code, unused loading/error states)
- Removed Query Tester (3/6 params not sent to backend)
- Consolidated into single Chat Debug view
- Added model/temperature/topK controls to toolbar
- Fixed game endpoint (`/games` → `/shared-games`)
- Fixed `getProcessingQueue` param mismatch (`status` → `statusFilter`)
- Added 120s stream timeout
- Batched debug event state updates for performance
- Unified RAG strategies into single source of truth
- Added `data-testid` attributes
- Made debug panel mobile-friendly

---

## Summary of Spec-Panel Issues Addressed

| # | Issue | Severity | Task |
|---|-------|----------|------|
| 1 | Query Tester ignora model/temp/agent | CRITICAL | Task 3 (removed, controls added to Chat Debug) |
| 2 | Module-level HttpClient | CRITICAL | Task 4 |
| 3 | Stream SSE senza timeout | CRITICAL | Task 5 |
| 4 | Compare Tab dead code | HIGH | Task 2 |
| 5 | Fetch raw bypassa API client | HIGH | Task 3 (QueryTester removed) |
| 6 | Strategie incoerenti tra tab | MEDIUM | Task 6 |
| 7 | Game selector endpoint errato | HIGH | Task 3 |
| 8 | Performance debugEvents array | HIGH | Task 5 |
| 9 | Nessun data-testid | MEDIUM | Task 7 |
| 10 | Debug panel hidden su mobile | MEDIUM | Task 8 |
| 11 | `getProcessingQueue` param mismatch | CRITICAL | Task 1 (already fixed) |
