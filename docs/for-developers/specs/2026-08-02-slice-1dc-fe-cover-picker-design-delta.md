# Slice 1d-c — FE admin cover picker: design-delta

**Epic**: #3470 (admin cover editor su `/shared-games`)
**Data**: 2026-08-02
**Stato**: design ratificato, implementazione in corso (TDD, 4 PR)
**Riferimento di partenza**: la spec di design dell'epic (`docs/for-developers/specs/2026-08-02-admin-cover-editor-design.md`, attualmente sul branch `docs/spec-admin-cover-editor`, non ancora in `main-dev`) — sezioni D3 (entry-point overlay inline role-gated su card/hero, nessuna nuova rotta), SD1 (contesti Card/Hero/Social), SD5 (pick tra sorgenti materializzate = AdminOrEditor).

Questo documento riconcilia la spec D3 con la ricognizione del codice reale e ratifica le decisioni FE per l'ultima fetta rimasta (1d-c). Tutto il backend dell'epic è già mergiato in `main-dev` (1c-1 #3476, 1c-2 #3478, 1c-3 #3479, write #3481, 1d-a #3482, 1d-b #3483).

## 1. Delta dalla ricognizione (assunzioni del handoff corrette)

1. **La card pubblica è di fatto contract-locked.** `MeepleCardGame` è un adapter sottile su `MeepleCard`; con `href` presente `GridCard` renderizza **l'intera card come `<Link prefetch>`**. Un `<button>` interattivo dentro l'anchor viola `nested-interactive` (axe) e litiga con la navigazione. `MeepleCardProps` non ha `children`/slot. Due test di enforcement (`card-decision-table`, `no-inline-card-reimplementation` C4) + ESLint proteggono il componente.
2. **L'hero del detail pubblico è montabile pulito.** Usa `detail-layout/hero.tsx` (NON `MeepleCard variant="hero"`): cover-div già `relative`, non link-wrapped. La pagina `(public)/shared-games/[id]/page-client.tsx` è già `'use client'`; sessione/ruolo raggiungibili.
3. **`EditCoverOverlay` è solo il pulsante-matita**, non il modale. Il pattern a11y del modale (role=dialog, aria-modal, focus-trap, Escape) è il primitivo Radix `components/ui/overlays/dialog.tsx` (usato da `CustomCoverDialog`).
4. **Niente riuso diretto per candidati/focal-point.** `CoverPagePicker` è un page-number picker propose→approve, **senza focal-point**. `CoverImagePicker` ha 3 tab placeholder/pdf/upload — concetto diverso dai candidati Pdf/Bgg/Wikidata/Manual — e **senza chip provenienza/licenza né griglia**. La griglia candidati e il focal-point control sono **new-build**.

## 2. Decisioni ratificate

- **Mount = hero + card** (scelta esplicita: coprire entrambe le superfici pubbliche di D3).
- **Meccanica DS (card) = slot opzionale additivo, zero-diff quando assente.** Aggiungo `coverEditSlot?: ReactNode` a `MeepleCardProps` → `GridCard`. Quando assente (tutti i consumer attuali) l'output è byte-identico a oggi ⇒ zero blast radius, test verdi. Quando presente + `href`, `GridCard` avvolge `<Link>` + slot in un wrapper `relative` e monta lo slot come **sibling del Link** (fuori dall'anchor → niente nested-interactive); `group` e hover-lift si spostano sul wrapper così l'affordance traccia il sollevamento della card. `MeepleCardGame` inoltra il prop; l'iniezione avviene in `shared-games-grid.tsx`, che calcola `isEditorOrAbove` una volta sola e passa lo slot **solo agli admin** (non-admin ⇒ `undefined` ⇒ DOM invariato). I due test di enforcement non si attivano (nessun nuovo adapter `*Card`, nessuno star-glyph hand-rolled; `meeple-card/**` è escluso dagli scan).
- **Meccanica DS (hero) = slot opzionale.** Aggiungo `coverOverlay?: ReactNode` a `detail-layout/hero.tsx`; iniezione role-gated in `page-client.tsx`.
- **Affordance condivisa.** `AdminCoverEditAffordance(gameId, title)` possiede il `<button>` matita (stile `EditCoverOverlay`) + apre `AdminCoverSourceDialog`. Il DS (card/hero) espone solo uno slot cieco: non conosce admin/cover/dialog. Stesso nodo iniettato in entrambi gli slot.
- **Gate = progressive enhancement, non boundary.** L'affordance è gated FE via `useAdminRole().isEditorOrAbove` (SD5), ma il server ri-autorizza ogni mutation (`AdminOrEditorPolicy`). `useCurrentUser` ha `staleTime:0` → l'affordance appare post-hydration (flash accettabile).
- **Dialog.** Shell Radix `Dialog`; tab per-contesto Card/Hero/Social; candidati come thumbnail selezionabili con chip provenienza (`source`) + chip licenza; render **solo `previewUrl`** (R2 presigned, mai host BGG). `CoverFocalPointPicker`: dot draggabile normalizzato 0..1, keyboard-accessible, sopra l'anteprima del candidato scelto → alimenta `focalX/focalY`.

