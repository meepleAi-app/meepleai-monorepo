# Admin AI Spec-Panel Improvements — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare le 7 osservazioni prioritarie del spec-panel sulla sezione AI admin (`/admin/agents`).

**Architecture:** Modifiche esclusivamente frontend Next.js. Nessun nuovo endpoint backend richiesto — si riusano API esistenti. Ogni task è indipendente e committabile separatamente.

**Tech Stack:** Next.js 14 (app router), React Query (@tanstack/react-query), TypeScript, date-fns, Lucide icons, Tailwind CSS, Shadcn/ui components.

---

## File Map

| File | Task | Tipo |
|------|------|------|
| `apps/web/src/app/admin/(dashboard)/agents/analytics/page.tsx` | T1 | Modifica |
| `apps/web/src/app/admin/(dashboard)/agents/page.tsx` | T2, T3, T4 | Modifica |
| `apps/web/src/app/admin/(dashboard)/agents/usage/page.tsx` | T5 | Modifica |
| `apps/web/src/components/admin/agent-definitions/BuilderTable.tsx` | T6 | Modifica |
| `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx` | T7 | Modifica |

---

## Task 1 — Analytics: Inline Retry Button + Etichette Italiane

**Problema:** La card di errore mostra "Failed to load metrics. Please try again." senza un pulsante Retry inline. Inoltre le tab e i titoli grafici sono in inglese (Overview, Top Agents, Trends, Usage Over Time, Cost Breakdown by Model).

**File:**
- Modifica: `apps/web/src/app/admin/(dashboard)/agents/analytics/page.tsx`

- [ ] **Step 1.1: Aggiungere il pulsante Retry inline nella card errore**

Trovare il blocco dell'error state (riga ~193) e sostituirlo:

```tsx
{/* Prima (riga ~193-199): */}
{!is404(metricsError) && (metricsError || topAgentsError) && (
  <Card className="border-destructive">
    <CardContent className="py-4">
      <p className="text-destructive">Failed to load metrics. Please try again.</p>
    </CardContent>
  </Card>
)}

{/* Dopo: */}
{!is404(metricsError) && (metricsError || topAgentsError) && (
  <Card className="border-destructive">
    <CardContent className="py-4 flex items-center justify-between gap-4">
      <p className="text-destructive text-sm">
        Impossibile caricare le metriche. Riprova.
      </p>
      <Button variant="outline" size="sm" onClick={handleRefresh}>
        <RefreshCw className="h-4 w-4 mr-2" />
        Riprova
      </Button>
    </CardContent>
  </Card>
)}
```

- [ ] **Step 1.2: Tradurre le tab in italiano**

Trovare il blocco `<TabsList>` (riga ~202) e sostituirlo:

```tsx
{/* Prima: */}
<TabsList>
  <TabsTrigger value="overview">Overview</TabsTrigger>
  <TabsTrigger value="top-agents">Top Agents</TabsTrigger>
  <TabsTrigger value="trends">Trends</TabsTrigger>
</TabsList>

{/* Dopo: */}
<TabsList>
  <TabsTrigger value="overview">Panoramica</TabsTrigger>
  <TabsTrigger value="top-agents">Top Agenti</TabsTrigger>
  <TabsTrigger value="trends">Tendenze</TabsTrigger>
</TabsList>
```

- [ ] **Step 1.3: Tradurre i titoli dei grafici**

Trovare e sostituire il titolo "Usage Over Time" (riga ~219):

```tsx
{/* Prima: */}
<CardTitle className="flex items-center gap-2 text-sm font-semibold">
  <TrendingUp className="h-4 w-4 text-blue-500" />
  Usage Over Time
</CardTitle>

{/* Dopo: */}
<CardTitle className="flex items-center gap-2 text-sm font-semibold">
  <TrendingUp className="h-4 w-4 text-blue-500" />
  Utilizzo nel Tempo
</CardTitle>
```

Trovare e sostituire il titolo "Cost Breakdown by Model" (riga ~239):

```tsx
{/* Prima: */}
<CardTitle className="flex items-center gap-2 text-sm font-semibold">
  <BarChart3 className="h-4 w-4 text-emerald-500" />
  Cost Breakdown by Model
</CardTitle>

{/* Dopo: */}
<CardTitle className="flex items-center gap-2 text-sm font-semibold">
  <BarChart3 className="h-4 w-4 text-emerald-500" />
  Costi per Modello
</CardTitle>
```

