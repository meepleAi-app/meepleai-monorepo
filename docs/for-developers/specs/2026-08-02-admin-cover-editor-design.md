# Admin Cover Editor — design spec

**Status**: Slice 0 contract-locked (spec-panel design, 2026-08-02)
**Origine**: richiesta utente + spec-panel multi-esperto (Cockburn / Fowler / Nygard / Crispin), radicato nel codice della cover-stack (ADR-087, [[r2-cover-resolver-p1]]).
**Tracker**: epic [#3470](https://github.com/meepleAi-app/meepleai-monorepo/issues/3470).

## 1. Obiettivo

Come **admin/editor**, da `/shared-games`, con pochi click: scegliere quale cover usa un gioco tra le sorgenti disponibili (Wikidata, pagina PDF, BGG, **URL legale**) e **impostare cover diverse per contesto UI** (griglia ≠ hero ≠ thumbnail).

## 2. Decisioni ratificate (utente, 2026-08-02)

| # | Decisione | Scelta |
|---|---|---|
| D1 | Semantica "come usata nella UI" | **Cover per-contesto** (griglia/hero/thumbnail indipendenti) — non una cover unica |
| D2 | Scope | **Tutto**, incluso il path URL manuale (legale) |
| D3 | Entry-point | **Overlay inline role-gated** su card/hero (nessuna nuova rotta) |

> D1 è l'interpretazione a costo più alto: cambia DTO, resolver e i call-site di `MeepleCard`. Effort complessivo **L+**, da decomporre in slice.

### 2.1 Contratto Slice 0 — sub-decisioni ratificate (2026-08-02)

| # | Sub-decisione | Scelta |
|---|---|---|
| SD1 | Set `CoverContext` | **3**: `Card` (2:3, copre grid/list/thumbnail via crop), `Hero` (~16:9, copre hero/featured/hub), `Social` (1.91:1, OG) |
| SD2 | Per-contesto = sorgente o crop? | **Sorgente + focal-point configurabile**: l'admin sceglie una sorgente e regola il punto focale del crop per contesto (rende configurabile `Gravity`, oggi hardcoded `Center` in `WebpVariantGenerator.cs:96-97`) |
| SD3 | Override admin vs L3 user-custom | Admin override **sotto** L3: la cover personale dell'utente NON viene scavalcata (l'override vive in `ResolvePublicAsync`, sopra L4) |
| SD4 | Licenza URL manuale | **Attestazione admin** (licenza da whitelist `LicenseValidator`) + audit `attestedBy`+timestamp; responsabilità legale sull'admin |
| SD5 | Ruoli | Pick tra sorgenti materializzate = `AdminOrEditorPolicy`; path URL manuale (attestazione) = `AdminOnlyPolicy` |

## 3. Vincolo strutturale (stato attuale)

- Il `CoverUrlResolver` sceglie per **precedenza implicita** L3 user → L4 PDF → L2.5 BGG → L2 Wikidata → placeholder (`CoverUrlResolver.cs:51/82/102/116`). **Non esiste override esplicito**, né concetto di contesto.
- Il DTO espone **solo il vincitore** (`SharedGameDto.CoverUrl`, `SharedGameDetailDto.CoverUrl`), non i candidati. Le key per-sorgente esistono sull'entity (`WikidataCoverR2Key`, `PdfCoverR2Key`, `BggCoverR2Key`) ma non sono serializzate.
- FE: `MeepleCard` consuma **un solo** `coverUrl`, fanout a griglia/hero/list via `imageUrl={coverUrl ?? undefined}` (`meeple-card-game.tsx:106`, `[id]/page-client.tsx:342`).
- Chiavi centralizzate in `CoverKeyBuilder`/`CoverKind` (`SharedKernel/Domain/Covers`).

## 4. Architettura proposta

### 4.1 Modello dati — assegnazione per-contesto (D1/SD1/SD2) — schema Slice 0

Nuovo enum **`CoverContext`** (SharedKernel/Domain/Covers): `Card=0`, `Hero=1`, `Social=2` (aspect target: 2:3, 16:9, 1.91:1). Nuovo valore **`CoverKind.Manual`** in `CoverKeyBuilder` (+ suffisso in `SuffixFor`).

