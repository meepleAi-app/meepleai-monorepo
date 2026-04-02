# MeepleAI Logo Redesign — Design Spec

**Date:** 2026-04-01
**Branch:** feature/integration-mode-improvements
**Approach:** A — Brand completo + Framer Motion

---

## Obiettivo

Sostituire il logo attuale (gradiente blu/viola, non coerente col brand) con il design dal `LogoGuide.png`:
- Meeple arancione/amber con occhi cyan, corona dorata, esagono scuro con bordo glow
- 4 varianti: Primary (hex frame), Flat (solo meeple), Mono (bianco), Micro (favicon)
- `MeepleAvatar` animato via Framer Motion per la chat AI

---

## Asset PNG (già estratti)

Ritagliati da `LogoGuide.png` (1024×1536) e salvati in `apps/web/public/logo/`:

| File | Dimensioni | Uso |
|------|-----------|-----|
| `meepleai-logo-icon.png` | 490×435 | Primary hero (header, splash) |
| `meepleai-logo-primary-sm.png` | 200×200 | Primary small (navbar compact) |
| `meepleai-logo-flat.png` | 147×200 | Flat (wordmark, light bg) |
| `meepleai-logo-mono.png` | 150×200 | Mono (dark theme, docs) |
| `meepleai-logo-micro.png` | 155×200 | Micro (favicon, tab) |

---

## Design Tokens

```ts
const BRAND = {
  meeple_primary: '#f97316',   // arancione corpo
  meeple_light:   '#f59e0b',   // ambra chiara
  accent_ai:      '#22d3ee',   // cyan glow (occhi, bordo hex)
  gold:           '#fbbf24',   // corona
  bg_primary:     '#0f172a',   // dark background
  bg_secondary:   '#1e293b',   // pannelli
};
```

---

## Architettura Componenti

```
src/components/ui/meeple/
├── meeple-logo.tsx       ← rebuild (usa <img> PNG + wordmark)
├── meeple-avatar.tsx     ← rebuild (PNG flat + Framer Motion)
└── index.ts              ← esporta entrambi
```

### Nessun breaking change

I consumer esistenti mantengono le stesse prop:
- `UnifiedHeader.tsx` — `<MeepleLogo variant="full" size="md" />`
- `AuthLayout.tsx` — `<MeepleLogo variant="icon" />`
- `PublicFooter.tsx` — `<MeepleLogo variant="full" size="sm" />`
- `chat-message.tsx` — `<MeepleAvatar state="thinking" size="md" />`

---

## MeepleLogo — Spec

```tsx
interface MeepleLogoProps {
  variant?: 'full' | 'icon' | 'wordmark'  // default: 'full'
  size?: 'sm' | 'md' | 'lg' | 'xl'        // default: 'md'
  className?: string
  animated?: boolean                        // hover bounce (default: false)
}
```

**Mapping variante → PNG:**

| variant | PNG usato | Note |
|---------|-----------|------|
| `icon` | `meepleai-logo-icon.png` | Solo icona hex+meeple |
| `full` | `meepleai-logo-icon.png` + wordmark | Icon + testo "MeepleAI" |
| `wordmark` | — | Solo testo |

**Wordmark:** `<span>Meeple</span><span style="color:#22d3ee">AI</span>` — font display (Quicksand/Fredoka), colore `#f97316` per "Meeple", `#22d3ee` per "AI".

**Animazione (`animated=true`):** Framer Motion `whileHover={{ scale: 1.05 }}` sull'icona.

**Size map:**

| size | iconSize | fontSize |
|------|---------|---------|
| sm | 28px | 1.1rem |
| md | 36px | 1.4rem |
| lg | 48px | 1.8rem |
| xl | 64px | 2.4rem |

---

## MeepleAvatar — Spec

```tsx
type MeepleAvatarState =
  | 'idle' | 'thinking' | 'decision'
  | 'success' | 'error'
  | 'confident' | 'searching' | 'uncertain'  // compat con attuale

interface MeepleAvatarProps {
  state: MeepleAvatarState
  size?: 'sm' | 'md' | 'lg'   // sm:32px md:48px lg:64px
  className?: string
  ariaLabel?: string
}
```

**Struttura:** Container Framer Motion + `<img src="/logo/meepleai-logo-flat.png">` + overlay glow div.

> **Nota implementativa:** Poiché il logo è un PNG, le animazioni sono applicate a livello container (scale, shake, CSS filter, glow aura). Le animazioni "occhio" e "corona" vengono simulate con overlay SVG/div posizionati in absolute sopra l'immagine, non modificando i pixel del PNG.

```
<motion.div>               ← container principale (scale/shake)
  <motion.div>             ← glow aura (absolute, inset, blur-xl, z-0)
  <img>                    ← flat.png (z-10, relative)
  <motion.div>             ← crown overlay: emoji/SVG "👑" in absolute top-center (z-20)
  <motion.div>             ← eye blink overlay: 2 rect cyan in absolute (z-20)
```

**Animazioni per stato:**

| Stato | Scale | Glow opacity | CSS filter | Extra |
|-------|-------|-------------|-----------|-------|
| `idle` | `[1, 1.03, 1]` 2.5s | `[0.1, 0.25, 0.1]` | — | eye overlay opacity flicker lento |
| `thinking` | `[1, 1.08, 1]` 1.4s | `[0.2, 0.5, 0.2]` | — | eye overlay opacity pulse rapido |
| `decision` | `[1, 1.1, 1]` 0.5s | `[0.3, 0.7, 0.3]` 0.8s | — | sparkle SVG overlay |
| `success` | `[1, 1.05, 1]` | amber glow | `brightness(1.1)` | crown bounce |
| `error` | shake ±3px | arancio-rosso | `hue-rotate(-20deg)` | — |
| `confident` | `scale(1.05)` static | 0.3 | — | compat |
| `searching` | `[1, 1.03, 1]` | 0.2 | — | compat |
| `uncertain` | shake ±1px soft | 0.1 | — | compat |

**Glow color:** `#22d3ee` per tutti gli stati tranne `success` (`#fbbf24`) e `error` (`#f97316`).

**`prefers-reduced-motion`:** Tutte le animazioni disabilitate.

---

## File da NON modificare

- `UnifiedHeader.tsx`, `AuthLayout.tsx`, `PublicFooter.tsx`, `chat-message.tsx` — prop invariate
- SVG esistenti in `public/logo/` — lasciati in place per backward compat, non usati attivamente

---

## Scope fuori spec

- Favicon `app/favicon.ico` — aggiornamento separato
- Stories Storybook — aggiornamento post-implementazione
- Test unitari — aggiornamento post-implementazione