Trovare "Top Queries" (riga ~302) e tradurre il titolo del select:

```tsx
{/* Trovare in Select (riga ~269-276): */}
<SelectContent>
  <SelectItem value="invocations">By Usage</SelectItem>
  <SelectItem value="cost">By Cost</SelectItem>
  <SelectItem value="confidence">By Confidence</SelectItem>
</SelectContent>

{/* Sostituire con: */}
<SelectContent>
  <SelectItem value="invocations">Per Utilizzo</SelectItem>
  <SelectItem value="cost">Per Costo</SelectItem>
  <SelectItem value="confidence">Per Confidence</SelectItem>
</SelectContent>
```

- [ ] **Step 1.4: Commit**

```bash
cd D:\Repositories\meepleai-monorepo-backend
git add apps/web/src/app/admin/\(dashboard\)/agents/analytics/page.tsx
git commit -m "fix(admin-ai): analytics inline retry button + translate labels to Italian"
```

---

## Task 2 — Mission Control: KPI Delta Indicators

**Problema:** Le KPI card mostrano valori assoluti senza confronto con il periodo precedente. L'utente non può capire se i valori sono normali o anomali.

**Soluzione:** Aggiungere una seconda query React Query per il periodo precedente (dayBeforeYesterday → yesterday) e mostrare delta % sotto ogni KPI value.

**File:**
- Modifica: `apps/web/src/app/admin/(dashboard)/agents/page.tsx`

- [ ] **Step 2.1: Aggiungere import `subDays` (già presente) e aggiungere il calcolo della data precedente**

Trovare (riga ~88-89):
```typescript
const today = format(new Date(), 'yyyy-MM-dd');
const yesterday = format(subDays(new Date(), 1), 'yyyy-MM-dd');
```

Sostituire con:
```typescript
const today = format(new Date(), 'yyyy-MM-dd');
const yesterday = format(subDays(new Date(), 1), 'yyyy-MM-dd');
const dayBeforeYesterday = format(subDays(new Date(), 2), 'yyyy-MM-dd');
```

- [ ] **Step 2.2: Aggiungere query per il periodo precedente**

Dopo la query esistente `metrics` (riga ~95), aggiungere:

```typescript
const { data: prevMetrics } = useQuery({
  queryKey: ['admin', 'mission-control', 'metrics-prev'],
  queryFn: () =>
    api.admin.getAgentMetrics(dayBeforeYesterday, yesterday) as Promise<AgentMetrics>,
  staleTime: 300_000, // 5 min — dati storici cambiano poco
});
```

- [ ] **Step 2.3: Aggiungere helper per calcolare e formattare il delta**

Aggiungere subito dopo i formatters esistenti (dopo la funzione `formatErrorRate`, riga ~82):

```typescript
function computeDelta(current: number, previous: number): { pct: number; up: boolean } | null {
  if (!previous || previous === 0) return null;
  const pct = ((current - previous) / previous) * 100;
  return { pct: Math.abs(pct), up: pct >= 0 };
}

function DeltaBadge({ current, previous, invertColors = false }: {
  current: number;
  previous: number;
  invertColors?: boolean; // true per error rate: salire è negativo
}) {
  const delta = computeDelta(current, previous);
  if (!delta) return null;
  const isPositive = invertColors ? !delta.up : delta.up;
  return (
    <span
      className={`text-xs font-medium ${
        isPositive
          ? 'text-emerald-600 dark:text-emerald-400'
          : 'text-red-500 dark:text-red-400'
      }`}
    >
      {delta.up ? '▲' : '▼'} {delta.pct.toFixed(1)}%
    </span>
  );
}
```

- [ ] **Step 2.4: Aggiungere il delta sotto ogni KPI value**

Trovare la card "Esecuzioni Oggi" (riga ~207-213) e aggiungere il delta sotto il valore:

```tsx
{/* Prima: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <p className="text-2xl font-bold tracking-tight">{metrics?.totalInvocations ?? 0}</p>
)}

{/* Dopo: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <div className="flex items-end gap-2">
    <p className="text-2xl font-bold tracking-tight">{metrics?.totalInvocations ?? 0}</p>
    {metrics && prevMetrics && (
      <DeltaBadge current={metrics.totalInvocations} previous={prevMetrics.totalInvocations} />
    )}
  </div>
)}
```

