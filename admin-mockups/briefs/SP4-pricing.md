# SP4 — Brief Claude Design: Pricing / Plans landing page

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Tutti i token, convenzioni, DoD si applicano a questo brief.

## Contesto

La route `apps/web/src/app/(public)/pricing/page.tsx` esiste con 3 tier (Free/Pro/Team) ma è una pagina minimale costruita inline (HeroGradient + grid di `PricingCard` primitives). Manca un mockup canonico in `admin-mockups/design_files/` che la componga in un funnel d'acquisizione coerente — gap segnalato in [`docs/for-developers/audits/2026-05-12-mockup-gaps.md`](../../docs/for-developers/audits/2026-05-12-mockup-gaps.md) §5 e confermato in [`docs/for-developers/audits/2026-05-22-mockup-gaps.md`](../../docs/for-developers/audits/2026-05-22-mockup-gaps.md).

Issue tracking: **#1739** (P2 — Design v1 · B12).

Questo brief produce il mockup definitivo per `/pricing`, allineando l'attuale page.tsx al pattern delle public landing già consegnate (`public.html` con HeroGradient + value-prop block + sezioni in cards).

## Fonti di riferimento

- `_common.md` — entity palette + typography + responsive contract + light/dark obbligo
- `tokens.css` — **source of truth tokens** (HSL CSS vars); usa `--c-kb` (teal) per CTA principali in coerenza con altre public landing
- **Mockup esistenti come pattern**:
  - `admin-mockups/design_files/public.html` + `public.jsx` (root, sezione PricingPage) — current scope minimale di partenza
  - `admin-mockups/design_files/sp3-*.html` — pattern public-secondary v2 (footer/header)
  - `admin-mockups/design_files/02-desktop-patterns.html` — pattern hero gradient + grid
  - `admin-mockups/design_files/05-dark-mode.html` — light/dark verification surface
- **Codebase**:
  - `apps/web/src/app/(public)/pricing/page.tsx` — current implementation (3 tier già definiti, vedi tabella sotto)
  - `apps/web/src/components/ui/pricing-card/pricing-card.tsx` — primitive `PricingCard` esistente con prop `highlighted` per il tier raccomandato
  - `apps/web/src/components/ui/hero-gradient.tsx` — primitive Hero
  - `apps/web/src/app/(public)/contact/page.tsx` — pagina target del CTA Team `/contact`

## Audience

- **Visitatori non-loggati** (primary): landing pubblica linkata da homepage / footer / nav
- **Free user in upgrade-intent** (secondary): user loggati che cliccano upsell banner / quota-exceeded modal

Mobile-first obbligatorio: la pagina deve essere completa in 375px (single column), responsive a 768px (2 col) e 1280px+ (3 col cards lato a lato).

## Tier model (locked, 2026-05-31)

I tier sono già implementati in code e validati. **NON inventare nuovi tier o prezzi** senza esplicita richiesta utente.

| Tier | Prezzo | Highlight | CTA target | Audience |
|---|---|---|---|---|
| Free | €0 | no | `/register` | Esploratori, casual gamer |
| Pro | €9/mese | **sì** (`highlighted: true`) | `/register?plan=pro` | Boardgamer serio, gioca spesso |
| Team | €29/mese | no | `/contact` (sales) | Boardgame café, club, gruppi organizzati |

**Feature breakdown per tier** (vedi `apps/web/src/app/(public)/pricing/page.tsx:18-61` per la lista canonica):

- **Free**: 2 giochi salvati · 10 chat al mese · session mode base · accesso community catalog · support community
- **Pro**: giochi illimitati · chat illimitate · RAG hybrid search · multi-agent completo · game nights & diary · priority support
- **Team**: 5 account inclusi · KB condivisa team · admin panel · game nights multi-account · dedicated support · early access funzionalità

## Schermate da produrre (4 totali)

### S1. Desktop landing — light mode (1280px+)

**File**: `admin-mockups/design_files/sp4-pricing.html` + `sp4-pricing.jsx`

**Pattern**: HeroGradient full-width + 3-col pricing grid + feature comparison + FAQ + footer CTA.

