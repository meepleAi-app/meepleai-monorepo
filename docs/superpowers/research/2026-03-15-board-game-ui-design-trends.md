# Modern UI/UX Design Trends for Board Game Digital Platforms (2024-2026)

**Research Date**: 2026-03-15
**Confidence Level**: 0.82 (High - multiple corroborating sources across domains)
**Scope**: Board game platform UI, card-based components, gaming aesthetics, color/typography trends

---

## Executive Summary

The board game digital platform space in 2025-2026 is converging on a visual language that blends **warm, tactile physicality** with **modern glassmorphism and spatial UI** patterns. The dominant trends are: dark-first interfaces with warm amber/orange accents, bento grid layouts for collections, glassmorphism cards with parallax micro-interactions, and playful-but-professional rounded geometric typography. Gaming storefronts (Steam, Epic, PlayStation) have established the template: hero imagery dominates, dark backgrounds maximize visual contrast, and cards serve as the atomic unit of content discovery.

**Key takeaway for MeepleAI**: Your existing MeepleCard v2 design tokens (warm brown shadows, entity-colored accents, Quicksand/Nunito typography) are already well-aligned with 2025-2026 trends. The areas with the most room for differentiation are: 3D parallax card interactions, glassmorphism overlays on dark mode, flip-card animations for game details, and bento grid collection displays.

---

## 1. Modern Board Game App/Platform Designs (2024-2026)

### BoardGameGeek (BGG)

BGG remains the dominant database with 10M+ registered users but is widely acknowledged as having an "old-school" interface. Their mobile app (iOS/Android, updated through 2025) has modernized more aggressively than the website -- taking "every single detail on the admittedly old-school site and making it look great on modern phones." The website itself has not undergone a major visual redesign, maintaining its information-dense, text-heavy layout. This creates a clear **market opportunity** for platforms that deliver the same depth of information with modern visual language.

**BGG's implicit design lessons:**
- Information density is valued by serious board gamers (ratings, weight, player count, play time all visible at a glance)
- User-generated content (reviews, images, session reports) needs first-class visual treatment
- Collection management is the killer feature -- how you display "your shelf" matters enormously

### Board Game Arena (BGA)

BGA (v4.1.0 August 2025, v4.2.1 December 2025) serves 10M+ players and has iterated toward:
- **Cross-platform sync** as a core UX feature
- **Enhanced accessibility** features (December 2025 update)
- Clean, functional game lobby interfaces prioritizing matchmaking over visual polish
- Game table UIs that faithfully represent physical components in 2D with clear state indicators

### Dized (Companion App)

Dized represents the **companion app** paradigm -- interactive tutorials that teach games while you play. The design philosophy is:
- Step-by-step visual instructions with highlighted game components
- Minimal chrome, maximum focus on the physical game being played
- Progressive disclosure of rules complexity

### Emerging Pattern: Hybrid Physical-Digital

A significant 2025 trend is hybrid gaming: companion apps, AR overlays, and shared touchscreen surfaces where "players gather around shared touchscreens using intuitive, tactile pieces that interact with the board." The design implication is that digital interfaces for board games should feel like they **belong alongside** physical components, not replace them.

---

## 2. Card-Based UI Design Trends (2025-2026)

### Glassmorphism: The Dominant Card Aesthetic

Glassmorphism has evolved from a trend into a mature pattern, especially for cards and floating panels. The 2026 iteration merges glass effects with **liquid-like interactions**, creating "interfaces that are fluid, haptic, and almost alive."

**Core implementation (production-ready CSS):**
```css
.glass-card {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  -webkit-backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 16px;
  box-shadow: 0 4px 30px rgba(0, 0, 0, 0.1);
}
```

**Five defining visual characteristics:**
1. **Backdrop blur** -- mimics foggy glass, obscures background without hiding it
2. **Transparency** -- semi-transparent layering with softened appearance
3. **Depth and layering** -- elements "float" over colorful backgrounds
4. **Soft borders** -- rounded corners (12-24px) with faint outline highlights
5. **Minimalist approach** -- clean typography paired with simplified icons

**Dark mode caution:** "Translucent panels sometimes glow instead of receding" in dark themes. Every glass effect must be tested in both light and dark modes -- "What dazzles in daylight may vanish at dusk."

**Best applications for board game platforms:**
- Game detail overlays floating above collection backgrounds
- Dashboard stat cards for play history and statistics
- Agent/AI chat interfaces with translucent message bubbles
- Modal dialogs for game session setup