Trovare la card "Latenza Media" (riga ~225-230) e aggiungere il delta:

```tsx
{/* Prima: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <p className="text-2xl font-bold tracking-tight">
    {metrics ? formatLatency(metrics.avgLatencyMs) : '—'}
  </p>
)}

{/* Dopo: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <div className="flex items-end gap-2">
    <p className="text-2xl font-bold tracking-tight">
      {metrics ? formatLatency(metrics.avgLatencyMs) : '—'}
    </p>
    {metrics && prevMetrics && (
      <DeltaBadge
        current={metrics.avgLatencyMs}
        previous={prevMetrics.avgLatencyMs}
        invertColors  // latenza alta = negativo
      />
    )}
  </div>
)}
```

Trovare la card "Error Rate" (riga ~243-247) e aggiungere il delta:

```tsx
{/* Prima: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <p className="text-2xl font-bold tracking-tight">{formatErrorRate(errorRate)}</p>
)}

{/* Dopo: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <div className="flex items-end gap-2">
    <p className="text-2xl font-bold tracking-tight">{formatErrorRate(errorRate)}</p>
    {metrics && prevMetrics && (
      <DeltaBadge
        current={errorRate}
        previous={1 - prevMetrics.userSatisfactionRate}
        invertColors  // error rate alto = negativo
      />
    )}
  </div>
)}
```

Trovare la card "Token Consumati" (riga ~258-263) e aggiungere il delta:

```tsx
{/* Prima: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <p className="text-2xl font-bold tracking-tight">
    {metrics ? formatTokens(metrics.totalTokensUsed) : '0'}
  </p>
)}

{/* Dopo: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <div className="flex items-end gap-2">
    <p className="text-2xl font-bold tracking-tight">
      {metrics ? formatTokens(metrics.totalTokensUsed) : '0'}
    </p>
    {metrics && prevMetrics && (
      <DeltaBadge current={metrics.totalTokensUsed} previous={prevMetrics.totalTokensUsed} />
    )}
  </div>
)}
```

Trovare la card "Costo Oggi" (riga ~275-280) e aggiungere il delta:

```tsx
{/* Prima: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <p className="text-2xl font-bold tracking-tight">
    {metrics ? formatCost(metrics.totalCost) : '$0.00'}
  </p>
)}

{/* Dopo: */}
{metricsLoading ? (
  <Skeleton className="h-7 w-20" />
) : (
  <div className="flex items-end gap-2">
    <p className="text-2xl font-bold tracking-tight">
      {metrics ? formatCost(metrics.totalCost) : '$0.00'}
    </p>
    {metrics && prevMetrics && (
      <DeltaBadge
        current={metrics.totalCost}
        previous={prevMetrics.totalCost}
        invertColors  // costo alto = negativo
      />
    )}
  </div>
)}
```

- [ ] **Step 2.5: Verificare che TypeScript non segnali errori**

```bash
cd D:\Repositories\meepleai-monorepo-backend\apps\web
pnpm typecheck 2>&1 | grep "agents/page"
```

Expected: nessun errore per `agents/page.tsx`.

- [ ] **Step 2.6: Commit**

```bash
cd D:\Repositories\meepleai-monorepo-backend
git add apps/web/src/app/admin/\(dashboard\)/agents/page.tsx
git commit -m "feat(admin-ai): add KPI delta indicators to Mission Control dashboard"
```

---

## Task 3 — Mission Control: Service Status Specifico

**Problema:** Reranker Service e Vector DB mostrano "Sconosciuto" perché non hanno endpoint dedicati. "Sconosciuto" non fornisce informazioni azionabili all'operatore.

**Soluzione:** Rinominare lo stato `unknown` in "Non Configurato" per questi servizi (con tooltip esplicativo) mantenendo comunque il colore grigio. Quando esiste un errore di rete nel fetch embedding, mostrare "Non Raggiungibile".

**File:**
- Modifica: `apps/web/src/app/admin/(dashboard)/agents/page.tsx`

