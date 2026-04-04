# Piano Migrazione Layout/Stile v0app → Main App + MeepleCard Integration

**Data**: 2026-04-03
**Scope**: `apps/web/` — design tokens, layout system, MeepleCard adoption

## Gap Analysis

| Dominio | v0app (mock) | Main App (attuale) | Action Required |
|---------|-------------|-------------------|-----------------|
| **Font** | Quicksand + Nunito | Quicksand + Inter | Sostituire Inter → Nunito |
| **Entity colors CSS vars** | ✅ In globals.css + @theme | ❌ Solo in JS (meeple-card-styles.ts) | Aggiungere CSS vars |
| **@theme entity-* utilities** | ✅ `bg-entity-game` funziona | ❌ Non funzionano | Aggiungere @theme tokens |
| **shadow-warm** | ✅ CSS var + utility | ❌ Non presenti | Aggiungere |
| **TopNavbar stile** | Logo hex + nav links + search expand + avatar ring | UserTopNav struttura esistente | Allineare stile |
| **MiniNav (Tier 2)** | ✅ Componente standalone | ❌ Assente | Creare `ContextMiniNav` |
| **FloatingActionPill (Tier 3)** | ✅ Desktop pill + mobile bottom bar | ❌ Assente | Portare componente |
| **MeepleCard in pagine** | Versione semplificata custom | ✅ Componente completo | Sostituire con il reale |

## Milestone

### M1 — Foundation CSS (prerequisito tutto il resto)
- [ ] `layout.tsx`: sostituire `Inter` con `Nunito`
- [ ] `design-tokens.css`: aggiungere entity colors + warm shadows
- [ ] `globals.css` @theme: esporre entity color utilities

### M2 — TopNavbar alignment
- [ ] `UserTopNav.tsx`: allineare styling a v0app

### M3 — Nuovi componenti layout
- [ ] Creare `ContextMiniNav.tsx` (port da v0app/components/mini-nav.tsx)
- [ ] Creare `FloatingActionPill.tsx` (port da v0app/components/floating-action-pill.tsx)

### M4 — MeepleCard adoption nelle pagine
- [ ] Library page: `<MeepleCard entity="game" variant="grid" />`
- [ ] Game detail header: `<MeepleCard entity="game" variant="featured" />`

## File da toccare

| File | Operazione | Rischio |
|------|-----------|---------|
| `apps/web/src/app/layout.tsx` | Inter → Nunito | Basso |
| `apps/web/src/styles/design-tokens.css` | Aggiungere entity colors + shadows | Basso |
| `apps/web/src/styles/globals.css` | Aggiungere @theme entity tokens | Basso |
| `apps/web/src/components/layout/UserShell/UserTopNav.tsx` | Allineare stile | Medio |
| `apps/web/src/components/layout/ContextMiniNav.tsx` | **Creare** (nuovo) | Basso |
| `apps/web/src/components/layout/FloatingActionPill.tsx` | **Creare** (nuovo) | Basso |
| `apps/web/src/app/(authenticated)/library/` | Adottare MeepleCard reale | Medio |
| `apps/web/src/components/layout/index.ts` | Esportare nuovi componenti | Basso |

## Token CSS da aggiungere

### design-tokens.css
```css
--entity-game:    hsl(25, 95%, 45%);
--entity-player:  hsl(262, 83%, 58%);
--entity-session: hsl(240, 60%, 55%);
--entity-agent:   hsl(38, 92%, 50%);
--entity-kb:      hsl(174, 60% 40%);
--entity-chat:    hsl(220, 80%, 55%);
--entity-event:   hsl(350, 89%, 60%);
--entity-toolkit: hsl(142, 70%, 45%);
--shadow-warm:    0 4px 20px rgba(180, 130, 80, 0.12);
--shadow-warm-lg: 0 8px 32px rgba(180, 130, 80, 0.15);
```

### globals.css @theme block
```css
--color-entity-game:    var(--entity-game);
--color-entity-player:  var(--entity-player);
--color-entity-session: var(--entity-session);
--color-entity-agent:   var(--entity-agent);
--color-entity-kb:      var(--entity-kb);
--color-entity-chat:    var(--entity-chat);
--color-entity-event:   var(--entity-event);
--color-entity-toolkit: var(--entity-toolkit);
```

## Decisioni architetturali

1. **MiniNav**: contestuale alla pagina, NON nel layout shell globale
2. **FloatingActionPill**: componente specifico di contesto (game detail), non nel layout globale
3. **Entity colors**: duplicazione accettata tra JS (meeple-card-styles.ts) e CSS (design-tokens.css)

## Componenti v0app da NON portare
- `v0app/components/ui/meeple-card.tsx` — usa il reale MeepleCard della main app
- `v0app/components/library/meeple-card.tsx` — usa il reale MeepleCard della main app
- `v0app/app/layout.tsx` — main app ha il suo sistema
- `v0app/components/theme-provider.tsx` — main app ha già ThemeProvider
