/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AA_NORMAL_TEXT, contrastRatio } from '@/test-utils/wcag';

import { InviteHostCard } from '../invite-host-card';

// #4910. The host avatar on /invites/[token] painted white initials on hsl(hue 60% 55%): 2.5:1 for the host the
// browser check was run with. The pair is measured on what the card renders, for every hue the page can derive.

function renderAvatar(hostHue: number): { avatar: HTMLElement; unmount: () => void } {
  const { unmount } = render(
    <InviteHostCard
      hostDisplayName="Test User"
      hostInitials="TU"
      hostHue={hostHue}
      welcomeMessage={null}
      hostLabel="Host"
      noMessageLabel="Nessun messaggio"
      avatarAriaLabel="Avatar di Test User"
    />
  );
  return { avatar: screen.getByRole('img', { name: 'Avatar di Test User' }), unmount };
}

describe('InviteHostCard avatar', () => {
  it('reaches 4.5:1 between its initials and its background, for every hue', () => {
    const failures: string[] = [];
    for (let hue = 0; hue < 360; hue += 1) {
      const { avatar, unmount } = renderAvatar(hue);
      // jsdom applies no stylesheet, so a colour that comes from a class would read back as the inherited default.
      // The foreground has to be explicit on the element for this measure to mean anything.
      if (avatar.style.color === '') {
        failures.push(`${hue}: no explicit foreground`);
      } else {
        const style = getComputedStyle(avatar);
        const ratio = contrastRatio(style.color, style.backgroundColor);
        if (ratio < AA_NORMAL_TEXT) failures.push(`${hue}: ${ratio.toFixed(2)}`);
      }
      unmount();
    }

    expect(failures).toEqual([]);
  });

  it('no longer takes its text colour from a fixed white class', () => {
    const { avatar } = renderAvatar(197);

    expect(avatar.className).not.toContain('text-white');
  });
});