- [ ] **Step 3.1: Aggiungere import `Tooltip` e `Info`**

Aggiungere agli import esistenti di lucide-react `Info`:
```typescript
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Bot,
  CircleDollarSign,
  Clock,
  FileSearch,
  Gauge,
  Info,       // ← aggiungere
  PlusCircle,
  Search,
  Zap,
} from 'lucide-react';
```

- [ ] **Step 3.2: Aggiungere l'import del Tooltip**

Trovare la sezione degli import dei componenti UI (riga ~21-24) e aggiungere:
```typescript
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
```

> **Nota:** Se il Tooltip non esiste in `@/components/ui/overlays/tooltip`, usare il pattern HTML con `title` attribute invece:
> ```tsx
> <span title="Nessun endpoint di monitoraggio configurato per questo servizio.">
>   {healthBadge(status)}
> </span>
> ```

- [ ] **Step 3.3: Aggiornare i label dello stato `unknown`**

Trovare e modificare la riga dell'etichetta `unknown` nella funzione `healthBadge` (riga ~155-157):

```typescript
// Prima:
unknown: {
  label: 'Sconosciuto',
  className: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400',
},

// Dopo:
unknown: {
  label: 'Non Configurato',
  className: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400',
},
```

- [ ] **Step 3.4: Aggiungere `unreachable` come nuovo stato**

Aggiungere il tipo `HealthStatus` e il nuovo stato nella funzione `healthBadge`:

```typescript
// Prima:
type HealthStatus = 'healthy' | 'degraded' | 'down' | 'unknown';

// Dopo:
type HealthStatus = 'healthy' | 'degraded' | 'down' | 'unknown' | 'unreachable';
```

Aggiungere nella `variants` map all'interno di `healthBadge`:
```typescript
unreachable: {
  label: 'Non Raggiungibile',
  className: 'bg-red-50 text-red-600 dark:bg-red-900/20 dark:text-red-400',
},
```

- [ ] **Step 3.5: Aggiornare `getServiceHealth` per Embedding con errore di rete**

Trovare la funzione `getServiceHealth` (riga ~124-137) e modificare il branch Embedding:

```typescript
// Prima:
if (name === 'Embedding Service') {
  if (!embeddingInfo) return 'unknown';
  return (embeddingInfo as EmbeddingInfo).status === 'healthy' ? 'healthy' : 'degraded';
}

// Dopo:
if (name === 'Embedding Service') {
  if (embeddingError) return 'unreachable';
  if (!embeddingInfo) return 'unknown';
  return (embeddingInfo as EmbeddingInfo).status === 'healthy' ? 'healthy' : 'degraded';
}
```

Per fare questo, aggiungere `isError: embeddingError` alla query embedding:

```typescript
// Prima (riga ~103-107):
const { data: embeddingInfo } = useQuery({
  queryKey: ['admin', 'mission-control', 'embedding'],
  queryFn: () => api.admin.getEmbeddingInfo(),
  staleTime: 120_000,
});

// Dopo:
const { data: embeddingInfo, isError: embeddingError } = useQuery({
  queryKey: ['admin', 'mission-control', 'embedding'],
  queryFn: () => api.admin.getEmbeddingInfo(),
  staleTime: 120_000,
  retry: 1,
});
```

- [ ] **Step 3.6: Aggiungere tooltip per servizi Non Configurati nel render**

Trovare il render dei servizi (riga ~292-308) e aggiungere il tooltip per stato `unknown`:

```tsx
{services.map(svc => {
  const Icon = svc.icon;
  const status = getServiceHealth(svc.name);
  return (
    <div
      key={svc.name}
      className="flex items-center justify-between rounded-lg border border-slate-200/60 dark:border-zinc-700/40 px-3 py-2"
    >
      <div className="flex items-center gap-2">
        <Icon className="h-4 w-4 text-muted-foreground" />
        <span className="text-sm">{svc.name}</span>
        {status === 'unknown' && (
          <span
            title="Nessun endpoint di monitoraggio configurato per questo servizio."
            className="cursor-help"
          >
            <Info className="h-3 w-3 text-muted-foreground" />
          </span>
        )}
      </div>
      {healthBadge(status)}
    </div>
  );
})}
```

- [ ] **Step 3.7: Commit**

