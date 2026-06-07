import type { JSX } from 'react';

import { simpleHash } from '@/lib/util/simple-hash';

/**
 * Issue #1674: deterministic gradient thumbnail for embeddings result rows.
 *
 * Security note: pure visual, computed client-side from seed=hash(chunkIdx).
 * Backend NEVER sends raw vector values (Newman corpus-reconstruction risk
 * mitigation). See spec §7.
 */
export interface VecThumbProps {
  seed: number | string;
}

export function VecThumb({ seed }: VecThumbProps): JSX.Element {
  const hash = simpleHash(String(seed));
  const hue1 = hash % 360;
  const hue2 = (hash * 7) % 360;
  const hue3 = (hash * 13) % 360;

  return (
    <div
      className="mt-1.5 h-7 overflow-hidden rounded-md"
      style={{
        background: `linear-gradient(90deg, hsl(${hue1} 60% 50% / .35), hsl(${hue2} 60% 50% / .05), hsl(${hue3} 60% 50% / .25))`,
      }}
      aria-hidden="true"
    />
  );
}
