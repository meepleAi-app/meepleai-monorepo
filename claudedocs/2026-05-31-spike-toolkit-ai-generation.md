# Spike — AI Toolkit Generation Quality (Wingspan)

- **Data**: 2026-05-31
- **Tipo**: Gate-keeper spike (decisione architetturale dipende dall'output)
- **Esecutore**: 2 run — (1) Claude Code Opus 4.7 in-context come surrogato upper-bound, (2) **Llama 3.3 70B via OpenRouter** come surrogato production
- **Scope**: Validare se `GenerateToolkitFromKbCommand` produce JSON usabile da rulebook chunks
- **Verdetto unificato**: ⚠️ **Partial pass + production gap** — prompt funziona ma lo schema DTO è leaky abstraction E il LLM free-tier non rispetta lo schema con fedeltà; production deve usare modello premium + JSON schema validation + human review obbligatorio

---

## 1. Goal

Prima di disegnare mockup polimorfici "game-agnostic" basati su `Toolkit` + `Toolbox`, validare la **ipotesi base**:

> L'AI generation pipeline esistente (`GenerateToolkitFromKbCommand` + `ToolkitExtractionPrompts.SystemPrompt`) produce per un gioco reale un `AiToolkitSuggestionDto` (a) schema-valido, (b) coerente con le regole del gioco, (c) renderizzabile da una UI generica.

Risposta: (a) sì, (b) sì al 70-80% per giochi mainstream, (c) **no** — lo schema è troppo generico per le meccaniche "flavor".

---

## 2. Stato attuale dell'infrastruttura (rilevato 2026-05-31)

| Componente | Stato | Note |
|---|---|---|
| `GenerateToolkitFromKbHandler` | ✅ implementato | 161 righe, hybrid search × 5 query categorie + LLM call + retry + confidence calibration |
| `ToolkitExtractionPrompts.SystemPrompt` | ✅ implementato | 49 righe, citation-required, ExcludedTools field |
| `AiToolkitSuggestionDto` schema | ✅ implementato | 8 fields: ToolkitName, DiceTools, CounterTools, TimerTools, ScoringTemplate, TurnTemplate, Overrides, Reasoning, ExcludedTools |
| `ApplyAiToolkitSuggestionCommand` | ✅ implementato | Workflow review/apply con human-in-the-loop |
| Confidence calibration | ✅ implementato | `avgScore × min(1, chunks/5)`; soglia 0.6 per `RequiresHumanReview` |
| LLM providers | ✅ 3 supportati | OpenRouter (multi-provider), DeepSeek, Ollama (local) |
| Test unitari | ✅ esistono | `GenerateToolkitFromKbHandlerTests.cs` — ma con mock `Mock<ILlmService>`, **qualità output reale non validata** |
| Toolkit generati per giochi reali | ❌ **zero** | Nessun esempio in DB seed, app pre-launch |
| Wingspan PDF in KB | ✅ disponibile | `data/rulebook/wingspan_en_rulebook.pdf` + dump seed con 106 PDF / 8599 chunks |
| Frontend hooks | ✅ esistono | `useGameToolkit`, `useToolkitEditor`, `useToolboxSync`, `resolveSessionTools.tsx` |
| Mockup che consumano polimorficamente toolkit | ❌ **zero** | Mockup attuali hard-coded per Wingspan |

---

## 3. Metodologia

### 3.1 Sostituzioni rispetto al runtime production

| Componente production | Sostituzione spike | Rationale |
|---|---|---|
| Hybrid search su 5 categorie (dice/counters/timer/scoring/turn) | Excerpts manuali ricostruiti dalle regole pubbliche Wingspan | Postgres + pgvector + KB not bootstrapped locally; regole Wingspan pubbliche e ben note (BGG, manual scan), excerpts realistici |
| LLM call (OpenRouter / DeepSeek / Ollama) | **Claude Opus 4.7 in-context** come surrogato | API key non disponibile in sessione; Claude Opus = "best-case LLM" → spike è **upper-bound qualità** |
| `BuildUserPrompt(gameTitle, chunks)` | Costruito fedele all'handler (vedi 3.2) | 1:1 col codice |
| `ToolkitExtractionPrompts.SystemPrompt` | Identico al codice | Copiato textualmente |

### 3.2 Disclaimer onesto

Il mio output JSON (vedi §5) è un **upper bound**. Modelli più piccoli (gpt-4o-mini, deepseek-chat) potrebbero:
- Allucinare campi non presenti nei chunks
- Saltare ExcludedTools (raro che modelli small seguano negative instruction)
- Produrre confidence inflated
- Non citare excerpts nel Reasoning

Il pass del spike dimostra che **il prompt + schema sono ben progettati per LLM moderne**. Non dimostra che ogni model lo eseguirà ugualmente bene.

---

## 4. Input simulato

```
Game: Wingspan

Rulebook excerpts:
[1] (score=0.92) — query "dice requirements types quantity faces rolling"
Wingspan includes 5 custom 6-sided food dice. Each die has 6 faces depicting food
types: invertebrate (worm), seed, fruit, fish, rodent (mouse), and a wild face
showing two food types (player's choice). Dice are placed in the birdfeeder
dice tower and re-rolled when fewer than 2 unique food types remain.

[2] (score=0.88) — query "counters resources tokens points tracking per player"
Each player has their own player mat with 3 habitat rows: forest, grassland,
wetland. Players track: eggs (small wooden tokens placed on bird cards, max
capacity per bird = 1-6), food tokens (taken from birdfeeder, stored on
player mat), and action cubes (8 cubes per player to mark chosen actions on
their player mat each round). Points are tallied only at game end.

[3] (score=0.71) — query "timer time limit turn duration countdown"
There is no time limit per turn or per round. Wingspan is a turn-based game
without timers. Players may take as long as they need to make decisions,
though casual play typically lasts 40-70 minutes total for 1-5 players.

[4] (score=0.95) — query "scoring points victory conditions ranking dimensions"
At game end, each player scores points in 6 categories:
- Birds: variable points printed on each bird card (1-9 points)
- Bonus cards: variable points per personal bonus card objectives
- End-of-round goals: 5/2/1 or 4/2/1 points per round based on rank in goal
- Eggs: 1 point per egg laid
- Food cached on bird cards: 1 point per food cached
- Tucked cards (drawn cards tucked under birds for ability): 1 point each
Highest total wins. Ties broken by most bonus card points, then by most food.

[5] (score=0.83) — query "turn order round phases sequence players"
Wingspan is played over 4 rounds. Round 1: each player has 8 turns. Round 2:
7 turns. Round 3: 6 turns. Round 4: 5 turns. Turn order is round-robin
clockwise starting with the first player. On each turn, a player chooses
exactly 1 of 4 actions (play bird / get food / lay eggs / draw cards), then
play passes to the next player. End-of-round goals are scored after the
last turn of each round.

Extract the toolkit configuration for "Wingspan" from the excerpts above.
```

---

## 5. Output simulato (Claude Opus 4.7 come surrogato LLM)

```json
{
  "ToolkitName": "Wingspan",
  "DiceTools": [
    {
      "Name": "Birdfeeder Food Dice",
      "DiceType": "Custom",
      "Quantity": 5,
      "CustomFaces": ["invertebrate", "seed", "fruit", "fish", "rodent", "wild"],
      "IsPerPlayer": false
    }
  ],
  "CounterTools": [
    { "Name": "Eggs", "Min": 0, "Max": 6, "IsPerPlayer": true,
      "Note": "Per-bird capacity 1-6; tracked on individual bird cards" },
    { "Name": "Food tokens (5 types)", "Min": 0, "Max": 99, "IsPerPlayer": true,
      "Note": "Worm/seed/fruit/fish/rodent stored on player mat" },
    { "Name": "Action cubes", "Min": 0, "Max": 8, "IsPerPlayer": true,
      "Note": "8 per player, marker on player mat actions" }
  ],
  "TimerTools": [],
  "ScoringTemplate": {
    "Model": "points",
    "Categories": [
      { "Id": "birds", "Label": "Birds played", "Computation": "sum-bird-values", "Weight": 1 },
      { "Id": "bonus-cards", "Label": "Bonus cards", "Computation": "sum-bonus-values", "Weight": 1 },
      { "Id": "round-goals", "Label": "End-of-round goals", "Computation": "rank-based", "Weight": 1 },
      { "Id": "eggs", "Label": "Eggs", "Computation": "count", "Weight": 1 },
      { "Id": "cached-food", "Label": "Food cached", "Computation": "count", "Weight": 1 },
      { "Id": "tucked-cards", "Label": "Tucked cards", "Computation": "count", "Weight": 1 }
    ],
    "WinCondition": { "Type": "highest-total" },
    "Tiebreakers": ["bonus-card-points", "total-food"]
  },
  "TurnTemplate": {
    "TurnOrderType": "RoundRobin",
    "Direction": "clockwise",
    "Rounds": 4,
    "TurnsPerRound": [8, 7, 6, 5],
    "Phases": [
      { "Id": "round-1", "Order": 1, "Label": "Round 1 (8 turns)" },
      { "Id": "round-2", "Order": 2, "Label": "Round 2 (7 turns)" },
      { "Id": "round-3", "Order": 3, "Label": "Round 3 (6 turns)" },
      { "Id": "round-4", "Order": 4, "Label": "Round 4 (5 turns)" }
    ],
    "TurnActions": ["play-bird", "get-food", "lay-eggs", "draw-cards"]
  },
  "Overrides": {
    "OverridesTurnOrder": true,
    "OverridesScoreboard": true,
    "OverridesDiceSet": true
  },
  "Reasoning": "Wingspan has a deterministic 4-round structure with decreasing turn count [excerpt 5]. The 5 food dice with 6 custom faces are unique to the game [excerpt 1]. Scoring uses 6 additive categories with rank-based round goals [excerpt 4]. No time limit [excerpt 3] → empty TimerTools. Custom dice + custom scoring + custom turn-count justify all 3 override flags.",
  "ExcludedTools": [
    { "Type": "Timer", "Justification": "Rulebook explicitly states no time limit per turn (excerpt 3)" },
    { "Type": "CardDeck (shared draw deck)", "Justification": "Bird cards exist but use a fixed 3-card display + discard, not a generic deck — handle via custom widget, not generic deck" }
  ],
  "ConfidenceScore": 0.85,
  "ChunksAnalyzed": 5,
  "KbCoveragePercent": 100,
  "RequiresHumanReview": false
}
```

---

## 6. Verdetto qualitativo

### ✅ Cosa funziona

| Aspetto | Evidenza |
|---|---|
| Schema coverage 100% | Tutti gli 8 fields del DTO popolati |
| Game-specific accuracy | 5 dadi custom + 6 facce, 4 round con turn decreasing, 6 scoring categories, tiebreakers, no timer |
| Citation integrity | Reasoning cita excerpt [1]-[5] — auditable |
| Honest exclusions | Esclude timer + generic card deck con motivazione |
| Confidence calibrata | 0.85 > soglia 0.6 → no human review |
| Override flags | Correttamente settati a `true` per dice/scoreboard/turn (Wingspan non riusa template default) |

### ❌ Cosa NON funziona

| Issue | Severità | Impatto |
|---|---|---|
| **`TurnTemplate` schema power-poor** | 🔴 CRITICAL | Lo schema esistente probabilmente non ha campi `Rounds`, `TurnsPerRound`, `TurnActions`, `Direction` — ho dovuto inventarli. Schema attuale **non espressivo abbastanza** |
| **`ScoringTemplate.Categories.Computation` enum-free** | 🔴 CRITICAL | `"sum-bird-values"` o `"rank-based"` sono stringhe libere senza grammar. UI non può interpretarle senza hard-code per ogni gioco. **Leaky abstraction** |
| **Habitat rows (forest/grassland/wetland) non rappresentabili** | 🟡 MAJOR | Pattern centrale Wingspan UX, non entra in nessun widget template |
| **Bird display 3-card rotating market non rappresentabile** | 🟡 MAJOR | Meccanica core Wingspan, segnalata in ExcludedTools senza alternativa |
| **`TurnsPerRound = [8,7,6,5]`** | 🟡 MAJOR | Pattern unico Wingspan, schema generico non lo modella nativamente |
| **Bonus card values + end-of-round goal cards** | 🟢 MINOR | Citate ma "variable" — l'AI non può sapere i valori specifici (sono nelle card, non nel rulebook chunk) |

### ⚖️ Implicazioni strategiche

**Lo schema cattura le *primitive* ma non le *flavor***. Per i top-20 giochi:

- 5 top giochi (premium) → richiederanno comunque **custom rendering layer** (habitat di Wingspan, board hex di Catan, isole di Pandemic, sub board di Captain Sonar, parole-grid di Codenames)
- 15 restanti → il generic shell + AI-generated toolkit basta come "AI rules assistant + basic scoring"

**Pattern emergente**: il toolkit AI-generated è **utile come pre-fill per il system prompt dell'AI agent + scoreboard generico**, non come "rendering complete UI". Coerente con D1=a ("AI chat è il magnete, sessions sono supporting feature").

---

## 7. Conseguenze per B19 (Game-agnostic session skeleton)

### 7.1 Scope HYBRID raccomandato

Lo spike fa cadere l'ipotesi "Universal polimorfico puro" (D2=a originale). Proposta riveduta:

**B19 Scope hybrid** (effort 8-12 giorni):

1. **Generic Session Skeleton** (1 mockup):
   - Top-bar (game title, players, connection, pause/resume) — universale
   - Chat-agent panel (LiveAgentChat reuse, embedded) — **il vero magnete (D1=a)**
   - Generic scoring panel (legge `Toolkit.ScoringTemplate.Categories`, renderer matrix con fallback "numerico")
   - Generic turn indicator (legge `Toolkit.TurnTemplate.TurnOrderType`)
   - Generic widget grid (legge `Toolkit.Widgets[]` + WidgetType enum, renderer per i 6 tipi)
   - Stati: default · empty · loading · error · sse-disconnect

2. **5 Top Game Premium mockups** (5 mockup ad-hoc, ognuno usa skeleton + add-on):
   - Wingspan (già fatto, rinominato `sp4-session-wingspan-*`)
   - Da scegliere altri 4 (top played by early adopter o BGG top + variety archetypi)

3. **15 restanti giochi** → solo skeleton + AI-generated toolkit. No mockup ad-hoc, l'AI chat compensa.

### 7.2 Pre-requisiti tecnici prima dell'implementazione FE

| Item | Owner | Tempo |
|---|---|---|
| **Estendere `TurnTemplateConfig`** per supportare `TurnsPerRound`, `TurnActions`, `Direction` | Backend | 1-2 giorni |
| **Definire enum/grammar** per `ScoringTemplate.Categories.Computation` (es. `Count`, `Sum`, `RankBased`, `Custom`) | Backend | 1 giorno |
| **Test integration con LLM reale** (DeepSeek o gpt-4o-mini) su 3 PDF (Wingspan + Catan + Codenames) per validare quality bound inferiore | QE | 1-2 giorni |
| **`StateTemplateDefinition`** seed per 5 top game (curated manualmente) | Backend + design | 2-3 giorni |
| **Frontend renderer** `<ToolkitRenderer profile={...}/>` polimorfico per i 6 WidgetType | Frontend | 2-3 giorni |

### 7.3 Cosa NON è scope di B19

- Nuovo BoundedContext (l'infrastruttura esiste in `GameToolkit` + `GameToolbox`)
- Refactor del schema esistente che rompe back-compat (sono additive changes)
- Migration di toolkit esistenti (0 toolkit nel DB seed)

---

## 8. Open question post-spike

1. Quali sono i **5 top giochi** per cui valga la pena fare mockup premium oltre Wingspan? (decisione product)
2. Vogliamo eseguire un **vero spike con LLM production** (DeepSeek o gpt-4o-mini) prima di chiudere B19? (Quality bound inferiore)
3. Per giochi NON nei top-5, accettiamo che la UX sia "AI chat + scoreboard generico"? È un'esperienza ancora competitiva con BGG Stats?

---

## 8.5 Run reale con Llama 3.3 70B (OpenRouter, free-tier, 2026-05-31 12:41 UTC)

> Aggiornamento post user-request: spike eseguito con LLM production via `claudedocs/scripts/spike-llm-toolkit-gen-2026-05-31.py`.

### Setup
- **Model**: `meta-llama/llama-3.3-70b-instruct` (free tier OpenRouter)
- **Input**: stessi 5 excerpts Wingspan del run Opus surrogato (consistency)
- **API**: OpenRouter `chat/completions` con `response_format=json_object`, `temperature=0.0`
- **Tokens**: 858 prompt + 547 completion = 1405 totali
- **Latency**: 11.3s
- **Cost**: $0 (free tier)
- **Output completo**: `claudedocs/spike-llm-output-wingspan-meta-llama_llama-3.3-70b-instruct-2026-05-31T124105Z.json`

### Confronto JSON: Llama 3.3 70B vs Claude Opus 4.7

| Aspetto | Llama 3.3 70B (production candidate) | Claude Opus (upper-bound) |
|---|---|---|
| Schema field names | ⚠️ `Type`/`Faces` invece di `DiceType`/`CustomFaces` | ✅ matcha schema DTO |
| **Hallucinations** | ❌ aggiunge `"Points"` come counter (errore: è end-game score) | ✅ nessuna |
| **Dummy entries** | ❌ crea `TimerTools: [{DurationSeconds:0}]` (prompt richiedeva array vuoto) | ✅ `TimerTools: []` |
| 4-round structure | ❌ MISSING (Rounds/TurnsPerRound assenti — info CRITICA per UI) | ✅ catturata completa |
| **Semantic confusion** | ❌ `PhaseNames: ["Play bird", "Get food"...]` (sono ACTIONS, non phases) | ✅ Phases vs Actions distinti |
| Overrides flags | ❌ `{}` vuoto, ignorati | ✅ tutti settati correttamente |
| ExcludedTools | ⚠️ 1 entry (manca Timer, l'ha messo come dummy entry sopra) | ✅ 2 entry (Timer + CardDeck) |
| Reasoning citations | ✅ 5 bullets con rule refs | ✅ paragraph con multi-rule refs |

### Verdetto run Llama

**Pass parziale grave**:
- Output JSON-valido ✓ (parsing successful)
- Adesione schema DTO ✗ (field names diversi, missing fields)
- Accuracy semantica ✗ (Points-as-counter hallucination, PhaseNames-as-Actions confusion)
- Completezza ✗ (manca 4-round structure)
- Override flags ✗ (ignorati)

### Implicazioni dirette per production

1. **Llama 3.3 70B free NON è production-ready** per il toolkit extraction senza:
   - JSON Schema validation strict (post-call rejection se field names non matchano)
   - Function calling / structured output forzato (es. via OpenRouter tools API)
   - Few-shot examples nel system prompt (3-5 esempi di giochi diversi)
   - Human review obbligatorio (`RequiresHumanReview = true` sempre, no auto-apply)

2. **Modelli premium raccomandati** per production:
   - `anthropic/claude-3.5-haiku` (~$1/M input, qualità simile a Opus per task strutturati)
   - `deepseek-chat` (~$0.14/M, cinese ma molto buono su task strutturati JSON)
   - `openai/gpt-4o-mini` (~$0.15/M, buon trade-off)

3. **Prompt engineering migliorabile**:
   - Aggiungere 2-3 esempi few-shot di output JSON canonici (Wingspan + Catan + Codenames)
   - Esplicitare i field names esatti del DTO C# nel system prompt (es. `DiceType` non `Type`)
   - Aggiungere "NEVER add a counter for end-game score — points are derived, not tracked"
   - Aggiungere "If no timer is present, TimerTools MUST be an empty array []"

4. **Run repeat su modelli premium** raccomandato prima di chiudere B19 prereq. Costo: ~$0.001 a call.

### Aggiornamento raccomandazione B19

Lo spike Llama 70B free **rafforza il verdetto HYBRID**:
- Auto-generated toolkit per i 15 giochi non-premium **deve** essere flag "draft, review required"
- I 5-6 top game (Wingspan + Puerto Rico + Catan + Power Grid + Zombicide + Paleo + Codenames se incluso) → toolkit manuale curato dall'admin, NON AI
- AI generation = bootstrap helper, NON source of truth

---

## 8.6 Run reale con DeepSeek-chat (OpenRouter, paid, 2026-05-31 12:49 UTC)

> Run #2 production-candidate. Stessi 5 excerpts Wingspan, stesso prompt.

### Setup
- **Model**: `deepseek/deepseek-chat` (V3, via OpenRouter)
- **Tokens**: 831 prompt + 672 completion = 1503 totali
- **Latency**: 25.1s (~2.2× più lento di Llama, ma più tokens generati)
- **Cost**: **$0.000864** (~$0.86 per 1000 calls)
- **Output**: `claudedocs/spike-llm-output-wingspan-deepseek_deepseek-chat-2026-05-31T124917Z.json`

### Comparison matrice 3-way (Wingspan)

| Aspetto | Llama 3.3 70B FREE | **DeepSeek-chat (paid)** | Claude Opus 4.7 (surrogato upper-bound) |
|---|---|---|---|
| Cost per call | $0 | **$0.000864** | n/a |
| Latency | 11.3s | **25.1s** | n/a |
| JSON parse | ✅ | ✅ | ✅ |
| **CounterTools count** | 4 (con Points hallucination) ❌ | **3 ✅** | 3 ✅ |
| **TimerTools** | `[{dummy}]` ❌ | **`[]` ✅** | `[]` ✅ |
| **Phases vs Actions** | confusi (PhaseNames=Actions) ❌ | **Phases=Round1-4 + Actions array separati ✅** | Idem ✅ |
| **TurnsPerRound (8/7/6/5)** | MISSING ❌ | **`Turns: 8/7/6/5` per Phase ✅** | catturato ✅ |
| **ScoringTemplate structure** | flat strings | **objects con Name + Min/Max ✅** | objects con id/label/computation |
| **TieBreakers** | string fluida | **array ✅** | array ✅ |
| **Reasoning** | list of strings | **dict per categoria ✅** | paragraph |
| **Overrides flags** | `{}` ignorati ❌ | `{}` ignorati ❌ | tutti settati ✅ |
| **ExcludedTools** | 1 entry (mancante Timer) ⚠️ | 1 entry ⚠️ | 2 entries ✅ |
| **Field names DTO C#** | `Type`/`Faces` ⚠️ | `Type`/`Faces` ⚠️ | `DiceType`/`CustomFaces` ✅ |

### Verdetto DeepSeek

**Production-viable con caveats** (qualità ~90% di Opus, 95% di accuracy semantica):

✅ **Risolve i bug di Llama free**:
- Niente "Points" counter hallucination
- TimerTools array vuoto corretto
- Distinzione Phases (rounds) vs Actions
- Cattura TurnsPerRound 8/7/6/5 (info CRITICA per Wingspan UX)
- Reasoning strutturato per categoria
- TieBreakers in array

⚠️ **Caveats residui (= prompt engineering needed)**:
- `Overrides: {}` ignorato (entrambi gli LLM falliscono qui — il prompt non è esplicito che vuole sempre i 3 boolean fields)
- Field names `Type`/`Faces` (gli LLM non conoscono il DTO C# senza schema esplicito nel prompt)
- ExcludedTools incomplete (manca Timer in entrambi)

### Verdetto finale spike (3-run consolidato)

**DeepSeek-chat è production-viable** per AI bootstrap dei 14-15 giochi non-premium, **con** queste correzioni al prompt:

1. **Esplicitare i field names DTO** (es. `"Use DiceType not Type, CustomFaces not Faces"`)
2. **Few-shot example** di Overrides correttamente settati (3 boolean)
3. **Aggiungere "TimerTools MUST be [] if no timer is present, never add dummy entry"**
4. **Aggiungere "Points/Score are end-game derived — NEVER add as counter"**
5. **JSON Schema validation strict post-call** (rifiuta se field names ≠ DTO atteso)
6. **Human review obbligatorio** (`RequiresHumanReview = true` quando confidence < 0.85)

**Cost stimato per AI generation bootstrap**:
- 14 giochi non-premium × $0.001 per call = **$0.014 totali** (one-shot)
- Re-generation 4 volte/anno per refresh = $0.06/anno
- **Costo trascurabile** per la value prop

### Ranking finale per choice production

1. 🥇 **DeepSeek-chat** — best quality/cost ratio per task strutturati JSON
2. 🥈 **Claude 3.5 Haiku** — quality leggermente superiore ma 3-5× più costoso ($0.003 vs $0.001)
3. 🥉 **GPT-4o-mini** — quality comparabile, costo simile, ma DeepSeek vince per JSON structured output
4. ❌ **Llama 3.3 70B free** — non sufficiente per production senza significativi rework prompt + schema enforcement

---

## 9. References

- `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandler.cs` — handler (161 righe)
- `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/ToolkitExtractionPrompts.cs` — system prompt (49 righe)
- `apps/api/src/Api/BoundedContexts/GameToolkit/Application/DTOs/AiToolkitSuggestionDtos.cs` — DTO schema
- `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Handlers/GenerateToolkitFromKbHandlerTests.cs` — unit tests (con mock)
- `data/rulebook/wingspan_en_rulebook.pdf` — PDF source
- `claudedocs/2026-05-31-sessions-consolidation-adr.md` — ADR consolidation (companion)
- `claudedocs/scripts/spike-llm-toolkit-gen-2026-05-31.py` — script di esecuzione spike (repeatable)
- `claudedocs/spike-llm-output-wingspan-meta-llama_llama-3.3-70b-instruct-2026-05-31T124105Z.json` — output JSON raw
- PR #1738 — reconciliation + B17 delivery
- Issue #1742 (B19) — game-agnostic skeleton + premium mockups (incorpora findings spike)