### 3D Parallax Tilt Cards

Websites with 3D interactive elements see **up to 40% increase in time on page** (2025 behavioral analytics). The parallax tilt card is the most impactful card-level interaction pattern:

**How it works:**
- JavaScript tracks mouse position relative to card center
- CSS `transform: perspective(1000px) rotateX(Xdeg) rotateY(Ydeg)` creates tilt
- Inner elements shift at different rates for parallax depth
- `transform-style: preserve-3d` enables layered depth within the card

**Key libraries:**
- **Atropos** -- touch-friendly 3D parallax, production-ready
- **Vanilla Tilt** -- zero dependencies, smooth 3D tilt
- **Card3d.js** -- includes glare, scaling, mobile gyroscope support
- **react-parallax-tilt** -- React-specific with all features

**Application for MeepleAI:**
- Game cards in collection view: subtle tilt on hover reveals depth (game image shifts behind title overlay)
- Featured/hero cards: more dramatic tilt with glare effect simulating holographic card stock
- Agent cards: personality visualization with layered elements

### Neumorphism (Evolved)

Soft UI / neumorphism has matured from its problematic 2020 origins. The 2026 version uses "soft shadows and highlights" for subtle 3D effects while maintaining accessibility standards. Best suited for:
- Toggle switches and settings interfaces
- Stat counters and progress indicators
- Input fields in form contexts
- **Not recommended** for primary navigation or content cards (accessibility concerns persist)

### Micro-Interactions

The shift from "flashy animations" to **"purposeful Motion UI"** is definitive in 2026. Micro-interactions now "inject brand personality into every tap, swipe, and hover" while serving functional purposes:

- **Card hover lift**: translate-y + shadow expansion (0.2s ease-out)
- **Loading shimmer**: skeleton states with traveling gradient
- **State transitions**: smooth color shifts on selection/deselection
- **Haptic feedback**: subtle scale pulse on tap (mobile)
- **Progressive reveal**: stagger-animate card grid items on scroll

---

## 3. Board Game Aesthetic in Digital Interfaces

### The "Refined Tactile" Direction

Modern board game digital platforms are moving away from heavy skeuomorphism (literal wood grain textures, faux leather) toward what could be called **"refined tactile"** -- subtle physical references that maintain cleanliness:

**What works in 2025-2026:**
- **Warm-toned shadows** instead of neutral grays (MeepleAI's `rgba(180,130,80,x)` approach is exactly right)
- **Subtle paper/linen texture overlays** at very low opacity (3-5%) on card backgrounds
- **Rounded corners everywhere** (12-24px) echoing physical card stock
- **Meeple/dice iconography** as accent elements, not dominant decoration
- **Board game piece silhouettes** in empty states and loading screens

**What feels dated:**
- Literal wood plank backgrounds
- Faux leather textures
- Heavy drop shadows simulating physical stacking
- Ornate medieval/fantasy borders unless the platform is specifically themed

### Design-Led Board Game Influence

The rise of design-led publishers (CMYK, Itten, Weast Coast) is pushing board game aesthetics toward sophistication:
- **Wavelength**: "minimal typography and spacey, 1970s-hued" visuals
- **Tokyo Highway**: rubber materials and functional design elements
- **Snakes of Wrath**: "Edwardian-inspired typography and bold serpentine iconography"

The principle: "how a game looks should tell you something about how it plays." For a platform displaying many games, the **platform chrome should be neutral and warm**, allowing individual game art to provide the visual variety.

### Physical-to-Digital Translation Patterns

| Physical Element | Digital Translation |
|---|---|
| Card stock weight/thickness | Box shadows with warm brown tones, subtle border |
| Linen finish texture | 2-3% opacity noise overlay on card surface |
| Rounded card corners | 12-16px border-radius consistently |
| Meeple pieces | SVG icon system with entity-colored fills |
| Dice | Randomization UI with tumble animations |
| Game box cover | Hero image with controlled aspect ratio (4:3 or 16:9) |
| Board game shelf | Bento grid or horizontal scroll carousel |
| Score track | Radial progress or stepped progress bar |
| Player tokens | Avatar system with entity-colored rings |

---

## 4. Color and Typography Trends

### Color: Warm, Intentional, Restrained

The 2026 direction is "calmer, deeper, and more focused on function" -- one or two base neutrals carry most screens, with accent shades limited, clear, and linked to actions.

**Recommended dark mode palette for gaming platforms:**

| Role | Approach |
|---|---|
| **Background primary** | Near-black with warm undertone (`hsl(220, 15%, 8%)` or `hsl(30, 5%, 6%)`) |
| **Background secondary** | Slightly elevated warm dark (`hsl(220, 12%, 12%)`) |
| **Surface/card** | Lifted surface (`hsl(220, 10%, 15%)`) |
| **Accent primary** | Warm amber/orange (MeepleAI's game entity orange: `25 95% 45%` is on-trend) |
| **Accent secondary** | Complementary cool tone for contrast (teal or indigo) |
| **Text primary** | Off-white, never pure white (`hsl(40, 10%, 90%)`) |
| **Text secondary** | Muted warm gray (`hsl(40, 5%, 60%)`) |
| **Success/Error/Warning** | Desaturated versions in dark mode (never use saturated colors on dark surfaces) |

**Key principle from color research:** "Shimmering gold and warm orange feel like a rare drop reveal" -- the warm amber palette evokes **reward and discovery**, which is emotionally perfect for a game collection platform.

**The 60/30/10 Rule for game UI:**
- 60% dark background neutrals
- 30% surface/card colors and secondary elements
- 10% accent colors (entity colors, CTAs, highlights)

### Typography: Playful Geometry Meets Readability

**2026 typography trends relevant to gaming platforms:**

1. **Rounded geometric sans-serifs** dominate -- Quicksand (already in MeepleAI), Poppins, Figtree, Geist Sans
2. **Variable fonts** enabling fluid responsiveness across devices
3. **"Playful colour palettes, dynamic sizing, and a freer, less disciplined approach"** in headings
4. **Serifs making a comeback** for warmth and brand personality, but paired with clean sans-serif body text

**Specific font recommendations for board game platforms:**

| Role | Font | Why |
|---|---|---|
| **Headings** | Quicksand 600-700 | Rounded terminals = friendly, geometric = modern, excellent at large sizes |
| **Body text** | Nunito 400-600 | Rounded but highly readable at small sizes, pairs naturally with Quicksand |
| **Metadata/Labels** | Geist Sans or Inter | Clean, neutral, excellent at 10-12px for stats and counts |
| **Display/Hero** | Fraunces or Playfair Display | Serif option for featured content, adds warmth and personality |
| **Monospace (stats)** | JetBrains Mono or Fira Code | For aligned numerical data like scores, play counts |

**Accessibility mandate:** Never use gray text for readability -- "use deeper colors for text instead of pure black to make reading softer on the eyes while maintaining high contrast." WCAG AA minimum 4.5:1 contrast ratio.

---

## 5. Specific Design Patterns

### Card Flip Animations

Card flipping is one of the most natural interactions for a board game platform -- it directly maps to the physical act of flipping a game card.

**Implementation approach:**
```css
.flip-container { perspective: 1000px; }
.flip-card {
  transition: transform 0.6s ease-in-out;
  transform-style: preserve-3d;
}
.flip-card.flipped { transform: rotateY(180deg); }
.flip-card-front, .flip-card-back {
  backface-visibility: hidden;
  position: absolute; inset: 0;
}
.flip-card-back { transform: rotateY(180deg); }
```

**Best practices:**
- Transition duration: **0.4s-0.8s** for natural-feeling rotation
- Use `perspective: 800-1200px` for realistic depth (lower = more dramatic)
- Hardware-accelerated via `will-change: transform` for 60fps
- **Front face**: Game cover image, title, quick stats (rating, player count)
- **Back face**: Description, category tags, your rating, play history, quick actions

**Board game platform applications:**
- Collection grid: tap/click to flip and see game details without navigating away
- Discovery feed: flip to reveal why this game was recommended
- Wishlist: flip to show purchase links or availability

### Collection/Shelf Displays

The **bento grid** is the cutting-edge pattern for game collections, with 67% of top SaaS sites now using some form of bento layout.

**Bento grid for game collections:**
- **Hero slot** (spans 2 columns): Currently playing or recently added game with large cover art
- **Standard slots** (1 column): Collection items in consistent card format
- **Stats slot** (spans 2 columns): Collection statistics, play history graph
- **Recommendation slot**: AI-suggested games based on collection

**Implementation fundamentals:**
```css
.collection-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}
.hero-card { grid-column: span 2; grid-row: span 2; }
.stats-card { grid-column: span 2; }
```

**Responsive behavior:**
- Desktop: 4-6 columns with varied spans
- Tablet: 3 columns, maintain proportions
- Mobile: Single column stacks, hero card still full-width

**Alternative: Horizontal scroll "shelf":**
- Mimics physical bookshelf with horizontal scrolling rows
- Categories as shelf labels ("Strategy", "Party Games", "Co-op")
- Peeking cards at edges hint at scrollability
- Snap scrolling for satisfying interaction
- Used by Netflix, Steam Discovery Queue, PlayStation Store

**Visual enhancements:**
- **Skeleton loading**: Maintain layout stability during data fetching
- **Stagger animation**: Cards animate in with 50ms delays on scroll-into-view
- **Color-coded grouping**: Subtle background tints for category sections (as in Notion's approach)

### Rating/Scoring Visualizations

**Modern alternatives to plain star ratings:**

1. **Radial/donut progress**: Game rating shown as a colored arc (0-10 scale maps well to 0-360 degrees). Entity color fills the arc. Works beautifully at small sizes (32-48px).

2. **Multi-axis radar chart**: For complex games, show Weight, Strategy, Luck, Interaction, Theme as a pentagon radar. Compact and instantly communicative.

3. **Segmented bar**: Horizontal bar divided into rating distribution segments. Shows community consensus at a glance (how many rated 8+, 6-7, etc.)

4. **Custom icon rating**: Replace stars with **meeple icons** -- filled meeples for rating, outline for remainder. Reinforces brand identity in every rating display.

5. **Comparative bar**: "Your rating vs. Community average" shown as two overlapping bars. Highlights personal taste divergence.

**Implementation with SVG** (recommended over CSS for animation control):
```jsx
// Radial rating - entity colored
<svg viewBox="0 0 36 36">
  <circle cx="18" cy="18" r="16" fill="none"
    stroke="hsl(var(--color-entity-game) / 0.15)" strokeWidth="3" />
  <circle cx="18" cy="18" r="16" fill="none"
    stroke="hsl(var(--color-entity-game))" strokeWidth="3"
    strokeDasharray={`${rating * 10} 100`}
    strokeLinecap="round" transform="rotate(-90 18 18)" />
  <text x="18" y="20" textAnchor="middle" fontSize="10" fontWeight="bold">
    {rating}
  </text>
</svg>
```

### Tag/Category Systems with Game-Themed Visuals

**Component hierarchy (from research):**
- **Badges** (static): Player count indicator, complexity weight, game status (owned, wishlisted, played)
- **Tags** (interactive): Game categories (Strategy, Party, Co-op), mechanics (Deck Building, Worker Placement)
- **Chips** (input): Filter selections, search refinements

**Game-themed enhancements:**
- Category tags with **small leading icons** -- dice icon for luck-heavy, brain icon for strategy, people icon for party
- **Entity-colored tag borders** that match the game type color system
- Mechanic tags with **subtle gradient backgrounds** using muted game colors
- Weight indicator as a **filled bar** (1-5) inline with the tag
- Player count as a **meeple icon x N** rather than plain text

**Touch targets:** Minimum 48x48px on mobile with 8px spacing between interactive tags.

**Naming convention:** Use explicit terms like "Filter tags" or "Status badges" to prevent designer/developer confusion.

### Dark Mode for Gaming Platforms

Dark mode is used by **81.9% of smartphone users** and is the expected default for gaming platforms.

**Gaming platform dark mode principles:**

1. **Never pure black** (`#000`). Use warm near-blacks: `hsl(220, 15%, 8%)` or `hsl(30, 8%, 7%)`.

2. **Desaturate accent colors** for dark surfaces. Saturated colors "visually vibrate" against dark backgrounds. Reduce saturation 10-20% and increase lightness 5-10% for dark mode variants.

3. **Elevation through lightness, not shadow.** Higher surfaces = slightly lighter background. Cards sit at a lighter shade than the page background. This matches Material Design elevation but with warm undertones.

4. **"Tinted frosting" for glassmorphism.** Instead of white frosted glass, use entity-colored tinted frost: `rgba(255, 160, 50, 0.05)` for game cards, `rgba(168, 85, 247, 0.05)` for player cards.

5. **Preserve game cover art vibrancy.** The entire dark UI exists to make game artwork pop. Covers should have no overlay darkening in default state.

6. **Text hierarchy through opacity, not color changes.** Primary text at 87% white, secondary at 60%, disabled at 38% (Material Design dark theme pattern).

---

## 6. Inspiration from Gaming Industry Storefronts

### Steam Store

**Card presentation patterns:**
- **Capsule images** are the atomic unit: Header (920x430), Small (462x174), Main (1232x706), Vertical (748x896)
- Design for **smallest size first** -- logo must be readable at 120x45px
- **Genre must be obvious in under one second** from the capsule art alone
- Dark interface demands **high contrast** -- "mid-tone elements blend together"
- Strict content rules: no review scores, award logos, or marketing text on capsules
- **Hover states**: subtle scale + info overlay with tags, price, and review summary

**Design lessons for MeepleAI:**
- Game covers are the hero -- the card design must serve the artwork first
- A single focal point per card (one mood, one character, one focus)
- Dark backgrounds are non-negotiable for game content platforms

### Epic Games Store

Epic's design system was built on:
- **Unified visual design language (VDL)** with universal guidelines
- Component systems including buttons, typography, and **dark theme variants** with contained overlays
- Responsive wireframes for web and mobile
- **User journey maps** informing card layout priorities
- Information architecture designed for "game consumer, developer, and game publisher interactions"

The Epic approach emphasizes that a storefront is a **"gateway to a cohesive, intuitive user-centered product ecosystem"** -- the card is not just a display unit but a navigation portal.

### PlayStation Store / Xbox Store

**Common patterns across console storefronts:**
- **Hero carousel** at top: 16:9 or wider aspect ratio, auto-advancing with manual control
- **Horizontal scroll rows** organized by category ("New Releases", "Trending", "For You")
- **Consistent card sizes within rows** but varied sizes between rows (larger for featured)
- **Hover/focus state**: scale up + elevated shadow + info panel slide-in from bottom
- **Dark mode only** -- no light mode option
- **Accent colors** match platform branding (PlayStation blue, Xbox green) but game content uses its own colors

### Cross-Platform Design Principles

| Pattern | Steam | Epic | PlayStation | Recommendation for MeepleAI |
|---|---|---|---|---|
| Background | Dark slate | Dark charcoal | Dark blue-black | Warm dark (`hsl(220, 15%, 8%)`) |
| Card shape | Landscape capsule | Mixed aspect | Landscape dominant | Support both landscape (grid) and portrait (shelf) |
| Hover effect | Scale + overlay | Scale + highlight border | Scale + info slide | Scale + warm shadow expand + tilt |
| Rating display | Colored percentage bar | Star rating | None on cards | Radial meeple-themed rating |
| Tags | Blue pills | Gray chips | Hidden on cards | Entity-colored interactive tags |
| Typography | System fonts | Custom geometric sans | Console system font | Quicksand/Nunito (warm, rounded) |

---

## 7. Cutting-Edge Patterns for 2025-2026

### Bento Grid + Glassmorphism Fusion

The most forward-looking pattern combines bento grid layouts with glassmorphism cards:
- Asymmetric grid with glass-effect cards over a gradient or blurred background
- **"Exaggerated corner rounding and subtle micro-interactions within each tile"** make it feel tactile
- Hero tile with frosted glass overlay on a vibrant game image
- Stat tiles with semi-transparent backgrounds showing dashboard data
- Maximum 12-15 visible cards to maintain organization

### AI-Personalized Layouts

71% of consumers expect personalization. For MeepleAI:
- AI-curated "For You" bento row with explanation chips ("Because you played Wingspan")
- Agent recommendation cards with glassmorphism treatment
- Dynamic grid reordering based on engagement patterns

### Motion Design System

The 2026 standard is a **codified motion system**, not ad-hoc animations:
- `--duration-fast: 150ms` (micro-interactions: button press, toggle)
- `--duration-normal: 300ms` (card state changes, hover effects)
- `--duration-slow: 500ms` (card flip, page transitions)
- `--duration-dramatic: 800ms` (hero reveals, first-load animations)
- `--easing-standard: cubic-bezier(0.4, 0, 0.2, 1)` (Material standard)
- `--easing-decelerate: cubic-bezier(0, 0, 0.2, 1)` (entering elements)
- `--easing-accelerate: cubic-bezier(0.4, 0, 1, 1)` (exiting elements)

### Aurora/Gradient Effects

Gradients in 2026 are "mood-setting tools" with animated chromatic transitions:
- Subtle aurora gradient behind collection page header
- Entity-colored gradient overlays on hero cards (already in MeepleCard v2 -- extend with animation)
- Animated mesh gradients as page section dividers
- **Implementation**: CSS `@property` animations for smooth gradient transitions, or canvas-based for complex aurora effects

---

## Methodology

**Search Strategy**: 14 parallel web searches across 6 domains (board game platforms, card UI trends, physical-digital translation, color/typography, specific patterns, gaming storefronts). 8 deep-content extractions from authoritative sources.

**Source Tiers Applied**:
- Tier 1 (0.9-1.0): Official documentation (Steam Capsule Guide, Epic Design Guidelines)
- Tier 2 (0.7-0.9): Industry publications (Creative Bloq, It's Nice That, Clay Global)
- Tier 3 (0.5-0.7): Design community resources (Dribbble, Behance, Medium design blogs)
- Tier 4 (0.3-0.5): Aggregate trend articles (general "trends 2026" roundups)

**Confidence Notes**:
- Board game platform UI specifics (BGG, BGA): moderate confidence -- these platforms do not publish detailed design documentation
- Card UI implementation patterns: high confidence -- multiple corroborating technical sources
- Gaming storefront patterns: high confidence -- official documentation available
- Trend predictions for 2026: moderate confidence -- based on established trajectories, not guarantees

---

## Sources

- [12 UI/UX Design Trends That Will Dominate 2026 (Data-Backed)](https://www.index.dev/blog/ui-ux-design-trends)
- [Glassmorphism in UX: Reshaping Modern Interfaces](https://clay.global/blog/glassmorphism-ui)
- [Bento Grid Design: Modern Modular Layouts Guide 2026](https://landdding.com/blog/blog-bento-grid-design-guide)
- [Steam Capsule Art Design Guide](https://www.steamcapsule.com/guide)
- [Steam Store Graphical Assets Documentation](https://partner.steamgames.com/doc/store/assets/standard)
- [Steam Capsule Art Guide 2026 (presskit.gg)](https://presskit.gg/field-guides/steam-capsule-art-guide)
- [Colors in Game UI (Dakota Galayde)](https://www.galaydegames.com/blog/colors-i)
- [The Rise of Design-Led Board Games (It's Nice That)](https://www.itsnicethat.com/features/game-on-the-rise-of-design-led-board-games-graphic-design-041224)
- [Badges vs Pills vs Chips vs Tags (Smart Interface Design Patterns)](https://smart-interface-design-patterns.com/articles/badges-chips-tags-pills/)
- [Epic Games Store UX Design (Phillip Harris Portfolio)](https://philharrisdesign.com/portfolio/epic-games-store/)
- [Modern App Colors: Palettes That Work in 2026](https://webosmotic.com/blog/modern-app-colors/)
- [Gaming Color Palette Combinations](https://www.media.io/color-palette/gaming-color-palette.html)
- [Dark Mode in App Design (Ramotion)](https://www.ramotion.com/blog/dark-mode-in-app-design/)
- [Board Game Arena](https://www.boardgamearenagame.com/)
- [Game UI Database](https://www.gameuidatabase.com/)
- [Board Game Industry Trends 2025](https://gameshaven.co.uk/board-game-industry-trends-2025/)
- [CSS Flip Cards Collection (freefrontend)](https://freefrontend.com/css-flip-cards/)
- [3D Parallax Hover Effect (TailwindCSS)](https://dev.to/shofol/3d-parallax-hover-effect-using-tailwindcss-2ff2)
- [React Parallax Tilt](https://awesome-react.dev/library/react-parallax-tilt)
- [Typography Trends 2025 (Fontfabric)](https://www.fontfabric.com/blog/top-typography-trends-2025/)
- [Breaking Rules and Bringing Joy: Typography Trends 2026 (Creative Bloq)](https://www.creativebloq.com/design/fonts-typography/breaking-rules-and-bringing-joy-top-typography-trends-for-2026)
- [Best UI Design Fonts 2026](https://www.designmonks.co/blog/best-fonts-for-ui-design)
- [Web Design Trends 2026 (Elementor)](https://elementor.com/blog/web-design-trends-2026/)
- [Dribbble: Board Game UI](https://dribbble.com/tags/board-game-ui)
- [Behance: Board Game UI Projects](https://www.behance.net/search/projects/board%20game%20ui)
