/**
 * Game Nights Section Layout
 * Issue #33 — P3 Game Night Frontend
 *
 * Mounts GameNightsNavConfig for all /game-nights sub-routes.
 */

'use client';

import { type ReactNode } from 'react';

import { GameNightsNavConfig } from './NavConfig';

export default function GameNightsLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <GameNightsNavConfig />
      {children}
    </>
  );
}
