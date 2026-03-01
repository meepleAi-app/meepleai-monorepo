# Piano Implementazione — EntityLink + GameToolkit + Card Navigation

> **Creato**: 2026-02-23
> **Epic**: #5127 (A), #5128 (B), #5129 (C)
> **Issue totali**: 36
> **Design spec**: `docs/frontend/entity-link-card-relationships.md`

---

## Visione d'insieme

```
EPIC A (backend)     EPIC B (frontend/toolkit)     EPIC C (frontend/links)
main-dev             frontend-dev                   frontend-dev

A1→A2→A3            B1→B2→B3                       [attende A8 completata]
         ↓                   ↓
    A4,A5,A6,A7         B4,B5                       C3→C4
         ↓                   ↓                           ↓
      A8,A9            B6,B7,B8,B9,B10,B11          C1,C2,C5,C6,C7
         ↓                   ↓                           ↓
  A10,A11,A12,A13         B12                           C8
         ↓                   ↓                           ↓
        A14                 B13                          C9
```

**Epic A e B possono partire in parallelo.**
**Epic C parte dopo A8 (endpoints user disponibili).**

---

## FASE 1 — Fondamenta Backend + Toolkit Domain

> **Parallelizzabile**: A e B simultaneamente su branch diversi

### 🔵 Epic A — Sprint 1: BC + Domain

```bash
# Branch: feature/issue-5130-entityrelationships-bc
/implementa 5130
# Risultato: EntityRelationships BC scaffold + DDD folders

# Branch: feature/issue-5131-entitylink-aggregate
/implementa 5131
# Risultato: EntityLink aggregate + EntityLinkType enum

# Branch: feature/issue-5132-entitylinks-migration
/implementa 5132
# Risultato: EF Core config + migration entity_links
```

**DoD Fase 1A**: BC creato, domain testato, migration applicata su dev DB.

---

### 🟢 Epic B — Sprint 1: Toolkit Domain (parallelo con A)

```bash
# Branch: feature/issue-5144-toolkit-domain
/implementa 5144
# Risultato: Toolkit + ToolkitWidget domain + WidgetType enum

# Branch: feature/issue-5145-toolkit-migration
/implementa 5145
# Risultato: EF migration toolkits + toolkit_widgets
```

**DoD Fase 1B**: Toolkit domain testato, migration applicata.

---

## FASE 2 — CQRS Layer

> A3 (migration) completata → si sblocca A4,A5,A6,A7 in parallelo
> B2 (migration) completata → si sblocca B3,B4,B5 in parallelo

### 🔵 Epic A — Sprint 2: Commands + Queries (4 in parallelo)

```bash
# Branch: feature/issue-5133-create-entitylink-command
/implementa 5133
# Risultato: CreateEntityLinkCommand + Validator + Handler

# Branch: feature/issue-5134-delete-entitylink-command
/implementa 5134
# Risultato: DeleteEntityLinkCommand + Handler

# Branch: feature/issue-5135-get-entitylinks-query
/implementa 5135
# Risultato: GetEntityLinksQuery con supporto bidirezionalità

# Branch: feature/issue-5136-get-entitylink-count
/implementa 5136
# Risultato: GetEntityLinkCountQuery per badge
```

**Strategia**: Aprire 4 branch contemporaneamente, implementare in parallelo, merge in sequenza su main-dev.

---

### 🟢 Epic B — Sprint 2: Auto-create + Queries (3 in parallelo)

```bash
# Branch: feature/issue-5146-toolkit-autocreate
/implementa 5146
# Risultato: Auto-create default Toolkit on Game add to library

# Branch: feature/issue-5147-toolkit-queries
/implementa 5147
# Risultato: GetToolkitQuery + OverrideToolkitCommand

# Branch: feature/issue-5148-toolkit-session-state
/implementa 5148
# Risultato: ToolkitSessionState in SessionTracking BC
```

---

## FASE 3 — Endpoints REST

> A4-A7 merge completati → si sbloccano A8, A9

