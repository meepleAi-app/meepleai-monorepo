/**
 * Dashboard entity color tokens.
 * All widget files import from here — single source of truth.
 *
 * M4 note: These are component-scoped HSL values. A future migration to
 * CSS custom properties would swap these for var(--color-*) references.
 */
export const C = {
  game: 'hsl(25,95%,45%)',
  player: 'hsl(262,83%,58%)',
  session: 'hsl(240,60%,55%)',
  chat: 'hsl(220,80%,55%)',
  kb: 'hsl(174,60%,40%)',
  event: 'hsl(350,89%,60%)',
  agent: 'hsl(38,92%,50%)',
  success: 'hsl(142,70%,45%)',
} as const;

export type DashboardColor = (typeof C)[keyof typeof C];
