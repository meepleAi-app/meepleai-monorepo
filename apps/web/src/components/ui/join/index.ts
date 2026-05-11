/**
 * Wave A.2 (`/join` route migration) — v2 component family barrel.
 *
 * Spec: `docs/superpowers/specs/2026-04-27-v2-migration-wave-a-2-join.md`.
 * Mockup: `admin-mockups/design_files/sp3-join.jsx`.
 */

export { AlphaPill } from '@/components/ui/join/alpha-pill';
export type { AlphaPillProps } from '@/components/ui/join/alpha-pill';

export { FeatureMiniCard } from '@/components/ui/join/feature-mini-card';
export type { FeatureMiniCardProps } from '@/components/ui/join/feature-mini-card';

export { JoinHero } from '@/components/ui/join/join-hero';
export type { JoinHeroFeature, JoinHeroProps } from '@/components/ui/join/join-hero';

export { GamePreferenceSelect } from '@/components/ui/join/game-preference-select';
export type {
  GamePreferenceSelectLabels,
  GamePreferenceSelectProps,
} from '@/components/ui/join/game-preference-select';

export { JoinSuccessCard } from '@/components/ui/join/join-success-card';
export type { JoinSuccessCardLabels, JoinSuccessCardProps } from '@/components/ui/join/join-success-card';

export { JoinForm } from '@/components/ui/join/join-form';
export type { JoinFormLabels, JoinFormProps } from '@/components/ui/join/join-form';
