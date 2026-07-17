# SP7 — Spec-Panel Review + Invariant Diff (issue #1889, half achievable)

**Data**: 2026-07-15
**Metodo**: spec-panel critique statico (NO claude.ai/design web demo — vedi § "Cosa resta bloccato")
**Panel virtuale**: Wiegers (requirements), Adzic (specification-by-example), Cockburn (use-case/actor), Fowler (interface/architecture), Nygard (failure-mode/operational), Crispin (testing), Doumont (clarity)
**Target 1 — mockup game-night consegnati**: A `sp7-game-night-new`, B `sp7-game-night-detail-rsvp`, K `sp7-game-night-live`, L `sp7-game-night-transition`, M `sp7-game-night-summary`
**Target 2 — brief agent-builder (mockup NON ancora autorati)**: sez. D–H di `admin-mockups/briefs/SP7-game-night-agent-builder.md`
**Invarianti di riferimento**: [`docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`](../specs/2026-06-04-gamenight-session-domain-model.md) (#10/#15/#16/#17/#19)
**Issue**: [#1889](https://github.com/meepleAi-app/meepleai-monorepo/issues/1889) (OPEN, `deferred`)

---

## § 0 — Contesto e cosa copre questo documento

L'issue #1889 chiede di **rifare il demo Claude Design post-SP7** per validare il flow game-night + agent-builder contro le invarianti #10/#15/#16/#17/#19. L'issue è **`deferred`** per due blocchi reali (commento 2026-07-14):

1. **Mockup agent-builder mancanti** — dei 13 mockup SP7 del brief, i 5 della superficie agent (D `agent-proposals-list`, E `agent-builder-create`, F `agent-builder-test`, G `agent-builder-edit`, H `library-game-agent`) **non esistono** in `admin-mockups/design_files/`. Esiste solo il brief.
2. **Il replay demo non è automatizzabile** — richiede l'interfaccia web manuale `claude.ai/design` (upload bundle → chat interattiva → export handoff).

Questo documento **non è il gap report del demo** (che resta bloccato). È una **spec-panel review statica** che copre — senza il tool web — **2 dei 5 acceptance criteria** di #1889:

- ✅ **Diff specifico contro invarianti #10/#15/#16/#17/#19** → § 1 (sui mockup game-night consegnati).
- ✅ **Candidati nuovi invarianti agent-builder per il domain model** → § 3.
- ⏳ **Rebuild bundle + replay demo + gap report web** → resta bloccato (§ 5).

Metodologia: 5 agenti di analisi hanno letto i JSX dei mockup game-night ed estratto evidenze riga-per-riga contro le invarianti; la critique del brief agent-builder applica le lenti spec-panel al testo del brief.

---

## § 1 — Parte A · Diff invarianti sui mockup game-night consegnati

### 1.1 Matrice verdetti

| Invariante | A `new` | B `detail-rsvp` | K `live` | L `transition` | M `summary` |
|---|---|---|---|---|---|
| **#10** max 1 live | n/a (no toggle) | ⚠️ **GAP** | ✅ COMPLIANT | ✅ COMPLIANT | — |
| **#15** planned→in-progress trigger | ○ non-addressed (no contraddizioni) | — | — | — | — |
| **#16** tagged vs invited | ✅ mockup / ❌ **brief diverge** | 🔴 **VIOLATION/NOT-ADDRESSED** | — | — | — |
| **#17** invited pending fino a RSVP | — | ✅ COMPLIANT (1 riserva) | — | — | — |
| **#19** no parallel-live | — | — | ✅ COMPLIANT | ✅ COMPLIANT | ✅ COMPLIANT |
| #7/#9 cardinalità/naming | — | — | — | — | ✅ COMPLIANT (drift lessicale) |

**Sintesi**: le invarianti "sequenziali" del live (#10/#19) sono **solide** nei mockup live/transition/summary (K/L/M). Il buco reale è sul **contratto tagged→invited (#16)**, che nel detail page consegnato (B) **non è realizzato** — ed è una regressione rispetto a una decisione di dominio già presa.

### 1.2 🔴 FINDING CRITICO — #16 tagged vs invited non realizzato nel flow consegnato

**Cosa dice l'invariante** (#16 + #17, risoluzione Tensione 2 del domain model, 2026-06-04): la GameNight ha 2 stati di popolazione player distinti — **tagged** (aggiunti nel wizard, *nessuna notifica*) e **invited** (dopo un'azione **esplicita e separata "Invia inviti"** dal detail page, notifica spedita). Il tagging in creazione è **silente**.

**Cosa fa il mockup consegnato**:
- **A `new`** è *conforme* al lato creazione: CTA submit = `✓ Crea serata` (riga 1582, non "Crea e invia inviti"), RSVP card marcata `Bozza`, bottoni RSVP disabled con copy *"I bottoni saranno attivi dopo la creazione"* (r.1340). Il wizard **tagga, non invita**. ✅
- **B `detail-rsvp`** — **manca completamente la CTA "Invia inviti"**. Le sole azioni host sono `✏️ Modifica serata` + `✕ Cancella` (r.439-453) + tie-resolver + `🎯 Avvia sessione live`. Il modello dati `RSVP_ICON` (r.305-310) ha solo `yes/maybe/no/pending` — **non esiste lo stato "tagged"**. Lo stato `pending` è reso come *"Notifica inviata · in attesa"* (r.364), cioè **già-invitato**. Il mockup **collassa "tagged" e "invited-pending" in un unico stato che presuppone la notifica già spedita** — esattamente la distinzione che #16 impone di mantenere separata. 🔴

**Perché è grave**: il gap report 2026-06-04 aveva già flaggato "Tagging vs RSVP" (gap #4, severity high) → risolto introducendo #16/#17. Il detail mockup SP7 **non ha implementato la risoluzione**: l'azione esplicita "Invia inviti" (il gate tagged→invited) non ha superficie in nessuno dei mockup consegnati. La 2ª metà del contratto è invisibile. Un implementatore FE che parte da B costruirebbe un modello a-invito-implicito, ri-aprendo il gap chiuso.

**Divergenza brief↔mockup su A** (da correggere a monte): il **brief** prescrive per A la CTA *"Crea e invia inviti"* + toast *"Invitato 6 giocatori, attendi RSVP"* (righe 252-253 del brief). Questa formulazione **violerebbe #16** (invio inviti al submit del wizard). Il mockup ha scelto la variante conforme (`Crea serata`); **il brief è la fonte sbagliata** e va allineato.

**Tensione laterale su A** (`Auto-RSVP regulars`, r.872-873): il copy *"Giulia, Luca, Sara, Davide vengono confermati automaticamente"* implica una **conferma RSVP automatica al tagging**, in tensione con lo spirito di #16 (tag silente, nessun side-effect di stato). Da validare col domain owner: una conferma automatica implica RSVP=confermato senza l'azione "Invia inviti".

### 1.3 ⚠️ FINDING — #10 max-1-live senza rappresentazione UI del gate

Le invarianti #10/#19 sono **compliant per costruzione** in K/L (fixture con un solo game `inprogress`; transition che termina-prima-di-avviare; nessun affordance parallelo; "+Aggiungi gioco" disabled in live). **Ma il *gate* non ha mai una rappresentazione UI**:

- **B state-02** — CTA `🎯 Avvia sessione live` è **incondizionata** (r.1111-1118): nessun `disabled`, nessun controllo "una live è già in corso", nessuna menzione del vincolo. GAP.
- **L** — il CTA "Avvia prossima session" **assume** che il game precedente sia già finalizzato (winner banner + score top-3 come evidenza) ma **non dichiara il precondition-check**. Se in backend la session precedente non fosse `Finalized` al click, #10 verrebbe violata. È un rischio di *contratto backend*, non una violazione del mockup.
- **K** — single-live enforced *by fixture shape*, non difensivamente: la UI non mostra cosa succede se il backend rifiuta un 2º start (`MaxLiveSessionsExceededException` HTTP 409, già in dominio).

Nessun mockup mostra lo **stato disabled/errore** del vincolo max-1-live. L'invariante esiste in dominio ma è **UI-invisibile** lungo tutto il flow.

### 1.4 Note minori (spec-quality, non-invarianti)

- **B eccede lo scope P1 dichiarato**: il commento HTML dice tab Voting/Chat + mobile sticky "deferred to follow-up" (#951), ma il mockup li implementa completi. Brief/mockup divergono su cosa fosse in-scope.
- **B counter "6/8 confermati"** (r.1281) hardcoded incoerente con lo scenario `host-pending` (3 `yes`). Baco di fedeltà.
- **M drift lessicale** (#9): stesso schermo alterna "Sessioni totali" (KPI r.342) e "giochi/game completati" (r.343, r.857) per la stessa entità partita-interna. Da armonizzare la convenzione UI-facing.
- **M regola-MVP** documentata solo in commento (r.24 "1 win + most events"), non derivata dai fixture (`MVP` hardcoded r.122) → rischio divergenza BE/FE sulla formula.
- **L** manca lo stato "0 upcoming games" (ultimo game della serata → unica azione sensata "Termina serata").
- **#17 riserva**: il trattamento "GameNight card semitrasparente/pending" lato invitato **non è reso in B** (la semitrasparenza qui è solo sulle `RSVPRow` no/pending). Plausibilmente vive nella list view (`sp4-game-nights-index`) — confine di scope, non difetto, ma non-coperto qui.

---

## § 2 — Parte B · Critique spec-panel del brief agent-builder (D–H)

I 5 mockup agent-builder **non esistono ancora**. Questa sezione revisiona il **brief** (la fonte che li genererà) per evitare che ambiguità vengano "cotte" nei mockup. AB-11 (riconciliazione backend) è il finding fondazionale e apre la sezione; seguono le ambiguità di spec AB-1..AB-10.

### AB-11 🔴 CRITICAL — "backend già pronto" è fuorviante: entità/route/campi non corrispondono (Wiegers-traceability/Nygard)
Verifica del codebase (`KnowledgeBase` BC) contro il claim del brief (righe 26-28) *"backend già pronto ... `AgentDefinition` + `AgentProposal` flow"*:
- **`AgentProposal` NON esiste** — il termine compare **solo nel brief** (grep repo-wide: 1 match). Il backend reale è `AgentDefinition` (`Domain/Entities/AgentDefinition.cs:19`), aggregato con nomenclatura e semantica diverse.
- **Route diverse e admin-gated**: endpoint reali = `/admin/agent-definitions` protetti da `RequireAdminSessionFilter` (**admin-only**), NON `/editor/agent-proposals*` del brief (r.67-71). → **contraddice direttamente la persona** "Marco power-user regolare crea agenti al `/editor/`": oggi **solo gli admin** possono costruire agenti (collega AB-4).
- **State machine parziale**: enum `AgentDefinitionStatus` = `{Draft=0, Testing=1, Published=2}` — **manca `Archived`**, che il brief usa come status badge + filter tab in D (r.143/557/568). Guard reali esistono e maturi (`StartTesting()`, `Publish()` blocca Draft→Published diretto, `Unpublish()`, `SoftDelete()/Restore()`).
- **Confidence threshold + tone preset + system prompt strutturato: ASSENTI**. `AgentDefinitionConfig` persiste solo `Model/MaxTokens/Temperature`; `CreateAgentDefinitionCommand` non ha `ConfidenceThreshold/Tone/SystemPrompt` (solo `Prompts` generico JSONB). → E-Step 3 (r.619-633, tone picker + confidence slider) e G33.5 speccano capability **inesistenti a backend**.
- **Version history / rollback: ASSENTI**. → G (r.757-764) + G33.9 senza alcun supporto.
- **Pienamente supportato**: solo KB+Game linking (`KbCardIds` + `GameId`), CQRS core (Create/Update/Delete/Publish/Unpublish/StartTesting + query GetAll/Search/Stats), testing via `PlaygroundChatCommand` separato.

**Verdetto**: esiste un backend `AgentDefinition` maturo che copre il **cuore** del builder, ma "backend già pronto" per lo scope dei mockup D–G è **falso su 4 punti** (naming, route/authz, `Archived`, confidence/tone/version). **Decisione che precede tutto il resto**: allineare i mockup al backend esistente (`/admin/agent-definitions`, admin-only, no Archived/version/confidence-field) **oppure** estendere il backend allo scope del brief. Non si autorano i mockup D–H prima di questa scelta.

### AB-1 🟠 HIGH — State machine dell'Agent Proposal non definita nel brief (Wiegers/Fowler)
Il brief nomina 4 status (Published/Draft/Testing/Archived — righe 142-143, 557) ma **non definisce transizioni legali né trigger** (mentre il backend ne ha una parziale, vedi AB-11):
- **"Testing" è uno status persistito o uno stato UI effimero?** D filtra `[Bozze][In test]` come tab separati (r.557 → implica persistito); il backend lo conferma persistito (`Testing=1`), ma il brief non dice *quando* avviene Draft→Testing (apertura playground? prima query?).
- E toggle "Pubblica subito" → Draft o Published; F "Pubblica agente" → Draft→Published (r.709). Ma il backend **blocca il publish diretto da Draft** (`Publish()` richiede passaggio da Testing) → il brief E state-08 ("Pubblica subito" da wizard) **è in conflitto col backend**. **Published→Testing** e **Archived→Draft** non definiti (e `Archived` non esiste a backend).
- **Impatto**: senza allineamento i tab-filtro di D mostrano bucket incoerenti e il wizard E promette una transizione (Draft→Published diretta) che il dominio rifiuta.

### AB-2 🔴 CRITICAL — Semantica della confidence threshold ambigua + campo assente a backend (Wiegers/Doumont)
E step 3 (r.631) definisce threshold 0.5→0.9 *"Quando rispondere?"* — framing da **GATE**. Ma F state-04 (confidence 0.45 → "Non sono sicuro" + suggerimento, **risponde comunque**) e H state-03 (0.4 → "Non sono certo, controlla pag. X", **risponde comunque**) la trattano come **tier di disclaimer visualizzato**, non gate. **Contraddizione**: sotto-soglia l'agente **rifiuta** o **solo disclaima**? Indefinito → implementazioni inconsistenti. Serve UNA definizione deterministica. **Aggravante backend** (AB-11): `AgentDefinitionConfig` non persiste alcun `ConfidenceThreshold` → il campo va comunque aggiunto al dominio prima che la UI del slider abbia senso.

### AB-3 🟠 HIGH — Cardinalità Agent↔Game + selezione inline non definite (Cockburn/Fowler)
D ammette più agenti *"Linked to: Twilight Imperium"* (N agenti per game). H route `/library/games/[gameId]/agent` è **singolare** e state-08 dice *"Nessun agente per Twilight Imperium"* (assunzione 0-o-1). **Quale agente mostra H quando ne esistono >1?** Logica di selezione indefinita. Inoltre: gli agenti Draft/Testing sono mai esposti inline, o solo i Published?

### AB-4 🟠 HIGH — Modello ownership & visibilità non definito, e in conflitto col backend admin-only (Cockburn/Nygard-authz)
D persona (r.546): *"Marco (sue bozze) o Aaron (superadmin vede tutte)"* → implica ownership per-utente + visibilità admin-all. Ma: **chi può pubblicare?** Un agente pubblicato è privato dell'autore o **visibile a tutti gli utenti di quel game** (via H inline)? **Conflitto verificato** (AB-11): il backend reale è **admin-only** (`RequireAdminSessionFilter` su `/admin/agent-definitions`) — oggi Marco (power-user regolare) **non può** creare agenti. La premessa persona dell'intera wave agent-builder (Marco autore al `/editor/`) **non è supportata**. **Personal-agent vs shared/community-agent + soglia di ruolo** è una decisione di dominio mancante che ridefinisce chi sono i mockup D–H (admin-tool vs user-tool).

### AB-5 🟠 HIGH — Publish gate: pubblicazione untested/0-KB ammessa in modo incoerente (Wiegers/Crispin)
E ammette *"Pubblica subito"* in creazione (state-08) — publish **senza alcun test** e con **0 KB** (state-03 avvisa ma non blocca, r.616). Eppure l'intero playground F esiste per validare *prima* di pubblicare. Tensione: **esiste un precondition di qualità per Published** (≥1 test? KB non-vuoto?)? Indefinito. Rischio: utenti pubblicano agenti rotti direttamente dal wizard.

### AB-6 🟠 HIGH — Versioning & rollback non definiti e non supportati a backend (Fowler)
G mostra "Last 5 versions" + score per-versione (*"v3 testato 12 volte, score 4.2/5"*) + rollback + "Salva sezione" granulare. Indefinito: ogni section-save = nuova versione o solo bump espliciti? Il rollback **crea una nuova versione** o muta la storia? Il rollback di un Published re-triggera publish/review? **Verificato** (AB-11): il backend **non ha alcun version history / rollback** → G e G33.9 speccano una feature intera non-esistente. Severità alzata a HIGH: non è solo ambiguità di spec, è capability mancante da costruire (o mockup G da tagliare).

### AB-7 🟡 MEDIUM — Precondition di readiness KB non specificato (Nygard-failure-mode)
E step 2 linka KB *"filtered by linked game"* con link "Indicizza nuovo KB". Ma: si può linkare un KB **ancora in indicizzazione** (non ready)? Cosa succede se un KB linkato **poi fallisce** l'indicizzazione? Nessuno stato copre KB not-ready/failed al momento del link. F/H non coprono il fallimento LLM-provider durante test/chat (solo H state-07 = network).

### AB-8 🟡 MEDIUM — Conflazione terminologica "training" vs "indexing" (Doumont)
Gli agenti sono **RAG su KB — non si "addestrano"**. Eppure la notifica I *"agent training failed"* (r.153) e J *"Agent events (training complete...)"* (r.908) usano vocabolario di training. I sample notifica implicano anche un **catalogo eventi agente** (publish, index-complete, error, usage-summary) che D–G **non definiscono mai come emessi**. Gap di coupling cross-superficie.

### AB-9 🟡 MEDIUM — Scenari Gherkin citati ma non specificati (Adzic/Wiegers-testability)
Il brief tagga G33.1–G33.10, G13.1, G13.5 come "coverage" ma **non fornisce mai i corpi Given/When/Then**. **Verificato**: **nessun file `.feature`** esiste nel repo; i tag `G33.*`/`G31.*` compaiono solo come stringhe di prosa (e come label decorative `US-31.N` nei JSX). Il coverage-map (`2026-06-14-mockup-us-coverage-map.md`) li classifica esplicitamente *"EMBED-LOCAL inline JSX state-variant labels, sample non esaustivo"* e dichiara *"le US non vivono nel codice, ZERO US identifier nei test"*. Esistono GWT reali ma a granularità **US-N** (non mappati 1:1 ai tag `G33.N`). Le coverage-claim del brief sono quindi **non falsificabili**. Critica-Adzic: *"citi l'ID dell'esempio ma dov'è l'esempio eseguibile?"*

### AB-10 🟢 LOW — Oscillazione persona/route authz (Cockburn)
Prefisso `/editor/` (D/E/F/G) implica ruolo editor; H `/library/` implica per-utente. Il brief fa creare sia Marco che Aaron. Il gating di ruolo per la creazione agenti è non dichiarato.

### Punti di forza (review bilanciata)
- **DRY forte**: riuso 100% dei primitivi esistenti con tabella esplicita (righe 87-98) — nessun clone.
- Mapping entity-color esplicito, **nessun nuovo entity type** introdotto.
- **Igiene sicurezza**: vincolo dati "no token secret-like" (r.159-161) + gate grep FREEZE (r.195-201).
- Decomposizione wizard 4-step pulita; buona empatia negli empty-state (r.995).

---

## § 3 — Candidati nuovi invarianti agent-builder (per il domain model)

Da consolidare in una sessione socratic di dominio **prima** di autorare i mockup D–H (altrimenti i mockup bakeranno le ambiguità AB-1..AB-10), e **dopo** la riconciliazione brief↔backend (AB-11), che stabilisce il perimetro reale (admin-tool vs user-tool, quali capability esistono). Formato coerente con `2026-06-04-gamenight-session-domain-model.md`.

| # | Invariante candidato | Deriva da | Decisione richiesta |
|---|---|---|---|
| **I-AB-1** | State machine Agent Proposal: `Draft → Testing → Published → Archived` con transizioni legali + trigger espliciti | AB-1 | Testing è persistito? Published→Testing su re-test? Archived→Draft revive? |
| **I-AB-2** | Confidence threshold governa **tier-di-disclaimer** XOR **gate answer/refuse** — una semantica deterministica | AB-2 | Sotto-soglia: rifiuta o disclaima? |
| **I-AB-3** | Cardinalità Agent↔Game + regola di surfacing inline (quale singolo agente mostra H; solo Published inline) | AB-3 | 1 game → N agenti? Selezione di default per H? |
| **I-AB-4** | Ownership & visibilità agente (personal vs shared/community; chi pubblica; chi vede il pubblicato; gating editor/admin) | AB-4 | Agente pubblicato = privato o community per-game? |
| **I-AB-5** | Precondition di publish (test/KB richiesti, o decisione esplicita "nessun gate") | AB-5 | Publish richiede ≥1 test / KB non-vuoto? |
| **I-AB-6** | Semantica versioning (cosa = una versione; rollback = nuova-versione; comportamento rollback su Published; binding score↔versione) | AB-6 | Section-save = versione? Rollback muta o appende? |
| **I-AB-7** | Precondition readiness KB (solo KB `Ready` linkabili; gestione KB-linkato-poi-fallito) | AB-7 | KB in-indexing linkabile? |
| **I-AB-8** | Catalogo eventi lifecycle agente (quali emettono notifica) + fix terminologia (indexing, non training) | AB-8 | Publish/index-complete/error/usage emettono notifica? |

---

## § 4 — Tabella gap consolidata (stile gap report)

| # | categoria | superficie | descrizione | severity | proposta fix |
|---|---|---|---|---|---|
| 1 | ENTITY | B `detail-rsvp` | #16 tagged vs invited: manca CTA "Invia inviti"; `pending` = "già invitato", stato "tagged" assente | **high** | aggiungere azione esplicita "Invia inviti" + distinguere `tagged`(silente) da `invited/pending` nella lista RSVP |
| 2 | ENTITY | A `new` (brief) | Brief prescrive "Crea e invia inviti" + toast invito → violerebbe #16 | **high** | allineare il brief al mockup conforme (`Crea serata`, nessun invio al submit) |
| 3 | STATE | B `detail-rsvp` | #10 CTA "Avvia sessione live" incondizionata, nessun gate/disabled max-1-live | med | stato disabled/errore quando una live è già attiva (409) |
| 4 | STATE | L `transition` | #10 precondition di chiusura del game precedente non dichiarato nel CTA | med | esplicitare che la transition è raggiungibile solo con precedente `finalized` |
| 5 | ENTITY | A `new` | `Auto-RSVP regulars` implica conferma RSVP automatica al tagging (tensione #16) | med | chiarire se il side-effect di stato è voluto o solo pre-check UI |
| 6 | STATE | L `transition` | manca edge-case "0 upcoming games" (ultimo game) | low | stato "era l'ultimo → Termina serata" |
| 7 | TOKEN/COPY | M `summary` | drift lessicale "Session" ↔ "gioco/game" nello stesso schermo (#9) | low | fissare convenzione UI-facing |
| 8 | ENTITY | brief D–H vs backend | agent-builder: "backend già pronto" **falso su 4 punti** — `AgentProposal` inesistente (è `AgentDefinition`), route `/admin/*` admin-only (non `/editor/*`), no `Archived`, no confidence/tone/version (AB-11) | **high** | decidere: allineare mockup al backend esistente **o** estendere backend, PRIMA di autorare D–H |
| 9 | ENTITY | brief D–H | agent-builder: 10 ambiguità di dominio (AB-1..10) → §3 | **high** | socratic di dominio + 8 invarianti candidati prima di autorare i mockup |
| 10 | STATE | brief D–H | Gherkin G33.* citati senza corpo GWT, nessun `.feature` nel repo (non falsificabili) | med | scrivere gli scenari GWT o rimuovere le coverage-claim |

---

## § 5 — Follow-up raccomandati + cosa resta bloccato

### Azionabile subito (sblocca #1889 senza il tool web)
1. **[high] Fix #16 nel flow game-night**: aprire follow-up per aggiungere la CTA "Invia inviti" al detail page B + i due stati distinti `tagged`/`invited-pending`. È la risoluzione (già decisa 2026-06-04) mai arrivata in UI.
2. **[high] Correggere il brief su A**: allineare copy CTA/toast alla variante conforme #16.
3. **[high] Riconciliazione brief↔backend agent-builder** (AB-11): decidere se allineare i mockup D–H al backend `AgentDefinition` esistente (`/admin/agent-definitions`, admin-only, `{Draft/Testing/Published}`, no version/confidence-field) **oppure** estendere il backend allo scope del brief (`Archived`, version/rollback, confidence threshold, route `/editor/*` user-facing). **Questa decisione precede tutto** — cambia persino se l'agent-builder è admin-tool o user-tool.
4. **[high] Domain socratic agent-builder**: consolidare gli 8 invarianti candidati (§3) **dopo** la riconciliazione backend. Insieme, (3)+(4) sono **il vero unblock path** di #1889: senza queste decisioni i 5 mockup mancanti nascerebbero ambigui e disallineati dal dominio reale.
5. **[med] UI del gate max-1-live**: aggiungere lo stato disabled/errore (409) dove oggi il CTA "Avvia sessione live" è incondizionato.

### Resta bloccato (richiede tool web `claude.ai/design`, non automatizzabile)
- Rebuild bundle full-SP7 + replay demo interattivo + export handoff → gap report web (`docs/for-developers/audits/<data>-claude-design-gap-report-sp7.md`).
- Precondizione di sblocco reale (da commento #1889 + questo doc): **prima** autorare i 5 mockup agent-builder mancanti (D–H), che a loro volta richiedono la risoluzione degli 8 invarianti candidati (§3).

### Disposizione #1889
**Concludibile** (aggiornato 2026-07-15 dopo verifica gap residui — vedi §6). AC3/AC4/AC5 DONE, AC1 MOOT, AC2 metà (gap report statico ✅, replay web scorporato in chore non-bloccante #2980). L'intera wave agent-builder D–H è riconciliata al runtime shipped (ADR-085 + PR #2973). Residui tracciati: **#2978** (#17 pending-card, materiale), **#2979** (cleanup low).

---

## § 6 — Addendum: verifica gap residui (2026-07-15)

Verifica avversariale post-audit (3 verificatori paralleli) sui gap che il diff §1 aveva saltato o rimandato.

### 6.1 `sp7-game-night-join-public` vs #16/#17 — ✅ COMPLIANT (non era in Target-1)
Il 6° mockup game-night (RSVP pubblico anonimo via token/QR, `/join/event/[code]`, #1169) — mai incluso in Target-1 (A/B/K/L/M) — diffato ora: **conforme** a #16 e #17.
- **#16** COMPLIANT: il token-invitation esiste solo se un organizer lo conia esplicitamente (`CreateGameNightInvitationByEmailCommandHandler.cs:68-72` guard); la route pubblica è puramente lato-destinatario (`PublicJoinEventView.tsx:66-114`, GET + POST respond, nessun auto-invito). Mappa domain model :279/:290 (token = "invited", mai "tagged").
- **#17** COMPLIANT: `Pending` default (`GameNightInvitationStatus.cs:9-10`, `.Create:130`); pannello conferma solo dopo `alreadyRespondedAs` (`PublicJoinEventView.tsx:106,332-350`); transizione pending→confermato in `GameNightInvitation.Respond:280-305`.
- **Error-states** coperti: 410 expired/cancelled (`RespondToGameNightInvitationByTokenCommandHandler.cs:50-61`), 429 rate-limit (`GameNightEndpoints.cs:162/173` + FE countdown `PublicJoinEventView.tsx:187-207`).

### 6.2 Invariante #17 card pending lato invitato — 🔴 REAL_GAP → #2978
Il trattamento card pending dell'invitato (badge "Da confermare" + semitrasparenza + Conferma/Declina inline) in dashboard "Prossimi" e list `/game-nights` **non esiste**: solo contatori aggregati host-side (`ProssimiSection.tsx:37-39`; `GameNightListCard.tsx:168-196` gated su role, non su viewer-RSVP), e `GameNightDto` non porta `myRsvpStatus` (`game-nights.schemas.ts:33-50`). L'RSVP inline esiste solo sulla detail page. Unico gap **funzionale** materiale → **#2978** (BE DTO + FE dashboard/list + test).

### 6.3 Notifiche I/J vs #16 — ⚠️ runtime corretto, gap solo doc/cleanup → #2979
Runtime cablato end-to-end (`GameNightPublishedNotificationHandler` → `NotificationType.GameNightInvitation` deep-link → `GameNightDetailView` RsvpActionBar). I mockup I/J sono demo statici non nel diff + un dead-entry `game_night_published` FE/BE. Nessun gap funzionale → **#2979**.

### 6.4 Note
- **Mockup stale**: `sp7-game-night-detail-rsvp.jsx` intenzionalmente stale vs il fix #16 runtime (annotato nel file header); re-sync tracciato in #2979.
- **Tensione Auto-RSVP** (§4 row-5): domanda di dominio aperta → #2979.

---

## § 7 — Risoluzione #2979 (2026-07-17)

Chiusura dei 4 residui di #2979. Verifica dello stato reale del repo (i mockup referenziati sono cambiati dopo il 2026-07-15):

1. **Notifiche I/J vs #16/#17** (doc) — già coperto da §6.3: runtime cablato end-to-end, i mockup `notifications.jsx`/preferences sono demo statici non collegati. Nessuna azione di codice; annotazione qui è sufficiente.
2. **Dead-entry `game_night_published`** (contract cleanup) — ✅ **RISOLTO**. Confermato che il BE (`NotificationType.cs`) definisce solo 4 tipi game-night (`invitation`, `rsvp_received`, `reminder`, `cancelled`) e **non emette mai** `game_night_published`. Rimosso dai 3 punti FE (`notifications.schemas.ts` `KNOWN_NOTIFICATION_TYPES`, `NotificationItem.tsx` `getTypeIcon`, `notifications/page.tsx` filtro `events`). Nessun test lo referenziava.
3. **Tensione Auto-RSVP regulars** (domain question) — ⚪ **MOOT**. Il mockup `sp7-game-night-new.jsx` che conteneva il toggle "Auto-RSVP per i regular" è stato **eliminato** in `90f731b4f` (DS-17-16, PR #2988, "remove migrated page-mocks"). La stringa "Auto-RSVP"/"confermati automaticamente" **non esiste più in alcun artefatto live** (né runtime, né story/fixture migrate, né brief SP9). Nessuna superficie da correggere; l'invariante #16 (tag silente) resta la legge. Se una futura wave mobile game-night (SP9) reintroduce un affordance "auto-conferma al tag", deve onorare #16 (pre-check UI di selezione invito, **non** RSVP=confermato al tagging).
4. **Re-sync `sp7-game-night-detail-rsvp.jsx`** (mockup) — ⚪ **MOOT**. Mockup **eliminato** nello stesso commit #2988 e migrato a story (`game-nights/[id]/game-night-detail-rsvp.stories.tsx`) che renderizza il componente runtime reale `GameNightDetailView` — il quale **ha già la CTA "Invia inviti"** (#16, PR #2969, con test `GameNightDetailView.inviteCta.test.tsx`). La story è in-sync per costruzione col runtime; il gap chiuso non può ri-emergere da un mockup statico che non esiste più.

**Esito**: 1 fix di codice (item 2) + 3 item chiusi come già-coperti/moot. Issue #2979 chiudibile.

---

*Prodotto da spec-panel critique statico (Opus 4.8) — 2026-07-15, addendum §6 stessa data, §7 risoluzione 2026-07-17.*
