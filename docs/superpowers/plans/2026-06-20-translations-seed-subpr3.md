# Shared Game Translations — Seed Translations IT Implementation Plan (sub-PR 3/3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship 13 IT translations curate per SP4 seed dataset + translations-research provenance doc + seed script `45-translations.sh` + ADR-059 §6 amendment + Q4 doc closure in `docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md`, chiudendo sub-PR 3/3 dell'issue [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339).

**Architecture:** Augment seed dataset (`infra/scripts/seed-sp4/data.json`) with `gameTranslations[]` array, sequential POST to BE admin endpoint shipped in Wave 5 (`POST /api/v1/admin/games/{id}/translations`), idempotent via list+filter pattern (mirror of `40-agents.sh`). Research doc è prerequisite manual work — pubblica IT title verification per gioco.

**Tech Stack:** Bash + `curl` + `jq` + Postgres `meepleai_staging` DB target via existing tunnel pattern + admin login cookie jar (shared with other seed steps).

**Spec source**: [`docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`](../specs/2026-06-20-translations-fe-hook-design.md) §11 (sub-PR 3/3 scope preview)

**Dependency**: BE admin endpoint `POST /api/v1/admin/games/{id}/translations` (Wave 5 di issue #2339). Se non shipped yet, Task 4 fail-fast con messaggio chiaro.

---

## Branch & PR conventions

- **Parent branch**: `main-dev` (per CLAUDE.md branch hygiene)
- **Feature branch**: `feature/issue-2339-translations-seed`
- **Branch parent config**:
  ```bash
  git config branch.feature/issue-2339-translations-seed.parent main-dev
  ```
- **PR target**: `main-dev`
- **PR title**: `feat(catalog): #2339 sub-PR 3/3 — seed IT translations + ADR-059 amendment`
- **Commit prefix**: `feat(seed)`, `chore(docs)`, `chore(adr)`

---

## File Structure

### Files to CREATE

```
infra/scripts/seed-sp4/translations-research.md   (provenance research, manual fill)
infra/scripts/seed-sp4/45-translations.sh         (seed script, idempotent)
```

### Files to MODIFY

```
infra/scripts/seed-sp4/data.json                  (add gameTranslations[] array)
infra/scripts/seed-sp4/seed-sp4.sh                (update step list comment to include 45)
docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md
  → add §6 amendment "Translations seed legal posture"
docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md
  → §9 Q4 closure note: "RESOLVED via #2339 sub-PR 3/3"
```

---

## Pre-flight checks

- [ ] **Pre-flight 0.1: Verify HEAD is on main-dev clean**

  ```bash
  git branch --show-current  # MUST print main-dev
  git status                 # MUST show clean tree
  git pull --ff-only
  ```

- [ ] **Pre-flight 0.2: Verify Wave 5 BE admin endpoint shipped**

  ```bash
  grep -rn "MapPost.*translations" apps/api/src/Api/Routing/ | head -3
  ```

  Expected: confirms `POST /api/v1/admin/games/{gameId:guid}/translations` is registered. If absent, **STOP** and complete Wave 5 first (Task 14 of `docs/superpowers/plans/2026-06-15-shared-game-translations.md`).

- [ ] **Pre-flight 0.3: Verify dev DB schema includes translations table**

  ```bash
  pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c '\d shared_game_translations'"
  ```

  Expected: confirms 13 columns + 4 indices. If table missing, run `cd apps/api/src/Api && dotnet ef database update` to apply Wave 2 migration.

- [ ] **Pre-flight 0.4: Verify 13 SP4 games seeded**

  ```bash
  cd infra && make seed-sp4
  pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c \"SELECT COUNT(*) FROM shared_games WHERE deleted_at IS NULL;\""
  ```

  Expected: ≥13 rows. If less, run `make seed-sp4` to bootstrap.

- [ ] **Pre-flight 0.5: Create feature branch**

  ```bash
  git checkout -b feature/issue-2339-translations-seed
  git config branch.feature/issue-2339-translations-seed.parent main-dev
  ```

---

## Task 1: Research doc skeleton

**Files:**
- Create: `infra/scripts/seed-sp4/translations-research.md`

### Step 1.1: Create skeleton with empty research table

```markdown
# SP4 Translations IT Research

Research provenance per IT translation curate del seed SP4. Compilato manualmente
prima del shipping di `45-translations.sh` per assicurare verification dei titoli
e classification corretta del `source` field.

Issue: #2339 sub-PR 3/3
Date: 2026-06-20

## Classification policy

- `source: "manual"` — IT title è ufficiale (publisher italiano confermato via fonte
  primaria pubblica) ED revisionato da native speaker (in MVP single-FTE: badsworm@gmail.com,
  IT-native).
- `source: "auto-openrouter"` — IT title NON è ufficiale o NON verificato; usato il
  Wave 1 backend translation service (DeepSeek V3) come fallback. Subject ad admin
  revision in follow-up.
- `source: "community"` — riservato per future contributions, NON usato in MVP seed.

## Verified IT Titles

| # | Game (EN) | IT Title proposed | Source verified? | Publisher IT | Publisher URL | Native review by | Decision (manual/auto/skip) |
|---|---|---|---|---|---|---|---|
| 1 | Azul | ? | TBD | Asmodee Italia? | ? | badsworm | TBD |
| 2 | Catan | I Coloni di Catan | YES — historical Asterion edition | Asterion Press (storica) / Giochi Uniti (corrente) | https://giochiuniti.it/games/catan | badsworm | manual |
| 3 | Wingspan | Wingspan | NO — Ghenos mantiene EN | Ghenos Games | https://www.ghenosgames.com/portfolio/wingspan/ | badsworm | manual (EN retained) |
| 4 | Brass: Birmingham | Brass: Birmingham | NO — Mancalamaro mantiene EN | Mancalamaro | https://mancalamaro.com/brass-birmingham | badsworm | manual (EN retained) |
| 5 | Gloomhaven | Gloomhaven | NO — Asmodee Italia mantiene EN | Asmodee Italia | ? | badsworm | manual (EN retained) |
| 6 | Ark Nova | Ark Nova | NO — Cranio Creations mantiene EN | Cranio Creations | https://www.craniocreations.it/ark-nova | badsworm | manual (EN retained) |
| 7 | Spirit Island | Spirit Island | NO — Ghenos mantiene EN | Ghenos Games | ? | badsworm | manual (EN retained) |
| 8 | 7 Wonders Duel | 7 Wonders Duel | NO — Asterion mantiene EN | Asterion Press | ? | badsworm | manual (EN retained) |
| 9 | Codenames | Nome in codice | YES — Cranio Creations edizione IT | Cranio Creations | https://www.craniocreations.it/nome-in-codice | badsworm | manual |
| 10 | Carcassonne | Carcassonne | YES — Giochi Uniti mantiene il nome (toponimo) | Giochi Uniti | https://giochiuniti.it/games/carcassonne | badsworm | manual (toponym retained) |
| 11 | Ticket to Ride | Ticket to Ride | NO — Asmodee Italia mantiene EN | Asmodee Italia | ? | badsworm | manual (EN retained) |
| 12 | Pandemic | Pandemic | NO — Asmodee Italia mantiene EN (storica "Pandemia" deprecata) | Asmodee Italia | ? | badsworm | manual (EN retained) |
| 13 | Terraforming Mars | Terraforming Mars | NO — Ghenos mantiene EN | Ghenos Games | https://www.ghenosgames.com/portfolio/terraforming-mars/ | badsworm | manual (EN retained) |

## Decision summary

- 13 giochi × IT translation:
  - **3** con IT title diverso da EN (Catan, Codenames, ... + eventuali aggiornamenti post-research).
  - **10** mantengono EN (publisher italiano ha mantenuto il nome originale).
- Tutti classified `source: "manual"` perché il titolo è confermato dalla fonte
  publisher anche quando coincide con EN — la decisione di mantenere EN è un atto
  curatorial.
- Zero `auto-openrouter` in MVP (research-driven only).

## TODO before sub-PR 3/3 ships

- [ ] Verificare i 4 publisher URL marcati `?` (Azul, Spirit Island, 7 Wonders Duel, Ticket to Ride, Pandemic Asmodee Italia).
- [ ] Cross-check con BGG IT page per ciascuno (es. https://boardgamegeek.com/boardgame/13/catan → "Italian language editions").
- [ ] Aggiornare tabella → no più `TBD` o `?` lasciati.

## Description field

Per MVP `45-translations.sh` shippa SOLO `title` field (description=null).
Description IT curata è deferred — admin compila post-launch via PUT.

## Out of scope

- Translation di altri giochi nel catalogo (160+ rows snapshot — solo 13 SP4 in scope).
- Translation di campi `description` per i 13 (richiede effort 10-20× il title).
- Translation `auto-openrouter` di tipo fall-back (deferred a admin discretion).
- Locale altri (`en-GB`, `de`, `fr`, `es`) — out of scope MVP.

## References

- Issue tracker: #2339 sub-PR 3/3
- Wave 1 spec: `docs/superpowers/specs/2026-06-15-shared-game-translations-design.md`
- Sub-PR 2/3 design: `docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`
- ADR-059 amendment: `docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md` §6 (added in this PR)
- BGG IT editions: per-game `https://boardgamegeek.com/boardgame/<bgg_id>/<slug>/credits` § Versions
```

### Step 1.2: Commit skeleton

```bash
git add infra/scripts/seed-sp4/translations-research.md
git commit -m "docs(seed): add translations-research.md skeleton (#2339 sub-PR 3/3)"
```

---

## Task 2: Manual research — fill in the verified URLs and decisions

**Files:**
- Modify: `infra/scripts/seed-sp4/translations-research.md`

### Step 2.1: Verify publisher URLs and edition info

Per ciascun gioco marcato `TBD` o `?` nella tabella di Task 1, eseguire:

1. Visita BGG game page → tab Versions → filter by language=Italian.
2. Click su edition IT → annota nome publisher + ISBN + url eventuale.
3. Cerca publisher home page → annota url IT del gioco.
4. Conferma se il publisher IT ha cambiato il titolo o mantenuto EN.

**Esempio cluster lookup (esegui manualmente)**:

- Azul → BGG ID 230802 → versions IT → Lacerta editions ed Asmodee Italia. Cerca "Azul Asmodee Italia" → conferma se "Azul" rimane (toponym/pattern reference).
- Spirit Island → BGG ID 162886 → versions IT → Ghenos Games. Cerca pubblicazione "Spirit Island Ghenos" → conferma title.

### Step 2.2: Update tabella in `translations-research.md`

Sostituire i `?` con URL verificati + colonna `Source verified?` con `YES`/`NO`/`PARTIAL`.

### Step 2.3: Lock decision summary

Aggiornare § "Decision summary" con count finale (X giochi con IT title diverso da EN, Y con EN retained).

### Step 2.4: Commit research

```bash
git add infra/scripts/seed-sp4/translations-research.md
git commit -m "docs(seed): verify IT publisher URLs for all 13 SP4 games (#2339 sub-PR 3/3)"
```

---

## Task 3: Add `gameTranslations[]` to `data.json`

**Files:**
- Modify: `infra/scripts/seed-sp4/data.json`

### Step 3.1: Add `gameTranslations` array

Append before the closing `}` of the JSON root:

```json
  "gameTranslations": [
    { "gameSlug": "azul",        "locale": "it", "title": "Azul",              "source": "manual" },
    { "gameSlug": "catan",       "locale": "it", "title": "I Coloni di Catan", "source": "manual" },
    { "gameSlug": "wingspan",    "locale": "it", "title": "Wingspan",          "source": "manual" },
    { "gameSlug": "brass",       "locale": "it", "title": "Brass: Birmingham", "source": "manual" },
    { "gameSlug": "gloomhaven",  "locale": "it", "title": "Gloomhaven",        "source": "manual" },
    { "gameSlug": "arknova",     "locale": "it", "title": "Ark Nova",          "source": "manual" },
    { "gameSlug": "spirit",      "locale": "it", "title": "Spirit Island",     "source": "manual" },
    { "gameSlug": "7wonders",    "locale": "it", "title": "7 Wonders Duel",    "source": "manual" },
    { "gameSlug": "codenames",   "locale": "it", "title": "Nome in codice",    "source": "manual" },
    { "gameSlug": "carcassonne", "locale": "it", "title": "Carcassonne",       "source": "manual" },
    { "gameSlug": "ticket",      "locale": "it", "title": "Ticket to Ride",    "source": "manual" },
    { "gameSlug": "pandemic",    "locale": "it", "title": "Pandemic",          "source": "manual" },
    { "gameSlug": "terraforming","locale": "it", "title": "Terraforming Mars", "source": "manual" }
  ]
```

(Verify final values from `translations-research.md` — example above shows the MVP picks but Task 2 manual research may revise specific titles, e.g. Wingspan IT could come back as `"Wingspan: Ali del Mondo"` if publisher revised name.)

### Step 3.2: Validate JSON

```bash
jq empty infra/scripts/seed-sp4/data.json
echo "Translations count: $(jq '.gameTranslations | length' infra/scripts/seed-sp4/data.json)"
```

Expected: no errors, count = 13.

### Step 3.3: Commit

```bash
git add infra/scripts/seed-sp4/data.json
git commit -m "feat(seed): add gameTranslations[] array for 13 SP4 IT titles (#2339 sub-PR 3/3)"
```

---

## Task 4: Implement `45-translations.sh` script

**Files:**
- Create: `infra/scripts/seed-sp4/45-translations.sh`

### Step 4.1: Implement idempotent seed script

```bash
#!/usr/bin/env bash
# 45-translations.sh — Create IT translations for the 13 SP4 SharedGames.
#
# Idempotent: GET /admin/games/{id}/translations to find existing locale entries.
# Mirrors 40-agents.sh pattern for ownership/login.
#
# Requires:
#   - 20-games.sh executed (state_file has games[slug] → gameId)
#   - BE admin endpoint POST /api/v1/admin/games/{id}/translations shipped (Wave 5)
#   - Migration AddSharedGameTranslations applied (Wave 2)
#
# Issue: #2339 sub-PR 3/3

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "45 — Game Translations (13 SP4 IT)"
ADMIN_JAR=$(cookie_jar_for "admin")
[[ -s "$ADMIN_JAR" ]] || { admin_login; }

total=0; created=0; existing=0; failed=0; skipped=0

while IFS= read -r t; do
  game_slug=$(jq -r '.gameSlug' <<< "$t")
  locale=$(jq -r '.locale'      <<< "$t")
  title=$(jq -r '.title'        <<< "$t")
  source=$(jq -r '.source'      <<< "$t")
  total=$((total + 1))

  # Resolve game_id
  game_id=$(state_get "games" "$game_slug")
  if [[ -z "$game_id" || "$game_id" == "null" ]]; then
    warn "[$game_slug/$locale] missing game_id — run 20-games first; skipping"
    skipped=$((skipped + 1)); continue
  fi

  # Pre-check: does translation for this locale already exist?
  list_resp=$(curl_get "/admin/games/$game_id/translations" "$ADMIN_JAR")
  list_body=$(echo "$list_resp" | sed '$d')
  list_code=$(echo "$list_resp" | tail -n1)

  if [[ "$list_code" == "200" ]]; then
    existing_id=$(echo "$list_body" | jq -r --arg l "$locale" \
      '.[]? | select(.locale==$l) | .locale' 2>/dev/null | head -1)
    if [[ -n "$existing_id" && "$existing_id" != "null" ]]; then
      skip "$game_slug/$locale already exists — skipping"
      existing=$((existing + 1)); continue
    fi
  elif [[ "$list_code" != "404" ]]; then
    warn "[$game_slug/$locale] list endpoint HTTP $list_code unexpected"
  fi

  # Create translation
  create_payload=$(jq -nc \
    --arg locale "$locale" \
    --arg title "$title" \
    --arg source "$source" \
    '{locale:$locale, title:$title, description:null, source:$source}')

  cr=$(curl_json POST "/admin/games/$game_id/translations" "$ADMIN_JAR" "$create_payload")
  cr_body=$(echo "$cr" | sed '$d')
  cr_code=$(echo "$cr" | tail -n1)

  if http_check "201|200" "$cr_code" "$cr_body" "create $game_slug/$locale"; then
    ok "Created $game_slug/$locale → '$title' ($source)"
    created=$((created + 1))
  else
    failed=$((failed + 1))
  fi
done < <(data_get_compact '.gameTranslations[]')

log "Summary: total=$total  created=$created  existing=$existing  skipped=$skipped  failed=$failed"
[[ $failed -gt 0 ]] && fail "45-translations completed with $failed failures"
ok "45-translations complete"
```

### Step 4.2: Make executable

```bash
chmod +x infra/scripts/seed-sp4/45-translations.sh
```

### Step 4.3: Smoke-test locally (dev DB)

```bash
cd infra
# Ensure 20-games has run first (state file populated)
./scripts/seed-sp4/seed-sp4.sh --step 45
```

Expected output:
```
45 — Game Translations (13 SP4 IT)
Created azul/it → 'Azul' (manual)
Created catan/it → 'I Coloni di Catan' (manual)
... 13 lines total ...
Summary: total=13  created=13  existing=0  skipped=0  failed=0
45-translations complete
```

Verify in DB:

```bash
pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c \"SELECT COUNT(*) FROM shared_game_translations WHERE NOT is_deleted;\""
```

Expected: 13.

### Step 4.4: Idempotency check — re-run

```bash
./scripts/seed-sp4/seed-sp4.sh --step 45
```

Expected output:
```
Summary: total=13  created=0  existing=13  skipped=0  failed=0
```

### Step 4.5: Commit script

```bash
git add infra/scripts/seed-sp4/45-translations.sh
git commit -m "feat(seed): add 45-translations.sh seed script (#2339 sub-PR 3/3)"
```

---

## Task 5: Update orchestrator + seed-sp4.sh banner

**Files:**
- Modify: `infra/scripts/seed-sp4/seed-sp4.sh`

### Step 5.1: Update banner comment

In `seed-sp4.sh` at the step list comment block (lines 14-25), add:

```bash
#   45 translations   ← 13 IT game title translations (#2339)
```

between `40 agents` and `50 toolkits`.

### Step 5.2: Verify orchestrator picks up new step

```bash
./infra/scripts/seed-sp4/seed-sp4.sh --help
# Expected output now lists 45-translations.sh in the step enumeration
```

Note: The orchestrator uses `ls [0-9][0-9]-*.sh` glob (line 55), so the new script is auto-picked-up — no further code change needed beyond the banner comment.

### Step 5.3: Run full pipeline locally

```bash
cd infra && make seed-sp4
```

Expected: all 12 steps complete green (00, 10, 20, 30, 40, 45, 50, 60, 70, 80, 90, 95).

### Step 5.4: Commit

```bash
git add infra/scripts/seed-sp4/seed-sp4.sh
git commit -m "chore(seed): register 45-translations in orchestrator banner (#2339 sub-PR 3/3)"
```

---

## Task 6: ADR-059 §6 amendment

**Files:**
- Modify: `docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md`

### Step 6.1: Append §6 amendment after current §5 (User-side BGG asset ban)

Add this section right before the `## References` block:

```markdown
## 6. Amendment 2026-06-20 — Translations seed legal posture (issue #2339 sub-PR 3/3)

ADR-059 §2 narrowly addressed metadata fields fetched from external providers
(Wikidata primary / BGG whitelisted fallback). Issue #2339 sub-PR 3/3 introduces
a new data stream: **IT translations of game titles** for the SP4 seed dataset,
seeded via `infra/scripts/seed-sp4/45-translations.sh` against the
`shared_game_translations` table (Wave 2 of #2339).

The legal vectors are different from §2 because translations are:
1. **Curated by us**, not fetched from a third-party provider's database.
2. **Fact-based** in Feist v. Rural sense — a title is a factual identifier of
   a published product, not a creative expression. The IT translation "I Coloni
   di Catan" is the publicly available product name of the Italian-licensed
   edition (Asterion Press / Giochi Uniti). Feist 499 U.S. 340 (1991) and the
   parallel EU jurisprudence both exclude facts from copyright.
3. **Provenance-tracked** via the per-row `translations-research.md` document
   that records publisher URL + verification status for each title.

### 6.1 Decision

**Translations seed data is OUT OF SCOPE of §2 BGG whitelist** because:
- The translation data is NOT fetched from BGG. It is researched against the
  Italian publisher's public website (Asterion, Cranio Creations, Ghenos,
  Asmodee Italia, Mancalamaro, Giochi Uniti).
- The data shape is `{ shared_game_id, locale, title, source }` — title is a
  fact, source is provenance metadata.
- The `source` enum values (`manual` / `auto-openrouter` / `community`) record
  the authoring path so audit trail mirrors §2 provenance approach.

### 6.2 In-scope governance for translation seed

- **Verification mandate** (§6.3 below): every `source: "manual"` row MUST have a
  recorded publisher URL in `translations-research.md` at the time of seed
  shipping. PRs touching `gameTranslations[]` in `data.json` MUST update the
  research doc in the same commit.
- **`source: "auto-openrouter"` records** (when added): MUST be marked clearly
  in the research doc as machine-translated and subject to admin review before
  promotion to `manual`.
- **`source: "community"` records**: BLOCKED in MVP. No community contribution
  workflow exists; field is reserved for future feature.

### 6.3 Per-row provenance schema

`infra/scripts/seed-sp4/translations-research.md` carries the table:

| Game (EN) | IT Title | Source verified? | Publisher IT | Publisher URL | Native review by | Decision |
|---|---|---|---|---|---|---|

The "Decision" column must be one of:
- `manual` — publisher confirmed, native review done.
- `manual (EN retained)` — publisher kept the English name; the row exists for
  consistency but the title equals canonical EN.
- `auto-openrouter` — fall-back, admin TODO to verify.
- `skip` — game intentionally excluded from translations seed.

### 6.4 Out of scope for §6

- Translations for non-EN languages other than IT (deferred to per-locale follow-up).
- Translations of `description` field (BE Wave 1 schema supports it; seed scope is title-only).
- Community-sourced translations workflow.
- Auto-translation triggered at fetch time (would be ADR-059 §2 territory because OpenRouter is a third-party provider; but no FE consumer triggers this in MVP).

### 6.5 References

- Issue: [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339) sub-PR 3/3
- Wave 1 spec (BE foundation): [`docs/superpowers/specs/2026-06-15-shared-game-translations-design.md`](../../../superpowers/specs/2026-06-15-shared-game-translations-design.md)
- Sub-PR 2/3 design: [`docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`](../../../superpowers/specs/2026-06-20-translations-fe-hook-design.md)
- Sub-PR 3/3 plan: [`docs/superpowers/plans/2026-06-20-translations-seed-subpr3.md`](../../../superpowers/plans/2026-06-20-translations-seed-subpr3.md)
- Provenance research: `infra/scripts/seed-sp4/translations-research.md`
- Feist Publications v. Rural Telephone Service, 499 U.S. 340 (1991)
```

### Step 6.2: Commit ADR

```bash
git add docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md
git commit -m "chore(adr): ADR-059 §6 — translations seed legal posture (#2339 sub-PR 3/3)"
```

---

## Task 7: Close Q4 mitigation note in seed-kb-coverage-evaluation spec

**Files:**
- Modify: `docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md`

### Step 7.1: Replace Q4 closure block (§9)

Find the existing block starting `- **Q4 — RESOLVED via canonical EN + translation deferred**:` (around line 323) and replace with:

```markdown
- **Q4 — RESOLVED via #2339 (all sub-PRs shipped)**: titolo del seed Catan rimane `Catan` canonical EN; il dual-row anomaly su snapshot DB è risolto da Wave 1 (PR #2370). IT translation `I Coloni di Catan` shipped via sub-PR 3/3 (seed script `infra/scripts/seed-sp4/45-translations.sh` + `gameTranslations[]` array in `data.json`). Translation service backend wire-up: `IGameTitleResolver` arricchisce `SharedGameDto.Translations[]` su 4 query handler (sub-PR 1/3); FE hook `useGameTitle()` consume il payload con BCP-47 fallback + source priority (sub-PR 2/3).
  - **Schema attuale**: `shared_game_translations` table (Wave 2 migration `AddSharedGameTranslations`).
  - **Frontend i18n entity titles**: hook `useGameTitle()` in `apps/web/src/lib/i18n/use-game-title.ts` — vedi spec `docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`.
  - **Legal posture**: ADR-059 §6 (amendment 2026-06-20) lock-in translations seed governance separately from §2 BGG whitelist.
  - **Misurazione**: baseline su snapshot DB §9.1 ora include 13 IT translations attive; M3 ora atteso 12/13 (Gloomhaven excluded by design Q2).
```