## 3. Contratto BE (verificato sui record mergiati)

Base FE via proxy Next: `/api/v1/admin/shared-games/{id}/...`. Enum serializzati come **nomi PascalCase** (`JsonStringEnumConverter` globale, nessuna naming policy sull'enum). `PropertyNamingPolicy=CamelCase`; **nessun `DefaultIgnoreCondition`** ⇒ i null sono emessi (campi nullable sempre presenti con valore `null`).

**Enum**
- `CoverAssignmentSource` = `"Pdf" | "Bgg" | "Wikidata" | "Manual"`.
- `CoverContext` = `"Card" | "Hero" | "Social"`.

**GET** `/admin/shared-games/{id}/cover-candidates` → 200 `CoverCandidatesDto`, 404 se gioco assente. Solo sorgenti MATERIALIZZATE.
```
CoverCandidatesDto {
  gameId: string (guid)                       // req
  candidates: CoverCandidateDto[]             // req (può essere vuoto)
  assignments: {                              // req (oggetto sempre presente)
    card:   "Pdf"|"Bgg"|"Wikidata"|"Manual" | null   // sempre presente, nullable
    hero:   ... | null
    social: ... | null
  }
}
CoverCandidateDto {
  source: "Pdf"|"Bgg"|"Wikidata"|"Manual"     // req
  previewUrl: string                          // req, non-null (presigned R2)
  license: string | null                      // nullable (emesso null)
  attribution: string | null                  // nullable
  sourceUrl: string | null                    // nullable
}
```

**PUT** `/admin/shared-games/{id}/cover-assignments/{context}` (context path = `Card|Hero|Social`), body `{ source, focalX=0.5, focalY=0.5 }` → 200 `CoverAssignmentDto`, 400, 404. `AdminOrEditorPolicy`.
```
CoverAssignmentDto { context: CoverContext, source: CoverAssignmentSource, focalX: number, focalY: number }  // tutti req
```

**DELETE** `/admin/shared-games/{id}/cover-assignments/{context}` → 204 (idempotente).

**Mappatura Zod (nullability):** `previewUrl`/`source`/`gameId`/`candidates`/`assignments`/`context`/`focalX`/`focalY` → required. `license`/`attribution`/`sourceUrl` e `assignments.card/hero/social` → `.nullable().optional()` (nullable-sempre-emessi; `.optional()` difensivo verso mock e futuri `WhenWritingNull`).

## 4. Piano incrementi (4 PR mergeable → `main-dev`, TDD, review adversarial ciascuna)

| PR | Contenuto | Rischio | Dipende da |
|----|-----------|---------|------------|
| 1 — data layer | Zod (`schemas/admin/admin-cover.schemas.ts`) + `clients/admin/adminCoverClient.ts` (get/assign/remove) + hook react-query (`hooks/admin/`: candidates query + assign/remove mutation, key factory, invalidate + optimistic) + unit test | Basso | — |
| 2 — dialog+affordance | `components/features/cover-editor/`: `AdminCoverSourceDialog` + `CoverFocalPointPicker` + `AdminCoverEditAffordance` + test (MSW/hook mockati) + eventuale story | Medio | 1 |
| 3 — hero mount | slot `coverOverlay` su `detail-layout/hero.tsx` + iniezione role-gated in `(public)/shared-games/[id]/page-client.tsx` + test | Basso-Medio | 2 |
| 4 — card DS | `coverEditSlot` su `MeepleCard`/`GridCard` (+ forward `MeepleCardGame`) + iniezione in `shared-games-grid.tsx` + test (branch slot, href+slot no nested-interactive, non-admin no-wrapper) | Medio (isolato) | 2 |

Sequenza 1 → 2 → 3 → 4: il valore hero atterra presto (PR3), il pezzo DS a rischio è isolato in coda (PR4).

## 5. Vincoli (tutte le PR)

- **BGG asset ban (#2123)**: solo `previewUrl`/`coverUrl`, mai host BGG (`cf.geekdo-images.com` ecc.); gate ESLint `local/no-bgg-host` + `pnpm lint:bgg`.
- **Design token semantici**: `bg-background`/`bg-card`/`text-foreground`/entity utilities; mai colori hardcoded (ESLint `local/no-hardcoded-color-utility` = error).
- **a11y AA blocking (axe)**: nessun fail color-contrast/ARIA; l'affordance sulla card DEVE restare fuori dall'anchor (no nested-interactive).
- **TDD** (Vitest), un incremento mergeable per volta.