Nuova collezione figlia **`GameCoverAssignment`** (aggregato SharedGame, tabella `game_cover_assignments`):

| Colonna | Tipo | Note |
|---|---|---|
| `id` | uuid PK | |
| `shared_game_id` | uuid FK → shared_games | cascade delete |
| `context` | smallint | `CoverContext` (Card/Hero/Social) — UNIQUE(shared_game_id, context) |
| `source_kind` | smallint | `CoverKind` Pdf/Bgg/Wikidata/Manual (NON User: L3 è per-utente) |
| `focal_x` | real nullable | punto focale crop 0..1 (SD2), default 0.5 (center) |
| `focal_y` | real nullable | idem, default 0.5 |
| `generated_r2_key` | text nullable | WebP croppato per il contesto (prodotto da `WebpVariantGenerator` con focal-point) |
| `created_at`/`created_by`/`updated_at`/`updated_by` | audit | pattern standard |
| xmin | (system) | optimistic concurrency (ADR-060) |

Assenza di riga per un contesto ⇒ **fallback alla precedenza implicita** (retrocompat totale, nessun backfill). Persistenza EF come child collection con reconcile in `SharedGameRepository` (pattern [[ef-detached-graph-child-loss]]).

**Sorgente URL manuale** (SD4) — nuove colonne su `SharedGameEntity` (a specchio delle `WikidataCover*`, config come `SharedGameEntityConfiguration.cs:130-161`): `manual_cover_r2_key`, `manual_cover_license`, `manual_cover_attribution`, `manual_cover_source_url`, `manual_cover_attested_by`, `manual_cover_attested_at`. La cover manuale è **fetchata + ri-ospitata su R2** (mai URL grezzo).

**`WebpVariantGenerator`** (SD2): estendere `GenerateWebpAsync` con un parametro focal-point/gravity opzionale (default `Gravity.Center` = comportamento attuale, retrocompat) → `Extent` usa la gravità derivata dal focal-point invece dell'hardcoded `Center`.

> Alternativa scartata: colonne override per-contesto sull'entity — non scala col numero di contesti né col focal-point. Il child-table è preferito.

### 4.2 Resolver

Nuova firma **`ResolveForContextAsync(entity, context, blobStorage)`**: (1) se esiste `GameCoverAssignment` per `context` e la sua R2 key è risolvibile → vince; (2) altrimenti **cade sulla precedenza implicita** attuale (nessuna regressione). L3 user-custom per-utente resta **sopra** l'assegnazione admin in `ResolveForUserAsync` (personalizzazione utente non scavalcata — vedi §8).

### 4.3 DTO / API di lettura

- Nuovo read-shape **candidati**: endpoint/campo che elenca, per gioco, le sorgenti disponibili (Wikidata+licenza, ogni pagina PDF, BGG) con anteprima + quale è "in uso" per ciascun contesto. Serve al picker (oggi il DTO dà solo il vincitore).
- **DTO cover per-contesto**: `coverUrls: { card, hero, thumbnail }` (map). Retrocompat: `coverUrl` resta = `coverUrls.card` per i consumer esistenti. I 6 query-handler che calcolano `CoverUrl` diventano context-aware.

### 4.4 FE — overlay picker (D3)

