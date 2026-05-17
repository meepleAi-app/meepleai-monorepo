// tools/a11y/contrast-calc.ts
import { readFileSync } from 'node:fs';

interface HSL { h: number; s: number; l: number; }
interface RGB { r: number; g: number; b: number; }

function hslToRgb({ h, s: sPct, l: lPct }: HSL): RGB {
  const s = sPct / 100;
  const l = lPct / 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs((h / 60) % 2 - 1));
  const m = l - c / 2;
  let r = 0, g = 0, b = 0;
  if (h < 60) { r = c; g = x; }
  else if (h < 120) { r = x; g = c; }
  else if (h < 180) { g = c; b = x; }
  else if (h < 240) { g = x; b = c; }
  else if (h < 300) { r = x; b = c; }
  else { r = c; b = x; }
  return { r: (r + m) * 255, g: (g + m) * 255, b: (b + m) * 255 };
}

function relativeLuminance({ r, g, b }: RGB): number {
  const norm = (c: number) => {
    const v = c / 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * norm(r) + 0.7152 * norm(g) + 0.0722 * norm(b);
}

export function contrastRatio(fg: HSL, bg: HSL): number {
  const lFg = relativeLuminance(hslToRgb(fg));
  const lBg = relativeLuminance(hslToRgb(bg));
  const [light, dark] = lFg > lBg ? [lFg, lBg] : [lBg, lFg];
  return (light + 0.05) / (dark + 0.05);
}

// Self-check entry point
if (require.main === module) {
  // bg-card light: #ffffff = HSL 0,0,100
  const bgLight: HSL = { h: 0, s: 0, l: 100 };
  // bg-card dark: #1e1710 ≈ HSL 30, 33, 9
  const bgDark: HSL = { h: 30, s: 33, l: 9 };

  const tokens: Record<string, { light: HSL; dark: HSL }> = {
    game:    { light: { h: 25,  s: 95, l: 38 }, dark: { h: 28,  s: 95, l: 58 } },
    player:  { light: { h: 262, s: 83, l: 45 }, dark: { h: 262, s: 75, l: 70 } },
    session: { light: { h: 240, s: 60, l: 35 }, dark: { h: 235, s: 70, l: 70 } },
    agent:   { light: { h: 38,  s: 92, l: 32 }, dark: { h: 38,  s: 92, l: 62 } },
    kb:      { light: { h: 174, s: 60, l: 30 }, dark: { h: 174, s: 60, l: 55 } },
    chat:    { light: { h: 220, s: 80, l: 40 }, dark: { h: 218, s: 80, l: 68 } },
    event:   { light: { h: 350, s: 89, l: 38 }, dark: { h: 350, s: 85, l: 70 } },
    toolkit: { light: { h: 142, s: 70, l: 30 }, dark: { h: 142, s: 60, l: 58 } },
    tool:    { light: { h: 195, s: 80, l: 32 }, dark: { h: 195, s: 75, l: 62 } },
  };

  console.log('Token | Light Ratio (vs #fff) | Dark Ratio (vs #1e1710)');
  console.log('-----|----------------------|------------------------');
  for (const [name, { light, dark }] of Object.entries(tokens)) {
    const lightRatio = contrastRatio(light, bgLight).toFixed(2);
    const darkRatio  = contrastRatio(dark, bgDark).toFixed(2);
    const lightOK = parseFloat(lightRatio) >= 4.5 ? '✅' : '❌';
    const darkOK  = parseFloat(darkRatio)  >= 4.5 ? '✅' : '❌';
    console.log(`${name} | ${lightRatio}:1 ${lightOK} | ${darkRatio}:1 ${darkOK}`);
  }
}
