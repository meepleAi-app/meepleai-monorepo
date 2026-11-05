/**
 * Text colour for the initials avatars (#4910).
 *
 * The avatars paint their initials on `hsl(hue, 60%, 55%)`, a background whose lightness is the same for every hue but
 * whose luminance is not: yellows and greens are far brighter than blues. White text reaches the 4.5:1 that WCAG AA asks
 * of 11–16px text on only a band of blue-violet hues; axe measured 2.52 on #47afd1 (hue 197), the colour of one real
 * host. So the text is white where white passes and black everywhere else. Black clears 4.5:1 on every background where
 * white does not, which is what the tests measure, hue by hue.
 *
 * `GameNightAvatar` and `InviteHostCard` share this formula on purpose; any other avatar painted on the same background
 * can take its text colour from here.
 */

/** Saturation and lightness of the avatar background; `hsl(hue, 60%, 55%)` in the components. */
const AVATAR_SATURATION = 0.6;
const AVATAR_LIGHTNESS = 0.55;

const LIGHT_TEXT = 'hsl(0 0% 100%)';
const DARK_TEXT = 'hsl(0 0% 0%)';

/** WCAG AA for normal-size text. */
const MINIMUM_CONTRAST = 4.5;

function linearChannel(channel: number): number {
  return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
}

/** WCAG relative luminance of `hsl(hue, 60%, 55%)`. */
function backgroundLuminance(hue: number): number {
  const normalized = ((hue % 360) + 360) % 360;
  const a = AVATAR_SATURATION * Math.min(AVATAR_LIGHTNESS, 1 - AVATAR_LIGHTNESS);
  const channel = (n: number): number => {
    const k = (n + normalized / 30) % 12;
    return AVATAR_LIGHTNESS - a * Math.max(-1, Math.min(k - 3, 9 - k, 1));
  };
  return (
    0.2126 * linearChannel(channel(0)) +
    0.7152 * linearChannel(channel(8)) +
    0.0722 * linearChannel(channel(4))
  );
}

/** CSS colour for the initials of an avatar whose background is `hsl(hue, 60%, 55%)`. */
export function avatarForeground(hue: number): string {
  const whiteContrast = 1.05 / (backgroundLuminance(hue) + 0.05);
  return whiteContrast >= MINIMUM_CONTRAST ? LIGHT_TEXT : DARK_TEXT;
}
