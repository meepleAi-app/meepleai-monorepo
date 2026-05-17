# Nanolith Storybook fixtures — Phase A runthrough

> **Scope**: foto pagina Storybook + ground-truth traduzioni IT per E2E del flusso N3 (photo → OCR → segmentation → translate).

## Cosa contiene questa cartella

```
tests/fixtures/nanolith-storybook/
├── README.md                       # questo file
├── .gitignore                      # esclude binari fotografici reali (copyright)
├── manifest.json                   # metadata per ogni fixture (committato)
├── expected-translations.json      # ground-truth IT annotato da Aaron (committato)
├── synthetic/                      # foto sintetiche/redacted (committate, OK CI)
│   └── (vuoto — verranno aggiunte dopo Phase A)
└── local/                          # foto reali Aaron (NON committate, copyright)
    └── (gitignore — solo dev locale)
```

## Tre tipi di fixture

| Tipo | Path | Commit? | Uso |
|---|---|---|---|
| **Real local** | `local/*.jpg\|png\|webp\|heic` | ❌ gitignore | Phase A runthrough manuale di Aaron |
| **Synthetic** | `synthetic/*.jpg\|png` | ✅ commit | E2E CI Playwright deterministico |
| **Redacted** | `synthetic/*-redacted.jpg` | ✅ commit | Foto reale con narrative blurred (placeholder § visibili) |

**Regola copyright**: lo Storybook Nanolith è © Side Room Studios. Foto integrali NON in repo. Fixture committate = solo placeholder testuali sintetici o redacted (testo originale non leggibile).

## Workflow Aaron — caricamento foto Phase A

1. Scattare 3-5 foto di pagine reali con telefono al tavolo (dogfooding):
   - **happy.jpg** — pagina con 3 paragrafi visibili, luce normale
   - **low-light.jpg** — stessa pagina, luce bassa salotto sera
   - **dense.jpg** — pagina senza § numerati visibili (testo continuo)
   - **blurry.jpg** _(opzionale)_ — pagina sfocata movimento
2. Salvare in `local/` con nomi sopra (verranno gitignorati automaticamente)
3. Aggiornare `manifest.json` con metadata per ogni foto (numero §, capitolo, condizioni di scatto)
4. Per ogni foto runthrough: dopo traduzione, annotare in `expected-translations.json` la traduzione IT che Aaron giudica corretta (gold standard) — usato come reference per BLEU score futuro

## Manifest schema

Vedi `manifest.json` per template. Campi:
- `id` — identifier stabile (es. `nano-§147-happy`)
- `paragraph_id` — numero § nella pagina target (es. `§147`)
- `chapter` — capitolo narrativo (es. `Cap.4 — La grotta dei Reaver`)
- `conditions` — `light_normal` | `light_low` | `dense_text` | `blurry`
- `expected_segments` — array di paragrafi che OCR dovrebbe rilevare (es. `["§146", "§147", "§148"]`)
- `expected_match_quality` — `exact` | `partial` | `none`
- `min_ocr_confidence` — soglia minima accettabile (es. `0.7` per happy, `0.4` per low-light)
- `path_local` — path foto reale (popolato da Aaron in dev)
- `path_synthetic` — path versione sintetica/redacted (popolato post-Phase A)

## Expected-translations schema

`expected-translations.json` è il **ground truth** per validare LLM output. Aaron annota:
- `paragraph_id` — id del paragrafo
- `source_en` — testo EN originale (≤ 600 char, fair use citazione)
- `expected_it` — traduzione IT che Aaron giudica corretta
- `glossary_terms` — termini che DEVONO restare consistenti (es. `Voidstone → Pietra del Vuoto`)
- `min_bleu_score` — soglia minima per pass test (es. `0.6`)
- `read_fluent` — boolean post-runthrough (criterio §0.3 baseline spec)

## CI / E2E uso

```typescript
// apps/web/e2e/nanolith-runthrough/photo-translate-happy.spec.ts
import manifest from '../../../tests/fixtures/nanolith-storybook/manifest.json';
import expected from '../../../tests/fixtures/nanolith-storybook/expected-translations.json';

const happy = manifest.fixtures.find(f => f.id === 'nano-§147-happy');
// upload foto synthetic, verifica segments, traduci, confronta con expected
```

CI usa **solo `synthetic/`** (no copyright). `local/` è dev-only.

## Riferimenti spec

- Baseline: `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md` §3 (N3 scenari Gherkin)
- Phase A: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md` (TBD)
- Failure modes: §6 baseline (foto sfocata, OCR low-conf, non-Latin script)
