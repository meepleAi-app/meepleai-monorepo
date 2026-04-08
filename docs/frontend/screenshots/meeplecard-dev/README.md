# MeepleCard Dev Showcase — Visual Verification Artifacts

Screenshots reference del rendering live della pagina `/dev/meeple-card`,
catturati con Playwright headless in modalità compact (JPEG q=85, scale 1x,
viewport 1440×900).

## Come rigenerarli

```bash
# 1. Avvia il dev server
cd apps/web && PORT=3000 pnpm dev

# 2. In un altro terminale, rilancia lo script in modalità docs
cd apps/web && \
  FORMAT=jpeg SCALE=1 QUALITY=85 \
  OUT_DIR=../../docs/frontend/screenshots/meeplecard-dev \
  node scripts/meeplecard-flip-backs-verify.mjs
```

Per screenshot hi-DPI (PNG, 2x scale) usare:

```bash
cd apps/web && node scripts/meeplecard-flip-backs-verify.mjs
# → output in apps/web/tmp/flip-backs-screenshots/
```

## Catalogo screenshots

| # | File | Contenuto | Verifica |
|---|---|---|---|
| 00 | `00-full-top.jpg` | Top della pagina dev | Header + prime sezioni |
| 01 | `01-flip-fronts.jpg` | Sezione "Flip Cards — Back per Entity Type" con tutti gli 8 fronti | GAME / TOOLKIT / CHAT / KB / AGENT / SESSION / PLAYER / TOOL badge visibili |
| 02 | `02-flip-game-back.jpg` | Back del game card (Catan) | Header arancio, descrizione generica 2 paragrafi, footer "Descrizione generica" |
| 03 | `03-flip-toolkit-back.jpg` | Back del toolkit card (Catan Toolkit) | Header verde, 5 action items (Dice Roller/Score Tracker/Turn Timer/Resource Calculator/Longest Road disabled) |
| 04 | `04-flip-agent-back.jpg` | Back dell'agent card (CatanHelper AI) | Header ambra, 2 section "rows": Configurazione + Utilizzo |
| 05 | `05-flip-player-back.jpg` | Back del player card (Marco Rossi) | Header viola, STATS rows (5 righe) + SOCIAL links (BGG/Twitter/Discord) |
| 06 | `06-flip-tool-back.jpg` | Back del tool card (Dice Roller) | Header blu chiaro, 8 tiri di storico con subtitle + timestamp |
| 07 | `07-nav-behavior.jpg` | Sezione "Nav Click Behavior" | 4 behavior cards (0/1/N/Chat) con entity colors |
| 08 | `08-disabled-navitems.jpg` | Sezione "NavItems Disabled + Tooltip" | 3 esempi: agent limit / kb+agent pending / archived agent |
| 09 | `09-feature-matrix.jpg` | Sezione "Feature Matrix" | Tabella 13 righe (Entity Types → Accessibility) |
| 10 | `10-showcase-completo-badges.jpg` | Sezione "Showcase Completo — Tutte le Feature" | 9 card con badge leggibili (bg-black/10 + text-primary) |
| 11 | `11-entity-table.jpg` | Sezione "Table View — EntityTable" | Tabella sortable (Titolo ▲) con entity-colored borders + nav icons |

## Feature coperte

**PR #282** (#282 feat: dev page showcase + mobile preview + badge contrast)
- Badge contrast fix su Grid/List/Compact/Featured variants → screenshot 10
- Showcase Completo con tutte le feature → screenshot 10
- Mobile Device Preview → non catturato (richiede viewport mobile)

**PR #285** (feat: EntityTable component — closes Table View gap)
- EntityTable sortable con entity-colored rows → screenshot 11

**PR #287** (feat: FlipBack component + 8 entity flip backs + mockup alignment)
- FlipBack component con 5 section kinds → screenshots 02-06
- 8 entity flip backs (game/toolkit/chat/kb/agent/session/player/tool) → screenshot 01 + 02-06
- Nav Click Behavior documentation → screenshot 07
- NavItems Disabled demo → screenshot 08
- Feature Matrix → screenshot 09

## Note tecniche

- **Cookie banner**: appare in overlay sul bottom di alcune screenshot (Next.js
  Cookie Policy banner del tema). Non è parte del dev page ma è utile tenerlo
  visibile per contesto.
- **API 502 errors**: visibili in console durante lo script run — la dev page
  è in route group `(public)` e non richiede l'API; gli errori sono innocui.
- **Flip state residuo**: dopo il click per capturare un back, lo script
  rigira la card per isolare il test successivo. L'ultimo (tool) rimane
  girato perché è l'ultimo.

## Aggiornamento

Dopo ogni modifica significativa al dev page o al componente MeepleCard,
rilanciare lo script per aggiornare questi artefatti. Il commit dovrebbe
essere in un PR separato dalla feature branch con le modifiche al codice.
