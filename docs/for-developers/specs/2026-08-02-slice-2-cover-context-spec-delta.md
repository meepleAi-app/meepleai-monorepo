# Slice 2 — per-context CoverUrl: spec-delta

**Epic**: #3470 (admin cover editor su `/shared-games`)
**Data**: 2026-08-02
**Stato**: spec-delta (pre-implementazione) — colma i gap della spec D3 §4.3 emersi dallo spec-panel
**Riferimento**: `2026-08-02-admin-cover-editor-design.md` §4.2/§4.3/§9 (branch `docs/spec-admin-cover-editor`) · Slice 1 mergiata (`CoverUrlResolver.ResolveForContextAsync`, `GameCoverAssignment`, read DTO candidati)

La spec definisce Slice 2 come un bullet — «per-contesto completo (map DTO + MeepleCard/**6 call-site**) + direct-apply pagina PDF + trigger Wikidata on-demand» — senza mai **enumerare i 6 call-site** né mappare ciascuno al contesto. Questo delta li enumera (verificati sul codice) e ne deriva acceptance criteria testabili.

## 1. Reconciliation dei contratti

| Punto | Spec D3 | Realtà (codice) | Azione Slice 2 |
|---|---|---|---|
| Chiavi map DTO | §4.3 `coverUrls: { card, hero, thumbnail }` | Slice 1 ha ratificato **SD1 = Card/Hero/Social** e il DTO mergiato `CoverContextAssignmentsDto(Card, Hero, Social)` | **Allinea a `{ card, hero, social }`**. `thumbnail` è un *crop* del contesto Card, non un contesto → rimuovi la chiave stale. |
| Metodo resolver | §4.2 `ResolveForContextAsync(entity, context, blob)` | **Esiste già** (`CoverUrlResolver.cs:171`): public/no-user, sotto L3 user-custom, fallback alla precedenza implicita | I 6 handler NON lo chiamano (usano il context-blind `ResolvePublicWithSourceAsync` / `ResolveForUserAsync`). Slice 2 = **wire** del metodo esistente. |
| Retrocompat | §4.3 `coverUrl = coverUrls.card` | I DTO espongono un solo `CoverUrl` | Ogni handler passa il **suo** contesto (non necessariamente Card — vedi §2). Non c'è una map unica: la scelta di contesto è **per-superficie**. |
| Focal-point | SD2 = sorgente + focal configurabile | Read DTO `assignments` espone **solo `source`**, non `focalX/focalY` (in Slice 1d-c il picker FE ha dovuto usare un dirty-guard perché non può pre-riempire il focal salvato) | **Aggiungi `focalX/focalY` per contesto** al read-shape candidati. |

## 2. I 6 call-site — tabella handler → contesto (verificata)

Tutti e 6 sono **context-BLIND** oggi: chiamano `ResolvePublicWithSourceAsync` (precedenza implicita L4 PDF → L2.5 BGG → L2 Wikidata) o `ResolveForUserAsync`, **non** `ResolveForContextAsync`. L'assegnazione admin per-contesto (`GameCoverAssignmentEntity`, Slice 1) è quindi **ignorata** in tutte le superfici di render.

| # | Query-handler | Superficie | Contesto | Cambio Slice 2 |
|---|---|---|---|---|
| 1 | `SearchSharedGamesQueryHandler` (`:415-453`) | Catalogo pubblico / Discover (grid) | **Card** | wire `ResolveForContextAsync(g, Card, blob)`; invalida cache tag `search-games` su assign |
| 2 | `GetSharedGameByIdQueryHandler` (`:403-408`) | Detail pubblico (hero) — **unica superficie Hero** | **Hero** | wire `ResolveForContextAsync(e, Hero, blob)`; invalida `shared-game:{id}` su assign |
| 3 | `GetFilteredSharedGamesQueryHandler` (`:114-153`) | Lista admin / coda filtrata (grid) | **Card** | wire Card |
| 4 | `GetPendingApprovalGamesQueryHandler` (`:65-104`) | Coda approvazione admin (grid) | **Card** | wire Card |
| 5 | `GetAllSharedGamesQueryHandler` (`:84-124`) | Lista admin "all games" | **Card** | ⚠️ **dormant**: nessun endpoint lo dispatcha oggi — verifica se vivo prima di toccarlo |
| 6 | `GetUserLibraryQueryHandler` (`:182-217`, UserLibrary) | My Library (grid) | **Card** | **unico che usa `ResolveForUserAsync`** (L3 user-custom sopra l'override admin, per **SD3**) → mantieni la stratificazione: user-custom > admin-assignment > precedenza |

**Superficie Social/OG (non tra i 6, sul FE)**: `openGraph.images` di `(public)/shared-games/[id]` (#3452) usa oggi `detail.coverUrl` — cioè il valore risolto **Hero** dell'handler #2. **Non esiste risoluzione Social dedicata.** → Slice 2 deve esporre un campo Social nel `SharedGameDetailDto` (es. `coverUrls.social` o `socialCoverUrl`) risolto con `ResolveForContextAsync(e, Social, blob)`, che l'OG meta consuma.

### Fuori scope (segnalati per completezza)
- `GetCoverCandidatesQueryHandler` — è il read-model del picker (Slice 1c-3), NON una superficie di render; già l'unico che legge le assignment Card/Hero/Social (ma solo per riportare il `Source` pinnato).
- `GetDashboardQueryHandler` (`:192`) + `ActivityTimelineService` (`:204`) — espongono un campo `CoverUrl` ma leggono la **tombstone legacy `ImageUrl`** (vuota post-#2123), non R2. Migrazione separata, non parte di Slice 2.

## 3. Gap critico — invalidazione cache su assign

Ogni handler di render ha `HybridCache` con TTL lunghi (Search L1 15min/L2 1h tag `search-games`; Detail L1 30min/L2 2h tag `shared-game:{id}`). **Da verificare**: `AssignCoverCommand`/`RemoveCoverAssignmentCommand` (mergiati in Slice 1) invalidano questi tag? Se no, l'assegnazione admin **non compare** fino a scadenza TTL. → Slice 2 DEVE bustare i tag di cache pertinenti nell'handler di assign/remove (per gameId: `shared-game:{id}`; per la grid: `search-games` + le liste admin). **Acceptance**: test che dopo un assign il `SearchSharedGamesQuery` ritorna la nuova cover senza attendere il TTL.

## 4. Acceptance criteria — Slice 2

- **AC-1 (per-context wiring)**: ciascuno dei 6 handler (escluso #5 se dormant) risolve via `ResolveForContextAsync` col contesto della tabella §2. Test per-handler: un assign su Card **non** cambia l'Hero e viceversa (isolamento per-contesto, come §9 spec «Card override non tocca Hero»).
- **AC-2 (Social)**: `SharedGameDetailDto` espone la cover Social risolta; l'OG meta (#3452) la consuma. Test: assign Social → OG cambia, hero no.
- **AC-3 (retrocompat)**: i consumer che leggono `CoverUrl` non-context-aware continuano a funzionare (default = contesto della loro superficie, non forzato a Card).
- **AC-4 (SD3 layering)**: in `GetUserLibraryQueryHandler` la cover personale utente (L3) resta **sopra** l'override admin. Test: user con custom cover + admin assignment → vince la custom.
- **AC-5 (focal)**: read-shape candidati espone `focalX/focalY` per contesto; il picker FE li pre-riempie (rimuove il dirty-guard workaround di 1d-c).
- **AC-6 (cache)**: assign/remove invalidano i tag `HybridCache` pertinenti (§3).
- **AC-7 (attribution source-aware)**: l'attribution segue la sorgente vincente del contesto (già fixato in 1d-a #3482 via `CoverAttribution.ForWinningSource`) — verifica che regga col wiring per-contesto.

## 5. Direct-apply PDF + Wikidata on-demand

- **Direct-apply PDF** (bypass del loop propose→approve per admin): specifica ruolo (`AdminOrEditorPolicy` per pick materializzato, SD5) **+ evento di audit** (chi/quando/da quale `pdfDocumentId`/pagina) — ogni bypass di un gate va tracciato.
- **Wikidata on-demand**: fetch sincrono durante l'interazione admin, rate-limit Wikimedia ~1 req/sec. Specifica timeout, stato di loading e comportamento su fallimento/rate-limit (**non bloccare** il picker; degradare a "sorgente non ancora materializzata").

## 6. Dipendenza di sicurezza

Slice 2 non tocca fetch remoti → nessuna dipendenza SSRF. **Slice 3** (manual-URL) sì: vedi issue **#3495** (SSRF egress hardening P0, da chiudere prima e separatamente).