### 🔵 Epic A — Sprint 3: Endpoints (2 in parallelo)

```bash
# Branch: feature/issue-5137-user-entitylinks-endpoints
/implementa 5137
# Risultato: GET/POST/DELETE /api/v1/library/entity-links

# Branch: feature/issue-5138-admin-entitylinks-endpoints
/implementa 5138
# Risultato: GET/POST/DELETE /api/v1/admin/entity-links
```

> ⚠️ **Checkpoint**: Dopo A8 merge → Epic C può iniziare Sprint 1

---

## FASE 4 — Fix di Business Rules + BGG Import + adminClient

> A8, A9 completate → si sbloccano A10, A11, A12, A13 in parallelo

### 🔵 Epic A — Sprint 4: Business Rules + Integrations

```bash
# Branch: feature/issue-5139-session-games-required
/implementa 5139
# Risultato: Fix Session.Games da opzionale a 1..*
# ⚠️ ATTENZIONE: migration con gestione dati esistenti

# Branch: feature/issue-5140-agent-kbcard-validation
/implementa 5140
# Risultato: Fix Agent validazione min 1 KbCard dal Game

# Branch: feature/issue-5141-bgg-expansion-importer
/implementa 5141
# Risultato: BggExpansionImporter + admin endpoint trigger

# Branch: feature/issue-5142-entitylink-client-methods
/implementa 5142
# Risultato: adminClient + userClient methods EntityLink
```

---

## FASE 5 — Componenti Frontend EntityLink (Epic C)

> **Prerequisito**: A8 completata (endpoint user disponibile)
> **Branch target**: frontend-dev

### 🔴 Epic C — Sprint 1: Componenti atomici (3 in parallelo)

```bash
# Branch: feature/issue-5159-entitylink-chip
/implementa 5159
# Risultato: EntityLinkChip (8 link types, colori, direzionalità)
# PRIMO: è dipendenza di C1,C2,C4,C5,C6,C7

# Una volta C3 mergiato:

# Branch: feature/issue-5157-entitylink-badge
/implementa 5157
# Risultato: EntityLinkBadge + nuovi props MeepleCard

# Branch: feature/issue-5160-entitylink-card
/implementa 5160
# Risultato: EntityLinkCard mini-card
```

---

### 🔴 Epic C — Sprint 2: Sezioni composite (3 in parallelo)

```bash
# Branch: feature/issue-5158-entitylink-preview-row
/implementa 5158
# Risultato: EntityLinkPreviewRow nel footer card

# Branch: feature/issue-5161-related-entities-section
/implementa 5161
# Risultato: RelatedEntitiesSection + Links tab nel Drawer

# Branch: feature/issue-5162-add-entity-link-modal
/implementa 5162
# Risultato: AddEntityLinkModal con search autocomplete
```

---

## FASE 6 — Widget Toolkit (Epic B)

> B3-B5 completati → widget possono partire in parallelo

### 🟢 Epic B — Sprint 3: Widget (6 in parallelo)

```bash
# Branch: feature/issue-5149-widget-random-generator
/implementa 5149
# Risultato: RandomGenerator widget

# Branch: feature/issue-5150-widget-turn-manager
/implementa 5150
# Risultato: TurnManager widget

# Branch: feature/issue-5151-widget-score-tracker
/implementa 5151
# Risultato: ScoreTracker widget

# Branch: feature/issue-5152-widget-resource-manager
/implementa 5152
# Risultato: ResourceManager widget

# Branch: feature/issue-5153-widget-note-manager
/implementa 5153
# Risultato: NoteManager widget

# Branch: feature/issue-5154-widget-whiteboard
/implementa 5154
# Risultato: Whiteboard widget
```

**Strategia**: I 6 widget sono completamente indipendenti tra loro. Implementare in sprint separati o tutti assieme se le risorse lo permettono.

---

## FASE 7 — Graph View + Nav Graph + toolkit Card

### 🔴 Epic C — Sprint 3: Graph View

