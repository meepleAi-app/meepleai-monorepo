# MeepleCard — Bug Fixes & Quality Improvements

**Date:** 2026-04-04
**Status:** Approved
**Precedence:** Builds on `2026-04-02-meeplecard-redesign-design.md` (visual) and `2026-04-03-meeplecard-adapter-migration-design.md` (adapter layer).
**Goal:** Correggere i bug visibili/silenziosi e ridurre il debito tecnico nel sistema MeepleCard senza stravolgere l'architettura esistente.

---

## 1. Bug Critici

### BUG-1 — Bottom bar sempre renderizzata (24px dead space)

**File:** `variants/MeepleCardGrid.tsx` ~linea 579
**Sintomo:** Il div `bottomBar` viene sempre renderizzato a 24px di altezza, anche quando `bottomStatValue` è `undefined`. Risultato: spazio bianco fisso in basso su ogni card.

**Causa:**
```tsx
// ❌ ATTUALE — div sempre presente
<div style={{ height: `${CARD_SECTION_HEIGHTS.bottomBar}px` }}>
  <span /> {/* sempre vuoto */}
  {bottomStatValue && <span>{bottomStatLabel}: {bottomStatValue}</span>}
</div>
```

**Fix:**
```tsx
// ✅ CORRETTO — renderizzato solo se c'è contenuto
{bottomStatValue && (
  <div style={{ height: `${CARD_SECTION_HEIGHTS.bottomBar}px`, ... }}>
    {bottomStatLabel && <span>{bottomStatLabel}:</span>}
    <span>{bottomStatValue}</span>
  </div>
)}
```

**Impatto:** Tutte le card senza `bottomStatValue` guadagnano 24px di altezza utile per la TextBox o ManaLinkFooter.

---

### BUG-2 — `--hover-color` CSS var impostata ma mai consumata

**File:** `meeple-card-quick-actions.tsx` ~linea 143
**Sintomo:** Gli icon button impostano `style={{ '--hover-color': entityColor }}` sul nodo, ma nessuna regola CSS consuma questa variabile. Il colore hover degli icon è sempre bianco statico invece che entity-colored.

**Causa:**
```tsx
// Impostata ma mai consumata
<button style={{ '--hover-color': `hsl(${entityHsl})` }}>
  <Icon />
</button>
```

**Fix:** Aggiungere in `globals.css` (o nel componente con `<style>`) la regola:
```css
.mc-quick-action:hover svg,
.mc-quick-action:focus-visible svg {
  color: var(--hover-color, white);
}
```

In alternativa, applicare il colore inline con Tailwind via `group-hover` passando la classe generata dal token HSL:
```tsx
// Via className con CSS var consumption nel componente stesso
<button
  className="mc-quick-action group"
  style={{ '--hover-color': `hsl(${entityHsl})` } as React.CSSProperties}
>
  <Icon className="transition-colors group-hover:text-[var(--hover-color)]" />
</button>
```

---

### BUG-3 — DeckStack aperto con items vuoti (UX failure silenziosa)

**Status: GIÀ FIXATO in `main-dev`** tramite migrazione a `useCascadeNavigationStore`.
Documentato qui per completezza. Verificare che `ManaLinkFooter.tsx` usi `useCascadeNavigationStore` e non il vecchio stato locale.

---

## 2. Miglioramenti Importanti

### IMP-1 — `key={index}` su array di metadati

**File:** `variants/MeepleCardGrid.tsx` (e altri variant)
**Sintomo:** Dove si renderizza un array di metadata chips/pills usando `index` come key React, si introduce potenziale glitch di reconciliation quando l'array cambia ordine.

**Fix:** Usare `key={item.label}` o `key={item.id}` invece di `key={index}`.

```tsx
// ❌ Fragile
{metadata.map((item, index) => <MetadataChip key={index} {...item} />)}

// ✅ Stabile
{metadata.map((item) => <MetadataChip key={item.label} {...item} />)}
```

---

### IMP-2 — `CartaEstesa` disconnessa dal sistema entity

**File:** `CartaEstesa.tsx`
**Sintomo:** Usa emoji `🎲` hardcoded per ogni entità e non importa `MeepleEntityType` né `entityColors`. Di fatto è un componente separato che non rispetta il sistema visivo unificato.

**Fix:** Aggiungere `entity: MeepleEntityType` come prop e usare `entityColors[entity]` per colorare il frame, e le icone entity-specific per la TypeLine. Non deve essere un redesign completo — solo allineamento al token system.

