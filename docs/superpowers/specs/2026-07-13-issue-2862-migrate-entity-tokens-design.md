# Design — Issue #2862 (C5): migrate variants to canonical entity tokens

**Data:** 2026-07-13
**Issue:** [#2862](https://github.com/meepleAi-app/meepleai-monorepo/issues/2862) (ST8) — umbrella [#2863](https://github.com/meepleAi-app/meepleai-monorepo/issues/2863) "MeepleCard family debt teardown"
**Audit:** `docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md` (§4.4 "la card RI-DERIVA i colori in JS invece di leggere le CSS-var → diverge in dark"; §6 ST8)
**Branch:** `feature/issue-2862-migrate-entity-tokens` da `main-dev`
**Dipendenze:** B3 (Phase B `--c-*` canonici, mergiato) + C1 (#2858, mergiato).

---

## 1. Problema

La verifica sul codice distingue due sistemi (come nelle issue C1-C4, la premessa "migrare `--mc-*` + entityHsl" va calibrata):

1. **Chrome di superficie** — i 13 token `--mc-*` (`--mc-bg-card`, `--mc-border`, `--mc-text-*`, `--mc-shadow-*`, definiti in `design-tokens.css:501-518` + `.dark` 588-605): valori privati "warm glass" (#4604), **theme-aware in CSS** (light + dark). **Nessun bug dark.** Migrarli ai semantici è un redesign visivo, non un fix di debito → **fuori scope** (Q1).
2. **Accenti entità** — `entityHsl()`/`entityHslText()`/`entityTokens()` in `tokens.ts`: emettono stringhe HSL **hardcoded LIGHT-ONLY** (da `entityColors`/`entityTextOverrides` JS). È **esattamente il debito** dell'audit: la card ri-deriva i colori in JS invece di leggere le CSS-var `--c-*` (Phase B, AA-tuned, theme-aware, con `-text` + dark override) → diverge in dark. ~19 call-site su varianti + parts.

## 2. Decisioni (brainstorm 2026-07-13)

1. **Q1 — accenti entità → canonico, tieni `--mc-*`.** Migrare gli accenti entità alle CSS-var `--c-*`; lasciare i `--mc-*` di superficie (theme-aware, deliberati).
2. **Q2 — completa `entityHslText`.** Aggiungere i `--c-*-text` canonici mancanti (event/agent/chat) così `entityHslText` diventa CSS-var single-source.
3. **`entityColors` NON si elimina** (adattamento post-discovery): è esportato e ha **consumer esterni** (`MeeplePlayerStateCard.tsx:52` legge `entityColors.player.h/s/l`; + regression test `tokens.test.ts` #636 su `entityColors` AA). Resta come **palette raw disaccoppiata**; `entityHsl`/`entityHslText` vengono riscritti **indipendenti** da esso.

## 3. Migrazione — riscrivere gli helper, non le call-site

Il 2° argomento di `entityHsl(entity, alpha?)` è **alpha** (`hsla(h,s,l,alpha)`), quindi la sostituzione canonica è pulita.

**`tokens.ts` — riscrivere 2 helper (CSS-var-backed, disaccoppiati da `entityColors`):**

```ts
// entity -> canonical --c-* var name (gameNightEvent reuses event's rose palette)
const CSS_ENTITY: Record<MeepleEntityType, string> = {
  game: 'game', player: 'player', session: 'session', agent: 'agent', kb: 'kb',
  chat: 'chat', event: 'event', toolkit: 'toolkit', tool: 'tool', gameNightEvent: 'event',
};

export function entityHsl(entity: MeepleEntityType, alpha?: number): string {
  const v = `var(--c-${CSS_ENTITY[entity]})`;
  return alpha !== undefined ? `hsl(${v} / ${alpha})` : `hsl(${v})`;
}

// entities with a canonical --c-*-text var (AA-safe text on light/tinted bg)
const HAS_TEXT_VAR: ReadonlySet<MeepleEntityType> = new Set([
  'game', 'kb', 'toolkit', 'session', 'event', 'agent', 'chat',
]);

export function entityHslText(entity: MeepleEntityType, alpha?: number): string {
  const name = CSS_ENTITY[entity];
  const suffix = HAS_TEXT_VAR.has(entity) ? '-text' : ''; // player/tool fall back to solid
  const v = `var(--c-${name}${suffix})`;
  return alpha !== undefined ? `hsl(${v} / ${alpha})` : `hsl(${v})`;
}
```

(The `alpha` param is retained for signature stability though the sole consumer `EntityBadge` calls it without alpha.)

- **Effetto**: `entityTokens()` (costruito su `entityHsl`) e **tutte le ~19 call-site** (dot CompactCard/ListCard, glow Grid/Featured, gradienti Cover/Hero, AccentBorder, TagStrip, ManaPips, ConnectionChip via entityTokens, EntityBadge via entityHslText) diventano **theme-aware + single-source `--c-*`** con **zero modifiche alle call-site**.
- `entityHsl(entity, alpha)` → `hsl(var(--c-<e>) / alpha)`: gli alpha-tint (fill 0.12, border 0.35, glow 0.18…) e i gradienti (Cover 0.35→0.55, Hero 0.85→0.4) mantengono la struttura, base color letta dalla var. Le firme di `entityHsl`/`entityHslText` restano invariate (param `alpha` mantenuto per stabilità; l'unico consumer di `entityHslText`, `EntityBadge`, lo chiama senza alpha).

**`design-tokens-canonical.css` — aggiungere 3 `--c-*-text` (light + dark):**

| Var | Light (AA su cream #f7f3ee) | Dark (AA su bg scuro) |
|---|---|---|
| `--c-event-text` | `350 89% 32%` (dai JS override, ~5.2:1) | `350 85% 72%` |
| `--c-agent-text` | `38 92% 24%` (~5.7:1) | `38 90% 68%` |
| `--c-chat-text` | `220 80% 38%` (~5.4:1) | `220 85% 75%` |

I valori light provengono da `entityTextOverrides` (già AA su cream). I dark seguono il pattern esistente (più chiari; `--c-game-text` dark 70%, `--c-toolkit-text` dark 72%). `game/kb/toolkit/session` hanno già i `-text`. `player`/`tool` non hanno override (fallback al solido, replica il comportamento JS).

**`EntityBadge` — pill theme-aware (aggiunta Opzione 2).** Il testo `entityHslText` diventa theme-aware (`--c-*-text` flippa: dark in light-theme, light in dark-theme), ma la pill è oggi `bg-white/85` (bianca **invariante**) → in dark il testo chiaro finirebbe su pill bianca = AA fail. Fix: `EntityBadge.tsx:22` cambia `bg-white/85` → **`bg-card/85`** (glass theme-aware: chiara in light, scura in dark) e si **rimuove l'`eslint-disable local/no-hardcoded-color-utility`** (non più necessario con l'utility semantica). Così `--c-*-text` è AA su entrambe le pill. È un cambio **visivo** alla pill (glass bianco → glass card) → designer-review.

**`entityColors` / `entityTextOverrides`:** `entityColors` **resta** (consumer esterni + regression #636). `entityTextOverrides` non è più usato da `entityHslText` dopo la riscrittura → si può rimuovere SE non ha altri consumer (verifico via grep; se orfano, delete). `entityTokens`/`statusColors`/`entityLabel`/`entityIcon` invariati.

## 4. Testing

- **`tokens.test.ts`** (aggiornare la forma asserita di `entityHsl`, righe 84-90): `entityHsl('game')` → `hsl(var(--c-game))`; `entityHsl('game', 0.5)` → `hsl(var(--c-game) / 0.5)` (non più `hsla(`). I test `entityColors` + regression #636 su `entityColors` **restano invariati** (entityColors non cambia).
- **Nuovi assert** in `tokens.test.ts`: `entityHsl(e)`/`entityHsl(e,a)` emettono `hsl(var(--c-<map(e)>) [/ a])` per tutte le 10 entità (+ `gameNightEvent→event`); `entityHslText(e)` emette `hsl(var(--c-<e>-text))` per le 7 con text-var e `hsl(var(--c-<e>))` per player/tool.
- **Nuovi `--c-*-text`**: assert di esistenza + **AA-contrast** (riuso `hslToRgb`+`contrastRatio` già in tokens.test.ts) per event/agent/chat text su cream (light) e su bg dark ≥ 4.5:1. Se un dark value fallisce AA, aggiustarlo.
- **Gate Phase B** (`entity-token-golden` / `entity-token-consistency`): **non toccati** — escludono esplicitamente i `-text` (`--c-game:` sì, `--c-game-text:` no). Restano verdi.
- **Varianti/acceptance-matrix**: verdi (nessuna asserzione di classe/colore; nessun crash).
- **Designer-review sul PR**: i gate non colgono regressioni visive. Delta atteso: entità ~1% lightness (JS 39% → canonico 38%) + **ora corrette in dark** (prima light-only).
- `pnpm typecheck` / `lint` / `build`.

## 5. Scope

**In scope:** riscrivere `entityHsl`/`entityHslText` → CSS-var; aggiungere 3 `--c-*-text` canonici; aggiornare/estendere `tokens.test.ts`; rimuovere `entityTextOverrides` se orfano.

**Fuori scope (deferiti):**
- Eliminare `entityColors` → ha consumer esterni + regression #636; riconciliare quei consumer (`MeeplePlayerStateCard`, `Confetti`, `StatsHero`, `sidebar-filters`) al canonico è follow-up.
- Migrazione `--mc-*` di superficie → redesign visivo (Q1), designer-led.
- `statusColors` → palette status (non entità), hardcoded ma fuori dal tema entità.
- Conversione HubGameCard/HubToolkitCard (grandfathered in C4).

## 6. Rischi

| Rischio | Mitigazione |
|---|---|
| Regressione visiva non colta dai gate | Delta ~1% lightness (più-corretto AA); designer-review esplicita sul PR |
| Un dark value `--c-*-text` non-AA | Test contrast dedicato (riuso helper); aggiustare il valore se < 4.5:1 |
| `entityColors` (39%) diverge da `--c-*` (38%) reso | Accettato: `entityColors` è palette raw per consumer non-variante; riconciliazione = follow-up. I gate/#636 restano coerenti |
| Consumer esterno di `entityHsl` con alpha si aspetta `hsla(` | Nessuno: `entityHsl` è usato in `style`/gradient (accetta `hsl(.. / a)`); l'unico test sulla forma è `tokens.test.ts` (aggiornato) |
| `entityHslText` chiamato con alpha da qualche consumer | Verificato: unico consumer `EntityBadge` chiama `entityHslText(entity)` senza alpha; il param si rimuove |

## 7. Acceptance criteria

- [ ] `entityHsl(entity, alpha?)` emette `hsl(var(--c-<map(entity)>) [/ alpha])` (theme-aware); `gameNightEvent→event`.
- [ ] `entityHslText(entity)` emette `hsl(var(--c-<e>-text))` per game/kb/toolkit/session/event/agent/chat e `hsl(var(--c-<e>))` per player/tool.
- [ ] `--c-event-text`/`--c-agent-text`/`--c-chat-text` presenti in canonical (light + dark), tutti ≥ 4.5:1 (light su `--bg-card` #ffffff, dark su `--bg-card` dark).
- [ ] `EntityBadge.tsx` pill `bg-white/85` → `bg-card/85`, `eslint-disable` rimosso; testo `--c-*-text` AA su entrambi i temi.
- [ ] `entityTokens` e le ~19 call-site invariate ma ora theme-aware/single-source; `entityColors` invariato (+ #636 verde).
- [ ] `entityTextOverrides` rimosso se orfano (grep-verificato).
- [ ] `tokens.test.ts` aggiornato + nuovi assert verdi; golden/consistency verdi; acceptance-matrix verde.
- [ ] `pnpm typecheck`/`lint`/`build` verdi.
