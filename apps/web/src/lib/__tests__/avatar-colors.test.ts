import { describe, expect, it } from 'vitest';

import { AA_NORMAL_TEXT, contrastRatio } from '@/test-utils/wcag';

import { avatarForeground } from '../avatar-colors';

// #4910. The initials avatars paint white text on `hsl(hue, 60%, 55%)`. For most hues that is below the 4.5:1 that
// WCAG AA asks for 11–16px text (axe measured 2.52 on #47afd1, the colour of one real host). The foreground is chosen
// per hue, and these tests measure the outcome with their own copy of the WCAG formulas (test-utils/wcag), so a
// mistake in the helper cannot hide in the helper.

const WHITE = 'hsl(0 0% 100%)';
const BLACK = 'hsl(0 0% 0%)';

const avatarBackground = (hue: number): string => `hsl(${hue}, 60%, 55%)`;

describe('avatarForeground', () => {
  it('reaches 4.5:1 against the avatar background for every hue, in steps of half a degree', () => {
    const failures: string[] = [];
    for (let hue = 0; hue < 360; hue += 0.5) {
      const ratio = contrastRatio(avatarBackground(hue), avatarForeground(hue));
      if (ratio < AA_NORMAL_TEXT) failures.push(`${hue}: ${ratio.toFixed(2)}`);
    }

    expect(failures).toEqual([]);
  });

  it('uses dark text for the hue of the reported host, where white measured 2.5:1 (#47afd1, hue 197)', () => {
    expect(contrastRatio(avatarBackground(197), WHITE)).toBeLessThan(3);

    expect(avatarForeground(197)).toBe(BLACK);
  });

  it('reads a hue outside 0–360 as the same colour, as the background does', () => {
    expect(avatarForeground(197 + 360)).toBe(avatarForeground(197));
    expect(avatarForeground(197 - 360)).toBe(avatarForeground(197));
    expect(avatarForeground(240 - 720)).toBe(avatarForeground(240));
  });

  it('keeps white text where white already passes (hue 240)', () => {
    expect(contrastRatio(avatarBackground(240), WHITE)).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);

    expect(avatarForeground(240)).toBe(WHITE);
  });
});