> **Nota implementativa:** `entityColors` esiste in `meeple-card-styles.ts`. Non esiste ancora una mappa `entityIcons` centralizzata — icone simili sono in `compact-icon-bar.tsx` (`ENTITY_ICON_CONFIG`) e `entity-link-card.tsx` (`ENTITY_ICONS`). L'implementazione dovrà aggiungere una mappa emoji/icona in `meeple-card-styles.ts` o importare da `navigation-icons.tsx`.

```tsx
// ❌ Attuale
const icon = '🎲';
const color = '#c8963a';

// ✅ Corretto
import { entityColors } from '../meeple-card-styles';
// icona da navigation-icons.tsx o da nuova mappa da aggiungere a meeple-card-styles.ts
const { hsl } = entityColors[entity];
```

---

### IMP-3 — `viewTransitionName` senza feature detection

**File:** `variants/MeepleCardGrid.tsx` ~viewTransitionName
**Sintomo:** `viewTransitionName` viene assegnato direttamente senza verificare il supporto browser. Su browser che non supportano View Transitions API, non causa errori ma genera warning in console.

**Fix:** Condizionare l'assegnazione:
```tsx
const supportsViewTransition = typeof document !== 'undefined'
  && 'startViewTransition' in document;

style={{
  viewTransitionName: supportsViewTransition && entityId
    ? `meeple-card-${entityId}`
    : undefined,
}}
```

---

### IMP-4 — `isInteractive` logica contro-intuitiva

**File:** `types.ts` e consumer
**Sintomo:** La prop si chiama `isInteractive` ma il comportamento di default non è documentato. In alcuni contesti il default è `true`, in altri `false`, portando a inconsistenze.

**Fix:** Documenta il default esplicito nel tipo e nei renderer:
```tsx
interface MeepleCardProps {
  /** @default true — disabilitare in contesti di sola lettura (es. preview) */
  isInteractive?: boolean;
}
```
E nei variant: `const interactive = isInteractive ?? true;`

---

## 3. Miglioramenti Accessibilità

### ACC-1 — ARIA label non localizzabile

**File:** `meeple-card-quick-actions.tsx`
**Sintomo:** `aria-label="Open card actions"` è hardcoded in inglese. L'app supporta potenzialmente multilingua.

**Fix:** Accettare la label come prop con default inglese:
```tsx
interface QuickActionsProps {
  ariaLabel?: string; // default: 'Open card actions'
}
```

Oppure, a breve termine, usare `aria-label` in italiano coerente con il resto dell'UI: `"Azioni rapide"`.

---

## 4. Non-scope (esplicitamente escluso)

Le seguenti issue, sebbene identificate, **non fanno parte di questo spec** perché coprono refactor architetturali più ampi già gestiti dalle spec precedenti:

- **Props God Object** (~100 props in `MeepleCardProps`): affrontato nella spec adapter-migration (04-03) tramite layer mapper.
- **Open/Closed violation in `MeepleCardGrid`**: affrontato nella spec redesign (04-02) tramite entity-specific renderer strategy.
- **Split architettura incompleta** (file fuori da `meeple-card/`): scope della spec adapter-migration.
- **`--tw-ring-color` fragilità Tailwind v4**: da affrontare nel momento della migrazione a Tailwind v4.

---

## 5. File da modificare

| File | Modifiche |
|------|-----------|
| `variants/MeepleCardGrid.tsx` | BUG-1, IMP-1, IMP-3 |
| `meeple-card-quick-actions.tsx` | BUG-2, ACC-1 |
| `CartaEstesa.tsx` | IMP-2 |
| `types.ts` | IMP-4 (doc default) |
| `globals.css` (o stile locale) | BUG-2 (CSS var rule) |

---

## 6. Criteri di accettazione

- [ ] Nessun div bottomBar renderizzato quando `bottomStatValue` è undefined
- [ ] Gli icon button mostrano il colore entity al hover (visibile in DevTools + visualmente)
- [ ] `CartaEstesa` usa `entityColors` e `entityIcons` dal token system
- [ ] Nessun `key={index}` su array di contenuto variabile in MeepleCard
- [ ] `isInteractive` ha default documentato e coerente
- [ ] `viewTransitionName` assegnato solo se browser lo supporta
- [ ] Test unitari per BUG-1 (renderizzazione condizionale) e IMP-1 (key stabile)