- Matita **role-gated** (`useAdminRole().isEditorOrAbove`, `hooks/useAdminRole.ts:49`) su `MeepleCardGame`/Hero, riuso `EditCoverOverlay.tsx` (già a11y). Invisibile ai non-admin ⇒ contratto audience `(public)` intatto (#2118). Apre `AdminCoverSourceDialog` (overlay Radix, **niente rotta/nav**, non tocca `AppTopBar` #1977).
- Dialog: per **ciascun contesto** (tab o segmenti Card/Hero/Thumbnail), i candidati come thumbnail selezionabili con chip provenienza+licenza; riuso `CoverPagePicker.tsx` per la scelta pagina PDF e la shape 3-tab di `CoverImagePicker.tsx`.
- Persistenza optimistic + refetch; concorrenza via xmin (ADR-060) → set stale = 409.

### 4.5 Path URL manuale (D2, la slice legalmente sensibile)

Regola d'oro (Nygard): **l'URL è input transiente, mai render-source**. Nuovo comando **`SetManualCoverCommand`** (AdminOnlyPolicy, non Editor — è attestazione legale):
1. `SsrfSafeHttpClient` scheme+IP, **redirect disabilitati o ri-validati per-hop** (fix TOCTOU/DNS-rebinding, §7);
2. download ≤10MB, validazione **reale** via `IWebpVariantGenerator` (throw su non-immagine, non fidarsi del Content-Type);
3. **gate licenza**: l'admin dichiara una licenza da whitelist `LicenseValidator` (CC0/PD/CC-BY/CC-BY-SA) + attribution + fonte → 400 se non-whitelist; l'attestazione è dell'admin (registra `attestedBy`+timestamp per audit);
4. re-encode WebP → upload R2 raw-key → persiste **solo** la R2 key + license/attribution (nuovo `CoverKind.Manual`, colonne `ManualCover*` a specchio di `WikidataCover*`).
> Host BGG/geekdo: non bloccati nel fetch server-side (ADR-059 §2), ma di fatto rifiutati dal gate-licenza (non sono CC).

## 5. Slicing (decomposizione consigliata)

- **Slice 0** — contract/design lock: enumerare i `CoverContext`, confermare le sub-decisioni §8, migration schema.
- **Slice 1 (M)** — read-DTO candidati + `CoverSourceAssignment` + resolver context-aware + override su sorgenti **già materializzate** (Wikidata/PDF-esistente/BGG) + overlay picker mono-contesto.
- **Slice 2 (M)** — per-contesto completo (map DTO + MeepleCard/6 call-site) + direct-apply pagina PDF (bypass del loop propose→approve per admin) + trigger Wikidata on-demand.
- **Slice 3 (L)** — path URL manuale (fetch+attestazione+re-host R2) + SSRF hardening.

## 6. Riuso (abbatte il costo)

`CoverPagePicker`, `CoverImagePicker`, `EditCoverOverlay`, `MaterializePdfCoverCommand`, Wikidata single-command (`POST /admin/wikidata/enrichment/{gameId}`), `LicenseValidator`, `CoverKeyBuilder`, blob raw-key primitives, `useAdminRole`.

## 7. Bug latenti da correggere (trovati dal panel)

- **Attribution footer errato**: `GetSharedGameByIdQueryHandler.cs:441-443` emette `WikidataCoverLicense/Attribution` **incondizionatamente** anche quando vince la cover PDF/Manual → il footer mostra attribution Wikidata sbagliata. Rendere l'attribution **source-aware** (segue la sorgente vincente) — naturale in Slice 1.
- **SSRF TOCTOU/DNS-rebinding**: `ValidateResolvedIpAsync` risolve il DNS una volta e `GetAsync` un'altra (stesso gap in `BggCoverDownloader`). Fix in Slice 3: risolvere una volta e connettersi all'IP validato (pin), o ri-validare per-hop.

## 8. Sub-decisioni — CHIUSE in Slice 0 (2026-08-02)

Tutte ratificate (vedi §2.1): SD1 contesti = **Card/Hero/Social**; SD2 = **sorgente + focal-point configurabile**; SD3 = override **sotto** L3-utente; SD4 = **attestazione admin + audit** su URL manuale; SD5 = **AdminOrEditor** (pick) / **AdminOnly** (URL manuale). Contract lock completo → pronto per Slice 1.

## 9. Testing (Crispin)

- **Resolver override** (unit, valore massimo): estendere `CoverUrlResolverTests` — assignment vince su tutti i layer impliciti; assignment con key mancante → **fall-through**, non placeholder; per-contesto isolato (Card override non tocca Hero).
- **Contract** (`CoverKeyContractTests`): `CoverKind.Manual` suffix.
- **Manual URL** (integration): assert `CoverUrl != host input` (sempre R2), gate licenza (400 su non-whitelist), SSRF (redirect→IP privato bloccato).
- **FE**: overlay role-gated (invisibile a non-admin), picker per-contesto, propagazione.
- **Nota gap CI**: le assert R2-strict sono `test.fixme` (MinIO non wired in E2E) — l'assert propagazione-reale resta disabilitato finché non si wira l'emulatore S3.
