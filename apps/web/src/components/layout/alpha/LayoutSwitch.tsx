'use client';

/**
 * LayoutSwitch
 *
 * Client component that conditionally renders the alpha or default layout shell.
 * Both shells are passed as pre-rendered ReactNode slots from the server layout
 * to avoid importing server components into a client boundary.
 */

import { type ReactNode } from 'react';

import { useFeatureFlag } from '@/providers/FeatureFlagProvider';

interface LayoutSwitchProps {
  /** Content rendered inside AlphaShell (alpha-layout flag ON) */
  alphaSlot: ReactNode;
  /** Content rendered inside UnifiedShell (default) */
  defaultSlot: ReactNode;
}

export function LayoutSwitch({ alphaSlot, defaultSlot }: LayoutSwitchProps) {
  const isAlphaEnabled = useFeatureFlag('alpha-layout');

  return isAlphaEnabled ? <>{alphaSlot}</> : <>{defaultSlot}</>;
}