```bash
cd D:\Repositories\meepleai-monorepo-backend
git add apps/web/src/app/admin/\(dashboard\)/agents/page.tsx
git commit -m "fix(admin-ai): replace 'Sconosciuto' with specific service states + tooltip"
```

---

## Task 4 — Mission Control: Quick Actions Contestuali

**Problema:** Le 4 azioni rapide sono sempre le stesse indipendentemente dallo stato dei servizi. Se un servizio è degradato, l'azione prioritaria dovrebbe essere diagnosticare il problema.

**Soluzione:** Anteporre dinamicamente un'azione contestuale in cima alla lista quando un servizio è `degraded` o `unreachable`, usando i dati già disponibili da `getServiceHealth`.

**File:**
- Modifica: `apps/web/src/app/admin/(dashboard)/agents/page.tsx`

- [ ] **Step 4.1: Calcolare i servizi degradati**

Trovare la sezione `// ─── Quick Actions` (riga ~175) e aggiungere prima dell'array `quickActions`:

```typescript
// ─── Contextual Quick Actions ────────────────────────────────────────────

const degradedServices = services
  .map(svc => ({ ...svc, status: getServiceHealth(svc.name) }))
  .filter(svc => svc.status === 'degraded' || svc.status === 'unreachable');

const contextualActions = degradedServices.length > 0
  ? [{
      label: `Diagnostica ${degradedServices[0].name}`,
      icon: AlertTriangle,
      href: '/admin/agents/inspector',
      variant: 'destructive' as const,
    }]
  : [];

const quickActions = [
  ...contextualActions,
  { label: 'Testa Query RAG', icon: Search, href: '/admin/agents/playground', variant: 'outline' as const },
  { label: 'Ispeziona Esecuzioni', icon: FileSearch, href: '/admin/agents/inspector', variant: 'outline' as const },
  { label: 'Report Costi', icon: CircleDollarSign, href: '/admin/agents/usage', variant: 'outline' as const },
  { label: 'Nuovo Agente', icon: PlusCircle, href: '/admin/agents/definitions/create', variant: 'outline' as const },
];
```

- [ ] **Step 4.2: Aggiornare il render delle quick actions per supportare `variant`**

Trovare il render delle quick actions (riga ~317-330) e aggiornarlo:

```tsx
{/* Prima: */}
{quickActions.map(action => {
  const Icon = action.icon;
  return (
    <Button
      key={action.label}
      variant="outline"
      className="w-full justify-start gap-2"
      onClick={() => router.push(action.href)}
    >
      <Icon className="h-4 w-4" />
      {action.label}
    </Button>
  );
})}

{/* Dopo: */}
{quickActions.map(action => {
  const Icon = action.icon;
  return (
    <Button
      key={action.label}
      variant={action.variant}
      className="w-full justify-start gap-2"
      onClick={() => router.push(action.href)}
    >
      <Icon className="h-4 w-4" />
      {action.label}
    </Button>
  );
})}
```

- [ ] **Step 4.3: Commit**

```bash
cd D:\Repositories\meepleai-monorepo-backend
git add apps/web/src/app/admin/\(dashboard\)/agents/page.tsx
git commit -m "feat(admin-ai): contextual quick actions based on service health status"
```

---

## Task 5 — Usage & Costs: Timestamp Relativo + Label Italiane

**Problema:** Il timestamp "Updated 00:11:37" è ambiguo (ora del giorno vs elapsed time). Le sezioni "Overview", "Charts", "Rate Limits", "Recent Requests" sono in inglese.

**File:**
- Modifica: `apps/web/src/app/admin/(dashboard)/agents/usage/page.tsx`

- [ ] **Step 5.1: Aggiungere import `formatDistanceToNow` e locale italiana**

Trovare gli import di `date-fns` esistenti nel file e aggiungere:

```typescript
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
```

> **Nota:** Se `date-fns` non è già importato in questo file, aggiungere entrambe le righe. La libreria è già presente nel progetto (usata in analytics/page.tsx).

- [ ] **Step 5.2: Sostituire il timestamp con formato relativo**

Trovare (riga ~118-122):
```tsx
{dataUpdatedAt > 0 && (
  <span className="text-xs text-muted-foreground">
    Updated {new Date(dataUpdatedAt).toLocaleTimeString()}
  </span>
)}
```

