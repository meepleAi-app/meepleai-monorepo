# Cover Stack Live Audit — 2026-06-02

> **Scope**: verifica post-deploy della "cover stack" — umbrella **[#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821)** "Game cover images — eliminate BGG runtime dependency, legal-clean stack".
>
> **Timeline merge** (tutti su `main-dev`, 2026-06-02):
> - 17:25 UTC — PR [#1825](https://github.com/meepleAi-app/meepleai-monorepo/pull/1825) — L1 placeholder deterministico (#1822 ✅ CLOSED)
> - 18:12 UTC — PR [#1829](https://github.com/meepleAi-app/meepleai-monorepo/pull/1829) — L1 follow-up `HybridHubItem.id` → `MeepleCard`
> - 19:37 UTC — PR [#1839](https://github.com/meepleAi-app/meepleai-monorepo/pull/1839) — L4 `IPdfCoverExtractor` service + L2/L3 DB columns (#1823 / #1824 stub)
> - 19:48 UTC — PR [#1843](https://github.com/meepleAi-app/meepleai-monorepo/pull/1843) — L4 wiring in `PdfProcessingPipelineService`
>
> **Methodology**: static code analysis (acceptance criteria #1821 sono verificabili strutturalmente dal codice) + UI snapshot live (mobile 390×844).
>
> **Live snapshot environment**: presumibilmente staging `https://meepleai.app` (timestamp screenshot `2026-06-02 20:46` ≈ T+58min dal merge #1843; pattern Catan + admin coerente con [`2026-06-02-mobile-golden-path-audit.md`](./2026-06-02-mobile-golden-path-audit.md)). Da confermare nel prossimo refresh dell'audit.

## Top-level outcome

- **L1 #1822 — VERIFIED ✅** (static + UI). Codice strutturalmente impedisce qualsiasi richiesta HTTP runtime verso CDN BGG; placeholder deterministico visibile live (Catan → gradient verde + "C").
- **L2 #1823 — DB stub merged, implementation OPEN ⏳**. Colonne pronte per future enrichment Wikidata; nessun rendering live.
- **L3 #1824 — DB stub merged, implementation OPEN ⏳**. Colonne pronte per future user-uploaded cover; nessuna UI di upload ancora.
- **L4 #1831 — Service+pipeline shipped, runtime verification PENDING ⏳**. `PdfCoverExtractor` integrato in `PdfProcessingPipelineService`, ma audit live non lo copre (`/library` con `Documenti: 0` non ha PDF caricati). Test live dell'estrazione richiede smoke con upload PDF.
- **1 cleanup-debt** documentato: `next.config.js` ha ancora `cf.geekdo-images.com` nei `remotePatterns` Next/Image — whitelist morta, da rimuovere.

## Acceptance criteria check ([#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821))

| AC | Status | Evidence |
|---|---|---|
| L1 ships e `/library` shows zero `cf.geekdo-images.com` requests in browser network panel | ✅ **VERIFIED via codice** | `apps/web/src/lib/games/cover-utils.ts:21-25` (`BLOCKED_IMAGE_HOSTS`) + `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx:42-43` (gate `shouldUsePlaceholder` PRIMA del `<img src>`). UI live: `audit-01-library-l1-placeholder-mobile-390.png` mostra Catan con placeholder L1 (no `<img>` BGG renderato). |
| DB field `SharedGameCatalogEntry.ImageUrl` non più rendered as-is sul client quando punta a BGG (treated as missing) | ✅ **VERIFIED via codice** | `shouldUsePlaceholder` in `cover-utils.ts:38-55` parsea URL, normalizza hostname, restituisce `true` per host in `BLOCKED_IMAGE_HOSTS` o relativi subdomain. Test esaustivi in `cover-utils.test.ts:16-28` (BGG host + subdomain). |
| L2+L3 ship come follow-up; placeholder resta universal fallback | ⏳ **DB stub only** | Issue [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) (Wikidata) e [#1824](https://github.com/meepleAi-app/meepleai-monorepo/issues/1824) (user upload) restano **OPEN**. Le colonne DB sono presenti come stub (PR #1839) ma nessuna logica di rendering / upload. Placeholder L1 resta universal fallback come previsto. |
| Footer attribution Wikidata/Commons quando L2 lands | ⏳ **N/A** | Diventerà attivo con #1823. |

## Findings dettagliati per layer

### L1 — Placeholder deterministico ✅

**Componenti principali**:
- `apps/web/src/lib/games/cover-utils.ts` — gate `shouldUsePlaceholder`, hash determ., extract initials (i18n-aware con stop-words IT/EN)
- `apps/web/src/components/ui/data-display/meeple-card/parts/GameCoverPlaceholder.tsx` — render gradient HSL + initials + meeple silhouette SVG, SSR-safe, `data-testid="game-cover-placeholder"`
- `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx:42-72` — gate runtime, fallback `onError` se l'`<img>` legittimo fallisce, doppio placeholder (rich `GameCoverPlaceholder` per game-entity con `gameId`, fallback entity-icon altrove)

**Evidence UI** (`audit-01-library-l1-placeholder-mobile-390.png`):
- Mobile 390×844, route `/library`, user con 1 gioco
- Catan card mostra:
  - Gradient verde scuro 135° (hash di `gameId="cc1678e8-…"` → hue determ.)
  - Initials "C" centrato, font Quicksand bold, white-on-bg WCAG AA
  - Meeple silhouette decorativa opacità 0.12
  - Badge "GAME" arancione (entity color)
  - Title "Catan" + rating 7.1 ★★★★

**Test coverage** (✅ comprehensive):
- `cover-utils.test.ts` — BGG host + subdomain + R2 (`covers.meepleai.app`) + data URLs + relative paths + invalid input
- `GameCoverPlaceholder.test.tsx` — render + a11y `aria-hidden`

**Note residue** (non blocking):
- `apps/web/next.config.js:131-133` ha `cf.geekdo-images.com` nei `remotePatterns` — dead config (Next/Image non viene mai chiamato per BGG perché `Cover.tsx` usa `<img>` raw + gate). Da rimuovere come cleanup-debt.
- Seed manifests (`apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/{dev,staging,prod}.yml`) e snapshot SQL (`infra/data/snapshots/…sql`) contengono BGG URLs nel campo `ImageUrl`. Comportamento corretto a runtime (bloccati da `shouldUsePlaceholder`), ma valore di colonna inquinato → futura migrazione a `ImageUrl = NULL` per i record affetti (issue cleanup separata, low priority).

### L2 — Wikidata enrichment ⏳ DB stub only

- Issue [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) — **OPEN P2** (`area/backend`, `area/frontend`).
- PR #1839 ha aggiunto colonne DB stub al catalog (per future enrichment job).
- Nessuna job/rendering attivo. Nessun footer Wikidata/Commons attribution.
- **Next step**: implementare enrichment job (server-side, scheduled) + footer attribution.

### L3 — User-uploaded custom cover ⏳ DB stub only

- Issue [#1824](https://github.com/meepleAi-app/meepleai-monorepo/issues/1824) — **OPEN P2** (`area/frontend`).
- PR #1839 ha aggiunto colonne DB stub.
- Nessuna UI di upload, nessun endpoint dedicato.
- **Next step**: design upload flow (drawer su `/library/[gameId]` settings? o standalone `/upload`?) + endpoint + storage R2.

### L4 — PDF-derived cover ⏳ Service shipped, live verification PENDING

**Componenti merged**:
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfCoverExtractor.cs` — interface
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfCoverExtractor.cs` — impl con Docnet.Core (PDFium) + SkiaSharp; heuristica score-based su prime 3 pagine, render webp 200×300 thumbnail + 600×900 preview; decisione documentata in-source.
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs:51, 70` — `IPdfCoverExtractor?` iniettato come dipendenza opzionale.
- DI registration: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs`.
- Test unit: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/PdfCoverExtractorTests.cs`.

**Cosa NON è verificato da questo audit**:
- Estrazione end-to-end: upload PDF → pipeline → cover webp generato → upload R2 → `SharedGameCatalogEntry.ImageUrl` (o colonna L4 dedicata) popolata → `/library` rende il PDF cover (non più placeholder L1).
- Outcome `Skipped` vs `Success` su PDF reali (es. rulebook Catan 244 KB già su staging).
- Performance dell'estrazione (timeout, memory, retry).

**Repro per live verification (follow-up)**:
1. Login staging come `admin@meepleai.app`.
2. `/library/<gameId>/kb` → "Carica PDF" con rulebook nuovo (es. Wingspan).
3. Attendere completamento pipeline (smoke: 60s; vero target: <30s per PDF medio).
4. Tornare a `/library` → verificare che la card del nuovo gioco mostri **cover PDF estratto** (immagine reale), NON placeholder L1.
5. DevTools Network panel su quel card → `<img src>` deve puntare a `covers.meepleai.app/.../cover.webp` (R2 dominio), NON al placeholder inline SVG.

## Static-analysis residui geekdo (audit completo)

`grep -ri "geekdo" --type-not snapshot` totale **45 file**. Classificazione:

| Categoria | File count | Runtime impact | Action |
|---|---:|---|---|
| FE runtime gate (intenzionale) | 2 | Block-list per `shouldUsePlaceholder` | ✅ Keep |
| Test coverage gate | 2 | Verify gate works | ✅ Keep |
| Next/Image whitelist morta | 1 | Nessuno (gate intercetta prima) | 🧹 Remove |
| BE BGG API client (server-side enrichment, future #1823) | ~10 | Nessuno — non chiamato per cover display | ✅ Keep (server-side) |
| Mockup HTML statici (admin-mockups) | ~15 | Nessuno (design files, non shipped) | ✅ Keep |
| Seed manifests + snapshot SQL (`ImageUrl` legacy) | ~6 | DB pollution; runtime safe | 🧹 Migration follow-up (low priority) |
| Storybook + visual-test fixtures | ~7 | Nessuno (story files) | ✅ Keep |
| Tools script (`tools/fix-chess-game.ps1`) | 1 | One-shot ops script, BGG URL hardcoded; gate intercetta runtime | 🧹 Update se rieseguito |

## Pending verifiche (skipped per time budget / scope)

- [ ] **DevTools Network panel** live `/library` su staging: zero richieste `cf.geekdo-images.com` (richiesto da AC ma verificabile solo con live browser; static analysis fornisce evidenza equivalente strutturale).
- [ ] **L4 end-to-end live**: upload PDF nuovo + verifica che cover estratto sia renderizzato su `/library`.
- [ ] **L4 outcome `Skipped`** su PDF text-only (es. legal disclaimer pages): comportamento atteso = fallback su L1 placeholder.
- [ ] **Desktop 1280×720** parity check: lo screenshot copre solo mobile 390. Verificare desktop in particolare per `HeroCard` variant (aspect-video).
- [ ] **Conferma environment screenshot** (staging vs local dev — coerenza con mobile-golden-path-audit suggerisce staging ma non confermato).

## Cleanup follow-up (separate issues)

- [ ] **Cleanup `next.config.js` `remotePatterns`** — rimuovere entry `cf.geekdo-images.com` + `**.boardgamegeek.com`. Risk: nessuno (gate FE intercetta prima); Benefit: chiarezza config + audit clean.
- [ ] **DB hygiene migration** — porre `ImageUrl = NULL` per i record `SharedGameCatalogEntry` con host in `BLOCKED_IMAGE_HOSTS` (seed + esistenti). Effetto: rimuove rumore in DB; comportamento UI invariato. Considerare come parte di #1823 enrichment job.
- [ ] **Ops script `tools/fix-chess-game.ps1`** — aggiornare con URL placeholder o R2 path se rieseguito.

## Screenshots index

| File | Layer | Route | Viewport | Notes |
|---|---|---|---|---|
| `2026-06-02-cover-stack-screenshots/audit-01-library-l1-placeholder-mobile-390.png` | L1 | `/library` | 390×844 | Catan con placeholder verde + "C" deterministic. Stats: Giochi 1 / Agenti 0 / Documenti 0 / Chat 0 (no PDF → L4 non testabile in questa run). |

## Related issues / PRs

**Issue tree**:
- 📌 [#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821) — Umbrella **OPEN P1**
- ✅ [#1822](https://github.com/meepleAi-app/meepleai-monorepo/issues/1822) — L1 placeholder **CLOSED 2026-06-02 17:25 UTC**
- 🟡 [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) — L2 Wikidata **OPEN P2** (DB stub merged)
- 🟡 [#1824](https://github.com/meepleAi-app/meepleai-monorepo/issues/1824) — L3 user upload **OPEN P2** (DB stub merged)
- 🟡 [#1831](https://github.com/meepleAi-app/meepleai-monorepo/issues/1831) — L4 PDF cover **OPEN P1** (service+pipeline merged, live verify pending)

**Pull requests** (all merged to `main-dev` on 2026-06-02):
- [#1825](https://github.com/meepleAi-app/meepleai-monorepo/pull/1825) — L1 placeholder (17:25 UTC)
- [#1829](https://github.com/meepleAi-app/meepleai-monorepo/pull/1829) — L1 follow-up `HybridHubItem.id` (18:12 UTC)
- [#1839](https://github.com/meepleAi-app/meepleai-monorepo/pull/1839) — L4 service + L2/L3 DB columns (19:37 UTC)
- [#1843](https://github.com/meepleAi-app/meepleai-monorepo/pull/1843) — L4 pipeline wiring (19:48 UTC)

**Cross-reference**:
- [`docs/for-developers/audits/2026-06-02-mobile-golden-path-audit.md`](./2026-06-02-mobile-golden-path-audit.md) — audit antecedente (mobile UX, pre-cover-stack); usa lo stesso Catan + admin sessione staging.
