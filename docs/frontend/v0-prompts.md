# MeepleAI — v0.dev Prompts

> Incolla ogni prompt direttamente su https://v0.dev
> Usa sempre il **contesto base** prima di ogni pagina specifica.
> Ordine consigliato: Game Detail → Library → Active Session → Landing

---

## CONTESTO BASE
> Copia questo blocco all'inizio di ogni nuovo thread v0.

```
MeepleAI is a dark-mode-first board game assistant web app built with Next.js + shadcn/ui + Tailwind CSS.

Design system:
- Background: #0f172a (base), #1e293b (surface/cards), #263347 (card hover)
- Primary accent: hsl(25, 95%, 38%) — warm orange
- Fonts: Quicksand (headings, weights 500-700) + Nunito (body/UI, weights 400-800)
- Shadows: warm brown-toned, e.g. rgba(180,130,80,0.12) — not neutral gray
- Dark mode only. No light theme needed.
- Border radius: 12px for cards, 8px for buttons/inputs
- Entity color system:
  - game → hsl(25, 95%, 45%) orange
  - player → hsl(262, 83%, 58%) purple
  - session → hsl(240, 60%, 55%) indigo
  - agent → hsl(38, 92%, 50%) amber
  - document → hsl(210, 40%, 55%) slate
  - chat → hsl(220, 80%, 55%) blue
  - event → hsl(350, 89%, 60%) rose

Use shadcn/ui components (Card, Button, Badge, Avatar, Input, Tabs, etc.) wherever applicable.
Use Lucide icons.
```

---

## PROMPT 1 — Game Detail Page

```
Using the MeepleAI design system above, build a Game Detail page for a board game app.

Layout:
- Sticky top navbar (height 52px, background #0f172a/95 with backdrop-blur):
  - Left: hexagonal logo icon (purple gradient #6366f1→#8b5cf6) + wordmark "MeepleAi" (Quicksand bold, "Ai" in orange)
  - Center: nav links — Libreria, Catalogo, Sessioni (Nunito 600, muted color, active link has subtle bg)
  - Right: search bar (expandable, searches game catalog) + user avatar
- MiniNav tabs below navbar (height 40px, surface bg): Panoramica | Sessioni | Documenti | Statistiche

Page content (max-width 1100px, centered, padding 24px):
- Page header section:
  - Game cover image (80x80px, rounded-lg, orange border accent, placeholder gradient)
  - Game title (Quicksand bold 24px)
  - Metadata pills row: "3–4 giocatori" · "90 min" · "★ 7.8 BGG" (small badges, muted bg)
  - Entity badge top-left: "GIOCO" pill in orange (10px Quicksand bold uppercase)
- Stats row: 4 equal cards side by side
  - "42 sessioni totali", "6h 20m ultima sessione", "3 PDF caricati", "Mario · più vittorie"
  - Cards: surface bg, warm shadow, Quicksand bold number, Nunito muted label
- Section: "Documenti AI" — list of uploaded PDFs with status badges (green "Pronto", yellow "Indicizzando", red "Errore") + upload CTA button
- Section: "Ultime sessioni" — 3 compact session cards (date, players avatars row, winner name, duration)

Floating glassmorphism pill (fixed, bottom-center, z-50):
- Background: rgba(30,41,59,0.85) with backdrop-blur-md
- Border: 1px solid rgba(255,255,255,0.10)
- Border-radius: 40px
- Shadow: 0 8px 32px rgba(0,0,0,0.4)
- Content left: context label "🎲 Settlers of Catan" in muted text (Nunito 600 11px)
- Thin vertical divider
- Buttons: "▶️ Nuova sessione" (primary orange filled, rounded-full), "🤖 AI" (ghost), "❤️" (ghost), "📤" (ghost), "⋯" (ghost)
- All pill buttons: Nunito bold 12px, compact padding

Mobile (< 640px):
- Hide nav links, show hamburger
- Replace floating pill with: context breadcrumb strip + sticky bottom action bar (4 icon+label slots)
- FAB: orange circle 48px fixed bottom-right above action bar, context-aware icon
```

---

## PROMPT 2 — Library Page (Game Grid)

