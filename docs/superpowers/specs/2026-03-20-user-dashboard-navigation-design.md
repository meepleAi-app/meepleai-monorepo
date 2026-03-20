# User Dashboard & Navigation System — Design Spec

**Date**: 2026-03-20
**Status**: Approved
**Approach**: C — Tavolo + Hub ibrido
**Review**: Passed (iteration 2 — 14 + 3 issues fixed)

## Overview

Redesign del sistema dashboard utente e navigazione di MeepleAI. La dashboard diventa un hub centralizzato dove tutto e' una card (`MeepleCard`). La navigazione usa una navbar minimale fissa + context bar dinamica. La metafora "Il Tavolo" si applica solo ai contesti di gioco; il resto usa uno stile Hub pulito. La navigazione veloce in sessione live usa un overlay ibrido (bottom sheet mobile / side panel desktop) con deck stack sfogliabili.

## Principi

- **Se e' un'entita, e' una MeepleCard.** Il colore dice cosa e'. Il tap apre l'overlay. Sempre.
- **Tavolo dove si gioca, Hub dove si gestisce.** Due linguaggi visivi, un design system.
- **Mobile e desktop pari importanza.** Responsive, non adattivo.
- **Overlay prima, navigazione poi.** Info rapida senza perdere contesto, espansione a pagina completa su richiesta.
- **`prefers-reduced-motion` sempre rispettato.** Tutte le animazioni usano `motion-reduce:transition-none` (Tailwind) o check `useReducedMotion`. Con reduced motion attivo, le transizioni diventano cambi di stato istantanei.

## Entity Type Reference

Mapping canonico tra nomi usati in questa spec e `MeepleEntityType` dal codebase:

| Spec name | `MeepleEntityType` | Colore | Uso |
|-----------|---------------------|--------|-----|
| game | `game` | orange (25°) | Giochi, suggeriti |
| player | `player` | purple (262°) | Giocatori |
| session | `session` | indigo (240°) | Sessioni attive e passate |
| event | `event` | rose (350°) | Game night, eventi |
| agent | `agent` | amber (38°) | Agenti AI |
| kb | `kb` | slate/teal | Regole, FAQ, documenti RAG |
| chatSession | `chatSession` | blue (220°) | Thread chat (nota: `chat` e' un alias deprecato nel drawer system, usare sempre `chatSession`) |
| tool | `tool` | sky (199°) | Strumenti |
| note | `note` | warm gray | Appunti, turni sessione |

---

## 1. Navbar + Context Bar

### 1.0 Navigation Migration

**Stato attuale** del codebase che questa spec sostituisce:

| Componente attuale | Destino | Note |
|--------------------|---------|------|
| `UserTopNav` (56px, `h-14`) | **Ridotto a 48px** (`h-12`), rimuove `sectionTitle` dal centro | Diventa la navbar minimale |
| `UserDesktopSidebar` (56/180px) | **Ritirato** | La navigazione principale si sposta nelle quick actions (context bar) e nei tab di pagina |
| `UserTabBar` (bottom, mobile) | **Ritirato** | Sostituito dalla context bar + navigazione interna alle pagine |
| `CardRack` (hidden md:flex) | **Ritirato** | Funzionalita assorbita dal CardDeck e dagli overlay |
| `SwipeableContainer` | **Ritirato** | Il CardDeck gestisce lo swipe dove serve; le pagine non usano piu swipe per tab-switch |

**Nuova struttura DOM di `UserShellClient`**:

```
<div class="flex flex-col h-screen">
  <UserTopNav />           <!-- 48px, fisso -->
  <ContextBar />           <!-- 44px, dinamico, collassabile -->
  <main class="flex-1 overflow-y-auto">
    {children}             <!-- contenuto pagina -->
  </main>
</div>
```

Nessun sidebar. Nessun bottom tab bar. Navigazione primaria via context bar + contenuto pagina.

### 1.1 Navbar primaria (fissa, 48px)

Sempre visibile, minimale, glass morphism.

| Sinistra | Centro | Destra |
|----------|--------|--------|
| Logo MeepleAI (link -> dashboard) | Breadcrumb o sectionTitle | Notifiche (badge count) + Avatar utente (dropdown) |

- Mobile: logo compatto (solo icona), notifiche + avatar
- **Centro**: Per route con profondita >= 3 segmenti (es. `/sessions/abc/scoreboard`) mostra breadcrumb navigabile ("Sessione > Monopoly > Punteggi"). Per route con profondita < 3, mostra il `sectionTitle` corrente da `useNavigation()`. Il hook `useNavigation()` viene trasformato da semplice re-export di `useNavStore` a un wrapper che chiama `useNavStore()` + `usePathname()` e calcola `breadcrumbs: { label: string, href: string }[]` dal pathname corrente.

### 1.2 Context Bar (dinamica, 44px)

Subito sotto la navbar. Cambia completamente per contesto.

| Contesto | Contenuto |
|----------|-----------|
| **Dashboard** | Quick actions pills ("Nuova Partita", "Cerca Gioco", "Game Night") + ricerca globale |
| **Sessione Live** | Icona gioco + nome partita, Timer, Turno N, Giocatori (avatar stack), Punteggio leader |
| **Libreria** | Filtri toggle (Tutti/Posseduti/Wishlist/Prestati), Ordinamento, Vista grid/list, Ricerca |
| **Discovery** | Categorie dropdown, Trending/Nuovi/Top toggle, Filtri avanzati, Ricerca catalogo |
| **Game Night (dettaglio)** | Nome evento, Data countdown, Invitati (avatar stack), Giochi count, Stato |
| **Chat/AI** | Agente attivo (avatar + nome), Contesto (gioco/generico), Thread switcher |

**Note**: `GameNightContextBar` si applica solo a `/game-nights/[id]` (pagina dettaglio evento). La lista `/game-nights` non ha context bar specifica — la barra collassa.

**Comportamento scroll**:
- Scroll down 50px+ -> context bar si nasconde (slide up 150ms)
- Scroll up -> riappare
- **Eccezione**: `LiveSessionContextBar` imposta `alwaysVisible: true` nello store (vedi architettura sotto) — non si nasconde mai
- Transizione tra contesti: crossfade 200ms, altezza 44px fissa, zero layout shift

**Architettura — Zustand store pattern**:

Il `ContextBarProvider` NON e' un React Context (incompatibile con i layout RSC). E' un Zustand store, coerente con il pattern `useQuickViewStore` gia presente nel codebase.

```ts
// lib/stores/context-bar-store.ts
interface ContextBarState {
  content: ReactNode | null;
  options: { alwaysVisible: boolean };
  setContent: (content: ReactNode | null) => void;
  setOptions: (options: Partial<ContextBarOptions>) => void;
  clear: () => void;
}
```

Ogni route group registra il suo contenuto tramite un componente client `'use client'` nel proprio layout:

```tsx
// Esempio: (authenticated)/sessions/live/[id]/layout.tsx
// Il layout e' un Server Component, ma include:
<ContextBarRegistrar>  {/* 'use client' component */}
  <LiveSessionContextBar />
</ContextBarRegistrar>

// ContextBarRegistrar chiama:
// useContextBarStore().setContent(<LiveSessionContextBar />)
// useContextBarStore().setOptions({ alwaysVisible: true })
// + cleanup su unmount: useContextBarStore().clear()
```

Il componente shell `ContextBar` in `UserShellClient` legge dallo store:

```tsx
// components/layout/ContextBar/ContextBar.tsx
const { content, options } = useContextBarStore();
// Se content === null, la barra collassa (slide up 150ms)
// Se options.alwaysVisible, disabilita auto-hide on scroll
```

Route group mapping:

| Route Group | Registrar in | Content Component |
|-------------|-------------|-------------------|
| `(authenticated)/dashboard/` | `layout.tsx` | `DashboardContextBar` |
| `(authenticated)/sessions/live/[id]/` | `layout.tsx` | `LiveSessionContextBar` |
| `(authenticated)/library/` | `layout.tsx` | `LibraryContextBar` |
| `(authenticated)/discover/` | `layout.tsx` | `DiscoveryContextBar` |
| `(authenticated)/game-nights/[id]/` | `layout.tsx` | `GameNightContextBar` |
| `(chat)/` | `layout.tsx` | `ChatContextBar` |

### 1.3 Specifiche Context Bar per contesto

**DashboardContextBar**:
```
[nuova-partita] [cerca-gioco] [game-night]  |  [ricerca globale...]
```
- Pills con icona + label
- Ricerca: input compatto, espande a full width su focus
- Mobile: pills scrollabili orizzontalmente, ricerca diventa icona lente che espande

**LiveSessionContextBar**:
```
[cover] Catan - timer 1:23:45 - T7 - avatars - leader: 8
```
- Cover gioco: 28px thumbnail circolare
- Timer: live, font monospace per evitare layout shift
- Turno: abbreviato "T7"
- Avatar: stack sovrapposto max 4, +N se piu
- Punteggio: solo leader, abbreviato
- Ogni elemento tappabile (apre overlay relativo)
- Mobile: elementi meno critici in overflow scrollabile

**LibraryContextBar**:
```
[Tutti|Posseduti|Wishlist|Prestati]  [Ordina]  [Vista]  [cerca...]
```
- Filtri: toggle group, uno attivo alla volta
- Ordina: dropdown (nome, data, rating, partite)
- Vista: toggle grid/list
- Mobile: filtri scrollabili, ordina e vista diventano icone

**DiscoveryContextBar**:
```
[Categorie v]  [Trending|Nuovi|Top]  [Filtri]  [cerca catalogo...]
```
- Categorie: dropdown con lista
- Toggle group per ordinamento
- Filtri avanzati: apre overlay

**GameNightContextBar** (solo `/game-nights/[id]`):
```
[Serata di Marco] - Sab 22 Mar - 4/6 confermati - 3 giochi - [edit]
```
- Info condensate dell'evento
- Edit button -> overlay modifica evento
- Mobile: nome + data, resto in overflow

**ChatContextBar**:
```
[avatar] MeepleBot - Contesto: Catan - [Thread v]
```
- Avatar agente attivo
- Contesto gioco (se specifico, altrimenti "Generale")
- Thread switcher: dropdown ultimi 3 thread

---

## 2. Dashboard — Tutto e' Card

La dashboard e' una griglia di `MeepleCard` raggruppate per zona. Il colore entity comunica il tipo. Il tap apre l'overlay ibrido. L'unica eccezione sono le `StatCard` (KPI numerici).

### 2.1 Gerarchia zone (ordine priorita)

**Zona 1 — Quick Actions** (context bar, non card)
Pills nella context bar: "Nuova Partita", "Cerca Gioco", "Game Night".

**Zona 2 — Sessione Attiva** (condizionale, stile Tavolo)
- `MeepleCard entity="session" variant="featured"` — Immagine gioco, nome, timer, avatar giocatori, punteggio leader. Bordo verde pulse. Tap -> entra in sessione live (navigazione diretta, non overlay).
- Multiple sessioni: row di `MeepleCard entity="session" variant="list"`.
- Nessuna sessione: zona collassa, non visibile.

**Zona 3 — Prossimo Game Night** (condizionale, stile Tavolo)
- `MeepleCard entity="event" variant="featured"` — Nome evento, countdown, avatar invitati, mini deck stack cover giochi scelti. Tap -> overlay dettaglio evento.
- Nessun evento: suggestion card "Pianifica una serata di gioco" con CTA.

**Zona 4 — Agenti AI**
- Row orizzontale di `MeepleCard entity="agent" variant="compact"` — Avatar agente, nome, stato (badge), ultimo messaggio troncato. Tap -> overlay chat agente.
- Nessun agente: card singola `entity="agent"` con CTA "Chiedi all'AI".

**Zona 5 — Stats**
- Grid di `StatCard` (2x2 mobile, 4 colonne desktop): Partite mese, Gioco top, Win rate, Streak.
- Non sono `MeepleCard` — sono KPI puri.

**Zona 6 — Feed Attivita (card miste)**
- Lista verticale di `MeepleCard variant="list"` con entity type misto:
  - `entity="game"` (orange) — "Hai aggiunto Catan"
  - `entity="session"` (indigo) — "Partita completata: Ticket to Ride"
  - `entity="player"` (purple) — "Mario si e' unito al gruppo"
- Ultime 5-8 card. Tap -> overlay dettaglio. Link "Vedi tutto".

**Zona 7 — Suggeriti AI**
- Carousel orizzontale di `MeepleCard entity="game" variant="compact"` con badge AI.
- Visibile solo se l'utente ha >= 3 giochi in libreria.

### 2.2 Responsive

| Breakpoint | Layout |
|------------|--------|
| Mobile (<640px) | Stack verticale full width. Carousel suggeriti touch-scrollable |
| Tablet (640-1024px) | 2 colonne per stats. Zone 2-3 affiancate se entrambe presenti |
| Desktop (>1024px) | Zone 2-3 affiancate. Stats 4 colonne. Feed + Suggeriti side by side |

### 2.3 Zona Tavolo nella Dashboard

Le zone 2 e 3 (Sessione Attiva e Game Night) sono racchiuse in un contenitore con stile Tavolo:

```
+--- Dashboard (Hub bg) -------------------------+
|                                                 |
|  [Quick Actions - context bar]                  |
|                                                 |
|  +--- Tavolo zone ----------------------------+ |
|  | warm bg + texture                           | |
|  |  [Sessione Attiva card]                     | |
|  |  [Game Night card]                          | |
|  +---------------------------------------------+ |
|                                                 |
|  [Agenti AI cards]  — Hub style                |
|  [Stats]            — Hub style                |
|  [Feed cards]       — Hub style                |
|  [Suggeriti cards]  — Hub style                |
+-------------------------------------------------+
```

Contenitore Tavolo: `border-radius: 16px`, sfondo warm, padding interno, shadow calda. Emerge dallo sfondo Hub.

---

## 3. Sessione Live — Navigazione Veloce

### 3.1 Context Bar Sessione

Sempre visibile (no auto-hide). Ogni elemento e' tappabile:

| Tap su | Overlay mostra |
|--------|----------------|
| Cover gioco | `MeepleCard entity="game" variant="expanded"` — dettagli, regole, FAQ |
| Timer | Controlli pausa/riprendi (solo host) |
| Turno | Deck stack di card turno (storico) |
| Avatar stack | Deck stack di `MeepleCard entity="player"` — stats, punteggio corrente |
| Punteggio leader | Scoreboard completo — lista di `MeepleCard entity="player" variant="list"` ordinate per punteggio |

### 3.2 Contenuto pagina sessione

Tab principali sotto la context bar:
- **Tavolo** — Vista principale, punteggi live, azioni rapide
- **Giocatori** — Grid di `MeepleCard entity="player" variant="grid"`
- **Punteggi** — Scoreboard con input punteggio
- **Note** — Appunti sessione
- **AI** — Chat con agente in contesto gioco
- **Foto** — Galleria momenti

Ogni tab usa card come unita visiva. I tab sono nella pagina, non nella context bar.

### 3.3 Deck Stack nella Sessione

**Deck Giocatori**: Swipe tra `MeepleCard entity="player"` con punteggio sessione, turni giocati, achievement live.

**Deck Storico Turni**: Swipe tra card turno (dal piu recente). Usa `MeepleCard entity="note" variant="compact"` — entity "note" (warm gray) per rappresentare singoli turni. Ogni card porta `metadata[]` con: azioni del turno, punteggi delta, timestamp. Non si usa "session" per i turni perche un turno non e' una sessione.

**Deck Regole/FAQ**: Sezioni regolamento come card sfogliabili. `MeepleCard entity="kb"` (slate/teal) con contenuto RAG dal knowledge base. Ricerca inline nel deck.

---

## 4. Overlay Ibrido

### 4.1 Comportamento per device

| Aspetto | Mobile (bottom sheet) | Desktop (side panel) |
|---------|----------------------|----------------------|
| Apertura | Slide up 60vh | Slide right 400px |
| Chiusura | Swipe down / tap backdrop | Click X / click fuori |
| Espansione | Swipe up -> full page | Click "Espandi" -> full page |
| Deck navigation | Swipe left/right | Frecce + keyboard <- -> |
| Persistenza | Chiude su back button | Chiude su Escape |

### 4.2 Anatomia

```
+-----------------------------+
| === handle (mobile only)    |
|                             |
| [Entity Icon] Titolo    [X] |
| Subtitle / meta info        |
|-----------------------------|
|                             |
|   MeepleCard content        |
|   (variant="expanded")      |
|                             |
|   ...scrollabile...         |
|                             |
|-----------------------------|
| [< Prev] [dots] [Next >]   |  <- solo se in deck stack
| [  Espandi pagina completa  ]|
+-----------------------------+
```

### 4.3 Transizioni

- Apertura: 250ms ease-out (spring-like)
- Chiusura: 200ms ease-in
- Deck swipe: 150ms con snap magnetico
- Espansione a full page: 300ms morph animation (card si espande fino a riempire viewport)

Con `prefers-reduced-motion: reduce`: tutte le transizioni diventano istantanee (0ms). Usare la variante Tailwind `motion-reduce:transition-none` o il check `useReducedMotion` gia esistente nel codebase.

### 4.4 Deep linking

L'overlay usa un Zustand store (`useOverlayStore`) come source of truth, con sincronizzazione opzionale all'URL per le route che lo supportano.

**Approccio**:
1. Lo stato overlay vive in `useOverlayStore` (Zustand) — apertura/chiusura/contenuto
2. Solo le route di sessione live e dashboard sincronizzano lo stato overlay con `?overlay=` query param
3. La sincronizzazione usa `window.history.pushState()` (non `router.push`) per evitare re-render del Server Component tree
4. `useSearchParams()` wrapped in `<Suspense>` legge il param iniziale al mount per ripristinare l'overlay su refresh
5. Il listener `popstate` chiude l'overlay su back button

**URL format**: `?overlay=<entityType>:<entityId>`
- `/sessions/abc?overlay=player:mario`
- `/dashboard?overlay=game:catan`

**Flusso back button**:
1. Overlay aperto -> `pushState` aggiunge entry con `?overlay=...`
2. Utente preme back -> `popstate` fired -> store chiude overlay
3. URL torna a quello senza `?overlay`
4. Nessun re-render SSR (pushState non tocca Next.js router)

**Route che supportano deep linking**: `/sessions/live/[id]`, `/dashboard`. Altre route usano solo lo store (nessun URL sync).

### 4.5 Gerarchia overlay

Un solo overlay alla volta:
- **Stesso tipo** -> swipe/navigazione nel deck (non apre nuovo overlay)
- **Tipo diverso** -> sostituisce overlay corrente con crossfade 200ms
- **Link esterno** -> espandi a pagina completa
- **MiniCardDeck tap dentro una card** -> `e.stopPropagation()` per evitare che il tap attivi l'overlay della card parent. Il MiniCardDeck apre direttamente l'overlay con il deck stack completo.

### 4.6 "Torna al Tavolo"

Quando l'utente espande a pagina completa da una sessione live, appare un FAB persistente con icona tavolo + "Torna alla partita". Tap -> istantaneamente alla sessione live. Scompare quando si torna in sessione.

---

## 5. CardDeck — Componente Riutilizzabile

**Nota naming**: Il codebase ha gia un componente `DeckStack` (`components/ui/data-display/deck-stack/DeckStack.tsx`) che funziona come popover/bottom-sheet per liste di item. Il nuovo componente descritto in questa sezione si chiama `CardDeck` per evitare collisioni. Il `DeckStack` esistente resta invariato. La variante inline si chiama `MiniCardDeck`.

### 5.1 API

```tsx
<CardDeck
  items={MeepleCardProps[]}
  activeIndex={number}
  onIndexChange={(index: number) => void}
  orientation="horizontal"
  renderItem={(item, isActive) => ReactNode}
  emptyState?: ReactNode
  header?: ReactNode
  showIndicators={true}
  showArrows={true}
  keyboardNav={true}
  preloadCount={1}
  snapThreshold={0.3}
/>
```

### 5.2 Comportamento swipe

| Gesto | Azione |
|-------|--------|
| Swipe left | Card successiva (snap magnetico) |
| Swipe right | Card precedente |
| Swipe < 30% | Ritorno elastico alla card corrente |
| Swipe > 30% | Completa transizione |
| Tap su card | Azione dell'overlay |

Animazione: Card uscente scala a 0.95 + opacity 0.5, card entrante scala da 0.95 a 1. Durata 200ms, easing ease-out. Con `prefers-reduced-motion`: nessuna animazione, cambio istantaneo.

Peeking: Card adiacenti visibili come bordi (8px) ai lati, suggerendo contenuto sfogliabile.

### 5.3 Varianti d'uso

**In overlay**: Larghezza 100% container. Frecce invisibili (solo swipe), compaiono su hover desktop. Indicatori pallini in basso.

**Standalone**: Larghezza da parent. Frecce visibili desktop. Per carousel suggeriti, giochi game night.

**Mini deck (inline)**: Versione compatta, 3-4 cover sovrapposte con offset 8px. Non sfogliabile, solo visualizzazione "mazzo" con count. Tap -> `e.stopPropagation()` + apre overlay con deck stack completo. Usato nella Game Night card.

### 5.4 Accessibilita

| Input | Azione |
|-------|--------|
| `<-` / `->` | Card precedente / successiva |
| `Home` / `End` | Prima / ultima card |
| `Escape` | Chiude overlay contenente |
| `Enter` / `Space` | Attiva azione card |
| Screen reader | `role="tablist"` + `role="tabpanel"`, annuncia "Card N di M" |

### 5.5 Performance e conflitti touch

- Virtualizzazione: Solo 3 card renderizzate (attiva + 2 adiacenti)
- Preload immagini card adiacenti in background
- `touch-action: pan-y` per non bloccare scroll verticale
- Animazioni via `requestAnimationFrame` durante drag
- **Conflitto SwipeableContainer**: Non applicabile — `SwipeableContainer` viene ritirato (vedi Sezione 1.0). Se in futuro venisse reintrodotto un container swipeable parent, il CardDeck deve chiamare `e.stopPropagation()` sui touch events orizzontali per evitare conflitti.

---

## 6. Metafora "Il Tavolo" vs Hub

### 6.1 Dove si applica

| Contesto | Stile |
|----------|-------|
| Dashboard — Zona Sessione Attiva | Tavolo |
| Dashboard — Zona Game Night | Tavolo |
| Sessione Live — pagina intera | Tavolo |
| Game Night — pagina dettaglio | Tavolo |
| Libreria, Discovery, Chat/AI, Admin, Stats, Feed, Suggeriti | Hub |

### 6.2 CSS Variables — Stile Tavolo

Definiti tramite `@layer utilities` in `styles/design-tokens.css` (Tailwind 4 approach, no `tailwind.config.js`):

```css
@layer utilities {
  .env-tavolo {
    --env-bg: hsl(25, 30%, 15%);
    --env-shadow-offset: 6px;
    --env-shadow-color: hsl(25 40% 8% / 0.4);
    --env-card-lift: -4px;
    --env-texture: url('/textures/felt-subtle.webp');
    --env-texture-opacity: 0.04;
  }
}
```

- Sfondo marrone scuro caldo con texture feltro/legno a bassissima opacita (3-5%)
- Dark mode: piu scuro `hsl(25, 20%, 10%)`, texture quasi invisibile
- Card: shadow pronunciata, leggera rotazione random deterministica (-1deg a +1deg su card non attive, basata su index)
- Hover: translateY -4px, shadow espansa
- No texture su mobile basso DPI (`@media (max-resolution: 1dppx)`)

### 6.3 CSS Variables — Stile Hub

```css
@layer utilities {
  .env-hub {
    --env-bg: hsl(0, 0%, 98%);
    --env-shadow-offset: 2px;
    --env-shadow-color: hsl(0 0% 0% / 0.08);
    --env-card-lift: -2px;
    --env-texture: none;
    --env-texture-opacity: 0;
  }
}
```

- Sfondo neutro, nessuna texture
- Shadow standard, hover translateY -2px
- Dark mode: `hsl(0, 0%, 8%)`

### 6.4 Transizione

`MeepleCard` legge le variabili CSS dell'ambiente senza logica condizionale. Navigando tra Hub e Tavolo: sfondo crossfade 300ms (0ms con reduced motion). Context bar fa da ponte visivo (invariata).

---

## 7. Stati Vuoti, Errori, Onboarding

### 7.1 Stati vuoti per zona

| Zona | Stato vuoto | CTA |
|------|-------------|-----|
| Sessione Attiva | Zona collassa, non visibile | — |
| Game Night | Card placeholder "Nessuna serata pianificata" | "Pianifica Game Night" |
| Agenti AI | Card singola "Il tuo assistente AI e' pronto" | "Inizia una chat" |
| Stats | StatCard con "—", "Gioca la prima partita per le stats" | "Nuova Partita" |
| Feed | Card neutra "Nessuna attivita recente" | — |
| Suggeriti | Non visibile se < 3 giochi in libreria | — |

Regola: Mai stato vuoto senza CTA se l'utente puo agire. Se non puo, messaggio breve senza CTA.

### 7.2 Onboarding — Primo accesso

Dashboard vuota sostituita da flusso a card:
1. Welcome Card (featured): "Benvenuto su MeepleAI!"
2. Deck Stack di 3 step card:
   - Step 1: "Aggiungi il tuo primo gioco" -> Cerca catalogo
   - Step 2: "Invita un amico" -> Invita
   - Step 3: "Gioca!" -> Nuova Partita
3. Step completato -> checkmark, card meno prominente
4. Tutti completati -> onboarding scompare, dashboard normale
5. "Salta, esplorero da solo" disponibile sempre

**Stato onboarding — SSR safety**:
Lo stato onboarding e' in `localStorage` ma viene letto SOLO dentro `useEffect` (mai durante SSR). Il server renderizza sempre la dashboard shell (non il flusso onboarding). Al client mount, `useEffect` legge localStorage e se l'onboarding non e' completato, mostra il flusso con una transizione. Questo evita hydration mismatch.

```tsx
const [showOnboarding, setShowOnboarding] = useState(false); // false = server default
useEffect(() => {
  const completed = localStorage.getItem('meepleai-onboarding-complete');
  if (!completed) setShowOnboarding(true);
}, []);
```

### 7.3 Error states

**Errore zona singola**: Card warning (amber) "Impossibile caricare [sezione]" + CTA "Riprova". Altre zone continuano normalmente. Nessun error boundary globale.

**Errore connessione sessione live**: Indicatore rosso lampeggiante in context bar. Banner "Connessione persa. Riconnessione..." con spinner. Dati cached mostrati. Riconnessione -> banner verde 3s.

**Errore overlay**: Card error state dentro overlay "Impossibile caricare i dettagli" + "Riprova" / "Chiudi". Non blocca navigazione.

### 7.4 Loading states

| Contesto | Loading |
|----------|---------|
| Dashboard zone | Skeleton card shimmer (stesse dimensioni card reale) |
| Overlay apertura | Skeleton dentro overlay |
| Deck stack swipe | Card adiacente preloadata, no loading visibile |
| Context bar | Placeholder larghezza fissa, no layout shift |

**MeepleCard skeleton** (da implementare): Ogni variante di `MeepleCard` (`grid`, `list`, `compact`, `featured`, `hero`, `expanded`) deve avere una corrispondente skeleton state attivata dalla prop `loading` gia presente. La skeleton rispetta esattamente le dimensioni della variante reale. Zero layout shift. Implementazione: shimmer gradient animato su placeholder boxes che ricalcano la struttura della card.

---

## Componenti da creare/modificare

### Nuovi componenti

| Componente | Path | Descrizione |
|------------|------|-------------|
| `ContextBar` | `components/layout/ContextBar/` | Shell che legge da `useContextBarStore` |
| `ContextBarRegistrar` | `components/layout/ContextBar/` | Client component per registrare content nello store |
| `useContextBarStore` | `lib/stores/context-bar-store.ts` | Zustand store per content + options |
| `DashboardContextBar` | `components/dashboard-v2/` | Content bar dashboard |
| `LiveSessionContextBar` | `components/session/` | Content bar sessione live |
| `LibraryContextBar` | `components/library/` | Content bar libreria |
| `DiscoveryContextBar` | `components/discovery/` | Content bar discovery |
| `GameNightContextBar` | `components/game-night/` | Content bar game night |
| `ChatContextBar` | `components/chat/` | Content bar chat |
| `OverlayHybrid` | `components/ui/overlays/` | Bottom sheet + side panel |
| `useOverlayStore` | `lib/stores/overlay-store.ts` | Zustand store per overlay state + URL sync |
| `CardDeck` | `components/ui/data-display/card-deck/` | Componente deck sfogliabile (nota: `DeckStack` esistente in `deck-stack/` resta invariato) |
| `MiniCardDeck` | `components/ui/data-display/card-deck/` | Variante inline compatta |
| `TavoloZone` | `components/dashboard-v2/` | Contenitore stile Tavolo |
| `OnboardingFlow` | `components/dashboard-v2/` | Flusso primo accesso (SSR-safe) |
| `BackToSessionFAB` | `components/session/` | FAB "Torna alla partita" |

### Componenti da modificare

| Componente | Modifica |
|------------|----------|
| `UserTopNav` | Ridurre a 48px (`h-12`), rimuovere `sectionTitle`, aggiungere breadcrumb condizionale |
| `UserShellClient` | Rimuovere `UserDesktopSidebar`, `UserTabBar`, `CardRack`, `SwipeableContainer`. Aggiungere `ContextBar` sotto navbar. Nuovo DOM layout flex column. |
| `useNavigation()` | Trasformare da re-export di `useNavStore` a wrapper hook che chiama `useNavStore()` + `usePathname()` e calcola `breadcrumbs: { label: string, href: string }[]` |
| `MeepleCard` | Implementare skeleton state per tutte le 6 varianti (prop `loading` gia esiste). Aggiungere lettura CSS env variables per shadow/lift. |
| `DashboardRenderer` | Riscrivere con nuovo layout a zone card-based |
| Layout route groups | Aggiungere `ContextBarRegistrar` con contenuto specifico |

### Componenti da ritirare

| Componente | Motivo |
|------------|--------|
| `UserDesktopSidebar` | Navigazione spostata in context bar + pagine |
| `UserTabBar` | Sostituito da context bar |
| `CardRack` | Funzionalita assorbita da CardDeck + overlay |
| `SwipeableContainer` | Non piu necessario senza tab-switch swipe |

### CSS / Design tokens

| File | Modifica |
|------|----------|
| `styles/design-tokens.css` | Aggiungere `.env-tavolo`, `.env-hub` via `@layer utilities` (Tailwind 4) |
| `public/textures/felt-subtle.webp` | **Nuovo asset**: texture feltro/legno, ~10KB, tileable |

### Asset da creare

| Asset | Path | Spec |
|-------|------|------|
| Felt texture | `public/textures/felt-subtle.webp` | Tileable, ~10KB, feltro/legno naturale, per uso come `background-image` a bassa opacita |
