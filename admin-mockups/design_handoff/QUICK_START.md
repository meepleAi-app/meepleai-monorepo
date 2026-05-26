# Quick Start — Claude Code Setup

> Da fare PRIMA di lanciare qualsiasi prompt di feature.

## 1. Verifica prerequisiti nel codebase

Apri Claude Code e incolla:

```
Verifica che il progetto abbia:
- [ ] React 18 + TypeScript
- [ ] Stack di routing (Next.js app/pages o Router)
- [ ] Data fetching layer (TanStack Query, SWR, Apollo, RTK Query)
- [ ] Stato globale (Zustand, Redux, Jotai, Context API)
- [ ] CSS approach (Tailwind, CSS modules, styled-components, vanilla CSS)
- [ ] Test runner (Vitest, Jest, Playwright)
- [ ] Lint + format (ESLint, Prettier, Biome)
- [ ] Storybook o equivalente

Per ognuno, dimmi versione + dove configurato.
Se mancante, segnala. NON installare niente ora.
```

## 2. Importa il design system

```
Copia design/tokens.css → src/styles/tokens.css
Copia design/components.css → src/styles/components.css

Aggiungi i font Google a index.html:
<link rel="preconnect" href="https://fonts.googleapis.com"/>
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
<link href="https://fonts.googleapis.com/css2?family=Quicksand:wght@400;500;600;700&family=Nunito:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;600;700;800&display=swap" rel="stylesheet"/>

Importa tokens.css + components.css nell'entry (src/main.tsx, _app.tsx, layout.tsx).

Aggiungi data-theme="light" su <html>.

Conferma: nessun lint error, build passa.
```

## 3. Crea entity-color helper

```
Crea src/lib/entity-color.ts copiando il code in design_handoff/DESIGN_TOKENS.md
sezione "Helper TypeScript".

Aggiungi test base in src/lib/entity-color.test.ts:
- entityColor('game') restituisce 'hsl(28, 84%, 55%)'
- entityColor('agent', 0.12) restituisce 'hsla(38, 90%, 56%, 0.12)'
- Type EntityType è strettamente 9 valori
```

## 4. Genera tipi entità

```
Esegui il prompt 0.2 da BACKEND_PROMPTS.md (Genera tipi backend dai mock).

Output atteso: src/types/entities.ts con 14 types principali
(Game, Player, Session, Agent, KB, Chat, Event, Toolkit, Tool,
HouseRule, GameNight, RSVP, Notification, UserPreferences).
```

## 5. Audit del backend

```
Esegui il prompt 0.3 da BACKEND_PROMPTS.md (Schema DB consistency).

Output: design_handoff/SCHEMA_DIFF.md
```

## 6. Audit dei componenti UI già esistenti

```
Per ogni componente del design system MeepleAI:
- ConnectionBar
- MeepleCard
- EntityChip
- Drawer (con cascadeNavigationStore)
- Tabs (animated underline)
- MobileBottomBar
- RecentsBar

Verifica se esiste già in src/. Se sì, dimmi il path e la firma TS.
Se no, segnalalo come "da creare".

Lista anche i nuovi componenti v2 emersi dai mock:
- HouseRuleDrawer
- ConfidenceBadge
- SuggestedQueriesRow
- ChatHistoryTimeline
- KbDocList
- CitationExpandedPanel
- StepIndicator
- ConfirmModal

Per ognuno: trovato? Path? Da creare?

Output: design_handoff/COMPONENTS_AUDIT.md
```

## 7. Pilot screen

Adesso che il foundation è pronto, scegli UNA schermata pilot. Consigliato:

**Pilot suggerito: `/games/[id]` (sp4-game-detail)**

Perché:
- È P0 (critical)
- Ha varietà di stati (default/loading/empty/error)
- Usa ConnectionBar, EntityChip, Tabs (vedi se già esistono)
- Non richiede realtime (WebSocket/SSE)
- Backend likely già esistente (GET /api/games/{id})

```
Implementiamo /games/[id] come pilot.
Usa il prompt 4.1 da BACKEND_PROMPTS.md.
Procedi PASSO PASSO mostrando i diff prima di applicare.
```

## 8. Iter

Una volta validato il pilot, scegli la prossima in base a SCREENS.md → roadmap consigliata.

---

## Checklist setup completato

- [ ] Codebase audit fatto (design_handoff/CODEBASE_AUDIT.md)
- [ ] tokens.css + components.css importati e build passa
- [ ] entity-color.ts con test
- [ ] src/types/entities.ts generato
- [ ] design_handoff/SCHEMA_DIFF.md generato
- [ ] design_handoff/COMPONENTS_AUDIT.md generato
- [ ] Pilot screen (`/games/[id]`) implementata + revisionata
- [ ] PR pilot mergiata

Solo dopo questa checklist, parti con gli altri screen.