```
Using the MeepleAI design system above, build the personal Library page — the user's game collection.

Layout:
- Same sticky navbar as the Game Detail page (Libreria link active)
- Page header: title "La mia libreria" (Quicksand bold 22px) + game count badge + toggle grid/list view button
- Filter bar below header: horizontal chips row — All | Owned | Wishlist | filter by players (2,3,4,5+) | sort dropdown (A-Z, Last played, Rating, Added)
- Main content: responsive grid of MeepleCards
  - Mobile: 2 columns
  - Tablet: 3 columns
  - Desktop: 4–5 columns
  - Gap: 16px

MeepleCard (game entity, grid variant):
- Background: #1e293b, border-radius 12px, warm shadow
- Left border accent: 4px solid hsl(25,95%,45%) orange
- Top-left badge: "GIOCO" pill (Quicksand bold 10px uppercase, orange bg)
- Cover image: aspect-ratio 4/3, object-cover, top of card
- Card body (padding 12px):
  - Title: Quicksand bold 14px, white
  - Subtitle: publisher name, Nunito 12px muted
  - Metadata row: star rating + player count icon + duration icon (Nunito semibold 11px)
  - Bottom row: "Ultima: 3 gg fa" muted text + wishlist heart icon toggle
- Hover: left border 6px, shadow-warm-xl, subtle orange glow ring (outline)

Empty state (when no games):
- Centered illustration area (use a simple meeple emoji large + decorative elements)
- Heading: "La tua libreria è vuota" (Quicksand bold 18px)
- Subtext: "Aggiungi il tuo primo gioco dal catalogo community" (Nunito muted)
- CTA button: "Sfoglia il catalogo" (primary orange)

Floating glassmorphism pill (desktop, fixed bottom-center):
- Context: "📚 La mia libreria"
- Actions: "➕ Aggiungi gioco" (primary), "🔍 Filtra" (ghost), "↕️ Ordina" (ghost), "📊 Vista" (ghost)

Mobile bottom action bar (sticky):
- 3 slots: ➕ Aggiungi | 🔍 Cerca | ↕️ Ordina + overflow ⋯
```

---

## PROMPT 3 — Active Session Page

```
Using the MeepleAI design system above, build an Active Game Session page. This is used during live gameplay — players track scores while an AI assistant is available for rules questions.

Layout (desktop — two column):
- Sticky navbar (same as before, "Sessioni" active)
- MiniNav: "Punteggi" | "Note" | "Timer" | "Impostazioni"
- Main content split:
  - LEFT column (flex 1): score tracking
  - RIGHT column (360px, collapsible): AI chat panel

LEFT — Score Tracker:
- Game context header: small game cover + "Settlers of Catan" + session duration timer (live, Quicksand mono 20px orange)
- Players score list: each player row contains:
  - Avatar circle (purple gradient, player initial)
  - Player name (Nunito bold 14px)
  - Score (Quicksand bold 28px white, centered)
  - +/- buttons (rounded, secondary style)
  - Rank indicator (🥇🥈🥉 for top 3)
- Add player button (ghost dashed border)
- Winner highlight: top-score player row has orange left accent + subtle glow

RIGHT — AI Chat Panel:
- Panel header: "🤖 Assistente AI" title + collapse button (←)
- Game context chip: "🎲 Settlers of Catan · 3 PDF indicizzati" (small, muted, green dot)
- Chat messages area (scrollable):
  - User bubble: right-aligned, orange bg, Nunito 14px
  - AI bubble: left-aligned, surface bg, border, Nunito 14px, supports markdown
  - Citation card (inside AI bubble): small card with "📄 pag. 34 — Regolamento.pdf" + excerpt snippet, slate border
  - Typing indicator: three animated dots in AI bubble
- Input area (sticky bottom of panel):
  - Text input (full width, surface bg)
  - Send button (orange icon), mic icon, attach icon

Mobile layout:
- Single column score tracker
- AI panel hidden by default, accessible via "🤖 AI" action in bottom bar
- Bottom action bar: 🤖 AI | ⏱️ Timer | 📝 Note | ⏸️ Pausa | ✅ Termina

Floating pill (desktop, fixed bottom-center):
- Context: "▶️ Sessione in corso · 45 min"
- Actions: "🤖 AI" (primary), "⏸️ Pausa" (ghost), "✅ Termina" (ghost destructive style)

Session states to show in comments or as variants:
- "in progress" (default shown above)
- "paused": full-page semi-transparent overlay + "Riprendi" centered button
- "ended": podium results layout + "Salva e condividi" CTA
```

---

## PROMPT 4 — Landing / Home Page

