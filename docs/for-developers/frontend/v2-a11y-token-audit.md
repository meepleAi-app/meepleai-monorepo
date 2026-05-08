# v2 A11y Token Audit — 2026-05-09

**Methodology**: WCAG 2.1 AA SC 1.4.3 contrast ratio calculation via relative luminance formula (`tools/a11y/contrast-calc.ts`).

**Backgrounds tested**:
- Light mode: `bg-card` #FFFFFF
- Dark mode: `bg-card-dark` #1E1710

## Token contrast matrix

| Token | Light HSL | Light Ratio | Status | Dark HSL | Dark Ratio | Status |
|-------|-----------|-------------|--------|----------|------------|--------|
| `--c-game` | 25 95% 38% | 4.82:1 | ✅ | 28 95% 58% | 7.51:1 | ✅ |
| `--c-player` | 262 83% 45% | 8.55:1 | ✅ | 262 75% 70% | 5.49:1 | ✅ |
| `--c-session` | 240 60% 35% | 12.22:1 | ✅ | 235 70% 70% | 5.43:1 | ✅ |
| `--c-agent` | 38 92% 38% | 3.61:1 | ❌ | 38 92% 62% | 9.89:1 | ✅ |
| `--c-kb` | 174 60% 40% | 3.09:1 | ❌ | 174 60% 55% | 9.44:1 | ✅ |
| `--c-chat` | 220 80% 40% | 7.72:1 | ✅ | 218 80% 68% | 6.44:1 | ✅ |
| `--c-event` | 350 89% 38% | 6.79:1 | ✅ | 350 85% 70% | 6.38:1 | ✅ |
| `--c-toolkit` | 142 70% 35% | 3.74:1 | ❌ | 142 60% 58% | 9.39:1 | ✅ |
| `--c-tool` | 195 80% 38% | 4.08:1 | ❌ | 195 75% 62% | 8.68:1 | ✅ |

**Result**: 14 of 18 token values pass WCAG 2.1 AA (≥ 4.5:1). Light mode failures detected: `--c-agent` (3.61:1), `--c-kb` (3.09:1), `--c-toolkit` (3.74:1), `--c-tool` (4.08:1). All dark mode ratios pass.

## Analysis

### Light Mode Failures

Four tokens fail WCAG AA contrast on white (#FFFFFF) background:

1. **`--c-kb`** — 3.09:1 (most critical, 35.5% below threshold)
2. **`--c-toolkit`** — 3.74:1 (16.9% below threshold)
3. **`--c-agent`** — 3.61:1 (19.8% below threshold)
4. **`--c-tool`** — 4.08:1 (9.3% below threshold)

### Dark Mode Success

All tokens pass WCAG AA on dark background (#1E1710), with comfortable margins (5.43:1 minimum vs 4.5:1 threshold).

### Recommendation

Task 2 (token redesign) should increase L values for failing light-mode tokens. Estimated adjustments:
- `--c-kb`: L from 40% → ~32% (increases luminance contrast)
- `--c-toolkit`: L from 35% → ~28% (increases saturation + reduces lightness)
- `--c-agent`: L from 38% → ~30% (reduces lightness)
- `--c-tool`: L from 38% → ~30% (marginal adjustment, near threshold)

**Note**: Dark mode L values should remain stable to preserve dark theme visual balance.

## Refs
- Spec: docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md (referenced in task)
- Issue #807
- WCAG 2.1 SC 1.4.3: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum
