# ConnectionChip a11y + semantic fixes (Step 1.5)

**Data**: 2026-04-23
**Issue**: [#543](https://github.com/meepleAi-app/meepleai-monorepo/issues/543)
**Parent PR**: #542 (merged in `bf372d5bc`)
**Predecessore logico**: Step 1 (primitives creati) → **Step 1.5 = questo spec** → Step 1.6 (renderer integration)

## 1. Contesto

Il code review post-merge di PR #542 (MeepleCard ConnectionChip primitives) ha identificato 5 issue da risolvere prima di integrare le primitive nel renderer `MeepleCard.tsx` (Step 1.6). Queste sono **bloccanti** per l'integrazione perché riguardano semantica del componente e accessibilità.

### Findings da code review (`feature-dev:code-reviewer`)

**Critical**:
1. `ConnectionChip.tsx:165` — il ramo `<Link>` renderizza `next/link` anche quando `onCreate` è set con `count=0`, creando un "double-trigger" (sia navigazione via href che callback create). Il commento in `handleActivate:138-146` dice che `onCreate` deve avere precedenza quando count=0, ma il branching non lo rispetta.

**Important**:
2. `ConnectionChip.tsx:148-152` — `aria-label` usa `count` grezzo ("150 things") ma il badge visivo mostra "99+". Viola WCAG 2.1 SC 1.3.3 (Info and Relationships) perché screen reader legge un numero diverso da quello mostrato.
3. `ConnectionChipPopover.tsx:43-98` — `PopoverContent` di Radix non ha `aria-labelledby` né nome accessibile. Lo screen reader annuncia solo il ruolo "dialog" senza titolo.

**Test gaps**:
4. Combinazione `href + disabled` non testata. La guard `!disabled` a riga 160 evita il rendering come Link quando disabilitato, ma non c'è regression test.
5. Popover keyboard navigation non testata: Escape chiude + focus return al trigger, Tab cycling tra items.

## 2. Obiettivi

- **Risolvere il Critical** allineando branching alla semantica documentata in `handleActivate`
- **Matchare visuale e a11y label** per conformità WCAG 1.3.3
- **Dare nome accessibile al PopoverContent** tramite `aria-labelledby`
- **Colmare test gap** su `href + disabled` e keyboard nav del popover
- **Zero regressioni** sui 137 test esistenti della suite `meeple-card`

## 3. Design

### 3.1 Semantica `ConnectionChip` — onCreate ha precedenza su href

**Regola**: quando un chip ha `onCreate` set (empty state), la creazione vince sulla navigazione. Ha senso UX: se `count=0` e c'è un `onCreate`, l'utente dovrebbe creare la prima entità, non navigare a una pagina lista vuota.

**Branching attuale** (problema):
```tsx
if (href && !hasItems && !disabled) {
  return <Link href={href} onClick={hasCreate ? () => onCreate?.() : undefined}>...
}
```
Il `onClick` sul Link è un workaround che non previene la navigazione (click default Link non viene cancellato).

**Nuovo branching**:
```tsx
if (href && !hasItems && !hasCreate && !disabled) {
  return <Link href={href}>...</Link>
}
// altrimenti fallback a <button>
```

**Conseguenze**:
- `href + count=0 + onCreate` → `<button>` (invoca `onCreate` via `handleActivate`)
- `href + count=0 + no onCreate` → `<Link>` (naviga, nessun create handler)
- `href + count>0` → `<button>` con `hasItems=true` (apre popover; invariato)
- `href + disabled` → `<button disabled>` (invariato)

### 3.2 `aria-label` matcha il visuale — helper `formatCountForLabel`

**Regola**: il testo annunciato dallo screen reader deve riflettere ciò che l'utente vede. Badge mostra `99+` per `count > 99`, quindi label deve dire "99 or more".

**Helper puro** (in `ConnectionChip.tsx`, top-level):
```ts
function formatCountForLabel(count: number, label: string): string {
  if (count > 99) return `99 or more ${label}`;
  return `${count} ${label}`;
}
```

Usato al posto del template string inline a riga 148-152. Nessuna i18n: stringa hard-coded inglese coerente con il resto del componente.

### 3.3 `PopoverContent` accessibile — `aria-labelledby`

**In `ConnectionChipPopover.tsx`**:

1. Generare id stabile con `useId()`:
   ```tsx
   const headerId = useId();
   ```
2. Applicare al div header (riga ~52):
   ```tsx
   <div id={headerId} className="...">
     <h3>{title}</h3>
   </div>
   ```
3. Passare a `PopoverContent`:
   ```tsx
   <PopoverContent aria-labelledby={headerId}>
   ```

**Risultato**: screen reader annuncia "dialog, {title}" quando il popover apre.

## 4. Test plan (TDD)

**Approccio**: scrivere i test **prima** delle modifiche, verificare che falliscano, poi implementare.

### 4.1 Nuovi test in `ConnectionChip.test.tsx`

1. **`renders as button (not Link) when hasCreate is true and count is 0`**
   Props: `href="/x" count={0} onCreate={fn}`
   Asserzione: `queryByRole('link')` è null, `getByRole('button')` esiste
   Verifica anche che click invochi `onCreate` (non navighi).

2. **`renders as disabled button when href is provided and disabled is true`**
   Props: `href="/x" disabled count={0}`
   Asserzione: `queryByRole('link')` null, `getByRole('button').disabled === true`

3. **`uses "99 or more" in aria-label when count exceeds 99`**
   Props: `count={150} label="items"`
   Asserzione: `getByRole('button')` ha `aria-label` matching `/99 or more items/`
   Snapshot comportamento esistente per `count <= 99` in un secondo test.

### 4.2 Nuovi test in `ConnectionChipPopover.test.tsx`

4. **`PopoverContent has aria-labelledby pointing to header title`**
   Apri popover (trigger click).
   Query popover content via `document.querySelector` (Radix Portal).
   Asserzione: `content.getAttribute('aria-labelledby')` === id del div header.

5. **`pressing Escape closes popover and returns focus to trigger`**
   Apri popover, `fireEvent.keyDown(document.activeElement, { key: 'Escape' })`.
   Asserzione: popover unmounted, `document.activeElement === trigger`.

### 4.3 Regression guard

- Eseguire `pnpm vitest run apps/web/src/components/ui/data-display/meeple-card/` prima del merge
- **Baseline**: 137/137 pass (da PR #542)
- Eventuali fallimenti di test esistenti segnalano regressione → block merge

## 5. Scope & Out of scope

**In scope**:
- 3 modifiche a `ConnectionChip.tsx` (branching + helper + uso helper)
- 2 modifiche a `ConnectionChipPopover.tsx` (useId + labelledby)
- 3 nuovi test in `ConnectionChip.test.tsx`
- 2 nuovi test in `ConnectionChipPopover.test.tsx`

**Out of scope** (deferiti):
- Integrazione primitives in `MeepleCard.tsx` → Step 1.6
- Tab cycling test (gestito nativamente da Radix, non serve test custom)
- i18n delle stringhe "99 or more" → quando si introduce i18n globale
- Migrazione consumer legacy → Step 2

## 6. Rollout

**Branch**: `feature/issue-543-connectionchip-a11y-fixes` (da `main-dev`)
**Target PR**: `main-dev`
**Effort stimato**: ~1 giorno (TDD stretto, scope ristretto)
**Blocca**: Step 1.6 (non procedere a integrazione renderer finché #543 chiuso)

## 7. Riferimenti

- Code review: PR #542 comments (via `feature-dev:code-reviewer`)
- WCAG 2.1 SC 1.3.3: https://www.w3.org/WAI/WCAG21/Understanding/info-and-relationships
- Radix Popover docs: https://www.radix-ui.com/primitives/docs/components/popover
- Spec parent: `docs/superpowers/specs/2026-04-23-meeplecard-connectionchip-design.md`
