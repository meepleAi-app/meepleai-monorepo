# v2 A11y Token Audit — 2026-05-09

**Methodology**: WCAG 2.1 AA SC 1.4.3 contrast ratio calculation via relative luminance formula (`tools/a11y/contrast-calc.ts`).

**Backgrounds tested**:
- Light mode: `bg-card` #FFFFFF
- Dark mode: `bg-card-dark` #1E1710

## Token contrast matrix (final values, post Iteration 2)

| Token | Light HSL | Light Ratio | Status | Dark HSL | Dark Ratio | Status |
|-------|-----------|-------------|--------|----------|------------|--------|
| `--c-game` | 25 95% 38% | 4.82:1 | ✅ | 28 95% 58% | 7.51:1 | ✅ |
| `--c-player` | 262 83% 45% | 8.55:1 | ✅ | 262 75% 70% | 5.49:1 | ✅ |
| `--c-session` | 240 60% 35% | 12.22:1 | ✅ | 235 70% 70% | 5.43:1 | ✅ |
| `--c-agent` | 38 92% 32% | 4.87:1 | ✅ | 38 92% 62% | 9.89:1 | ✅ |
| `--c-kb` | 174 60% 30% | 5.12:1 | ✅ | 174 60% 55% | 9.44:1 | ✅ |
| `--c-chat` | 220 80% 40% | 7.72:1 | ✅ | 218 80% 68% | 6.44:1 | ✅ |
| `--c-event` | 350 89% 38% | 6.79:1 | ✅ | 350 85% 70% | 6.38:1 | ✅ |
| `--c-toolkit` | 142 70% 30% | 4.88:1 | ✅ | 142 60% 58% | 9.39:1 | ✅ |
| `--c-tool` | 195 80% 32% | 5.44:1 | ✅ | 195 75% 62% | 8.68:1 | ✅ |

**Result**: 18 of 18 token values pass WCAG 2.1 AA (≥ 4.5:1) on both light and dark mode backgrounds.

## Iteration history

### Iteration 1 (initial spec values)

Spec stimava L values per 9 entity tokens. Audit ha rivelato 4 light-mode FAILURE:

- `--c-kb` 174 60% L=40% → 3.09:1 ❌ (cyan/teal hue è perceptually light, richiede L molto basso)
- `--c-toolkit` 142 70% L=35% → 3.74:1 ❌ (green hue similar issue)
- `--c-agent` 38 92% L=38% → 3.61:1 ❌ (yellow/orange hue è perceptually light)
- `--c-tool` 195 80% L=38% → 4.08:1 ❌ (cyan, marginal)

### Iteration 2 (final values — 2026-05-09)

L values aggiustati down per 4 failing tokens:

| Token | Iter 1 L | Iter 2 L | Iter 1 Ratio | Iter 2 Ratio |
|-------|----------|----------|--------------|--------------|
| `--c-kb` | 40% | **30%** | 3.09:1 ❌ | 5.12:1 ✅ |
| `--c-agent` | 38% | **32%** | 3.61:1 ❌ | 4.87:1 ✅ |
| `--c-toolkit` | 35% | **30%** | 3.74:1 ❌ | 4.88:1 ✅ |
| `--c-tool` | 38% | **32%** | 4.08:1 ❌ | 5.44:1 ✅ |

Tutti gli altri 5 tokens (game, player, session, chat, event) hanno ratios già passanti, valori invariati.

Dark mode values **unchanged** (tutti già passing in Iter 1).

## Analysis

### Hue-based perception

Cyan/teal/green hues (kb 174°, tool 195°, toolkit 142°) richiedono L values significativamente più bassi rispetto a red/magenta/violet (event 350°, player 262°). Questo è coerente con la sensibilità del visione umana al verde + cyan (peak luminance perception around 555nm = green).

Yellow/orange (agent 38°) è anche perceptually light.

### Visual identity vs AA tradeoff

Lowering L from 35-40% to 30-32% rende i colori più "saturated/dark" ma preserva l'entity hue identity. Questo è il compromesso accettabile per AA compliance.

## Refs
- Spec: `docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md`
- Plan: `docs/superpowers/plans/2026-05-09-p2-807-token-redesign.md` (Task 2 uses these final values)
- Issue #807
- WCAG 2.1 SC 1.4.3: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum
