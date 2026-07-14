# `lint:storybook-states` — Canonical-state coverage gate (DEC-A5 / #2342)

**Status**: PROPOSED
**Date**: 2026-07-14
**Umbrella**: [#2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) Mockup-to-US Coverage execution — deliverable DEC-A5
**Companion**: DS-17 [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) mockup-to-app fidelity

## Contesto e problema

DEC-A5 (umbrella #2342) richiede che ogni sub-issue Tier 2-5 chiusa esporti in Storybook i 5 stati
canonici applicabili (`default · empty · loading · error · sse`). Il gate CI che avrebbe dovuto
verificarlo — `lint:storybook-states` — **non è mai stato costruito**: il commento #2342 del
2026-06-19 lo segnala esplicito («that script does not exist in `apps/web/package.json`») e la
checkbox «CI green» delle sub-issue Tier 2 era quindi insoddisfacibile repo-wide. Questo doc progetta
quel gate.

### Cosa esiste già (e non va ricostruito)

La ricognizione ha ribaltato l'assunzione dell'issue originale. Il repo **ha già** il registro di
quali stati canonici si applicano a una pagina: il campo `acceptance.states_covered` di ogni
`*.fidelity.json`, validato per schema + set-equality (`mockup.states ↔ states_covered`) da
`validate-fidelity.mjs` (`lint:fidelity`).

Conseguenze dirette:

- **Il "waiver N/A" è già risolto.** Se una pagina CRUD non ha `sse`, semplicemente non lo elenca in
  `states_covered`. Il meccanismo di waiver che l'issue immaginava esiste già — non se ne inventa uno
  nuovo.
- **Il vero gap non coperto**: `states_covered` è solo una *dichiarazione*. Niente verifica che la
  story referenziata (`acceptance.story_path`) **implementi davvero** quegli stati. Una fidelity può
  dichiarare `["default","loading","error"]` mentre la story esporta solo il frame `default` → la
  dichiarazione e l'implementazione divergono in silenzio.

Il gate colma **entrambi** i lati: spinge la copertura (route senza story) e verifica l'onestà delle
dichiarazioni esistenti (story che non implementa gli stati dichiarati).

## Scope

**In scope**

- Uno script Node ESM `apps/web/scripts/lint-storybook-states.mjs` che percorre la catena
  route → mockup → fidelity → story → stati e classifica ogni route mappabile.
- Modalità inventory (report) + strict (gate whitelist-incrementale), gemella di
  `lint-tokens-mockups.mjs`.
- Wiring CI in `.github/workflows/ci.yml` job `frontend-lint`, blocking.
- Report `audits/` JSON + Markdown.
- Test vitest.
- Reconciliation del body dell'umbrella #2342 (Tier 3 CLOSED + DEC-A5 shipped).

**Fuori scope (YAGNI)**

- Rendering runtime delle story o snapshot visivi (già coperti da `test:storybook:snapshots`).
- Auto-generazione di story mancanti.
- Enforcement degli stati non-DEC-A5 (`offline`, `quota-soft`, `quota-hard`): l'enum fidelity ne ha 8,
  ma il gate confronta solo i 5 canonici DEC-A5.
- Nuovo meccanismo di waiver (già coperto da `states_covered`).

## Stati canonici

Il gate considera esattamente i 5 stati DEC-A5:

```
default · empty · loading · error · sse
```

Gli altri valori dell'enum fidelity (`offline`, `quota-soft`, `quota-hard`) vengono scartati dal
confronto sia sul lato `states_covered` sia sul lato detection.

## Catena di verifica

```
route (admin-mockups/MOCKUPS_INDEX.md, denominatore "mappable")
  → mockup source
    → *.fidelity.json      (match su mockup.source)
      → acceptance.story_path
        → story file       → detectStates() → Set<CanonicalState>
```

Il denominatore delle route è lo stesso del gate `mockup-annotations:audit --denominator mappable`:
le route con mapping in `MOCKUPS_INDEX.md` (esclude admin/api/internal, prive di design surface).
Il parser di `MOCKUPS_INDEX.md` va riutilizzato dal modulo condiviso di `mockup-annotations` se
esiste; altrimenti se ne estrae uno riusabile invece di duplicare la logica di parsing.

## Classi di violazione

| Classe | Quando | Gravità | Trattamento nel gate |
|---|---|---|---|
| **COVERAGE-GAP** | La catena si rompe prima degli stati: mockup senza `fidelity.json`, oppure fidelity con `story_path` vuoto | Debito noto (migrazione DS-17 incompleta) | Whitelist-incrementale sotto `--max-baseline N` |
| **CONTRACT-VIOLATION** | `story_path` esiste ma la story **non implementa** tutti gli stati in `states_covered` | Bug reale — divergenza silenziosa | **Sempre bloccante**, fuori baseline |

**Skip** (non contano in nessun conteggio): fidelity con `design_intent: forward-refactor-obsolete`
(migrazione story SKIPPED per definizione; hanno già `obsolete_tracking_issue`).

### Razionale della separazione

Le coverage-gap sono ~65 pagine non ancora migrate: debito che si smaltisce col ratchet-down. Una
contract-violation è invece una fidelity che dichiara stati che la story non ha — un difetto
introdotto *ora*, non debito storico. Tenere le due classi separate impedisce a una nuova
dichiarazione disonesta di nascondersi sotto la baseline del debito pregresso.

## Logica del gate (modalità)

Segue il precedente `lint:tokens:mockups`:

- **Inventory** (default, `pnpm lint:storybook-states`): scansiona, scrive report JSON + MD in
  `audits/`, **exit 0 sempre**. Serve a stabilire/aggiornare la baseline.
- **Strict** (`--strict --max-baseline N`):
  - `exit 1` se `coverageGaps.length > N` **OR** `contractViolations.length > 0`
  - `exit 0` altrimenti
- La prima run stabilisce `N` (≈ numero di route mappabili senza story oggi); il valore va hardcoded
  in `ci.yml`. Migrare una pagina fa scendere il conteggio → si abbassa `N` (ratchet-down, come gli
  altri gate). `--strict` senza `--max-baseline` è errore d'invocazione (exit 2).

## Detection degli stati nella story (ibrido)

`detectStates(storySource) → Set<CanonicalState>`:

1. **Override esplicito**: se il meta della story espone `parameters.canonicalStates: string[]`, usa
   quello verbatim. Escape hatch per story che non seguono il pattern MSW.
2. **Euristica** (default): scan testuale del sorgente story per
   - chiamate `mswForState('X')`
   - `name:` dei frame e nomi degli export (es. `Frame17_ErrorState`)

   Ogni token grezzo passa per `normalizeState()`:
   - `empty-first-run`, `empty-filtered`, `empty-tab-agents`, `empty-*` → `empty`
   - `default`, `loading`, `error`, `sse` → identità
   - `offline`, `quota-soft`, `quota-hard` e qualunque token non riconosciuto → scartati
3. **Confronto**: `states_covered ∩ {5 canonici}` deve essere `⊆ detectStates`. Gli stati dichiarati
   ma non rilevati producono una CONTRACT-VIOLATION, con l'elenco preciso degli stati mancanti.

La detection è puramente testuale (regex su sorgente, non import/eval runtime), coerente con gli
altri script `.mjs` che non eseguono il codice del repo.

## CLI, flag, exit codes

Script gemello di `lint-tokens-mockups.mjs`:

| Flag | Effetto |
|---|---|
| *(nessuno)* | Inventory: scansiona, scrive report, exit 0 |
| `--strict --max-baseline N` | Gate: exit 1 se `coverageGaps > N` **or** `contractViolations > 0` |
| `--verbose` / `-v` | Elenca ogni route + verdetto |
| `--help` / `-h` | Uso |

Exit codes: `0` pass · `1` gate fallito · `2` errore d'invocazione. CLI-guard via
`import.meta.url === pathToFileURL(process.argv[1]).href`. Path relativi normalizzati POSIX su Windows.

`package.json`:

```json
"lint:storybook-states": "node scripts/lint-storybook-states.mjs"
```

## Output (report `audits/`)

`audits/2026-07-14-storybook-states-coverage.{json,md}`:

```jsonc
{
  "generatedFrom": "MOCKUPS_INDEX.md",
  "canonicalStates": ["default", "empty", "loading", "error", "sse"],
  "totalMappableRoutes": 68,
  "baselineMaxCoverageGaps": 65,
  "counts": {
    "coverageGaps": 65,
    "contractViolations": 0,
    "skippedObsolete": 4,
    "covered": 3
  },
  "coverageGaps": [
    { "route": "/...", "reason": "no-fidelity | no-story-path", "mockup": "..." }
  ],
  "contractViolations": [
    { "route": "/...", "storyPath": "...", "declared": ["default","loading","error"],
      "detected": ["default"], "missing": ["loading","error"] }
  ]
}
```

Il Markdown ripresenta gli stessi dati in tabella + due sezioni (gap vs violation) + una nota che
spiega la semantica del gate (ratchet-down, contract sempre bloccante), sullo stile del report
token-mockups.

## CI wiring

`.github/workflows/ci.yml`, job `frontend-lint`, nuovo step dopo `mockup-annotations:audit`,
**blocking** (niente `continue-on-error`). La baseline garantisce verde oggi (65 gap ≤ 65) e 0
contract-violation (le 3 fidelity attuali sono oneste), esattamente come `lint:tokens:mockups --strict`
è blocking da subito.

```yaml
- name: Storybook canonical-states coverage gate (DEC-A5 / #2342)
  run: pnpm lint:storybook-states --strict --max-baseline 65   # N dalla prima run

- name: Upload storybook-states report
  if: always()
  uses: actions/upload-artifact@v7
  with:
    name: storybook-states-${{ github.run_number }}
    path: |
      audits/2026-07-14-storybook-states-coverage.json
      audits/2026-07-14-storybook-states-coverage.md
    retention-days: 14
```

Il valore `65` è provvisorio: fissato dalla prima run reale prima del merge.

## Testing

Vitest (locazione della suite `.mjs` verificata in fase di piano — probabile `apps/web/scripts/__tests__/`):

- **Funzioni pure**:
  - `normalizeState()` — empty-variants → empty; quota/offline/token-ignoti → scartati; canonici → identità.
  - `detectStates()` — override `parameters.canonicalStates` vince sull'euristica; euristica combina
    `mswForState()` + frame names; normalizzazione applicata.
  - `classifyRoute()` — 5 esiti: covered / no-fidelity / no-story-path / contract-violation / skipped-obsolete.
- **End-to-end su fixture**: dir temporanea con MOCKUPS_INDEX finto + fidelity + story →
  asserisce conteggi, exit codes, report shape, e che una fidelity "bugiarda" produca
  CONTRACT-VIOLATION bloccante **anche quando** `coverageGaps ≤ baseline`.

## File toccati

| File | Azione |
|---|---|
| `apps/web/scripts/lint-storybook-states.mjs` | nuovo (script) |
| `apps/web/scripts/__tests__/lint-storybook-states.test.*` | nuovo (test) |
| `apps/web/package.json` | +1 script `lint:storybook-states` |
| `.github/workflows/ci.yml` | +1 step + artifact upload nel job `frontend-lint` |
| `audits/2026-07-14-storybook-states-coverage.{json,md}` | generato, committato per baseline |
| body/commento umbrella **#2342** | reconciliation (Tier 3 CLOSED + DEC-A5 shipped) |

## Precedenti riusati

- `apps/web/scripts/lint-tokens-mockups.mjs` — modalità inventory/strict, `--max-baseline`, report JSON+MD in `audits/`.
- `apps/web/scripts/mockup-annotations/validate-fidelity.mjs` — schema fidelity, glob `**/*.fidelity.{json,yml}`, set-equality.
- `mockup-annotations:audit --denominator mappable` — parser MOCKUPS_INDEX + esclusione admin/api/internal.
- Pattern CI artifact upload (`if: always()`, `retention-days: 14`) dei gate mockup esistenti.

## Rischi e mitigazioni

| Rischio | Mitigazione |
|---|---|
| Euristica MSW non riconosce un pattern di story nuovo | Override esplicito `parameters.canonicalStates` come escape hatch documentato |
| `MOCKUPS_INDEX.md` va stale → denominatore errato | Riuso dello stesso parser/denominatore già gated da `mockup-annotations:audit`, che tiene l'INDEX allineato |
| Baseline `65` sbagliata al primo giro | Fissata dalla prima run reale; il report inventory stampa il conteggio esatto |
| Falso CONTRACT-VIOLATION per naming stato granulare | `normalizeState()` mappa le varianti note; override disponibile per casi residui |