### Step 7.2: Update baseline measurement note in §9.1

Just under the baseline table (around line 352), append a note:

```markdown
**Post Q4 sub-PR 3/3 closure**: tabella ora include `it` translation in `shared_game_translations` table per ciascuno dei 13 giochi (10 `manual (EN retained)` + 3 `manual` con titolo IT divergente: Catan, Codenames, + eventuali revisioni post-research). Misura M3 invariata (translation NON impatta KB indexing).
```

### Step 7.3: Commit

```bash
git add docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md
git commit -m "chore(docs): close Q4 mitigation note — #2339 all sub-PRs shipped (#2339 sub-PR 3/3)"
```

---

## Task 8: Push, PR, close #2339

### Step 8.1: Final pre-push checks

```bash
# 1. Verify seed orchestrator green end-to-end
cd infra && make seed-sp4

# 2. Verify translations table populated
pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c \"SELECT game_id, locale, title, source FROM shared_game_translations WHERE NOT is_deleted ORDER BY locale, title;\""
# Expected: 13 rows, all source='manual'

# 3. Verify FE happy-path (manual smoke)
cd ../apps/web && pnpm dev
# Browser → http://localhost:3000/library → see "I Coloni di Catan" + "Nome in codice" titles
```

If any gate red, FIX before push.

### Step 8.2: Push

```bash
git push -u origin feature/issue-2339-translations-seed
```

### Step 8.3: Open PR

```bash
gh pr create --base main-dev \
  --title "feat(catalog): #2339 sub-PR 3/3 — seed IT translations + ADR-059 amendment" \
  --body "$(cat <<'EOF'
## Summary

Sub-PR 3/3 of #2339. Closes issue #2339 with all 3 waves shipped:
- Sub-PR 1/3 (BE foundation) — PR #2370 merged 2026-06-15
- Sub-PR 2/3 (FE hook + DTO + consumer migration) — PR #<TBD>
- Sub-PR 3/3 (this PR — seed IT translations + ADR-059 amendment + Q4 closure)

## Changes

- `infra/scripts/seed-sp4/data.json` → `gameTranslations[]` array (13 IT titles).
- `infra/scripts/seed-sp4/45-translations.sh` → idempotent seed script (mirror 40-agents.sh pattern).
- `infra/scripts/seed-sp4/translations-research.md` → per-row provenance research (publisher URL + native review).
- `docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md` → §6 amendment: translations seed legal posture (separate from §2 BGG whitelist).
- `docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md` → §9 Q4 closure note replaced.

## Test plan

- [x] `make seed-sp4` green end-to-end (12 steps incl. new step 45).
- [x] `45-translations.sh` idempotent: re-run shows `existing=13`.
- [x] Postgres `shared_game_translations` table populated with 13 active rows.
- [x] FE happy-path: IT browser locale shows IT translated titles on Library + Discover.
- [x] Provenance research doc filled, no `?` or `TBD` remaining.
- [x] ADR-059 §6 documents the translations-vs-BGG distinction.
- [x] Q4 doc closure note reflects all 3 sub-PRs shipped.

Closes #2339.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

### Step 8.4: Code review subagent (per CLAUDE.md `/implementa` Phase 6 rule)

```bash
# /code-review:code-review <PR_URL>
```

If reviewer finds blockers, fix in NEW commits.

### Step 8.5: Update #2339 body — final closure

```bash
gh issue edit 2339 --body-file <(gh issue view 2339 --json body --jq '.body' | sed 's|⏳ TODO|✅ MERGED|g')
```

Or hand-edit the progress table:
- Sub-PR 3/3 row: `⏳ TODO` → `✅ MERGED` with PR link and merge SHA.

### Step 8.6: Close issue

Once PR merges, GitHub auto-closes issue #2339 via the `Closes #2339` body line. Verify:

```bash
gh issue view 2339 --json state --jq '.state'
# Expected: CLOSED
```

### Step 8.7: Post-merge cleanup

```bash
git checkout main-dev
git pull
git branch -D feature/issue-2339-translations-seed
git remote prune origin
```

---

## Effort estimate

| Task | Effort |
|---|---|
| Task 1: Research doc skeleton | 0.5h |
| Task 2: Manual research (13 games × verification) | 2-3h |
| Task 3: `gameTranslations[]` add to data.json | 0.5h |
| Task 4: `45-translations.sh` implementation + smoke | 1.5h |
| Task 5: Orchestrator banner update + full pipeline run | 0.5h |
| Task 6: ADR-059 §6 amendment | 1h |
| Task 7: Q4 doc closure | 0.5h |
| Task 8: PR + review + cleanup | 0.5h |
| **Total** | **~7h** (~1gg single FTE post-research) |

The Task 2 manual research is the long pole. If publisher URLs are pre-verified in a quick async call, the actual implementation reduces to ~4h.

---

## Self-review checklist

- [ ] **Pre-flight Wave 5 verified**: admin POST endpoint live before Task 4.
- [ ] **Research doc completed**: no `?` or `TBD` in `translations-research.md` table at merge time.
- [ ] **Seed script idempotent**: re-run shows `existing=13` not `created=26`.
- [ ] **ADR-059 §6 differentiates** translation seed vs §2 BGG whitelist clearly.
- [ ] **Q4 doc closure** references all 3 sub-PRs (1/3 #2370, 2/3 <pr>, 3/3 <pr>).
- [ ] **PR body has `Closes #2339`** → auto-close on merge.
- [ ] **Code review subagent invoked** per CLAUDE.md /implementa Phase 6.
- [ ] **Branch hygiene preflight 0.1-0.5 executed**.
- [ ] **PR target main-dev** confirmed.

---

## Cross-references

- **Spec source (sub-PR 2/3 + 3/3 scope)**: [`docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`](../specs/2026-06-20-translations-fe-hook-design.md) §11
- **Companion sub-PR 2/3 plan**: [`docs/superpowers/plans/2026-06-20-translations-fe-hook.md`](./2026-06-20-translations-fe-hook.md)
- **Wave 1 spec (BE foundation)**: [`docs/superpowers/specs/2026-06-15-shared-game-translations-design.md`](../specs/2026-06-15-shared-game-translations-design.md)
- **Wave 1 plan TDD**: [`docs/superpowers/plans/2026-06-15-shared-game-translations.md`](./2026-06-15-shared-game-translations.md)
- **Tracker**: [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339)
- **Wave 1 shipped PR**: [#2370](https://github.com/meepleAi-app/meepleai-monorepo/pull/2370) (`cd041ca35`)
- **Q4 closure target**: [`docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md`](../../for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md) §9
- **ADR-059 (catalog seed legal posture)**: [`docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md`](../../for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md)
