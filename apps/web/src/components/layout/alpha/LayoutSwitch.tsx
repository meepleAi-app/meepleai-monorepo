'use client';

/**
 * LayoutSwitch
 *
 * Client component that conditionally renders the alpha or default layout shell.
 * Both shells are passed as pre-rendered ReactNode slots from the server layout
 * to avoid importing server components into a client boundary.
 *
 * Safety note: Although both shells are server-rendered into the HTML, React only
 * hydrates and mounts the branch returned by this component. The inactive slot is
 * never mounted, so its hooks and data-fetching effects do not fire. This is safe
 * because React's reconciliation unmounts the subtree that is not returned.
 */

import { type ReactNode } from 'react';

import { useFeatureFlag } from '@/providers/FeatureFlagProvider';

interface LayoutSwitchProps {
  /** Content rendered inside AlphaShell (alpha_layout flag ON) */
  alphaSlot: ReactNode;
  /** Content rendered inside UnifiedShell (default) */
  defaultSlot: ReactNode;
}

export function LayoutSwitch({ alphaSlot, defaultSlot }: LayoutSwitchProps) {
  const isAlphaEnabled = useFeatureFlag('alpha_layout');

  return isAlphaEnabled ? <>{alphaSlot}</> : <>{defaultSlot}</>;
}
