/**
 * Convert a hex color string to HSL format string: "H S% L%"
 * Accepts hex with or without leading '#', in 3- or 6-digit form.
 * Returns undefined if the input is not a valid hex color.
 *
 * @example
 * hexToHsl('#7c3aed') // "262 83% 58%"
 * hexToHsl('fff')     // "0 0% 100%"
 * hexToHsl('nope')    // undefined
 */
export function hexToHsl(hex: string): string | undefined {
  const clean = hex.replace(/^#/, '');
  if (!/^[0-9a-fA-F]{3}([0-9a-fA-F]{3})?$/.test(clean)) return undefined;

  const fullHex =
    clean.length === 3
      ? clean
          .split('')
          .map(c => c + c)
          .join('')
      : clean;

  const r = parseInt(fullHex.slice(0, 2), 16) / 255;
  const g = parseInt(fullHex.slice(2, 4), 16) / 255;
  const b = parseInt(fullHex.slice(4, 6), 16) / 255;

  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;

  if (max === min) {
    return `0 0% ${Math.round(l * 100)}%`;
  }

  const d = max - min;
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

  let h = 0;
  if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
  else if (max === g) h = ((b - r) / d + 2) / 6;
  else h = ((r - g) / d + 4) / 6;

  return `${Math.round(h * 360)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%`;
}
