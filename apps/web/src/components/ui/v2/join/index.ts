/**
 * Wave A.2 (`/join` route migration) — v2 component family barrel.
 *
 * Spec: `docs/superpowers/specs/2026-04-27-v2-migration-wave-a-2-join.md`.
 * Mockup: `admin-mockups/design_files/sp3-join.jsx`.
 */

export { AlphaPill } from './alpha-pill';
export type { AlphaPillProps } from './alpha-pill';

export { FeatureMiniCard } from './feature-mini-card';
export type { FeatureMiniCardProps } from './feature-mini-card';

export { JoinHero } from './join-hero';
export type { JoinHeroFeature, JoinHeroProps } from './join-hero';

export { GamePreferenceSelect } from './game-preference-select';
export type {
  GamePreferenceSelectLabels,
  GamePreferenceSelectProps,
} from './game-preference-select';

export { JoinSuccessCard } from './join-success-card';
export type { JoinSuccessCardLabels, JoinSuccessCardProps } from './join-success-card';

export { JoinForm } from './join-form';
export type { JoinFormLabels, JoinFormProps } from './join-form';
