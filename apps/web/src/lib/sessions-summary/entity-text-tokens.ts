/**
 * Entity-as-text-on-light tokens — Wave D.3 a11y hotfix (Issue #756).
 *
 * Why a separate token set?
 *   The shared `entityColors` palette in
 *   `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` is
 *   tuned for "white text on entity background" (EntityBadge use case).
 *   Each entity sits at ≥ 4.5:1 against #ffffff in that direction.
 *
 *   D.3 components inverse the relation: entity colour is the **text** rendered
 *   on (near-white) light surfaces — `bg-card`, `bg-card/80` blended over
 *   `--background`, plus entity-tinted alpha overlays at 0.06 / 0.10 / 0.12 /
 *   0.14 / 0.18. In that direction the original `l` values land between
 *   3.4:1 and 4.1:1, failing axe-core color-contrast (WCAG SC 1.4.3 AA).
 *
 *   Modifying the shared `entityColors` would cascade across MeepleCard +
 *   visual baselines (high blast radius, out of scope for this hotfix).
 *   Instead this file exports a parallel "darker for text-on-light" palette
 *   used **only** by D.3 components.
 *
 * WCAG 2.1 AA verification (computed against worst-case backgrounds — all
 * pass ≥ 4.5:1, most ≥ 5:1):
 *
 *   | entity  |   l   | on white | on bg-pip 0.10 | on bg-tint 0.18 |
 *   |---------|------:|---------:|---------------:|----------------:|
 *   | game    |  32%  |   6.31  |     5.53     |      4.94      |
 *   | player  |  45%  |   8.55  |     7.36     |      6.53      |
 *   | session |  38%  |  11.35  |     9.53     |      8.19      |
 *   | agent   |  28%  |   6.01  |     5.30     |      4.77      |
 *   | kb      |  38%  |   6.47  |     5.75     |      5.18      |
 *   | chat    |  42%  |   7.19  |     6.33     |      5.68      |
 *   | event   |  38%  |   6.79  |     5.76     |      4.99      |
 *   | toolkit |  25%  |   6.52  |     5.73     |      5.13      |
 *
 * Background tints (ENTITY_BG_ALPHA) keep their original `l` so visual
 * baselines drift minimally — only the foreground text darkens. Borders
 * (ENTITY_RING_ALPHA) also keep originals.
 *
 * Consumers (D.3):
 *   - ConnectionBar (pip text colour)
 *   - SessionKpiGrid (KPI value text colour)
 *   - AchievementsCarousel (toolkit-tinted unlocked title)
 *   - ScoringBreakdownTable (toolkit-tinted winner cells)
 *   - SessionDiaryTimeline (session-tinted active filter pill + timestamp pill)
 *   - SessionShareCard (toolkit-tinted winner chip)
 *   - SessionSummaryHero (toolkit-tinted winner emoji + tied banner + podium border)
 */

/** Strict union of D.3 entity tokens (matches `ConnectionBarEntity` + `KpiEntityHint`). */
export type SessionSummaryEntity =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chat'
  | 'event'
  | 'toolkit';

/**
 * Darker `l` lightness values tuned for "entity colour as text on light
 * backgrounds". Each pair is `(h, s, lDark)` — see contrast table above.
 */
export const ENTITY_TEXT_HSL: Record<SessionSummaryEntity, string> = {
  game: 'hsl(25, 95%, 32%)',
  player: 'hsl(262, 83%, 45%)',
  session: 'hsl(240, 60%, 38%)',
  agent: 'hsl(38, 92%, 28%)',
  kb: 'hsl(210, 40%, 38%)',
  chat: 'hsl(220, 80%, 42%)',
  event: 'hsl(350, 89%, 38%)',
  toolkit: 'hsl(142, 70%, 25%)',
};

/**
 * Convenience helper to inline an entity text colour as a literal Tailwind
 * arbitrary value (`text-[hsl(...)]`). Components use this in `clsx()`
 * conditional classes to avoid sprinkling magic HSL literals.
 */
export function entityTextHslClass(entity: SessionSummaryEntity): string {
  // tailwind safelist — avoid template literals so the JIT can statically
  // resolve the class string at build time.
  switch (entity) {
    case 'game':
      return 'text-[hsl(25,95%,32%)]';
    case 'player':
      return 'text-[hsl(262,83%,45%)]';
    case 'session':
      return 'text-[hsl(240,60%,38%)]';
    case 'agent':
      return 'text-[hsl(38,92%,28%)]';
    case 'kb':
      return 'text-[hsl(210,40%,38%)]';
    case 'chat':
      return 'text-[hsl(220,80%,42%)]';
    case 'event':
      return 'text-[hsl(350,89%,38%)]';
    case 'toolkit':
      return 'text-[hsl(142,70%,25%)]';
  }
}

/**
 * Convenience helper for `border-t-[...]` arbitrary values. Same darker
 * shade — used by SessionSummaryHero podium top edge to match winner text
 * colour exactly.
 */
export function entityBorderHslClass(entity: SessionSummaryEntity, side: 't' = 't'): string {
  switch (entity) {
    case 'toolkit':
      return side === 't' ? 'border-t-[hsl(142,70%,25%)]' : 'border-[hsl(142,70%,25%)]';
    case 'session':
      return side === 't' ? 'border-t-[hsl(240,60%,38%)]' : 'border-[hsl(240,60%,38%)]';
    case 'game':
      return side === 't' ? 'border-t-[hsl(25,95%,32%)]' : 'border-[hsl(25,95%,32%)]';
    case 'player':
      return side === 't' ? 'border-t-[hsl(262,83%,45%)]' : 'border-[hsl(262,83%,45%)]';
    case 'agent':
      return side === 't' ? 'border-t-[hsl(38,92%,28%)]' : 'border-[hsl(38,92%,28%)]';
    case 'kb':
      return side === 't' ? 'border-t-[hsl(210,40%,38%)]' : 'border-[hsl(210,40%,38%)]';
    case 'chat':
      return side === 't' ? 'border-t-[hsl(220,80%,42%)]' : 'border-[hsl(220,80%,42%)]';
    case 'event':
      return side === 't' ? 'border-t-[hsl(350,89%,38%)]' : 'border-[hsl(350,89%,38%)]';
  }
}
