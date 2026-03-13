# Welcome Page Redesign — Spec

**Date**: 2026-03-12
**Branch**: `feature/layout-cleanup-redesign`
**Parent**: `frontend-dev`

## Problem

The current welcome page tries to do too much: animated avatar, three carousels, feature cards, how-it-works steps, and a CTA banner. It promises immediate interaction (search bar) but requires registration to use the AI. The messaging focuses on speed ("risposte immediate") rather than the product's real value: an AI game companion that helps throughout an entire game night.

## Goal

Redesign the welcome page as a minimal, focused landing that communicates the core value proposition — an AI arbitro at your game table — and drives registration through honest CTAs.

## Design Principles

- **Minimal**: fewer sections, more whitespace, strong typography
- **Honest**: no interactive elements that require registration to function
- **Narrative**: tell the real user story in progressive steps
- **Problem-first**: lead with the pain point, not the product
- **Dark-mode aware**: use Tailwind semantic tokens (`bg-background`, `bg-muted`, `text-foreground`), never literal color values

## Structure

### Section 1: Hero

**Layout**: centered, full viewport height

| Element | Content |
|---------|---------|
| Kicker | "Il tuo compagno di gioco AI" |
| Heading | "Ogni serata giochi merita un arbitro" |
| Subheading | "Setup, regole, punteggi, dispute — un agente AI che conosce il tuo gioco e vi aiuta al tavolo." |
| Primary CTA | "Inizia gratis" → `/register` |
| Secondary CTA | `<a href="#come-funziona">Scopri come funziona ↓</a>` — anchor link, no JS needed |

**Scroll behavior**: the secondary CTA is a plain anchor link to `#come-funziona`. Smooth scrolling is handled by `scroll-behavior: smooth` on `html` (already set in globals.css). No JavaScript required — `WelcomeHero` stays a server component.

**What's removed**: MeepleAvatar, search bar, trust indicator badges, gradient animated background, decorative blobs.

**Why no search bar**: the current hero has a prominent search, but AI chat requires registration. Showing search in the hero sets a false expectation. The search belongs in the authenticated catalog experience.

### Section 2: Come Funziona (Progressive Reveal)

**Anchor**: `id="come-funziona"` on the section wrapper element.

Four steps that mirror a real game-night scenario:

| Step | Icon | Title | Description |
|------|------|-------|-------------|
| 1 | 🎲 | Trova il gioco | Cerca nel catalogo o su BGG. Non c'è? Aggiungilo come gioco privato. |
| 2 | 📄 | Carica le regole | Upload del PDF del regolamento. L'AI lo indicizza automaticamente. |
| 3 | 🤖 | Gioca con l'arbitro AI | Setup, regole, punteggi, dispute — l'agente vi assiste al tavolo. |
| 4 | 💾 | Salva e riprendi | Non finite? L'agente ricorda lo stato della partita per la prossima volta. |

**Layout**: vertical on mobile, 2x2 grid on tablet, horizontal row on desktop. Each step has a number, icon, title, and one-line description.

**Connectors**: on desktop, a subtle dashed line connects steps 1→2→3→4 horizontally. On tablet (2x2 grid), connectors appear within each row only (1→2, 3→4) — no cross-row connectors. On mobile (vertical stack), a vertical dashed line runs between steps.

**Why 4 steps**: the save-and-resume feature is a strong differentiator. Three steps would omit it.

### Section 3: Social Proof

Single row of three stats on a `bg-muted/50` background:

| Stat | Value | Source |
|------|-------|--------|
| Giochi nel catalogo | 2.400+ | Static marketing copy, manually maintained |
| Accuratezza citazioni | 95%+ | Static marketing copy, manually maintained |
| Per iniziare | Gratis | Static |

**Design**: large number (`text-4xl font-bold`), small label beneath (`text-sm text-muted-foreground`). No testimonials, no fake reviews — only verifiable data.

**Data source**: these are static constants in the component. Accepted trade-off: values require manual updates when they become stale. A future iteration may fetch the game count from the API.

### Section 4: CTA Finale

| Element | Content |
|---------|---------|
| Heading | "Pronto per la prossima serata giochi?" |
| Subheading | "Registrati gratis e prepara il tuo primo agente AI in 5 minuti" |
| Primary CTA | "Inizia gratis" → `/register` |
| Secondary CTA | "Esplora il catalogo" → `/games` (public games route) |

**Note**: `/discover` is authenticated. The secondary CTA links to `/games` which is a public route.

**Design**: `bg-background`, centered, generous spacing. No gradients or decorations.

### Section 5: Footer (unchanged)

The existing `PublicFooter` with three columns (Brand + Quick links + About/Legal) remains as-is.

## Components to Remove

| Component | File | Stories | Tests |
|-----------|------|---------|-------|
| HeroSection | `components/landing/HeroSection.tsx` | `HeroSection.stories.tsx` | `__tests__/HeroSection.test.tsx` |
| GamesCarouselSection | `components/landing/GamesCarouselSection.tsx` | — | — |
| FeaturesSection | `components/landing/FeaturesSection.tsx` | `FeaturesSection.stories.tsx` | `__tests__/FeaturesSection.test.tsx` |
| HowItWorksSection | `components/landing/HowItWorksSection.tsx` | `HowItWorksSection.stories.tsx` | `__tests__/HowItWorksSection.test.tsx` |
| CallToActionSection | `components/landing/CallToActionSection.tsx` | — | `__tests__/CallToActionSection.test.tsx` |

**Orphaned code check**: after removing `GamesCarouselSection`, verify that hooks `useFeaturedGames`, `useTrendingGames`, and `useUserLibraryGames` are not used elsewhere. If orphaned, delete them.

## Components to Create

| Component | File | Type |
|-----------|------|------|
| WelcomeHero | `components/landing/WelcomeHero.tsx` | Server component |
| HowItWorksSteps | `components/landing/HowItWorksSteps.tsx` | Server component |
| SocialProofBar | `components/landing/SocialProofBar.tsx` | Server component |
| WelcomeCTA | `components/landing/WelcomeCTA.tsx` | Server component |

All new components are server components — no client-side JavaScript needed. This improves performance and simplifies the page.

## SEO Updates

- **Title**: "MeepleAI — Il tuo arbitro AI per le serate giochi da tavolo"
- **Description**: "Un agente AI che conosce le regole del tuo gioco. Setup, punteggi, dispute — ti assiste al tavolo. Gratis per iniziare."
- **Remove `aggregateRating`** from JSON-LD structured data — the current values (4.8/1250) are fabricated placeholders. Per the "Honest" design principle, remove the block entirely until real data is available.
- **Remove `alternates.languages['en-US']`** — the `/en` route does not exist. Keep only `'it-IT'`.
- **Update `featureList`** to match the 4 steps:
  ```
  [
    "Cerca giochi nel catalogo o importa da BGG",
    "Carica il PDF del regolamento per indicizzazione AI",
    "Agente AI per setup, regole, punteggi e dispute al tavolo",
    "Salva lo stato della partita e riprendi quando vuoi"
  ]
  ```
- **JSON-LD, Open Graph, Twitter Cards**: retain structure, update copy to match new tagline and description.

## Metadata Update

The metadata export updates:
- Title and description to match the new messaging
- Keywords: add "arbitro", "agente AI", "serata giochi"
- Open Graph and Twitter Card descriptions align with the new tagline
- Remove `alternates.languages['en-US']`
- Remove `aggregateRating` from structured data

## Page Component

The page component simplifies to four server components in sequence:

```
WelcomeHero → HowItWorksSteps → SocialProofBar → WelcomeCTA
```

Structured data script tag remains (static content, no user input). Authenticated user redirect to `/dashboard` remains.

## Responsive Behavior

| Breakpoint | Hero | Steps | Stats | CTA |
|------------|------|-------|-------|-----|
| Mobile (<640px) | Stack, smaller type | Vertical stack, vertical connector | Vertical stack | Stack buttons |
| Tablet (640-1024px) | Same, larger type | 2x2 grid, row-only connectors | Horizontal row | Side-by-side buttons |
| Desktop (>1024px) | Same, max-width 720px | Horizontal row, full connector | Horizontal row | Side-by-side buttons |

## Performance Targets

Same as current:
- Lighthouse Performance: ≥90
- Lighthouse Accessibility: ≥95
- First Contentful Paint: <1.5s
- Time to Interactive: <2.5s

All server components (no client JS) should improve these metrics.

## Out of Scope

- Navbar/header changes (handled by existing PublicLayout)
- Footer redesign
- Authentication flow changes
- Catalog or game detail pages
- Internationalization (stays Italian-only for now)

## Old Components Cleanup

After the new page is live and tested, delete the old landing components, their stories, and their tests. Check for orphaned hooks and utilities. Do not leave dead code.

## Testing

- **Unit tests**: create `__tests__/WelcomeHero.test.tsx`, `__tests__/HowItWorksSteps.test.tsx`, `__tests__/SocialProofBar.test.tsx`, `__tests__/WelcomeCTA.test.tsx` covering render output, CTA link targets, anchor IDs, and accessibility (axe-core)
- **Visual regression**: screenshot before/after
- **Accessibility**: axe-core audit on all sections
- **SEO**: verify structured data (no `aggregateRating`), meta tags, OG tags, no broken `hreflang`
- **Responsive**: test all three breakpoints
- **Redirect**: authenticated users still redirect to `/dashboard`
- **Dark mode**: verify all sections render correctly in both light and dark themes