Sostituire con:
```tsx
{dataUpdatedAt > 0 && (
  <span className="text-xs text-muted-foreground">
    Aggiornato {formatDistanceToNow(new Date(dataUpdatedAt), { addSuffix: true, locale: it })}
  </span>
)}
```

- [ ] **Step 5.3: Tradurre le intestazioni delle sezioni in italiano**

Trovare e sostituire ogni `<h2>` nell'OpenRouter tab:

```tsx
{/* Overview (riga ~170): */}
{/* Prima: */}
<h2 className="text-lg font-medium font-quicksand mb-4">Overview</h2>
{/* Dopo: */}
<h2 className="text-lg font-medium font-quicksand mb-4">Panoramica</h2>

{/* Charts (riga ~176): */}
{/* Prima: */}
<h2 className="text-lg font-medium font-quicksand mb-4">Charts</h2>
{/* Dopo: */}
<h2 className="text-lg font-medium font-quicksand mb-4">Grafici</h2>

{/* Rate Limits (riga ~195): */}
{/* Prima: */}
<h2 className="text-lg font-medium font-quicksand mb-4">Rate Limits</h2>
{/* Dopo: */}
<h2 className="text-lg font-medium font-quicksand mb-4">Limiti di Frequenza</h2>

{/* Recent Requests (riga ~203): */}
{/* Prima: */}
<h2 className="text-lg font-medium font-quicksand mb-4">Recent Requests</h2>
{/* Dopo: */}
<h2 className="text-lg font-medium font-quicksand mb-4">Richieste Recenti</h2>
```

- [ ] **Step 5.4: Tradurre le tab in italiano**

Trovare il `<TabsList>` (riga ~138-142):
```tsx
{/* Prima: */}
<TabsTrigger value="openrouter">OpenRouter</TabsTrigger>
<TabsTrigger value="token-balance">Token Balance</TabsTrigger>
<TabsTrigger value="chat-log">Chat Log</TabsTrigger>

{/* Dopo: */}
<TabsTrigger value="openrouter">OpenRouter</TabsTrigger>
<TabsTrigger value="token-balance">Saldo Token</TabsTrigger>
<TabsTrigger value="chat-log">Log Chat</TabsTrigger>
```

- [ ] **Step 5.5: Tradurre il messaggio di errore nel banner**

Trovare (riga ~155-166):
```tsx
{/* Prima: */}
<span>
  Failed to load usage data:{' '}
  {error instanceof Error ? error.message : 'Unknown error'}
</span>

{/* Dopo: */}
<span>
  Impossibile caricare i dati di utilizzo:{' '}
  {error instanceof Error ? error.message : 'Errore sconosciuto'}
</span>
```

- [ ] **Step 5.6: Commit**

```bash
cd D:\Repositories\meepleai-monorepo-backend
git add apps/web/src/app/admin/\(dashboard\)/agents/usage/page.tsx
git commit -m "fix(admin-ai): relative timestamp + Italian labels in Usage & Costs page"
```

---

## Task 6 — Agent Definitions: Fix Dual Status Badge

**Problema:** La colonna Status mostra due badge: uno per il lifecycle (Draft/Testing/Published) e uno per `isActive/Inactive`. Per un agente in Draft, "Inactive" è ridondante e confuso — un Draft non può essere attivo.

**Soluzione:** Mostrare il badge `isActive` solo per agenti Published (status === 2). Per Draft e Testing il lifecycle badge è sufficiente.

**File:**
- Modifica: `apps/web/src/components/admin/agent-definitions/BuilderTable.tsx`

- [ ] **Step 6.1: Modificare la cella Status**

Trovare il blocco della cella status (riga ~78-88):

```tsx
{/* Prima: */}
{
  id: 'status',
  header: 'Status',
  cell: ({ row }) => {
    const agent = row.original;
    return (
      <div className="flex items-center gap-2">
        {getStatusBadge(agent.status ?? 0)}
        <Badge variant={agent.isActive ? 'default' : 'outline'}>
          {agent.isActive ? 'Active' : 'Inactive'}
        </Badge>
      </div>
    );
  },
},

{/* Dopo: */}
{
  id: 'status',
  header: 'Stato',
  cell: ({ row }) => {
    const agent = row.original;
    const isPublished = (agent.status ?? 0) === 2;
    return (
      <div className="flex items-center gap-2">
        {getStatusBadge(agent.status ?? 0)}
        {isPublished && (
          <Badge variant={agent.isActive ? 'default' : 'outline'}>
            {agent.isActive ? 'Attivo' : 'Inattivo'}
          </Badge>
        )}
      </div>
    );
  },
},
```

