/**
 * WCAG 2.x contrast, for tests that need to measure a colour pair rather than assert on a class name (#4910).
 *
 * Deliberately independent of any production helper: a test that measures with the code under test cannot catch a
 * mistake in it. Accepts what jsdom and the components produce: `hsl(h, s%, l%)`, `hsl(h s% l%)`, `rgb(r, g, b)`
 * and `#rrggbb`.
 */

type Rgb = readonly [number, number, number];

function hslToRgb(hue: number, saturation: number, lightness: number): Rgb {
  const a = saturation * Math.min(lightness, 1 - lightness);
  const channel = (n: number): number => {
    const k = (n + hue / 30) % 12;
    return lightness - a * Math.max(-1, Math.min(k - 3, 9 - k, 1));
  };
  return [channel(0), channel(8), channel(4)];
}

/** sRGB channels in 0..1. Throws on a colour it does not understand, so a test cannot pass on a parse failure. */
export function parseCssColor(css: string): Rgb {
  const value = css.trim();

  const hsl = /^hsla?\(\s*([\d.]+)(?:deg)?[\s,]+([\d.]+)%[\s,]+([\d.]+)%\s*(?:[,/][^)]*)?\)$/i.exec(
    value
  );
  if (hsl) return hslToRgb(Number(hsl[1]), Number(hsl[2]) / 100, Number(hsl[3]) / 100);

  const rgb = /^rgba?\(\s*(\d+)[\s,]+(\d+)[\s,]+(\d+)\s*(?:[,/][^)]*)?\)$/i.exec(value);
  if (rgb) return [Number(rgb[1]) / 255, Number(rgb[2]) / 255, Number(rgb[3]) / 255];

  const hex = /^#([\da-f]{2})([\da-f]{2})([\da-f]{2})$/i.exec(value);
  if (hex)
    return [parseInt(hex[1]!, 16) / 255, parseInt(hex[2]!, 16) / 255, parseInt(hex[3]!, 16) / 255];

  throw new Error(`parseCssColor: cannot read "${css}"`);
}

function linear(channel: number): number {
  return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
}

export function relativeLuminance(css: string): number {
  const [r, g, b] = parseCssColor(css);
  return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);
}

/** Contrast ratio between two colours, 1..21. */
export function contrastRatio(a: string, b: string): number {
  const la = relativeLuminance(a);
  const lb = relativeLuminance(b);
  return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
}

/** WCAG AA for normal-size text. */
export const AA_NORMAL_TEXT = 4.5;
