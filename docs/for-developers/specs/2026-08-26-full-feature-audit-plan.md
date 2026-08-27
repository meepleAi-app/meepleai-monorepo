# Full Feature Audit — Implementation Plan (Ondata 0: harness)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Costruire l'harness che rende eseguibile l'audit esaustivo — inventario delle 220 rotte e ~1400 endpoint, risoluzione dei parametri dinamici, crawler Playwright con login reale, collettore di evidenze log/DB.

**Architecture:** Cinque script Node/TypeScript in `apps/web/scripts/audit/` (puri e testabili con Vitest, I/O iniettato) più un crawler Playwright in `apps/web/e2e/audit/` con config dedicata senza auth bypass. Ogni script ha una funzione pura testabile e un `main()` sottile che fa solo I/O.

**Tech Stack:** TypeScript 5.9 · tsx 4.23 · Vitest · Playwright 1.62 · `docker exec` per Postgres (nessuna dipendenza `pg` da aggiungere)

**Spec:** [`2026-08-26-full-feature-audit-design.md`](./2026-08-26-full-feature-audit-design.md)

## Global Constraints

- **Nessuna nuova dipendenza npm.** Postgres si interroga via `docker exec meepleai-postgres psql`; i log via `docker logs meepleai-api`. `pg` non è installato e non va installato.
- **Niente `PLAYWRIGHT_AUTH_BYPASS`** nella config dell'audit: il login deve essere reale, altrimenti l'audit non prova l'autenticazione.
- **I/O sempre iniettato**: ogni funzione che esegue comandi o legge il filesystem riceve l'esecutore come parametro con un default reale. È l'unico modo per testarle senza Docker.
- **Output deterministico**: ordinamento esplicito in ogni file generato (rotte per path, endpoint per file+riga). Due esecuzioni sullo stesso codice devono produrre file byte-identici, altrimenti il diff del tracker è illeggibile.
- **Percorsi**: script in `apps/web/scripts/audit/`, test in `apps/web/scripts/audit/__tests__/`, crawler in `apps/web/e2e/audit/`, output in `docs/for-developers/audits/2026-08-26-full-feature-audit/`.
- **Comandi**: test `pnpm vitest run scripts/audit/__tests__/<file>` da `apps/web/`; script `pnpm tsx scripts/audit/<file>.ts`.
- **Commit**: `chore(audit): <descrizione>`, subject ≤ 72 caratteri (il commit-msg hook rifiuta oltre).

---

## File Structure

| File | Responsabilità |
|---|---|
| `apps/web/scripts/audit/types.ts` | Tipi condivisi: `RouteEntry`, `EndpointEntry`, `InventoryRow` |
| `apps/web/scripts/audit/extract-fe-routes.ts` | `page.tsx` → rotte URL, con segmenti dinamici e route group |
| `apps/web/scripts/audit/extract-api-endpoints.ts` | `Routing/**/*.cs` + `Program.cs` → endpoint con path completo, auth, tag |
| `apps/web/scripts/audit/context-map.ts` | Mappatura esplicita prefisso rotta → bounded context |
| `apps/web/scripts/audit/build-inventory.ts` | Unisce rotte + endpoint + contesti → `inventory.csv` |
| `apps/web/scripts/audit/resolve-params.ts` | Id reali dal Postgres locale → `route-params.json` |
| `apps/web/scripts/audit/collect-evidence.ts` | Finestra log + diff conteggi tabella attorno a un'azione |
| `apps/web/scripts/audit/render-report.ts` | Report JSON del crawler → markdown + stato aggiornato nel CSV |
| `apps/web/playwright.audit.config.ts` | Config Playwright dedicata, senza auth bypass |
| `apps/web/e2e/audit/auth-setup.ts` | Login reale via UI per ruolo → `storageState` |
| `apps/web/e2e/audit/crawl.spec.ts` | Percorre l'inventario e cattura le evidenze per rotta |

---

### Task 1: Tipi condivisi e estrattore rotte frontend

**Files:**
- Create: `apps/web/scripts/audit/types.ts`
- Create: `apps/web/scripts/audit/extract-fe-routes.ts`
- Test: `apps/web/scripts/audit/__tests__/extract-fe-routes.test.ts`

**Interfaces:**
- Consumes: niente (primo task)
- Produces: `type RouteEntry = { route: string; group: string; dynamicSegments: string[]; file: string }` · `toRoute(relPath: string): RouteEntry | null` · `extractFeRoutes(appDir: string): RouteEntry[]`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/scripts/audit/__tests__/extract-fe-routes.test.ts
import { describe, expect, it } from 'vitest';

import { toRoute } from '../extract-fe-routes';

describe('toRoute', () => {
  it('elimina i route group dalla URL ma li conserva come gruppo', () => {
    expect(toRoute('(authenticated)/library/page.tsx')).toEqual({
      route: '/library',
      group: '(authenticated)',
      dynamicSegments: [],
      file: '(authenticated)/library/page.tsx',
    });
  });

  it('riconosce i segmenti dinamici', () => {
    expect(toRoute('(authenticated)/library/[gameId]/kb/page.tsx')).toEqual({
      route: '/library/[gameId]/kb',
      group: '(authenticated)',
      dynamicSegments: ['gameId'],
      file: '(authenticated)/library/[gameId]/kb/page.tsx',
    });
  });

  it('riconosce i catch-all', () => {
    const entry = toRoute('admin/docs/[...slug]/page.tsx');
    expect(entry?.route).toBe('/admin/docs/[...slug]');
    expect(entry?.dynamicSegments).toEqual(['...slug']);
  });

  it('mappa la root su /', () => {
    expect(toRoute('page.tsx')?.route).toBe('/');
  });

  it('usa il primo segmento come gruppo quando non ci sono parentesi', () => {
    expect(toRoute('admin/users/page.tsx')?.group).toBe('admin');
  });

  it('ignora i file che non sono page.tsx', () => {
    expect(toRoute('(authenticated)/library/layout.tsx')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/extract-fe-routes.test.ts`
Expected: FAIL — "Failed to resolve import '../extract-fe-routes'"

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/scripts/audit/types.ts
export type RouteEntry = {
  route: string;
  group: string;
  dynamicSegments: string[];
  file: string;
};

export type EndpointEntry = {
  method: string;
  path: string;
  auth: 'anonymous' | 'authenticated' | 'admin' | 'unknown';
  tags: string[];
  file: string;
  line: number;
};

export type InventoryRow = {
  id: string;
  tipo: 'route' | 'endpoint';
  path: string;
  metodo: string;
  contesto: string;
  ruolo: 'user' | 'admin';
  livello: 'L1' | 'L2' | 'L3';
  stato: string;
  evidenza: string;
  note: string;
};
```

```ts
// apps/web/scripts/audit/extract-fe-routes.ts
import { readdirSync, statSync } from 'node:fs';
import path from 'node:path';

import type { RouteEntry } from './types';

const ROUTE_GROUP = /^\(.+\)$/;

/** Converte un path relativo a src/app in una rotta URL. Ritorna null se non è una page. */
export function toRoute(relPath: string): RouteEntry | null {
  const normalized = relPath.split(path.sep).join('/');
  if (!normalized.endsWith('page.tsx')) return null;

  const segments = normalized.split('/').slice(0, -1); // via 'page.tsx'
  const group = segments.find((s) => ROUTE_GROUP.test(s)) ?? segments[0] ?? '(root)';
  const urlSegments = segments.filter((s) => !ROUTE_GROUP.test(s));
  const dynamicSegments = urlSegments
    .filter((s) => s.startsWith('[') && s.endsWith(']'))
    .map((s) => s.slice(1, -1));

  return {
    route: urlSegments.length ? `/${urlSegments.join('/')}` : '/',
    group,
    dynamicSegments,
    file: normalized,
  };
}

/** Percorre src/app e raccoglie tutte le page.tsx, ordinate per rotta. */
export function extractFeRoutes(appDir: string): RouteEntry[] {
  const found: RouteEntry[] = [];

  const walk = (dir: string): void => {
    for (const name of readdirSync(dir).sort()) {
      const full = path.join(dir, name);
      if (statSync(full).isDirectory()) {
        walk(full);
        continue;
      }
      const entry = toRoute(path.relative(appDir, full));
      if (entry) found.push(entry);
    }
  };

  walk(appDir);
  return found.sort((a, b) => a.route.localeCompare(b.route));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/extract-fe-routes.test.ts`
Expected: PASS — 6 test

- [ ] **Step 5: Verifica sul repo reale**

Run:
```bash
cd apps/web && pnpm tsx -e "import('./scripts/audit/extract-fe-routes').then(m => { const r = m.extractFeRoutes('src/app'); console.log('rotte:', r.length); console.log(r.slice(0,3)); })"
```
Expected: `rotte: 220` (il conteggio misurato il 2026-08-26). Se diverge, non è un errore automatico: il repo può essere cambiato. Verifica con `find src/app -name page.tsx | wc -l` che i due numeri coincidano.

- [ ] **Step 6: Commit**

```bash
git add apps/web/scripts/audit/types.ts apps/web/scripts/audit/extract-fe-routes.ts apps/web/scripts/audit/__tests__/extract-fe-routes.test.ts
git commit -m "chore(audit): estrattore delle rotte frontend"
```

---

### Task 2: Estrattore endpoint API

**Files:**
- Create: `apps/web/scripts/audit/extract-api-endpoints.ts`
- Test: `apps/web/scripts/audit/__tests__/extract-api-endpoints.test.ts`

**Interfaces:**
- Consumes: `EndpointEntry` da `./types`
- Produces: `statementFrom(source: string, index: number): string` · `methodBody(source: string, name: string): string` · `authFromChain(chain: string): EndpointEntry['auth'] | null` · `parseGroupPrefixes(source: string): Map<string, GroupInfo>` · `parseRoutingFile(source: string, file: string, groupPrefix: string): EndpointEntry[]` · `parseProgramPrefixes(source: string): Map<string, string>` · `extractApiEndpoints(apiDir: string): EndpointEntry[]`

**Contesto misurato sul codice reale** (2026-08-26 — conta, perché un parser ingenuo qui produce path plausibili e sbagliati):

- `Program.cs:792` definisce `var v1Api = app.MapGroup("/api/v1")`. Le registrazioni sono 187, quasi tutte nella forma `v1Api.MapGameEndpoints();` (nessun prefisso extra); una minoranza usa `v1Api.MapGroup("/admin/catalog/seeds").MapAdminCatalogSeedEndpoints();`.
- **95 file sotto `Routing/` dichiarano un `MapGroup` interno, e alcuni più d'uno** (es. `AdminAgentAnalyticsEndpoints.cs` ha `var agentsGroup = group.MapGroup("/admin/agents")`). Applicare il primo gruppo a tutti gli endpoint del file è sbagliato: il prefisso va risolto **per variabile ricevente**.
- L'autorizzazione è spesso dichiarata **sul gruppo**, non sull'endpoint. Forme presenti: `RequireAuthorization()` ×129, `"AdminOrEditorPolicy"` ×47, `"AdminOnlyPolicy"` ×33, `"RequireSuperAdmin"` ×15, `"RequireAdminOrAbove"` ×11, `policy => policy.RequireRole("SuperAdmin", "Admin")` ×6, `"AdminPolicy"` ×4, `"EditorOnlyPolicy"` ×2, `"RequireEditorOrAbove"` ×2. Un endpoint senza modificatore proprio **eredita** quello del suo gruppo.
- Il test `/admin/i` sulla policy classifica correttamente tutte le forme admin sopra (incluso `RequireSuperAdmin`) e lascia fuori quelle solo-editor.

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/scripts/audit/__tests__/extract-api-endpoints.test.ts
import { describe, expect, it } from 'vitest';

import {
  parseGroupPrefixes,
  parseProgramPrefixes,
  parseRoutingFile,
} from '../extract-api-endpoints';

describe('parseProgramPrefixes', () => {
  it('associa il metodo di registrazione al prefisso dichiarato', () => {
    const source = `
      var v1Api = app.MapGroup("/api/v1");
      v1Api.MapGroup("/admin/catalog/seeds").MapAdminCatalogSeedEndpoints();
      v1Api.MapGameEndpoints();
    `;
    const prefixes = parseProgramPrefixes(source);
    expect(prefixes.get('MapAdminCatalogSeedEndpoints')).toBe('/api/v1/admin/catalog/seeds');
    expect(prefixes.get('MapGameEndpoints')).toBe('/api/v1');
  });
});

describe('parseGroupPrefixes', () => {
  it('risolve i prefissi annidati per variabile', () => {
    const source = `
      var group = app.MapGroup("/admin");
      var agentsGroup = group.MapGroup("/agents");
    `;
    const groups = parseGroupPrefixes(source);
    expect(groups.get('group')?.prefix).toBe('/admin');
    expect(groups.get('agentsGroup')?.prefix).toBe('/admin/agents');
  });

  it('propaga l\'autorizzazione del gruppo padre al figlio', () => {
    const source = `
      var group = app.MapGroup("/admin").RequireAuthorization("AdminOnlyPolicy");
      var sub = group.MapGroup("/agents");
    `;
    expect(parseGroupPrefixes(source).get('sub')?.auth).toBe('admin');
  });
});

describe('parseRoutingFile', () => {
  it('estrae metodo, path completo e stato di autorizzazione', () => {
    const source = `
        group.MapGet("/games", HandleGetAllGames)
        .AllowAnonymous()
        .WithTags("Games");

        group.MapPost("/games", HandleCreateGame)
        .RequireAuthorization("AdminOnlyPolicy")
        .WithTags("Games");
    `;
    const found = parseRoutingFile(source, 'Routing/GameEndpoints.cs', '/api/v1');

    expect(found).toHaveLength(2);
    expect(found[0]).toMatchObject({
      method: 'GET',
      path: '/api/v1/games',
      auth: 'anonymous',
      tags: ['Games'],
    });
    expect(found[1]).toMatchObject({ method: 'POST', path: '/api/v1/games', auth: 'admin' });
  });

  it('marca authenticated quando RequireAuthorization non nomina una policy admin', () => {
    const source = `group.MapDelete("/games/{id}", H).RequireAuthorization();`;
    expect(parseRoutingFile(source, 'f.cs', '/api/v1')[0]).toMatchObject({
      method: 'DELETE',
      path: '/api/v1/games/{id}',
      auth: 'authenticated',
    });
  });

  it('applica il prefisso dichiarato dentro il file', () => {
    const source = `
      var group = app.MapGroup("/admin/agent-definitions");
      group.MapGet("/", H).RequireAuthorization("AdminOnlyPolicy");
    `;
    expect(parseRoutingFile(source, 'f.cs', '/api/v1')[0].path).toBe(
      '/api/v1/admin/agent-definitions/'
    );
  });

  it('usa il prefisso del gruppo ricevente, non del primo dichiarato nel file', () => {
    // 95 file dichiarano un MapGroup e alcuni ne dichiarano due: applicare il
    // primo a tutti gli endpoint produce path plausibili ma sbagliati.
    const source = `
      var group = app.MapGroup("/admin");
      var agentsGroup = group.MapGroup("/agents");
      group.MapGet("/health", H);
      agentsGroup.MapGet("/metrics", H);
    `;
    const paths = parseRoutingFile(source, 'f.cs', '/api/v1').map(e => e.path);
    expect(paths).toEqual(['/api/v1/admin/health', '/api/v1/admin/agents/metrics']);
  });

  it('eredita l\'autorizzazione dal gruppo quando l\'endpoint non la dichiara', () => {
    const source = `
      var group = app.MapGroup("/admin").RequireAuthorization("AdminOnlyPolicy");
      group.MapGet("/users", H);
    `;
    expect(parseRoutingFile(source, 'f.cs', '/api/v1')[0].auth).toBe('admin');
  });

  it('lascia prevalere AllowAnonymous sull\'autorizzazione del gruppo', () => {
    const source = `
      var group = app.MapGroup("/x").RequireAuthorization("AdminOnlyPolicy");
      group.MapGet("/public", H).AllowAnonymous();
    `;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('anonymous');
  });

  it('marca unknown quando non c\'è alcun modificatore, né sull\'endpoint né sul gruppo', () => {
    expect(parseRoutingFile(`group.MapGet("/ping", H);`, 'f.cs', '')[0].auth).toBe('unknown');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/extract-api-endpoints.test.ts`
Expected: FAIL — modulo inesistente

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/scripts/audit/extract-api-endpoints.ts
import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';

import type { EndpointEntry } from './types';

const MAP_CALL = /(\w+)\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]*)"/g;
const GROUP_DECL = /var\s+(\w+)\s*=\s*(\w+)\.MapGroup\(\s*"([^"]+)"\s*\)/g;
const PROGRAM_GROUP = /(\w+)\.MapGroup\(\s*"([^"]+)"\s*\)\s*\.\s*(Map\w+)\(/g;
const PROGRAM_DIRECT = /(\w+)\.(Map\w+Endpoints|Map\w+Routes)\(\s*\)/g;
const ADMIN_POLICY = /admin/i;

export type GroupInfo = { prefix: string; auth: EndpointEntry['auth'] | null };

/** Deduce l'autorizzazione da una catena di modificatori. null = nessun modificatore. */
export function authFromChain(chain: string): EndpointEntry['auth'] | null {
  if (chain.includes('.AllowAnonymous()')) return 'anonymous';
  const policy = chain.match(/RequireAuthorization\(([^;]*?)\)\s*(?:\.|;)/)?.[1] ?? null;
  if (policy === null && !chain.includes('.RequireAuthorization(')) return null;
  return ADMIN_POLICY.test(policy ?? '') ? 'admin' : 'authenticated';
}

/** Mappa variabile di gruppo → prefisso risolto e autorizzazione ereditata. */
export function parseGroupPrefixes(source: string): Map<string, GroupInfo> {
  const groups = new Map<string, GroupInfo>();
  for (const m of source.matchAll(GROUP_DECL)) {
    const [, name, receiver, groupPath] = m;
    const index = m.index ?? 0;
    const decl = source.slice(index, source.indexOf(';', index) + 1);
    const parent = groups.get(receiver);
    groups.set(name, {
      prefix: `${parent?.prefix ?? ''}${groupPath}`,
      auth: authFromChain(decl) ?? parent?.auth ?? null,
    });
  }
  return groups;
}

/** Costruisce la mappa metodoDiRegistrazione → prefisso completo leggendo Program.cs. */
export function parseProgramPrefixes(source: string): Map<string, string> {
  const rootMatch = source.match(/var\s+(\w+)\s*=\s*app\.MapGroup\(\s*"([^"]+)"\s*\)/);
  const rootVar = rootMatch?.[1] ?? 'app';
  const rootPrefix = rootMatch?.[2] ?? '';
  const prefixes = new Map<string, string>();

  for (const m of source.matchAll(PROGRAM_GROUP)) {
    const [, varName, groupPath, method] = m;
    prefixes.set(method, `${varName === rootVar ? rootPrefix : ''}${groupPath}`);
  }
  for (const m of source.matchAll(PROGRAM_DIRECT)) {
    const [, varName, method] = m;
    if (!prefixes.has(method)) prefixes.set(method, varName === rootVar ? rootPrefix : '');
  }
  return prefixes;
}

/** Estrae gli endpoint di un file, risolvendo il prefisso per variabile ricevente. */
export function parseRoutingFile(
  source: string,
  file: string,
  groupPrefix: string
): EndpointEntry[] {
  const groups = parseGroupPrefixes(source);
  const found: EndpointEntry[] = [];

  for (const m of source.matchAll(MAP_CALL)) {
    const [, receiver, verb, routePath] = m;
    const index = m.index ?? 0;
    // La catena di modificatori termina al primo ';' dopo la chiamata Map.
    const chain = source.slice(index, source.indexOf(';', index) + 1);
    const group = groups.get(receiver);

    found.push({
      method: verb.toUpperCase(),
      path: `${groupPrefix}${group?.prefix ?? ''}${routePath}`.replace(/\/{2,}/g, '/'),
      // L'endpoint vince sul gruppo; se tace, eredita; se nessuno dei due parla,
      // il livello di protezione va letto a mano e finisce nelle note del tracker.
      auth: authFromChain(chain) ?? group?.auth ?? 'unknown',
      tags: [...chain.matchAll(/\.WithTags\(\s*"([^"]+)"/g)].map(t => t[1]),
      file,
      line: source.slice(0, index).split('\n').length,
    });
  }
  return found;
}

/** Percorre Routing/ applicando i prefissi dedotti da Program.cs. */
export function extractApiEndpoints(apiDir: string): EndpointEntry[] {
  const prefixes = parseProgramPrefixes(readFileSync(path.join(apiDir, 'Program.cs'), 'utf8'));
  const found: EndpointEntry[] = [];

  const walk = (dir: string): void => {
    for (const name of readdirSync(dir).sort()) {
      const full = path.join(dir, name);
      if (statSync(full).isDirectory()) {
        walk(full);
        continue;
      }
      if (!name.endsWith('.cs')) continue;
      const source = readFileSync(full, 'utf8');
      const registrar = source.match(/public static \w+ (Map\w+)\(/)?.[1] ?? '';
      const rel = path.relative(apiDir, full).split(path.sep).join('/');
      found.push(...parseRoutingFile(source, rel, prefixes.get(registrar) ?? '/api/v1'));
    }
  };

  walk(path.join(apiDir, 'Routing'));
  return found.sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/extract-api-endpoints.test.ts`
Expected: PASS — 5 test

- [ ] **Step 5: Verifica sul repo reale e registrazione dello scarto**

Run:
```bash
cd apps/web && pnpm tsx -e "import('./scripts/audit/extract-api-endpoints').then(mod => { const m = mod.default ?? mod; const e = m.extractApiEndpoints('../api/src/Api'); console.log('endpoint:', e.length); console.log('auth:', JSON.stringify(e.reduce((a,x)=>{a[x.auth]=(a[x.auth]||0)+1;return a;},{}))); });"
```

**Esito misurato il 2026-08-26**: `endpoint: 1381` · `authenticated 628 · admin 620 · anonymous 39 · unknown 94`.

I 94 `unknown` sono la lista di lavoro dell'ondata 1: endpoint il cui livello di protezione il parser non deduce e che vanno letti a mano. Non è un difetto da nascondere.

Arrivarci ha richiesto quattro correzioni, tutte scoperte confrontando il parser col codice vero — e ognuna, presa da sola, avrebbe lasciato un audit che sembrava completo:

| Correzione | unknown residui |
|---|---|
| Parser iniziale (solo policy ASP.NET) | 1117 su 1381 (81%) |
| + filtri custom `.RequireAdminSession()` e affini (890 usi) | 739 |
| + `statementFrom`: la catena non si ferma al primo `;`, che negli handler inline cade **dentro** il lambda | 427 |
| + `AddEndpointFilter<XFilter>` sul gruppo e auth imperativa negli handler separati (`methodBody`) | **94** |

La lezione, valida per le ondate: quando un'euristica classifica l'80% dei casi come "non so", il difetto è nell'euristica, non nel codice esaminato.

- [ ] **Step 6: Commit**

```bash
git add apps/web/scripts/audit/extract-api-endpoints.ts apps/web/scripts/audit/__tests__/extract-api-endpoints.test.ts
git commit -m "chore(audit): estrattore degli endpoint API"
```

---

### Task 3: Mappa dei contesti e generatore di inventario

**Files:**
- Create: `apps/web/scripts/audit/context-map.ts`
- Create: `apps/web/scripts/audit/build-inventory.ts`
- Test: `apps/web/scripts/audit/__tests__/build-inventory.test.ts`

**Interfaces:**
- Consumes: `RouteEntry`, `EndpointEntry`, `InventoryRow` da `./types`; `extractFeRoutes`; `extractApiEndpoints`
- Produces: `contextForRoute(route: string): string` · `contextForEndpoint(e: EndpointEntry): string` · `buildInventory(routes: RouteEntry[], endpoints: EndpointEntry[]): InventoryRow[]` · `toCsv(rows: InventoryRow[]): string`

**Regole di assegnazione** (dallo spec, sezione Ondate):

- Una rotta genera **due righe** (ruolo `user` e ruolo `admin`), tranne le rotte sotto `/admin` che generano solo la riga `admin`.
- Livello iniziale: `L1` per le rotte; per gli endpoint `L2` se il metodo è POST/PUT/PATCH/DELETE, `L1` altrimenti. Il livello `L3` si assegna a mano nel CSV per le funzioni critiche elencate nello spec (auth, upload→indicizzazione, chat RAG, scoring live, quota).
- Stato iniziale: `⬜ non coperto`, tranne endpoint con `auth: 'admin'` raggiunti da nessuna rotta `/admin`, che restano `⬜` ma con nota `verificare raggiungibilità`.

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/scripts/audit/__tests__/build-inventory.test.ts
import { describe, expect, it } from 'vitest';

import { buildInventory, toCsv } from '../build-inventory';
import { contextForEndpoint, contextForRoute } from '../context-map';
import type { EndpointEntry, RouteEntry } from '../types';

const route = (route: string): RouteEntry => ({ route, group: 'x', dynamicSegments: [], file: 'f' });
const endpoint = (over: Partial<EndpointEntry> = {}): EndpointEntry => ({
  method: 'GET',
  path: '/api/v1/games',
  auth: 'anonymous',
  tags: [],
  file: 'Routing/GameEndpoints.cs',
  line: 1,
  ...over,
});

describe('contextForRoute', () => {
  it('mappa i prefissi noti sui bounded context', () => {
    expect(contextForRoute('/library')).toBe('UserLibrary');
    expect(contextForRoute('/library/[gameId]/kb')).toBe('DocumentProcessing');
    expect(contextForRoute('/chat/[threadId]')).toBe('KnowledgeBase');
  });

  it('usa Unmapped per i prefissi sconosciuti, invece di indovinare', () => {
    expect(contextForRoute('/qualcosa-di-nuovo')).toBe('Unmapped');
  });
});

describe('contextForEndpoint', () => {
  it('preferisce la cartella del file di routing quando presente', () => {
    expect(contextForEndpoint(endpoint({ file: 'Routing/SessionTracking/X.cs' }))).toBe(
      'SessionTracking'
    );
  });

  it('ricade sul path quando il file è nella root di Routing', () => {
    expect(contextForEndpoint(endpoint({ file: 'Routing/GameEndpoints.cs' }))).toBe(
      'GameManagement'
    );
  });
});

describe('buildInventory', () => {
  it('genera due righe per rotta non-admin e una per rotta admin', () => {
    const rows = buildInventory([route('/library'), route('/admin/users')], []);
    expect(rows.filter((r) => r.path === '/library').map((r) => r.ruolo)).toEqual([
      'admin',
      'user',
    ]);
    expect(rows.filter((r) => r.path === '/admin/users').map((r) => r.ruolo)).toEqual(['admin']);
  });

  it('assegna L2 alle mutazioni e L1 alle letture', () => {
    const rows = buildInventory([], [endpoint({ method: 'POST' }), endpoint({ method: 'GET' })]);
    expect(rows.map((r) => r.livello).sort()).toEqual(['L1', 'L2']);
  });

  it('parte da stato non coperto', () => {
    expect(buildInventory([route('/library')], [])[0].stato).toBe('⬜ non coperto');
  });

  it('produce id stabili e univoci', () => {
    const rows = buildInventory([route('/library'), route('/games')], [endpoint()]);
    expect(new Set(rows.map((r) => r.id)).size).toBe(rows.length);
    expect(buildInventory([route('/library')], [])[0].id).toBe(
      buildInventory([route('/library')], [])[0].id
    );
  });
});

describe('toCsv', () => {
  it('protegge le virgole nelle note', () => {
    const rows = buildInventory([route('/library')], []);
    rows[0].note = 'nota, con virgola';
    expect(toCsv(rows)).toContain('"nota, con virgola"');
  });

  it('emette l\'intestazione attesa', () => {
    expect(toCsv([]).trim()).toBe(
      'id,tipo,path,metodo,contesto,ruolo,livello,stato,evidenza,note'
    );
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/build-inventory.test.ts`
Expected: FAIL — moduli inesistenti

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/scripts/audit/context-map.ts
import type { EndpointEntry } from './types';

/** Prefissi rotta → bounded context. L'ordine conta: il primo match più lungo vince. */
const ROUTE_CONTEXTS: Array<[string, string]> = [
  ['/library/[gameId]/kb', 'DocumentProcessing'],
  ['/library', 'UserLibrary'],
  ['/chat', 'KnowledgeBase'],
  ['/agents', 'KnowledgeBase'],
  ['/games', 'GameManagement'],
  ['/shared-games', 'SharedGameCatalog'],
  ['/sessions', 'SessionTracking'],
  ['/game-nights', 'SessionTracking'],
  ['/toolkit', 'GameToolkit'],
  ['/toolbox', 'GameToolbox'],
  ['/achievements', 'Gamification'],
  ['/notifications', 'UserNotifications'],
  ['/login', 'Authentication'],
  ['/register', 'Authentication'],
  ['/profile', 'Authentication'],
  ['/admin/users', 'Administration'],
  ['/admin/config', 'SystemConfiguration'],
  ['/admin/audit', 'SecurityAudit'],
  ['/admin', 'Administration'],
  ['/', 'GameManagement'],
];

/** Cartelle sotto Routing/ che nominano già il proprio contesto. */
const FILE_CONTEXTS = new Set([
  'Administration',
  'AgentMemory',
  'Authentication',
  'BusinessSimulations',
  'DatabaseSync',
  'DocumentProcessing',
  'EntityRelationships',
  'GameManagement',
  'GameToolbox',
  'GameToolkit',
  'Gamification',
  'KbQuality',
  'KnowledgeBase',
  'SecurityAudit',
  'SessionTracking',
  'SharedGameCatalog',
  'SystemConfiguration',
  'Testing',
  'UserLibrary',
  'UserNotifications',
]);

export function contextForRoute(route: string): string {
  const match = ROUTE_CONTEXTS.filter(([prefix]) => route === prefix || route.startsWith(`${prefix}/`))
    .sort((a, b) => b[0].length - a[0].length)[0];
  return match?.[1] ?? 'Unmapped';
}

export function contextForEndpoint(e: EndpointEntry): string {
  const folder = e.file.split('/')[1];
  if (folder && FILE_CONTEXTS.has(folder)) return folder;
  return contextForRoute(e.path.replace(/^\/api\/v1/, '') || '/');
}
```

```ts
// apps/web/scripts/audit/build-inventory.ts
import { createHash } from 'node:crypto';

import { contextForEndpoint, contextForRoute } from './context-map';
import type { EndpointEntry, InventoryRow, RouteEntry } from './types';

const MUTATIONS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);
const HEADER = 'id,tipo,path,metodo,contesto,ruolo,livello,stato,evidenza,note';

const idFor = (parts: string[]): string =>
  createHash('sha1').update(parts.join('|')).digest('hex').slice(0, 8);

export function buildInventory(
  routes: RouteEntry[],
  endpoints: EndpointEntry[]
): InventoryRow[] {
  const rows: InventoryRow[] = [];

  for (const r of routes) {
    const roles: Array<'user' | 'admin'> = r.route.startsWith('/admin')
      ? ['admin']
      : ['admin', 'user'];
    for (const ruolo of roles) {
      rows.push({
        id: idFor(['route', r.route, ruolo]),
        tipo: 'route',
        path: r.route,
        metodo: 'GET',
        contesto: contextForRoute(r.route),
        ruolo,
        livello: 'L1',
        stato: '⬜ non coperto',
        evidenza: '',
        note: r.dynamicSegments.length ? `param: ${r.dynamicSegments.join(' ')}` : '',
      });
    }
  }

  for (const e of endpoints) {
    rows.push({
      id: idFor(['endpoint', e.method, e.path]),
      tipo: 'endpoint',
      path: e.path,
      metodo: e.method,
      contesto: contextForEndpoint(e),
      ruolo: e.auth === 'admin' ? 'admin' : 'user',
      livello: MUTATIONS.has(e.method) ? 'L2' : 'L1',
      stato: '⬜ non coperto',
      evidenza: '',
      note: e.auth === 'unknown' ? 'auth non dedotta: leggere il codice' : '',
    });
  }

  return rows.sort(
    (a, b) =>
      a.contesto.localeCompare(b.contesto) ||
      a.tipo.localeCompare(b.tipo) ||
      a.path.localeCompare(b.path) ||
      a.metodo.localeCompare(b.metodo) ||
      a.ruolo.localeCompare(b.ruolo)
  );
}

const cell = (value: string): string =>
  /[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;

export function toCsv(rows: InventoryRow[]): string {
  const body = rows.map((r) =>
    [r.id, r.tipo, r.path, r.metodo, r.contesto, r.ruolo, r.livello, r.stato, r.evidenza, r.note]
      .map(cell)
      .join(',')
  );
  return [HEADER, ...body].join('\n') + '\n';
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/build-inventory.test.ts`
Expected: PASS — 9 test

- [ ] **Step 5: Genera l'inventario reale**

Crea `apps/web/scripts/audit/main-inventory.ts`:

```ts
// apps/web/scripts/audit/main-inventory.ts
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { buildInventory, toCsv } from './build-inventory';
import { extractApiEndpoints } from './extract-api-endpoints';
import { extractFeRoutes } from './extract-fe-routes';

const OUT_DIR = path.resolve(
  '../../docs/for-developers/audits/2026-08-26-full-feature-audit'
);

const routes = extractFeRoutes('src/app');
const endpoints = extractApiEndpoints('../api/src/Api');
const rows = buildInventory(routes, endpoints);

mkdirSync(OUT_DIR, { recursive: true });
writeFileSync(path.join(OUT_DIR, 'inventory.csv'), toCsv(rows), 'utf8');

const unmapped = rows.filter((r) => r.contesto === 'Unmapped').length;
console.log(`rotte: ${routes.length} · endpoint: ${endpoints.length} · righe: ${rows.length}`);
console.log(`Unmapped: ${unmapped} (vanno mappati a mano in context-map.ts)`);
```

Run: `cd apps/web && pnpm tsx scripts/audit/main-inventory.ts`
Expected: il CSV esiste e `Unmapped` è un numero piccolo. **Se `Unmapped` supera il 10% delle righe, aggiungi i prefissi mancanti e rigenera**: un inventario per un quinto non classificato non permette di lavorare per ondate.

**Esito misurato il 2026-08-26**: `rotte: 220 · endpoint: 1381 · righe: 1725` · `Unmapped: 17 (1.0%)`.

La prima passata dava `Unmapped: 419 (24.3%)`: la superficie API non ricalca quella delle pagine (esistono famiglie di endpoint senza alcuna pagina — `agent-memory`, `achievements`, `emails`), quindi serve una `API_CONTEXTS` separata da `ROUTE_CONTEXTS`.

I 17 residui **coincidono** con i path anomali del Task 2: l'inventario indica da sé dove guardare.

Distribuzione risultante:

| Contesto | Righe | | Contesto | Righe |
|---|---|---|---|---|
| Administration | 520 | | SystemConfiguration | 24 |
| SessionTracking | 316 | | GameToolbox | 19 |
| SharedGameCatalog | 161 | | Unmapped | 17 |
| KnowledgeBase | 160 | | AgentMemory | 14 |
| GameManagement | 131 | | DatabaseSync | 11 |
| UserLibrary | 91 | | Testing | 9 |
| Authentication | 67 | | KbQuality | 6 |
| GameToolkit | 55 | | DesignSystem | 4 |
| DocumentProcessing | 55 | | BusinessSimulations | 4 |
| PublicPages | 31 | | Gamification | 2 |
| UserNotifications | 28 | | EntityRelationships · SecurityAudit | **0** |

- [ ] **Step 6: Verifica il determinismo**

Run:
```bash
cd apps/web && pnpm tsx scripts/audit/main-inventory.ts && cp ../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv /tmp/inv1.csv && pnpm tsx scripts/audit/main-inventory.ts && diff /tmp/inv1.csv ../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv && echo DETERMINISTICO
```
Expected: `DETERMINISTICO`

- [ ] **Step 7: Commit**

```bash
git add apps/web/scripts/audit/ docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv
git commit -m "chore(audit): inventario di rotte ed endpoint per contesto"
```

---

### Task 4: Risolutore dei parametri dinamici

**Files:**
- Create: `apps/web/scripts/audit/resolve-params.ts`
- Test: `apps/web/scripts/audit/__tests__/resolve-params.test.ts`

**Interfaces:**
- Consumes: niente
- Produces: `type SqlRunner = (sql: string) => string` · `PARAM_QUERIES: Record<string, string>` · `resolveParams(run: SqlRunner): Record<string, string>` · `psqlRunner(container?: string): SqlRunner`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/scripts/audit/__tests__/resolve-params.test.ts
import { describe, expect, it, vi } from 'vitest';

import { PARAM_QUERIES, resolveParams } from '../resolve-params';

describe('resolveParams', () => {
  it('restituisce un valore per ogni parametro noto', () => {
    const run = vi.fn().mockReturnValue('11111111-2222-3333-4444-555555555555\n');
    const params = resolveParams(run);

    expect(Object.keys(params).sort()).toEqual(Object.keys(PARAM_QUERIES).sort());
    expect(params.gameId).toBe('11111111-2222-3333-4444-555555555555');
    expect(run).toHaveBeenCalledTimes(Object.keys(PARAM_QUERIES).length);
  });

  it('omette il parametro quando la query non restituisce righe', () => {
    const params = resolveParams(() => '\n');
    expect(params.gameId).toBeUndefined();
  });

  it('omette il parametro quando la query fallisce, senza interrompere gli altri', () => {
    const run = vi.fn((sql: string) => {
      if (sql.includes('games')) throw new Error('relation does not exist');
      return 'ok-value\n';
    });
    const params = resolveParams(run);

    expect(params.gameId).toBeUndefined();
    expect(Object.keys(params).length).toBeGreaterThan(0);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/resolve-params.test.ts`
Expected: FAIL — modulo inesistente

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/scripts/audit/resolve-params.ts
import { execFileSync } from 'node:child_process';

export type SqlRunner = (sql: string) => string;

/** Un id reale per ogni segmento dinamico che compare nelle rotte. */
export const PARAM_QUERIES: Record<string, string> = {
  gameId: 'SELECT id FROM games WHERE is_deleted = false LIMIT 1',
  threadId: 'SELECT id FROM chat_threads LIMIT 1',
  sessionId: 'SELECT id FROM game_sessions LIMIT 1',
  userId: 'SELECT id FROM users LIMIT 1',
  agentId: 'SELECT id FROM agents LIMIT 1',
};

/** Esegue le query e raccoglie i valori. Un fallimento singolo non ferma gli altri. */
export function resolveParams(run: SqlRunner): Record<string, string> {
  const params: Record<string, string> = {};
  for (const [name, sql] of Object.entries(PARAM_QUERIES)) {
    try {
      const value = run(sql).trim();
      if (value) params[name] = value;
    } catch {
      // Tabella assente o DB non pronto: il parametro resta non risolto e il
      // crawler salterà le rotte che lo richiedono, segnalandole nel report.
    }
  }
  return params;
}

/** Runner reale: psql dentro il container Postgres dello stack locale. */
export function psqlRunner(container = 'meepleai-postgres'): SqlRunner {
  return (sql) =>
    execFileSync(
      'docker',
      ['exec', container, 'psql', '-U', 'meepleai', '-d', 'meepleai', '-t', '-A', '-c', sql],
      { encoding: 'utf8' }
    );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/resolve-params.test.ts`
Expected: PASS — 3 test

- [ ] **Step 5: Verifica contro il DB reale**

Prerequisito: stack avviato (`cd infra && make dev-from-snapshot`).

Run:
```bash
cd apps/web && pnpm tsx -e "import('./scripts/audit/resolve-params').then(m => { const p = m.resolveParams(m.psqlRunner()); console.log(p); require('fs').writeFileSync('e2e/audit/route-params.json', JSON.stringify(p, null, 2)); })"
```
Expected: un oggetto con almeno `gameId` risolto. **Se una tabella ha un nome diverso da quello ipotizzato in `PARAM_QUERIES`, la query fallisce in silenzio per costruzione**: controlla i nomi reali con `docker exec meepleai-postgres psql -U meepleai -d meepleai -c '\dt'` e correggi le query prima di proseguire — un parametro non risolto significa rotte non visitate.

- [ ] **Step 6: Commit**

```bash
git add apps/web/scripts/audit/resolve-params.ts apps/web/scripts/audit/__tests__/resolve-params.test.ts apps/web/e2e/audit/route-params.json
git commit -m "chore(audit): risolutore dei parametri dinamici dal DB"
```

---

### Task 5: Collettore di evidenze log e DB

**Files:**
- Create: `apps/web/scripts/audit/collect-evidence.ts`
- Test: `apps/web/scripts/audit/__tests__/collect-evidence.test.ts`

**Interfaces:**
- Consumes: `SqlRunner` da `./resolve-params`
- Produces: `type TableCounts = Record<string, number>` · `type Evidence = { errors: string[]; changedTables: Array<{ table: string; delta: number }> }` · `parseTableCounts(psqlOutput: string): TableCounts` · `diffCounts(before: TableCounts, after: TableCounts): Evidence['changedTables']` · `filterErrorLines(logOutput: string): string[]` · `snapshotCounts(run: SqlRunner): TableCounts` · `readErrorLogs(sinceIso: string, container?: string): string[]`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/scripts/audit/__tests__/collect-evidence.test.ts
import { describe, expect, it } from 'vitest';

import { diffCounts, filterErrorLines, parseTableCounts } from '../collect-evidence';

describe('parseTableCounts', () => {
  it('legge l\'output tabellare di psql -t -A', () => {
    expect(parseTableCounts('games|42\nusers|7\n')).toEqual({ games: 42, users: 7 });
  });

  it('ignora le righe vuote', () => {
    expect(parseTableCounts('games|42\n\n')).toEqual({ games: 42 });
  });
});

describe('diffCounts', () => {
  it('riporta solo le tabelle cambiate, ordinate per delta decrescente', () => {
    expect(
      diffCounts({ games: 1, sessions: 5, users: 3 }, { games: 2, sessions: 9, users: 3 })
    ).toEqual([
      { table: 'sessions', delta: 4 },
      { table: 'games', delta: 1 },
    ]);
  });

  it('riporta le tabelle comparse dopo l\'azione', () => {
    expect(diffCounts({}, { outbox: 2 })).toEqual([{ table: 'outbox', delta: 2 }]);
  });

  it('riporta le cancellazioni come delta negativo', () => {
    expect(diffCounts({ games: 5 }, { games: 3 })).toEqual([{ table: 'games', delta: -2 }]);
  });
});

describe('filterErrorLines', () => {
  it('tiene solo Error e Fatal', () => {
    const log = [
      '[12:00:00 INF] richiesta servita',
      '[12:00:01 ERR] NullReferenceException in GameHandler',
      '[12:00:02 WRN] cache miss',
      '[12:00:03 FTL] host terminato',
    ].join('\n');

    expect(filterErrorLines(log)).toEqual([
      '[12:00:01 ERR] NullReferenceException in GameHandler',
      '[12:00:03 FTL] host terminato',
    ]);
  });

  it('riconosce anche il formato con livello esteso', () => {
    expect(filterErrorLines('level=Error msg="boom"')).toEqual(['level=Error msg="boom"']);
  });

  it('restituisce lista vuota su log pulito', () => {
    expect(filterErrorLines('[12:00:00 INF] tutto bene')).toEqual([]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/collect-evidence.test.ts`
Expected: FAIL — modulo inesistente

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/scripts/audit/collect-evidence.ts
import { execFileSync } from 'node:child_process';

import type { SqlRunner } from './resolve-params';

export type TableCounts = Record<string, number>;
export type Evidence = {
  errors: string[];
  changedTables: Array<{ table: string; delta: number }>;
};

const COUNTS_SQL =
  "SELECT relname || '|' || n_live_tup FROM pg_stat_user_tables ORDER BY relname";
const ERROR_LINE = /\b(ERR|FTL|ERROR|FATAL)\b|level=(Error|Fatal)/;

export function parseTableCounts(psqlOutput: string): TableCounts {
  const counts: TableCounts = {};
  for (const line of psqlOutput.split('\n')) {
    const [table, value] = line.trim().split('|');
    if (table && value !== undefined) counts[table] = Number(value);
  }
  return counts;
}

export function diffCounts(
  before: TableCounts,
  after: TableCounts
): Evidence['changedTables'] {
  return Object.keys({ ...before, ...after })
    .map((table) => ({ table, delta: (after[table] ?? 0) - (before[table] ?? 0) }))
    .filter((d) => d.delta !== 0)
    .sort((a, b) => Math.abs(b.delta) - Math.abs(a.delta) || a.table.localeCompare(b.table));
}

export function filterErrorLines(logOutput: string): string[] {
  return logOutput.split('\n').filter((line) => ERROR_LINE.test(line));
}

export function snapshotCounts(run: SqlRunner): TableCounts {
  return parseTableCounts(run(COUNTS_SQL));
}

/** Righe di errore emesse dall'API dal marker in poi. */
export function readErrorLogs(sinceIso: string, container = 'meepleai-api'): string[] {
  const out = execFileSync('docker', ['logs', container, '--since', sinceIso], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  return filterErrorLines(out);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/collect-evidence.test.ts`
Expected: PASS — 8 test

- [ ] **Step 5: Verifica contro lo stack reale**

Run:
```bash
cd apps/web && pnpm tsx -e "import('./scripts/audit/collect-evidence').then(async m => { const { psqlRunner } = await import('./scripts/audit/resolve-params'); const c = m.snapshotCounts(psqlRunner()); console.log('tabelle:', Object.keys(c).length); console.log('errori ultimi 5m:', m.readErrorLogs('5m').length); })"
```
Expected: un numero di tabelle plausibile (decine) e un conteggio di errori. **Se `docker logs` esce con codice diverso da zero perché il container non esiste, il messaggio deve dirlo chiaramente** — non silenziare l'errore qui: un collettore che restituisce sempre zero errori renderebbe verde l'intero audit.

- [ ] **Step 6: Commit**

```bash
git add apps/web/scripts/audit/collect-evidence.ts apps/web/scripts/audit/__tests__/collect-evidence.test.ts
git commit -m "chore(audit): collettore di evidenze log e DB"
```

---

### Task 6: Config Playwright dell'audit e login reale

**Files:**
- Create: `apps/web/playwright.audit.config.ts`
- Create: `apps/web/e2e/audit/auth-setup.ts`
- Test: la verifica è l'esecuzione stessa (produce gli storageState)

**Interfaces:**
- Consumes: `authenticateViaAPI` NON viene usata — il login deve passare dalla UI
- Produces: `e2e/audit/.auth/user.json` e `e2e/audit/.auth/admin.json` (storageState per ruolo), progetto Playwright `audit-user` e `audit-admin`

**Credenziali**: da `infra/secrets/admin.secret` (vedi memoria di progetto e `admin-login-real.spec.ts`), esposte come `AUDIT_ADMIN_EMAIL` / `AUDIT_ADMIN_PASSWORD` / `AUDIT_USER_EMAIL` / `AUDIT_USER_PASSWORD`. L'utente non-admin va creato in ondata 1 con la registrazione reale — è il primo elemento dell'audit, non un prerequisito da aggirare con un seed.

- [ ] **Step 1: Scrivi la config**

```ts
// apps/web/playwright.audit.config.ts
/**
 * Config dedicata all'audit esaustivo (ondata 0+).
 * Differenza sostanziale da playwright.config.ts: NIENTE PLAYWRIGHT_AUTH_BYPASS.
 * Il login è reale, contro il backend dello stack locale.
 */
import path from 'node:path';

import { defineConfig, devices } from '@playwright/test';

const AUTH_DIR = path.join(__dirname, 'e2e/audit/.auth');

export default defineConfig({
  testDir: './e2e/audit',
  timeout: 90_000,
  retries: 0, // un audit non deve nascondere l'intermittenza dietro un retry
  workers: 1, // l'ordine e la correlazione con i log richiedono serialità
  reporter: [['list'], ['json', { outputFile: 'audit-results/crawl.json' }]],
  use: {
    baseURL: process.env.AUDIT_BASE_URL || 'http://localhost:3000',
    trace: 'retain-on-failure',
    screenshot: 'on',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [
    { name: 'setup', testMatch: /auth-setup\.ts/ },
    {
      name: 'audit-user',
      dependencies: ['setup'],
      testMatch: /crawl\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], storageState: path.join(AUTH_DIR, 'user.json') },
    },
    {
      name: 'audit-admin',
      dependencies: ['setup'],
      testMatch: /crawl\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], storageState: path.join(AUTH_DIR, 'admin.json') },
    },
  ],
});
```

- [ ] **Step 2: Scrivi il setup di autenticazione**

```ts
// apps/web/e2e/audit/auth-setup.ts
import path from 'node:path';

import { expect, test } from '@playwright/test';

const AUTH_DIR = path.join(__dirname, '.auth');

const credentials = {
  user: {
    email: process.env.AUDIT_USER_EMAIL,
    password: process.env.AUDIT_USER_PASSWORD,
    file: path.join(AUTH_DIR, 'user.json'),
  },
  admin: {
    email: process.env.AUDIT_ADMIN_EMAIL,
    password: process.env.AUDIT_ADMIN_PASSWORD,
    file: path.join(AUTH_DIR, 'admin.json'),
  },
};

for (const [role, creds] of Object.entries(credentials)) {
  test(`login reale come ${role}`, async ({ page }) => {
    expect(
      creds.email && creds.password,
      `AUDIT_${role.toUpperCase()}_EMAIL e AUDIT_${role.toUpperCase()}_PASSWORD devono essere impostate`
    ).toBeTruthy();

    await page.goto('/login');
    await page.getByLabel(/email/i).fill(creds.email as string);
    await page.getByLabel(/password/i).fill(creds.password as string);
    await page.getByRole('button', { name: /accedi|log ?in/i }).click();

    // Il login è riuscito solo se lasciamo /login: un errore in-page lascia l'URL invariato.
    await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
    await page.context().storageState({ path: creds.file });
  });
}
```

- [ ] **Step 3: Esegui il setup**

Prerequisiti: stack avviato, `apps/web` in esecuzione su :3000, variabili `AUDIT_*` esportate.

Run: `cd apps/web && pnpm exec playwright test --config=playwright.audit.config.ts --project=setup`
Expected: 2 test PASS, i file `e2e/audit/.auth/user.json` e `admin.json` esistono.

Se il login admin fallisce, **fermati e diagnostica**: è il primo finding potenziale dell'audit (l'ondata 1 copre proprio l'autenticazione). Non aggirarlo con `authenticateViaAPI`.

- [ ] **Step 4: Escludi i segreti dal versionamento**

```bash
cd /d/Repositories/meepleai-monorepo-main
printf 'e2e/audit/.auth/\napps/web/audit-results/\n' >> .gitignore
git check-ignore apps/web/e2e/audit/.auth/user.json && echo IGNORATO
```
Expected: `IGNORATO` — gli storageState contengono cookie di sessione validi e non vanno committati.

- [ ] **Step 5: Commit**

```bash
git add apps/web/playwright.audit.config.ts apps/web/e2e/audit/auth-setup.ts .gitignore
git commit -m "chore(audit): config Playwright con login reale per ruolo"
```

---

### Task 7: Crawler esplorativo

**Files:**
- Create: `apps/web/e2e/audit/crawl.spec.ts`
- Test: l'esecuzione stessa; il risultato è `audit-results/crawl.json`

**Interfaces:**
- Consumes: `inventory.csv` (Task 3), `route-params.json` (Task 4), storageState (Task 6)
- Produces: `audit-results/crawl.json` con, per ogni rotta visitata: `{ id, route, role, status, consoleErrors, failedRequests, bodyMarkers, screenshot }`

- [ ] **Step 1: Scrivi il crawler**

```ts
// apps/web/e2e/audit/crawl.spec.ts
import { appendFileSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

import { test } from '@playwright/test';

const INVENTORY = path.join(
  __dirname,
  '../../../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv'
);
const PARAMS = path.join(__dirname, 'route-params.json');
const RESULTS_DIR = path.join(__dirname, '../../audit-results');

/** Marker testuali che indicano un guasto anche senza errori tecnici. */
const FAILURE_MARKERS = [
  /qualcosa è andato storto/i,
  /something went wrong/i,
  /errore imprevisto/i,
  /application error/i,
  /404/,
];

type Row = { id: string; tipo: string; path: string; ruolo: string; note: string };

function readRoutes(role: string): Row[] {
  const [, ...lines] = readFileSync(INVENTORY, 'utf8').trim().split('\n');
  return lines
    .map((line) => line.split(',')) // le rotte non contengono virgole; le note quotate stanno in coda
    .map((c) => ({ id: c[0], tipo: c[1], path: c[2], ruolo: c[5], note: c[9] ?? '' }))
    .filter((r) => r.tipo === 'route' && r.ruolo === role);
}

/** Sostituisce [param] con un id reale; ritorna null se manca il valore. */
function fillParams(route: string, params: Record<string, string>): string | null {
  const filled = route.replace(/\[(\.\.\.)?(\w+)\]/g, (_, __, name) => params[name] ?? ' ');
  return filled.includes(' ') ? null : filled;
}

const role = process.env.AUDIT_ROLE ?? 'user';
const params = JSON.parse(readFileSync(PARAMS, 'utf8')) as Record<string, string>;

for (const row of readRoutes(role)) {
  test(`[${role}] ${row.path}`, async ({ page }, testInfo) => {
    const url = fillParams(row.path, params);
    test.skip(url === null, `parametro non risolto per ${row.path}`);

    const consoleErrors: string[] = [];
    const failedRequests: string[] = [];

    page.on('console', (msg) => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });
    page.on('response', (res) => {
      if (res.status() >= 400) failedRequests.push(`${res.status()} ${res.url()}`);
    });

    const response = await page.goto(url as string, { waitUntil: 'networkidle' });
    const body = await page.locator('body').innerText();
    const bodyMarkers = FAILURE_MARKERS.filter((m) => m.test(body)).map(String);

    const shotPath = path.join(RESULTS_DIR, 'shots', `${row.id}-${role}.png`);
    mkdirSync(path.dirname(shotPath), { recursive: true });
    await page.screenshot({ path: shotPath, fullPage: true });

    // Scrittura diretta su JSONL: il reporter JSON di Playwright salva gli
    // attachment come riferimenti su disco, non inline, quindi non è una
    // sorgente affidabile per il report. Con workers=1 l'append è sicuro.
    appendFileSync(
      path.join(RESULTS_DIR, 'entries.jsonl'),
      JSON.stringify({
        id: row.id,
        route: row.path,
        url,
        role,
        status: response?.status() ?? 0,
        consoleErrors,
        failedRequests,
        bodyMarkers,
        screenshot: path.relative(RESULTS_DIR, shotPath),
      }) + '\n',
      'utf8'
    );
    await testInfo.attach('screenshot', { path: shotPath, contentType: 'image/png' });

    // Il crawler NON asserisce: esplora e riporta. Un test rosso qui significherebbe
    // interrompere la passata al primo difetto, che è l'opposto di ciò che serve.
  });
}
```

- [ ] **Step 2: Esegui la passata come utente**

Run: `cd apps/web && AUDIT_ROLE=user pnpm exec playwright test --config=playwright.audit.config.ts --project=audit-user`
Expected: un test per rotta, tutti verdi (il crawler non asserisce), `audit-results/crawl.json` popolato.

- [ ] **Step 3: Esegui la passata come admin**

Run: `cd apps/web && AUDIT_ROLE=admin pnpm exec playwright test --config=playwright.audit.config.ts --project=audit-admin`
Expected: come sopra, sulle rotte con ruolo `admin`.

- [ ] **Step 4: Controlla la copertura effettiva della passata**

Le rotte saltate per parametro non risolto non finiscono nel JSONL: si contano per differenza.

Run:
```bash
cd apps/web && node -e "
const fs=require('fs');
const csv=fs.readFileSync('../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv','utf8').trim().split('\n').slice(1);
const attese=csv.map(l=>l.split(',')).filter(c=>c[1]==='route').length;
const visitate=fs.readFileSync('audit-results/entries.jsonl','utf8').trim().split('\n').filter(Boolean).length;
console.log('rotte in inventario:', attese, '| visitate:', visitate, '| mancanti:', attese-visitate);
"
```
Expected: `mancanti: 0`. **Ogni mancante è una rotta non verificata**: torna al Task 4, aggiungi la query assente a `PARAM_QUERIES` e rilancia. Uno skip lasciato lì diventa un buco silenzioso nella copertura, indistinguibile da una rotta sana.

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/audit/crawl.spec.ts
git commit -m "chore(audit): crawler esplorativo delle rotte"
```

---

### Task 8: Report e aggiornamento del tracker

**Files:**
- Create: `apps/web/scripts/audit/render-report.ts`
- Test: `apps/web/scripts/audit/__tests__/render-report.test.ts`

**Interfaces:**
- Consumes: `audit-results/crawl.json`, `inventory.csv`
- Produces: `classify(entry): 'ok' | 'sospetto' | 'rotto'` · `renderMarkdown(entries): string` · `applyStatuses(csv: string, entries): string`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/scripts/audit/__tests__/render-report.test.ts
import { describe, expect, it } from 'vitest';

import { applyStatuses, classify, renderMarkdown } from '../render-report';

const entry = (over = {}) => ({
  id: 'abc12345',
  route: '/library',
  role: 'user',
  status: 200,
  consoleErrors: [] as string[],
  failedRequests: [] as string[],
  bodyMarkers: [] as string[],
  ...over,
});

describe('classify', () => {
  it('ok quando tutto è pulito', () => {
    expect(classify(entry())).toBe('ok');
  });

  it('rotto quando la navigazione non è 2xx/3xx', () => {
    expect(classify(entry({ status: 500 }))).toBe('rotto');
  });

  it('rotto quando il corpo mostra un marker di guasto', () => {
    expect(classify(entry({ bodyMarkers: ['/404/'] }))).toBe('rotto');
  });

  it('sospetto quando ci sono errori di console ma la pagina risponde', () => {
    expect(classify(entry({ consoleErrors: ['TypeError'] }))).toBe('sospetto');
  });

  it('sospetto quando una richiesta secondaria fallisce', () => {
    expect(classify(entry({ failedRequests: ['500 /api/v1/games'] }))).toBe('sospetto');
  });
});

describe('applyStatuses', () => {
  const csv =
    'id,tipo,path,metodo,contesto,ruolo,livello,stato,evidenza,note\n' +
    'abc12345,route,/library,GET,UserLibrary,user,L1,⬜ non coperto,,\n';

  it('promuove a verificato le righe ok', () => {
    expect(applyStatuses(csv, [entry()])).toContain('✅ verificato');
  });

  it('marca le righe rotte come finding da aprire', () => {
    expect(applyStatuses(csv, [entry({ status: 500 })])).toContain('⚠️ finding');
  });

  it('lascia intatte le righe senza riscontro nel crawl', () => {
    expect(applyStatuses(csv, [])).toContain('⬜ non coperto');
  });
});

describe('renderMarkdown', () => {
  it('raggruppa per esito e conta', () => {
    const md = renderMarkdown([entry(), entry({ id: 'z', status: 500 })]);
    expect(md).toContain('rotto: 1');
    expect(md).toContain('ok: 1');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/render-report.test.ts`
Expected: FAIL — modulo inesistente

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/scripts/audit/render-report.ts
export type CrawlEntry = {
  id: string;
  route: string;
  url?: string;
  role: string;
  status: number;
  consoleErrors: string[];
  failedRequests: string[];
  bodyMarkers: string[];
  screenshot?: string;
};

export type Verdict = 'ok' | 'sospetto' | 'rotto';

export function classify(e: CrawlEntry): Verdict {
  if (e.status >= 400 || e.status === 0 || e.bodyMarkers.length > 0) return 'rotto';
  if (e.consoleErrors.length > 0 || e.failedRequests.length > 0) return 'sospetto';
  return 'ok';
}

const STATUS_BY_VERDICT: Record<Verdict, string> = {
  ok: '✅ verificato',
  sospetto: '⚠️ finding da triagare',
  rotto: '⚠️ finding da aprire',
};

/** Riscrive la colonna `stato` delle righe che il crawl ha toccato. */
export function applyStatuses(csv: string, entries: CrawlEntry[]): string {
  const verdicts = new Map(entries.map((e) => [e.id, classify(e)]));
  const [header, ...lines] = csv.trim().split('\n');

  const updated = lines.map((line) => {
    const cells = line.split(',');
    const verdict = verdicts.get(cells[0]);
    if (!verdict) return line;
    cells[7] = STATUS_BY_VERDICT[verdict];
    return cells.join(',');
  });

  return [header, ...updated].join('\n') + '\n';
}

export function renderMarkdown(entries: CrawlEntry[]): string {
  const byVerdict = { ok: 0, sospetto: 0, rotto: 0 };
  const problems: string[] = [];

  for (const e of entries) {
    const verdict = classify(e);
    byVerdict[verdict] += 1;
    if (verdict === 'ok') continue;
    problems.push(
      `| \`${e.route}\` | ${e.role} | ${verdict} | ${e.status} | ${
        [...e.bodyMarkers, ...e.failedRequests, ...e.consoleErrors].slice(0, 2).join(' · ') || '—'
      } |`
    );
  }

  return [
    '# Passata del crawler',
    '',
    `Rotte visitate: ${entries.length} — ok: ${byVerdict.ok} · sospetto: ${byVerdict.sospetto} · rotto: ${byVerdict.rotto}`,
    '',
    '| Rotta | Ruolo | Esito | HTTP | Segnale |',
    '|---|---|---|---|---|',
    ...problems,
    '',
  ].join('\n');
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/audit/__tests__/render-report.test.ts`
Expected: PASS — 9 test

- [ ] **Step 5: Genera il primo report reale**

Crea `apps/web/scripts/audit/main-report.ts`:

```ts
// apps/web/scripts/audit/main-report.ts
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { applyStatuses, classify, renderMarkdown, type CrawlEntry } from './render-report';

const OUT_DIR = path.resolve('../../docs/for-developers/audits/2026-08-26-full-feature-audit');
const CSV = path.join(OUT_DIR, 'inventory.csv');
const RESULTS_DIR = path.resolve('audit-results');
const EVIDENCE_DIR = path.join(OUT_DIR, 'evidence');

const entries: CrawlEntry[] = readFileSync(path.join(RESULTS_DIR, 'entries.jsonl'), 'utf8')
  .trim()
  .split('\n')
  .filter(Boolean)
  .map((line) => JSON.parse(line) as CrawlEntry);

if (entries.length === 0) {
  throw new Error('entries.jsonl è vuoto: la passata del crawler non ha prodotto evidenze');
}

// In evidence/ finiscono SOLO gli screenshot dei problemi: committare 440 immagini
// fullPage appesantirebbe il repo senza aggiungere informazione.
mkdirSync(EVIDENCE_DIR, { recursive: true });
for (const e of entries) {
  if (classify(e) === 'ok' || !e.screenshot) continue;
  copyFileSync(path.join(RESULTS_DIR, e.screenshot), path.join(EVIDENCE_DIR, path.basename(e.screenshot)));
}

writeFileSync(path.join(OUT_DIR, 'wave-0-harness.md'), renderMarkdown(entries), 'utf8');
writeFileSync(CSV, applyStatuses(readFileSync(CSV, 'utf8'), entries), 'utf8');
console.log(`entries: ${entries.length} · evidenze copiate: ${entries.filter((e) => classify(e) !== 'ok').length}`);
```

Run: `cd apps/web && pnpm tsx scripts/audit/main-report.ts`
Expected: `wave-0-harness.md` generato, `inventory.csv` con gli stati aggiornati, `evidence/` con gli screenshot dei soli problemi.

Lo script **fallisce di proposito** se il JSONL è vuoto: un report vuoto letto come "nessun problema" è il modo più facile di rendere verde un audit che non ha guardato nulla.

- [ ] **Step 6: Commit**

```bash
git add apps/web/scripts/audit/render-report.ts apps/web/scripts/audit/__tests__/render-report.test.ts apps/web/scripts/audit/main-report.ts docs/for-developers/audits/2026-08-26-full-feature-audit/
git commit -m "chore(audit): report della passata e aggiornamento tracker"
```

---

### Task 9: Chiusura dell'ondata 0

**Files:**
- Create: `docs/for-developers/audits/2026-08-26-full-feature-audit/README.md`
- Modify: `apps/web/package.json` (script `audit:*`)

- [ ] **Step 1: Aggiungi gli script npm**

In `apps/web/package.json`, sezione `scripts`, accanto a `audit:a11y`:

```json
"audit:inventory": "tsx scripts/audit/main-inventory.ts",
"audit:crawl": "playwright test --config=playwright.audit.config.ts",
"audit:report": "tsx scripts/audit/main-report.ts"
```

- [ ] **Step 2: Scrivi il README del deliverable**

```markdown
<!-- docs/for-developers/audits/2026-08-26-full-feature-audit/README.md -->
# Full Feature Audit — 2026-08-26

Verifica che un utente possa eseguire tutte le funzioni previste, provate dalla UI e confermate
su risposta API, stato del DB e log. Design: [spec](../../specs/2026-08-26-full-feature-audit-design.md) ·
Piano dell'harness: [plan](../../specs/2026-08-26-full-feature-audit-plan.md).

## Come si esegue

Prerequisiti: stack locale avviato (`cd infra && make dev-from-snapshot`), frontend su :3000,
variabili `AUDIT_USER_EMAIL` / `AUDIT_USER_PASSWORD` / `AUDIT_ADMIN_EMAIL` / `AUDIT_ADMIN_PASSWORD`
esportate (credenziali admin in `infra/secrets/admin.secret`).

```bash
cd apps/web
pnpm audit:inventory   # rigenera inventory.csv dal codice
pnpm audit:crawl       # passata del crawler (setup + audit-user + audit-admin)
pnpm audit:report      # aggiorna gli stati e genera wave-0-harness.md
```

## Legenda degli stati

| Stato | Significato |
|---|---|
| `⬜ non coperto` | Mai visitato |
| `✅ verificato` | Evidenza presente e coerente col livello (L1/L2/L3) |
| `⚠️ finding da triagare` | Anomalia non bloccante: console o richieste secondarie in errore |
| `⚠️ finding da aprire` | Guasto: HTTP ≥ 400, o marker di errore nella pagina |
| `🚫 non raggiungibile da UI` | Endpoint senza pagina corrispondente |

## Copertura

Rigenerabile con:

```bash
cd apps/web && node -e "
const fs=require('fs');
const rows=fs.readFileSync('../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv','utf8').trim().split('\n').slice(1).map(l=>l.split(','));
const per={};
for(const c of rows){ (per[c[4]] ??= {})[c[7]] = ((per[c[4]]||{})[c[7]]||0)+1; }
console.table(per);
"
```

## Corpus di riferimento (ondata 3)

Chunk indicizzati e giochi coperti nello snapshot usato: *da compilare al Task 9 Step 3*.

## Findings

Nessuno: l'ondata 0 costruisce l'harness e non produce findings di prodotto. Le ondate 1–5
aggiungono qui le proprie sezioni.
```

- [ ] **Step 3: Verifica la freschezza dello snapshot** (rischio #1 dello spec)

Run:
```bash
docker exec meepleai-postgres psql -U meepleai -d meepleai -t -A -c "SELECT count(*) FROM embeddings" \
  && docker exec meepleai-postgres psql -U meepleai -d meepleai -t -A -c "SELECT count(DISTINCT game_id) FROM embeddings"
```
Expected: un corpus non vuoto. Annota i due numeri nel README: sono il riferimento per giudicare l'ondata 3. Se il corpus è vuoto, l'ondata 3 non è eseguibile su questo snapshot e va dichiarato ora, non tra tre ondate.

- [ ] **Step 4: Verifica finale della qualità**

Run: `cd apps/web && pnpm vitest run scripts/audit/ && pnpm typecheck && pnpm lint`
Expected: tutti i test dell'audit verdi, typecheck pulito, lint entro la soglia esistente.

- [ ] **Step 5: Commit e PR**

```bash
git add apps/web/package.json docs/for-developers/audits/2026-08-26-full-feature-audit/README.md
git commit -m "chore(audit): script npm e README dell'harness"
git push -u origin feature/full-feature-audit
gh pr create --base main-dev --title "chore(audit): harness per l'audit esaustivo delle funzioni" --body "Implementa l'ondata 0 dello spec 2026-08-26-full-feature-audit-design.md."
```

---

## Da decidere prima dell'ondata 1

Due cose che l'inventario ha reso visibili e che vanno risolte con l'autore dell'audit, non da soli:

1. **L'ondata 1 è sbilanciata.** Authentication (67) + SystemConfiguration (24) + Administration (520) + SecurityAudit (0) = **611 righe**, il 35% del tracker, contro le 438 dell'ondata 2 e le 52 dell'ondata 5. Administration da sola pesa 520 perché raccoglie tutta l'area admin non attribuibile ad altro. Delle due l'una: si spezza Administration in più ondate (per esempio: utenti e ruoli · monitoraggio e analytics · resto), oppure l'ondata 1 si mette in conto che duri più sessioni.

2. **Due contesti hanno zero righe.** `EntityRelationships` e `SecurityAudit` sono dichiarati fra i 20 bounded context del CLAUDE.md ma non espongono né pagine né endpoint che il parser riconosca. O i loro endpoint vivono dentro file attribuiti ad altri contesti, o non hanno superficie raggiungibile. Va accertato in ondata 5: nel secondo caso è un finding, non una casella vuota.

3. **Sospetto aperto dal Task 2 — prefisso `/api/v1` raddoppiato.** `ReportingEndpoints`, `AlertConfigurationEndpoints`, `LlmAnalyticsEndpoints` e `PermissionRoutes` sono registrati su `v1Api` (già `/api/v1`) ma dichiarano al proprio interno `app.MapGroup("/api/v1/admin/...")`: ASP.NET annida i prefissi, quindi il path effettivo sarebbe `/api/v1/api/v1/admin/...`. Il frontend li chiama al path singolo (`lib/api/alert-config.api.ts:20`, `lib/api/clients/admin/adminMonitorClient.ts:135`) e nessun test backend li copre. **Da verificare con lo stack acceso all'inizio dell'ondata 1**: se confermato è P0 (funzioni admin irraggiungibili), se smentito va cancellato da qui.

## Dopo l'ondata 0

Le ondate 1–5 non sono implementazione ma esecuzione: seguono il runbook, non questo piano. Per ciascuna:

1. Esegui `pnpm audit:crawl` filtrando le rotte del contesto dell'ondata.
2. Percorri a mano le mutazioni del contesto (livello L2/L3 dallo spec), con `collect-evidence` attivo attorno a ogni azione.
3. **Correla endpoint e rotte del contesto**: ogni endpoint che nessuna rotta dell'ondata ha
   esercitato va marcato `🚫 non raggiungibile da UI`. Se implementa una funzione che l'utente
   dovrebbe poter eseguire, è un finding — non una riga da archiviare. L'harness non può dedurlo:
   serve il giudizio su cosa il prodotto promette.
4. Rileggi gli endpoint con nota `auth non dedotta: leggere il codice` (Task 2 Step 5): il parser
   non ha saputo classificarli e il loro livello di protezione va confermato a mano.
5. Compila `wave-N-<nome>.md` con le evidenze.
6. Apri le issue secondo la tabella di severity; correggi i P0 in sessione con PR verso `main-dev`.
7. Aggiorna `inventory.csv` e committa.

L'audit è concluso secondo i criteri della sezione finale dello spec.