- [ ] **Step 6.2: Tradurre le altre colonne in italiano**

Trovare gli header delle colonne e tradurli:

```typescript
// Prima:
{ accessorKey: 'name', header: 'Name' },
{ accessorKey: 'description', header: 'Description', ... },
{ accessorKey: 'config.model', header: 'Model', ... },
{ accessorKey: 'createdAt', header: 'Created', ... },

// Dopo:
{ accessorKey: 'name', header: 'Nome' },
{ accessorKey: 'description', header: 'Descrizione', ... },
{ accessorKey: 'config.model', header: 'Modello', ... },
{ accessorKey: 'createdAt', header: 'Creato', ... },
```

Tradurre anche i label delle azioni nel DropdownMenu:
```tsx
{/* Edit → Modifica */}
<Link href={`/admin/agents/definitions/${agent.id}/edit`}>
  <Pencil className="h-4 w-4 mr-2" />
  Modifica
</Link>

{/* Start Testing → Avvia Test */}
<DropdownMenuItem onClick={() => onStartTesting(agent.id)}>
  <FlaskConical className="h-4 w-4 mr-2" />
  Avvia Test
</DropdownMenuItem>

{/* Publish → Pubblica */}
<DropdownMenuItem onClick={() => onPublish(agent.id)}>
  <Rocket className="h-4 w-4 mr-2" />
  Pubblica
</DropdownMenuItem>
```

Trovare il resto del DropdownMenu (riga ~129+) e tradurre eventuali altre label (Unpublish → Revoca Pubblicazione, Delete → Elimina).

- [ ] **Step 6.3: Tradurre i STATUS_LABELS**

Trovare (riga ~27):
```typescript
const STATUS_LABELS = ['Draft', 'Testing', 'Published'] as const;
```

Sostituire con:
```typescript
const STATUS_LABELS = ['Bozza', 'In Test', 'Pubblicato'] as const;
```

- [ ] **Step 6.4: Commit**

```bash
cd D:\Repositories\meepleai-monorepo-backend
git add apps/web/src/components/admin/agent-definitions/BuilderTable.tsx
git commit -m "fix(admin-ai): fix dual status badge in agent definitions + translate labels"
```

---

## Task 7 — Playground: Model Dropdown da Config API

**Problema:** Il campo "Model" nel Query Tester è un testo libero (`<input>`) — l'utente può scrivere model ID non validi. I modelli configurati sono già disponibili via `getAiModels()`.

**Soluzione:** Caricare i modelli attivi da `/api/v1/admin/ai-models` e sostituire il text input con un `<Select>` dropdown.

**File:**
- Modifica: `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx`

- [ ] **Step 7.1: Aggiungere import per useQuery e Select**

Aggiungere agli import esistenti:

```typescript
import { useQuery } from '@tanstack/react-query';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
```

Aggiungere dopo gli import (module-level, come in usage/page.tsx):
```typescript
const _httpClient = new HttpClient();
const _adminClient = createAdminClient({ httpClient: _httpClient });
```

- [ ] **Step 7.2: Aggiungere la query dei modelli nel componente `QueryTesterTab`**

Trovare la funzione `QueryTesterTab` (riga ~61) e aggiungere la query all'inizio del componente, dopo gli `useState`:

```typescript
const { data: modelsData } = useQuery({
  queryKey: ['admin', 'ai-models', 'active'],
  queryFn: () => _adminClient.getAiModels({ status: 'active' }),
  staleTime: 300_000,
});

const availableModels = modelsData?.items ?? [];
```

- [ ] **Step 7.3: Sostituire il text input del model con un Select**

Trovare il campo model nel JSX di `QueryTesterTab`. Cercare il campo con `value={model}` e `onChange` per il model. Sostituire il `<input>` / `<Label>+<Input>` con:

```tsx
{/* Prima (cerca il blocco con placeholder="gpt-4o-mini" o simile): */}
<div>
  <label className="text-xs font-medium text-muted-foreground">Model</label>
  <input
    type="text"
    value={model}
    onChange={e => setModel(e.target.value)}
    placeholder="gpt-4o-mini"
    className="..."
  />
</div>

{/* Dopo: */}
<div>
  <label className="text-xs font-medium text-muted-foreground">Modello</label>
  <Select
    value={model}
    onValueChange={setModel}
  >
    <SelectTrigger>
      <SelectValue placeholder="Seleziona modello..." />
    </SelectTrigger>
    <SelectContent>
      {availableModels.length === 0 ? (
        <SelectItem value="" disabled>
          Nessun modello configurato
        </SelectItem>
      ) : (
        availableModels.map(m => (
          <SelectItem key={m.id} value={m.modelIdentifier}>
            {m.displayName} ({m.modelIdentifier})
          </SelectItem>
        ))
      )}
    </SelectContent>
  </Select>
</div>
```

> **Nota:** I campi esatti del tipo `AiModelDto` sono: `id` (UUID), `displayName` (string), `modelIdentifier` (string, es. "google/gemini-pro"), più altri campi. Confermato da `apps/web/src/lib/api/schemas/ai-models.schemas.ts:62`.

- [ ] **Step 7.4: Verificare il tipo `AiModelDto`**

```bash
grep -n "displayName\|modelId\|AiModelDto" D:\Repositories\meepleai-monorepo-backend\apps\web\src\lib\api\schemas\admin-ai.schemas.ts 2>/dev/null | head -20
```

Se `displayName` non esiste, usare il campo appropriato (`name`, `model`, `modelId`, ecc.).

- [ ] **Step 7.5: Verificare TypeScript**

```bash
cd D:\Repositories\meepleai-monorepo-backend\apps\web
pnpm typecheck 2>&1 | grep "playground/page"
```

Expected: nessun errore per `playground/page.tsx`.

- [ ] **Step 7.6: Commit**

```bash
cd D:\Repositories\meepleai-monorepo-backend
git add apps/web/src/app/admin/\(dashboard\)/agents/playground/page.tsx
git commit -m "feat(admin-ai): replace model text input with dropdown from AI models API"
```

---

## Self-Review del Piano

### Copertura spec-panel osservazioni

| Osservazione spec-panel | Task | Coperta? |
|------------------------|------|----------|
| Analytics broken endpoint senza retry | T1 | ✅ |
| Analytics etichette inglesi | T1 | ✅ |
| KPI senza contesto storico/delta | T2 | ✅ |
| "Sconosciuto" → stati diagnostici | T3 | ✅ |
| Azioni rapide statiche | T4 | ✅ |
| Timestamp "Updated 00:11:37" ambiguo | T5 | ✅ |
| Usage sezioni label inglesi | T5 | ✅ |
| Doppio badge stato agenti | T6 | ✅ |
| Model field text libero in Playground | T7 | ✅ |

### Osservazioni spec-panel NON in questo piano (medio termine / richiedono backend)

- Delta storici con sparkline → richiede nuovo endpoint o chart library
- Sistema alerting/threshold configurabile → richiede backend + notifiche
- Audit log modifiche configurazione → richiede backend
- RBAC matrix in tab separata → refactor config page, scope separato
- Confidence slider label "Any" → inspector page, bassa priorità

### Scan placeholder

Nessun "TBD", "TODO", "fill in", o "similar to task N" trovato.

### Consistenza tipi

- `AgentMetrics` usato in T2 è lo stesso tipo definito in `agents/page.tsx` riga 29
- `HealthStatus` esteso in T3 da 4 a 5 valori — usato solo localmente in `page.tsx`
- `AiModelDto` in T7 richiede verifica dei field — Step 7.4 è esplicitamente dedicato a questo

---

## Esecuzione

Piano salvato in `docs/superpowers/plans/2026-04-06-admin-ai-spec-panel-improvements.md`.

**Branch suggerito:** `feature/issue-admin-ai-spec-panel-improvements` da `main-dev`.

```bash
git checkout main-dev && git pull
git checkout -b feature/admin-ai-spec-panel-improvements
git config branch.feature/admin-ai-spec-panel-improvements.parent main-dev
```