```bash
# Branch: feature/issue-5163-entity-relationship-graph
/implementa 5163
# Risultato: EntityRelationshipGraph con React Flow + toggle list/graph
# Dipendenza: @xyflow/react (installare con pnpm add @xyflow/react)
```

### 🔴 Epic C — Sprint 4: Navigation Graph

```bash
# Branch: feature/issue-5164-nav-graph-update
/implementa 5164
# Risultato: ENTITY_NAVIGATION_GRAPH aggiornato (collection, event, toolkit)
```

### 🟢 Epic B — Sprint 4: toolkit Card

```bash
# Branch: feature/issue-5155-toolkit-entity-type
/implementa 5155
# Risultato: toolkit MeepleEntityType + Card + Drawer
```

---

## FASE 8 — Tests

> Tutte le implementazioni complete → test di chiusura

### Test finali (3 in parallelo)

```bash
# Branch: feature/issue-5143-tests-epic-a
/implementa 5143
# Risultato: 70+ test Epic A (unit + integration + E2E)

# Branch: feature/issue-5156-tests-epic-b
/implementa 5156
# Risultato: 54+ test Epic B (GameToolkit)

# Branch: feature/issue-5165-tests-epic-c
/implementa 5165
# Risultato: 37+ test unit + 7 E2E (Card Navigation)
```

---

## Riepilogo Sprint Timeline

| Sprint | Issue | Dipende da | Branch |
|--------|-------|-----------|--------|
| **S1** | A1(#5130), A2(#5131), B1(#5144) | — | main-dev, frontend-dev |
| **S2** | A3(#5132), B2(#5145) | S1 | main-dev, frontend-dev |
| **S3** | A4-A7(#5133-5136), B3-B5(#5146-5148) | S2 | main-dev, frontend-dev |
| **S4** | A8(#5137), A9(#5138) | S3(A) | main-dev |
| **S5** | C3(#5159) | S4(A8) | frontend-dev |
| **S6** | A10-A13(#5139-5142), C1(#5157), C4(#5160) | S4,S5 | main-dev, frontend-dev |
| **S7** | B6-B11(#5149-5154), C2(#5158), C5(#5161), C6(#5162) | S3(B), S5 | frontend-dev |
| **S8** | C7(#5163), C8(#5164), B12(#5155) | S6,S7 | frontend-dev |
| **S9** | A14(#5143), B13(#5156), C9(#5165) | tutti | main-dev, frontend-dev |

---

## Checklist Pre-Implementazione

Per ogni `/implementa {issue}`:

```
□ Leggere design spec: docs/frontend/entity-link-card-relationships.md
□ Verificare dipendenze mergirate
□ git checkout {parent-branch} && git pull
□ git checkout -b feature/issue-{N}-{desc}
□ git config branch.feature/issue-{N}-{desc}.parent {parent}
□ Implementare seguendo pattern CQRS (backend) / componenti atomici (frontend)
□ dotnet test / pnpm test — verde prima del PR
□ PR → parent branch (NON main)
□ Chiudere issue dopo merge
```

---

## Comandi di avvio rapido

```bash
# Inizia con Epic A (backend, main-dev)
git checkout main-dev && git pull
/implementa 5130  # A1: BC scaffold

# Inizia con Epic B in parallelo (frontend-dev)
git checkout frontend-dev && git pull
/implementa 5144  # B1: Toolkit domain
```

---

## Note tecniche

| Area | Nota |
|---|---|
| `@xyflow/react` | Installare prima di C7: `cd apps/web && pnpm add @xyflow/react` |
| A10 (Session.Games) | Migration NOT NULL — gestire dati esistenti con default temporaneo |
| A12 (BGG) | BGG API pubblica, no auth richiesta, rate limit: 1 req/2s |
| C7 (React Flow) | Usare `@xyflow/react` v12+, non la versione legacy `reactflow` |
| B11 (Whiteboard) | Usare `react-sketch-canvas` (leggero) o `tldraw` (completo) |
| EntityLinkChip | Implementare PRIMA di tutti gli altri componenti C (è dipendenza comune) |