```
Using the MeepleAI design system above, build the marketing landing page for MeepleAI — an AI-powered board game assistant.

This page is for unauthenticated visitors. Tone: playful, smart, community-oriented. Like Discord meets a board game café.

Sections:

1. NAVBAR (unauthenticated):
- Logo left, nav links center (Come funziona, Catalogo, FAQ), right: "Accedi" (ghost) + "Inizia gratis" (primary orange)

2. HERO:
- Background: #0f172a with subtle radial gradient glow in orange hsl(25,95%,20%) behind main element
- Headline (Quicksand bold 48px): "Il tuo assistente AI per i giochi da tavolo"
- Subheadline (Nunito 18px muted): "Tieni traccia delle tue partite, consulta i regolamenti con l'AI e scopri nuovi giochi con la community."
- CTA buttons: "Inizia gratis" (primary large) + "Scopri il catalogo" (ghost)
- Hero visual: stylized dashboard preview or meeple mascot illustration area (use emoji + gradient placeholder)

3. FEATURES (3 columns):
- 🤖 "Regole sempre a portata di mano" — carica il PDF del regolamento, chiedi all'AI durante la partita
- 🎲 "Traccia ogni partita" — punteggi, statistiche, storico sessioni
- 📚 "Catalogo community" — migliaia di giochi, aggiungi i tuoi preferiti alla libreria
- Cards: surface bg, icon large (gradient circle), title Quicksand bold 16px, description Nunito 14px muted

4. HOW IT WORKS (numbered steps, alternating layout):
- Step 1: Aggiungi giochi dal catalogo (or scan barcode)
- Step 2: Carica il regolamento PDF
- Step 3: Inizia una sessione e chiedi all'AI
- Step 4: Visualizza statistiche e storico
- Visual: step number in orange circle, Quicksand bold, connecting line between steps

5. CATALOG PREVIEW:
- Subtitle: "Scopri il catalogo community"
- Horizontal scroll row of 5–6 MeepleCards (game entity, compact variant)
- CTA: "Sfoglia tutti i giochi →"

6. CTA SECTION:
- Background: slightly lighter surface card
- Heading: "Pronto a giocare meglio?" (Quicksand bold 32px)
- Subtext + "Registrati gratis" button (primary large, orange)

7. FOOTER:
- Logo + tagline "Il tuo compagno AI al tavolo da gioco"
- Links: Come funziona · Catalogo · FAQ · Privacy · Termini
- Muted, minimal

No floating pill on landing page (unauthenticated, no context).
```

---

## PROMPT 5 — AI Chat Standalone Page

```
Using the MeepleAI design system above, build the standalone AI Chat page — a dedicated view for querying the AI assistant about board game rules.

Layout (desktop):
- Sticky navbar (same style, "AI" or no active link)
- Two-panel layout:
  - LEFT sidebar (280px, surface bg, border-right):
    - "Nuova chat" button (primary orange, full width)
    - Section "Chat recenti": list of past conversations
      - Each item: game icon emoji + "Catan — Regola del commercio" truncated + timestamp
      - Active item: orange left accent
    - Section "Giochi con KB": list of games that have indexed PDFs
      - Each item: game cover small + name + green "Pronto" dot
  - RIGHT main area (flex 1): active chat

MAIN CHAT AREA:
- Chat header: current game context chip ("🎲 Settlers of Catan · 3 PDF") or "Nessun gioco selezionato" with search CTA
- Messages area (scrollable, padding 24px):
  - Welcome state (empty):
    - Meeple AI avatar (purple hex icon, 56px)
    - "Ciao! Sono il tuo assistente AI per i giochi da tavolo."
    - 4 suggested question chips: "Qual è la regola del commercio?", "Come si vince?", "Quante risorse si pescano?", "Spiega le carte sviluppo"
  - User message bubble: right-aligned, orange bg rgba(180,60,0,0.3) with orange border, Nunito 14px
  - AI message bubble: left-aligned, surface bg, border rgba(255,255,255,0.08)
    - Supports markdown (bold, lists, headers)
    - Citation card inside bubble: "📄 pag. 34 — Regolamento Catan.pdf" with 1-line excerpt, slate border, small font
  - Typing indicator: three dots animated, inside AI bubble shell
- Input area (sticky bottom, padding 16px):
  - Game context selector (small chip button left): "🎲 Catan ▾"
  - Text input (flex 1, surface bg, border, rounded-xl): "Chiedi una regola…"
  - Icons right: 📎 attach, 🎤 voice, then 📤 send (orange)

Mobile:
- Hide left sidebar, accessible via hamburger or bottom nav
- Full-width chat, compact input
- Floating pill: not needed (chat has its own input)
```

---

## Tips per v0

- Dopo ogni generazione, chiedi: `"Make it darker and more premium. Increase contrast on text. Make card shadows warmer."` se il risultato è troppo piatto.
- Per la glass pill: `"The floating pill should feel more glassmorphism — more blur, thinner border, slightly more transparent background."`
- Per i font: v0 non carica Google Fonts automaticamente — aggiungi `"Assume Quicksand and Nunito are loaded via CSS. Apply font-family manually in className."`
- Se le card escono troppo simili: `"Apply the entity color system — game cards should have orange accent, player cards purple, session cards indigo."`
