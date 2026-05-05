# Mechanic Extractor AI-First — UI Mockups

**Parent ADR**: [ADR-051](../architecture/adr/adr-051-mechanic-extractor-ip-policy.md)
**Epic**: meepleAi-app/meepleai-monorepo#539
**Date**: 2026-04-23
**Status**: Draft for M1 implementation

## Surfaces

1. **Admin — Generation form** (new analysis trigger) → Issue #524
2. **Admin — Review UI per-claim** → Issue #526
3. **Admin — Publish confirm modal** → Issue #527
4. **Player — Public card page** → Issue #528
5. **Player — Citation side-panel** (M2 polish, see #530)

All mockups use ASCII to keep them version-controllable. Visual design applies:
- Palette Hybrid Warm-Modern (`--nh-bg-base`, amber accent)
- Typography Quicksand (headings) + Nunito (body)
- Component kit: `MeepleCard`, `Badge`, `Button`, `Skeleton`, `AlertDialog`

---

## 1. Admin — Generation Form (#524)

**Route**: `/admin/knowledge-base/mechanic-analyses/new`

**Purpose**: Trigger new AI analysis from a `(sharedGameId, pdfDocumentId)` pair.

```
┌─────────────────────────────────────────────────────────────────────────┐
│ ← Indietro                                    [ Admin / Knowledge Base ] │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Nuova Analisi Meccaniche (AI-first)                                    │
│  Genera card consultabile a partire dal manuale del gioco               │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ Gioco                                                           │   │
│  │ ┌───────────────────────────────────────────────────────────┐  │   │
│  │ │ 🔍  Cerca gioco…                                          ▼ │  │   │
│  │ └───────────────────────────────────────────────────────────┘  │   │
│  │                                                                 │   │
│  │ Catalogo community · 1.247 giochi                              │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ Manuale PDF                                                     │   │
│  │                                                                 │   │
│  │ ○ 📄  catan-rules-4th-ed-EN.pdf    · 19 pagine · Ready          │   │
│  │ ○ 📄  catan-rules-4th-ed-IT.pdf    · 22 pagine · Ready          │   │
│  │                                                                 │   │
│  │ ⚠️ Solo PDF con stato "Ready" sono eleggibili                  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ Configurazione avanzata                              ▶ Espandi │   │
│  │                                                                 │   │
│  │   Prompt version:  [ v1 (default) ▼ ]                          │   │
│  │   Cost cap:        [ €2.00         ]   (max per analisi)       │   │
│  │   Override:        ☐ Autorizza costo sopra cap (richiede motivo)│   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ╔═════════════════════════════════════════════════════════════════╗   │
│  ║  📊 Stima preliminare                                            ║   │
│  ║  • Chunks da analizzare:      ~34                                ║   │
│  ║  • Token input stimati:       ~18.000                            ║   │
│  ║  • Costo stimato:             €0.42 (DeepSeek primary)           ║   │
│  ║  • Tempo stimato:             ~45s                               ║   │
│  ╚═════════════════════════════════════════════════════════════════╝   │
│                                                                         │
│                              [ Annulla ]  [ ⚡ Genera Analisi ]        │
└─────────────────────────────────────────────────────────────────────────┘
```

### State variations

- **Game not yet selected**: PDF selector disabled with hint "Seleziona prima il gioco"
- **PDF exists but not Ready**: Shown greyed out with status badge (`Processing`, `Error`)
- **No PDF available**: Empty state with CTA "Carica manuale"
- **Cost estimate exceeds cap**: Red banner + require override + reason input
- **Generation in progress**: Full-form skeleton + spinner + "Analisi in corso… ~45s" + link "Apri in background"

---

## 2. Admin — Review UI per-claim (#526)

**Route**: `/admin/knowledge-base/mechanic-analyses/[analysisId]/review`

**Purpose**: Admin approva/rifiuta ogni singolo claim, con citation viewer inline al PDF.

### Header + summary

```
┌─────────────────────────────────────────────────────────────────────────┐
│ ← Indietro                                     [ Admin / Review ]       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Catan — Analisi Meccaniche                                             │
│  Generated 2026-04-23 14:32 · DeepSeek v1 · 19 pagine · €0.38           │
│                                                                         │
│  [ ⏳ PENDING REVIEW ]   claims: 23  ·  citations: 31                   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ Guardrail status                                                │   │
│  │                                                                 │   │
│  │  ✅ T1 Quote length ≤ 25 parole   (31/31)                      │   │
│  │  ✅ T2 No copie letterali         (0 rejection)                │   │
│  │  ✅ T3 Citation per claim         (23/23)                      │   │
│  │  ⚠️ T4 Pagine verificabili        (30/31 · 1 warning)          │   │
│  │  ✅ T8 Costo entro cap            (€0.38 / €2.00)              │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  Bulk actions:  [ Approva tutti ]  [ Rifiuta quote >20 parole ]        │
│                 [ Filtra: ⚠️ solo warning ]                            │
└─────────────────────────────────────────────────────────────────────────┘
```

### Two-column layout: claim list | PDF viewer

```
┌───────────────────────────────────┬─────────────────────────────────────┐
│ CLAIM LIST (sticky, scrollable)   │ PDF VIEWER (synced to selected)     │
├───────────────────────────────────┼─────────────────────────────────────┤
│                                   │                                     │
│ ╔═══════════════════════════════╗ │   [←] pag. 3 / 19    [→]  [100% ▼] │
│ ║ § Sommario                    ║ │  ┌───────────────────────────────┐ │
│ ║                               ║ │  │                               │ │
│ ║ □ Claim 1 · ⚡                ║ │  │  Catan è un gioco di          │ │
│ ║   Claim 1 text preview…       ║ │  │  commercio e colonizzazione   │ │
│ ║   📍 p.1 · ✅ T1-T4 ok        ║ │  │  per 3-4 giocatori dove…      │ │
│ ║   [ ✓ Approva ] [ ✗ Rifiuta ] ║ │  │                               │ │
│ ║                               ║ │  │  [HIGHLIGHT: "commercio e     │ │
│ ║ □ Claim 2                     ║ │  │   colonizzazione per 3-4"]    │ │
│ ║   Durata partita: 60-90 min.  ║ │  │                               │ │
│ ║   📍 p.2 · ✅ ok              ║ │  │  ...                          │ │
│ ║                               ║ │  │                               │ │
│ ╚═══════════════════════════════╝ │  │                               │ │
│                                   │  │                               │ │
│ ╔═══════════════════════════════╗ │  │                               │ │
│ ║ § Meccaniche (6)              ║ │  │                               │ │
│ ║                               ║ │  │                               │ │
│ ║ □ Claim 3 · ⭐ SELECTED       ║ │  │                               │ │
│ ║   "Il giocatore raccoglie     ║ │  │                               │ │
│ ║    materie prime basate sul   ║ │  │                               │ │
│ ║    tiro dei dadi"             ║ │  │                               │ │
│ ║   📍 p.4 · ✅ ok              ║ │  │                               │ │
│ ║                               ║ │  │                               │ │
│ ║   └─ Citation [p.4]           ║ │  │                               │ │
│ ║      "…ogni giocatore         ║ │  │                               │ │
│ ║       riceve risorse dei      ║ │  │                               │ │
│ ║       territori confinanti    ║ │  │                               │ │
│ ║       al numero tirato"       ║ │  │                               │ │
│ ║      (19 parole · ✅ T1)      ║ │  │                               │ │
│ ║                               ║ │  │                               │ │
│ ║   Note admin:                 ║ │  │                               │ │
│ ║   [____________________]      ║ │  │                               │ │
│ ║                               ║ │  │                               │ │
│ ║   [ ✓ Approva ] [ ✗ Rifiuta ] ║ │  └───────────────────────────────┘ │
│ ╚═══════════════════════════════╝ │                                     │
│                                   │                                     │
│ ╔═══════════════════════════════╗ │                                     │
│ ║ § Vittoria                    ║ │                                     │
│ ║ § Risorse (5)                 ║ │                                     │
│ ║ § Fasi                        ║ │                                     │
│ ║ § FAQ                         ║ │                                     │
│ ╚═══════════════════════════════╝ │                                     │
└───────────────────────────────────┴─────────────────────────────────────┘

     Footer sticky:
┌─────────────────────────────────────────────────────────────────────────┐
│  Progresso review: 12/23 approvati · 1 rifiutato · 10 pending          │
│                                                                         │
│  [ Salva bozza ]              [ ← Richiedi modifiche ]  [ Approva → ]  │
└─────────────────────────────────────────────────────────────────────────┘
```

### Claim status indicators

| Badge | Significato |
|-------|-------------|
| ✅ T1-T4 ok | Tutti guardrail verdi |
| ⚠️ T4 | Pagina citata non esiste (warning, non blocking) |
| ❌ T1 | Quote >25 parole (auto-rejected, mostrato per audit) |
| ⚡ | AI high-confidence |
| 🔍 | AI low-confidence (richiede review attento) |

### Interactions

- Click su claim → sync PDF viewer + highlight quote sulla pagina
- Click su citation badge `[p.N]` → stessa azione
- Keyboard shortcuts: `j/k` navigate claims, `a` approve, `r` reject, `n` focus note field
- Unsaved changes → `AlertDialog` "Sei sicuro di abbandonare?" on navigate away
- Submit review → transition `pending_review` → `approved` (all approved) o `rejected` (any rejected + overall rejected)
- Partial review → status resta `pending_review`, bozza salvata

### Footer attribution (replaces Variant C footer)

```
┌─────────────────────────────────────────────────────────────────────────┐
│ Analisi elaborata dall'AI sul manuale del gioco. Ogni affermazione è    │
│ riformulata in parole originali e cita la pagina del regolamento.       │
│ Copyright © degli editori per il testo originale del manuale.           │
│                                                                         │
│ Modello: DeepSeek v1 · 4.823 token · €0.38                              │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Admin — Publish confirm modal (#527)

**Trigger**: Click "Approva →" con tutti claim approvati → apre modal conferma.

```
╔═════════════════════════════════════════════════════════════════════════╗
║  Pubblica analisi come card?                                          × ║
╠═════════════════════════════════════════════════════════════════════════╣
║                                                                         ║
║  Gioco:        Catan                                                    ║
║  Analisi:      #a7f3-…-9bc2 · DeepSeek v1                              ║
║  Claim:        23 approvati, 0 rifiutati                                ║
║  Citations:    31 con pdf_page verificato                               ║
║                                                                         ║
║  La card sarà visibile a utenti loggati su:                             ║
║  /games/catan/card                                                      ║
║                                                                         ║
║  ⚠️ Questa azione:                                                       ║
║  • Pubblica immediatamente (nessun ulteriore gate)                      ║
║  • Sostituisce eventuale card precedente (versione +1)                  ║
║  • È tracciata in audit log                                             ║
║                                                                         ║
║  Titolo card:  [ Catan — Specchietto regole           ]                ║
║                                                                         ║
║                              [ Annulla ]   [ Pubblica ora ]            ║
╚═════════════════════════════════════════════════════════════════════════╝
```

On success → toast "Card pubblicata su /games/catan/card" + link + redirect a pagina review con badge `PUBLISHED`.

---

## 4. Player — Public card page (#528)

**Route**: `/games/[slug]/card` (login-gated in M1).

**Purpose**: Specchietto consultabile al tavolo + comprehension demo per utenti.

### Desktop layout

```
┌─────────────────────────────────────────────────────────────────────────┐
│  [Navbar MeepleAI]                                         [User menu] │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │  Catan                                                             │ │
│  │  Specchietto regole · versione 1 · pubblicata 23 apr 2026         │ │
│  │                                                                    │ │
│  │  [ 🤖 AI-generated · 👤 Human-reviewed ]   [ Come funziona? ]     │ │
│  │                                                                    │ │
│  │  🖨️ Stampa · 📤 Condividi · ⬇️ Download PDF                       │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                                                                         │
│  ╔═══════════════════════════════════════════════════════════════════╗ │
│  ║ 📖 Sommario                                                        ║ │
│  ║                                                                    ║ │
│  ║ Gioco di commercio e colonizzazione per 3-4 giocatori su isola    ║ │
│  ║ modulare. Obiettivo: accumulare 10 punti vittoria tramite         ║ │
│  ║ insediamenti, città e sviluppo territoriale.  [p.1] [p.2]         ║ │
│  ║                                                                    ║ │
│  ║ 🕐 60-90 min · 👥 3-4 giocatori · 🎯 Età 10+                      ║ │
│  ╚═══════════════════════════════════════════════════════════════════╝ │
│                                                                         │
│  ╔═══════════════════════════════════════════════════════════════════╗ │
│  ║ 🎲 Meccaniche principali                        6 meccaniche       ║ │
│  ║                                                                    ║ │
│  ║  • Raccolta risorse su tiro dadi  [p.4]                           ║ │
│  ║  • Commercio tra giocatori  [p.6]                                 ║ │
│  ║  • Costruzione strade/insediamenti  [p.5] [p.7]                   ║ │
│  ║  • Carte sviluppo (cavaliere, progresso, vittoria)  [p.9]        ║ │
│  ║  • Ladrone (blocco risorse + furto)  [p.8]                       ║ │
│  ║  • Porto (commercio con banco a rapporto favorevole)  [p.6]      ║ │
│  ╚═══════════════════════════════════════════════════════════════════╝ │
│                                                                         │
│  ╔═══════════════════════════════════════════════════════════════════╗ │
│  ║ 🏆 Come si vince                                                   ║ │
│  ║                                                                    ║ │
│  ║  Il primo giocatore a raggiungere 10 punti vittoria nel proprio   ║ │
│  ║ turno. [p.3]                                                       ║ │
│  ║                                                                    ║ │
│  ║  Fonti di punti:                                                   ║ │
│  ║   • Insediamento:     1 PV                                         ║ │
│  ║   • Città:            2 PV                                         ║ │
│  ║   • Strada più lunga: 2 PV (bonus)                                ║ │
│  ║   • Esercito grande:  2 PV (bonus)                                ║ │
│  ║   • Carte vittoria:   1 PV ciascuna                               ║ │
│  ╚═══════════════════════════════════════════════════════════════════╝ │
│                                                                         │
│  ╔═══════════════════════════════════════════════════════════════════╗ │
│  ║ 📦 Risorse                                      5 tipi             ║ │
│  ║                                                                    ║ │
│  ║  ┌──────────┬──────────┬─────────────────────────┬─────────────┐  ║ │
│  ║  │ Risorsa  │ Da       │ Serve per               │ Limitata    │  ║ │
│  ║  ├──────────┼──────────┼─────────────────────────┼─────────────┤  ║ │
│  ║  │ Legno    │ Foresta  │ Strade, insediamenti    │ No (banco)  │  ║ │
│  ║  │ Argilla  │ Collina  │ Strade, insediamenti    │ No (banco)  │  ║ │
│  ║  │ Grano    │ Campo    │ Insediamenti, città, C.S│ No (banco)  │  ║ │
│  ║  │ Pecora   │ Pascolo  │ Insediamenti, C.Svil.   │ No (banco)  │  ║ │
│  ║  │ Minerale │ Montagna │ Città, C.Svil.          │ No (banco)  │  ║ │
│  ║  └──────────┴──────────┴─────────────────────────┴─────────────┘  ║ │
│  ║                                                                    ║ │
│  ║  Dettagli: [p.4] [p.5]                                             ║ │
│  ╚═══════════════════════════════════════════════════════════════════╝ │
│                                                                         │
│  ╔═══════════════════════════════════════════════════════════════════╗ │
│  ║ ▶ Fasi del turno                                                   ║ │
│  ║                                                                    ║ │
│  ║  ①  Tiro dei dadi       → raccolta risorse  [p.10]                ║ │
│  ║  ②  Commercio           → marittimo/terrestre  [p.11]             ║ │
│  ║  ③  Costruzione         → strade, insediamenti, città, carte      ║ │
│  ║  ④  Fine turno          → passa al giocatore successivo           ║ │
│  ╚═══════════════════════════════════════════════════════════════════╝ │
│                                                                         │
│  ╔═══════════════════════════════════════════════════════════════════╗ │
│  ║ ❓ Domande frequenti                                               ║ │
│  ║                                                                    ║ │
│  ║  ▸ Cosa succede se esce il 7?                                     ║ │
│  ║    Il ladrone si muove. Giocatori con >7 carte scartano metà.    ║ │
│  ║    [p.8]                                                           ║ │
│  ║                                                                    ║ │
│  ║  ▸ Posso commerciare a qualunque rapporto?                        ║ │
│  ║    Con altri giocatori sì. Con banco standard 4:1, con porto     ║ │
│  ║    2:1 o 3:1.  [p.6]                                              ║ │
│  ║                                                                    ║ │
│  ║  ▸ Le strade si possono distruggere?                              ║ │
│  ║    No, una volta costruite restano fino a fine partita.  [p.7]    ║ │
│  ╚═══════════════════════════════════════════════════════════════════╝ │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │ Analisi elaborata dall'AI sul manuale del gioco. Ogni affermazione│ │
│  │ è riformulata in parole originali e cita la pagina del regolamento│ │
│  │                                                                    │ │
│  │ Copyright © CATAN GmbH per il testo originale del manuale.        │ │
│  │ MeepleAI analisi AI · v1 · 23 apr 2026                            │ │
│  │                                                                    │ │
│  │ 🚩 Segnala errore  ·  📧 Takedown request                         │ │
│  └───────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

### Citation badge interaction

- Hover `[p.4]` → tooltip `"ogni giocatore riceve risorse dei territori confinanti…" (19 parole)`
- Click `[p.4]` → **side-panel** (see surface #5 below)

### Mobile layout (portrait, ≤768px)

```
┌──────────────────────┐
│ [≡] Catan        [👤]│
├──────────────────────┤
│                      │
│  Catan               │
│  Specchietto · v1    │
│  🤖 AI · 👤 Review   │
│                      │
│  [🖨️] [📤] [⬇️]      │
│                      │
│  ━━━ 📖 Sommario ━━━ │
│                      │
│  Gioco di commercio  │
│  e colonizzazione    │
│  per 3-4 giocatori…  │
│  [p.1] [p.2]        │
│                      │
│  🕐 60-90 min        │
│  👥 3-4 giocatori    │
│                      │
│  ━━━ 🎲 Meccaniche ━━│
│                      │
│  • Raccolta risorse  │
│    [p.4]             │
│  • Commercio [p.6]   │
│  ...                 │
│                      │
│  [Scroll per vedere  │
│   tutte le sezioni]  │
└──────────────────────┘
```

### Print layout

- Stacked A4 con header gioco ripetuto su ogni pagina
- Citation badge collassati a piè di pagina come note (`¹ p.4 · ² p.6`)
- No background, no shadow, no interactive elements
- Footer con copyright + MeepleAI analysis metadata

---

## 5. Player — Citation side-panel (#530, M2 polish)

**Trigger**: Click su badge `[p.N]` dalla card pubblica.

```
┌──────────────────────────────────┬─────────────────────────────────┐
│  (card pubblica, dimmed)         │  Citation · Pagina 4         × │
│                                  │                                 │
│                                  │ ┌─────────────────────────────┐ │
│                                  │ │ 📄 PDF viewer               │ │
│                                  │ │                             │ │
│                                  │ │  ...                        │ │
│                                  │ │                             │ │
│                                  │ │  "ogni giocatore riceve     │ │
│                                  │ │   risorse dei territori     │ │
│                                  │ │   confinanti al numero      │ │
│                                  │ │   tirato"                   │ │
│                                  │ │  [HIGHLIGHTED]              │ │
│                                  │ │                             │ │
│                                  │ │  ...                        │ │
│                                  │ │                             │ │
│                                  │ └─────────────────────────────┘ │
│                                  │                                 │
│                                  │  📋 Copia citazione            │
│                                  │  ⬇️ Apri PDF                    │
│                                  │                                 │
│                                  │  Quote: 19 parole               │
│                                  │  © CATAN GmbH                   │
└──────────────────────────────────┴─────────────────────────────────┘
```

### A11y

- Focus trap su side-panel open
- ESC chiude panel e ritorna focus al badge `[p.N]`
- `aria-describedby` collega claim ↔ citation
- Keyboard nav: `Tab` attraverso badges, `Enter` apre panel, `Shift+Tab` torna indietro

---

## Component map → code

| UI element | Component | Path |
|------------|-----------|------|
| Outer layout | `DashboardShell` | `apps/web/src/components/shell/dashboard-shell.tsx` |
| Review claim card | **NEW** `ClaimReviewCard` | `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-analyses/_components/claim-review-card.tsx` |
| Citation badge | **NEW** `CitationBadge` | `apps/web/src/components/shared/citation-badge.tsx` (riuso su admin + player) |
| Citation side-panel | **NEW** `CitationSidePanel` | `apps/web/src/components/shared/citation-side-panel.tsx` |
| PDF viewer (admin review) | Riuso esistente | `apps/web/src/components/pdf/pdf-viewer.tsx` |
| Publish confirm modal | `AlertDialog` primitive | `apps/web/src/components/ui/overlays/alert-dialog-primitives` |
| Card section | Riuso `Card` + `CardHeader` | `apps/web/src/components/ui/data-display/card.tsx` |
| Guardrail status | **NEW** `GuardrailBadge` | `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-analyses/_components/guardrail-badge.tsx` |
| Stats row | Riuso `StatCard` (già in review/page.tsx) | promuovere a shared |

## API calls map

| UI action | Endpoint | Issue |
|-----------|----------|-------|
| Form submit "Genera" | `POST /api/v1/admin/mechanic-analyses` | #524 |
| Load review data | `GET /api/v1/admin/mechanic-analyses/{id}` | #524 |
| Submit review | `POST /api/v1/admin/mechanic-analyses/{id}/review` | #526 |
| Publish | `POST /api/v1/admin/mechanic-analyses/{id}/publish` | #527 |
| Load public card | `GET /api/v1/mechanic-cards?shared_game_slug=catan` | #528 |
| Load PDF page for citation | `GET /api/v1/pdfs/{pdfId}/download` + page anchor | existing |

## Design tokens usage

- Card bg: `bg-white/70 backdrop-blur-md` (admin) / `bg-white` (public, più solido)
- Section headers: `font-quicksand text-xl` + emoji-prefixed
- Citation badge: `border-amber-300 bg-amber-50 text-amber-900` (hover: darker)
- Guardrail verde: `bg-green-50 border-green-300 text-green-800`
- Guardrail warning: `bg-amber-50 border-amber-300 text-amber-800`
- Guardrail error: `bg-red-50 border-red-300 text-red-800`

## Next steps

Dopo validazione mockup → **Artifact 4** (piano tecnico M1.1-M1.3):
- Schema classes C# + EF configurations
- SQL idempotent migration completa
- Prompt template v1 con JSON schema
- Guardrail validators codice
- Test fixtures (Catan subset)