**Sezioni** (in ordine verticale):

1. **HeroGradient**:
   - Title: `Semplice, trasparente, [board-game friendly]` — l'ultimo span con `color: hsl(var(--c-game))` (entity orange) come in current page.tsx
   - Subtitle: `Nessun contratto. Cancella quando vuoi. Il piano Free rimane gratuito per sempre.`
   - No CTA inline (la grid sotto serve quel ruolo)

2. **Pricing cards grid** (3 col on desktop, grid-cols-3 gap-6):
   - 3 `PricingCard` per i tier (Free / Pro / Team)
   - Pro highlighted: border `--c-kb` 2px + badge "Più scelto" / "Raccomandato" sopra il prezzo
   - Each card: tier name (h3, font weight 600) + prezzo (display large, h1 size) + descrizione 1-line + checklist features (6 max) + CTA button full-width

3. **Feature comparison matrix** (nuova sezione, opzionale ma richiesta in #1739):
   - Table responsive 4-col (Feature label · Free · Pro · Team) con check/cross icons coerenti
   - Coperture: limiti giochi, limite chat, hybrid search, multi-agent, game nights, KB condivisa, admin panel, priority support, dedicated support
   - Visivo: header sticky, rows alternate background `--bg-card-subtle`, cell `--c-success` green check / `--c-muted` cross
   - Mobile: collapse a horizontal scroll OR accordion per-tier (designer choice — documenta nel mockup)

4. **FAQ commerciale** (5-7 domande chiave):
   - Pattern accordion (FAQ already in design system via `Accordion` primitive — check `admin-mockups/design_files/01-screens.html`)
   - Domande candidate (Q proposte; designer può ricambiare il wording):
     - "Posso cambiare piano in qualsiasi momento?"
     - "Cosa succede al mio Free piano dopo i limiti mensili?"
     - "Il pagamento è ricorrente automatico?"
     - "C'è un rimborso se cancello?"
     - "Cosa include il Team piano per gli account aggiuntivi?"
     - "Posso usare MeepleAI per il mio boardgame café commercialmente?"
     - "Quali metodi di pagamento accettate?" (Stripe / PayPal / SEPA — verificare con product)

5. **Footer CTA block**:
   - Banner full-width con `--bg-gradient-game-soft` o coerente con hero
   - Title: "Pronto a portare le tue serate di gioco al livello successivo?"
   - Dual CTA: primary "Inizia gratis" (`/register`) + secondary outline "Confronta tutti i piani" (anchor a #pricing-grid)

### S2. Desktop landing — dark mode (1280px+)

Stesso layout di S1 con `data-theme="dark"` applicato. Verifica:
- HeroGradient: gradient deve mantenere leggibilità (no contrast loss sull'header text)
- Cards Pro highlighted: il border `--c-kb` resta visibile
- Tabella comparativa: alternate row background non rompe contrast WCAG AA
- FAQ accordion: hover/expand state coerenti

### S3. Mobile landing — light mode (375px)

**Pattern**: single column. HeroGradient con title size scalato (h2 invece di h1), 3 cards stacked verticalmente, feature matrix collapsed (vedi nota S1 §3 — accordion-per-tier consigliato), FAQ stesso accordion ridotto a single column.

Stati specifici:
- Pricing card highlighted: stesso border ma con badge "Più scelto" in posizione top-right
- CTA button: full-width 100% in ogni card
- Footer CTA: stack verticale (primary sopra, secondary sotto)

### S4. Mobile landing — dark mode (375px)

Come S3 con `data-theme="dark"`. Verifica WCAG AA contrast su tutti gli elementi interattivi.

## Stati / edge cases

- **Empty state**: non applicabile (la pagina è statica pubblica)
- **Loading state**: nessuno (no fetch backend; tier sono hardcoded in TypeScript)
- **Error state**: nessuno
- **Mobile menu integration**: la nav pubblica esistente (`public.html` header pattern) deve avere link "Pricing" attivo quando in questa pagina

## Componenti v2 da designare

Riusa al massimo le primitive esistenti — non inventare:
- `HeroGradient` (esistente in `apps/web/src/components/ui/hero-gradient.tsx`) — riusa as-is
- `PricingCard` (esistente in `apps/web/src/components/ui/pricing-card/pricing-card.tsx`) — riusa as-is con prop `highlighted` per Pro
- `Accordion` (verificare path esatto) — per FAQ
- `Button` (esistente, variant `primary` + `outline`) — per CTA

Nuove primitive da introdurre (se servono):
- `FeatureComparisonTable` — pattern table con check/cross icons + sticky header + mobile-collapse strategy (accordion-per-tier o horizontal scroll, designer decide e documenta)
- `FAQItem` — eventuale wrapper se l'`Accordion` esistente non copre il pattern Q/A pubblicato

## Acceptance criteria (DoD)

- [ ] 4 file consegnati: `sp4-pricing.html` + `sp4-pricing.jsx` (per S1+S2 desktop) + frames mobile inline tramite media query
- [ ] Naming canonico: `sp4-pricing.html` + `sp4-pricing.jsx` sotto `admin-mockups/design_files/`
- [ ] `.fidelity.json` companion con `design_intent: "current"` + `viewports: ["mobile", "desktop"]` + `tokens_used: "canonical_only"` (come da pattern DS-17 Phase 2.5)
- [ ] Light + dark mode entrambe coperte (4 viewport totali: 375 light, 375 dark, 1280 light, 1280 dark)
- [ ] WCAG AA contrast verificato su tutti gli elementi interattivi
- [ ] Riuso primitive esistenti (no nuovi token/color invented)
- [ ] Update `admin-mockups/MOCKUPS_INDEX.md` con riga `/pricing` → `sp4-pricing.html`
- [ ] Update `docs/for-developers/frontend/v2-migration-matrix.md` se applicabile (verificare riga `/pricing`)
- [ ] Update `docs/for-developers/audits/2026-05-12-mockup-gaps.md` chiudendo gap #5 → CLOSED

## Out of scope

- Implementazione FE (segue mockup tramite stub extraction, tracciato in separata PR)
- Integrazione Stripe / backend billing
- A/B test variants (singolo set canonico)
- Tier policy decision (sono input dal team commerciale, già lockati 2026-05-31 — vedi tabella sopra)
- Annual-vs-monthly toggle (deferred a futura iterazione se richiesto)
- Testimonial / social-proof block (deferred — non scope MVP)

## Decisioni richieste (block handoff fino a risposta)

**DEC-1 — Mobile feature comparison strategy**: nel mockup mobile (375px), la feature matrix 4-col va presentata come:
- **A**: Horizontal scroll della table (pattern dataset-heavy, mantiene comparativa side-by-side)
- **B**: Accordion-per-tier (Free expandable → Pro expandable → Team expandable), feature lista per ciascuno
- **C**: Sticky tier-tab header + content swap (3 buttons "Free | Pro | Team" → renderizza features del tier selezionato)

**Recommended**: **B** (accordion-per-tier) — più tap-friendly, sicuro su small screens, no horizontal scroll che è anti-pattern mobile per content commerciale.

**DEC-2 — FAQ count + ordinamento**: 5 o 7 domande?

**Recommended**: **5** (less is more per landing commerciale). Le 5 più impattanti per conversione: cambio piano, limiti free, rimborso, Team account aggiuntivi, metodi pagamento. Le altre 2 (commercial cafe + ricorrenza auto) → spostare nella FAQ generale del footer.

**DEC-3 — Tier badge wording**: "Più scelto" / "Raccomandato" / "Best value"?

**Recommended**: **"Più scelto"** — coerente con tone of voice MeepleAI italiano (vedi `apps/web/src/app/(public)/pricing/page.tsx:73-77` "board-game friendly"). Evita anglicismi salvo necessario.

---

**Effort estimate**: 1.5-2 giorni mockup + 0.5 giorni di consolidation (fidelity.json + index updates).

**Sequenza consegna**: S1 → S3 → S2 → S4 (light desktop → light mobile → dark desktop → dark mobile, riusa la prima coppia per validare i token poi propaga al dark).
