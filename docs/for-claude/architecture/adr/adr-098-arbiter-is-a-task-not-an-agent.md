# ADR-098 — L'arbitro è un compito dell'agente unico, non un agente

**Date**: 2026-10-15
**Status**: Accepted — decisioni del committente nel brainstorming del 2026-10-15 («con un agente unico, la figura dell'agente arbitro ha senso?»)
**Related**: ADR-094 (un solo agente di sistema) · ADR-095 (pipeline unica e profilo con versioni; fetta 8 → #4171) · ADR-092 (validatore Python, **emendata** da questa ADR) · #4386 (via `AskArbiterCommandHandler`) · #4238 (assistente in partita)

---

## Context

ADR-094 ha lasciato un solo agente, ma ha escluso esplicitamente dal suo perimetro `ChatThread.AgentType` («un concetto **diverso**, che questa decisione non tocca»). Nel codice, al 2026-10-15 su `origin/main-dev`, «arbitro» nomina cinque cose diverse:

| Dove | Che cosa fa | Raggiungibile dal web |
|---|---|---|
| Scheda «Arbitro» di `/sessions/[id]/live`: `ArbitroModal` → `POST /game-night/sessions/{id}/disputes` → `SubmitRuleDisputeCommandHandler` | chiama `ILlmService` con un prompt di arbitrato in italiano e ne interpreta verdetto e riferimenti; **non recupera passaggi**, quindi le pagine citate non sono verificate | sì: è l'unico |
| Disputa strutturata: `/live-sessions/{id}/disputes` (apri, rispondi, scadenza, vota, conta) e `OnDisputeOverriddenAddHouseRuleHandler` (un verdetto ribaltato diventa house rule) | il seguito sociale del verdetto | **no**: `liveSessionsClient` espone i metodi, nessun componente li chiama |
| Selettore della chat (`AgentSelector`, `chat/entry/constants.ts`: `auto`, `tutor`, `arbitro`, `stratega`, `narratore`) | salva `AgentType` sul thread; la pipeline delle risposte **non lo legge**. Il dominio (`ChatThread.SwitchAgent`) accetta `auto`, `tutor`, `arbitro`, `decisore`: `stratega` e `narratore` sono rifiutati con `ArgumentException`, `decisore` non è offerto da nessuna UI | sì, ma senza effetto |
| `ArbitroAgentEndpoints.cs`: `POST /agents/arbitro/validate` (`ValidateMoveCommandHandler` → `ArbitroAgentService` → `MoveValidationDomainService`) e `POST /agents/arbitro/feedback` | validazione di una mossa e feedback del beta | no: `git grep -n "arbitro/" apps/web/src` → nessun risultato |
| `AskArbiterCommandHandler`; Python `arbitro_agent.py` | arbitrato fra due posizioni; LangGraph con confidence fissa | no: il primo esce con #4386, il secondo non gira su staging |

La landing promette «Il tuo arbitro AI per le serate giochi da tavolo».

## Decision

1. **Nessun agente arbitro.** C'è un agente (ADR-094), con un profilo, una pipeline, un gateway e una quota (ADR-095). «Arbitro» è il nome di un **compito**: dirimere una disputa sulle regole durante una partita. Un compito non ha un'identità propria; porta una forma della domanda e una forma della risposta, versionate nel profilo come il resto.

2. **L'arbitraggio vive solo nella partita dal vivo.** È l'unico punto che ha una situazione concreta, dei giocatori e un seguito. Il compito ha:
   - **input**: la situazione e le posizioni dei giocatori in contrasto;
   - **output**: un verdetto con citazioni da passaggi **recuperati**; «il regolamento non lo decide» è un esito ammesso, non un errore;
   - **seguito**: voto dei giocatori, ribaltamento, house rule (AgentMemory).

   Passa sulla pipeline con la fetta 8 di ADR-095 (#4171), che già lo prevede («la disputa cita pagine recuperate, oggi nessun recupero»). La disputa immediata e quella strutturata convergono in un solo flusso; oggi il web usa solo la prima, e il seguito sociale va reso raggiungibile, non solo mantenuto.

3. **Il validatore deterministico di ADR-092 è uno strumento della disputa.** Quando il gioco ha claim v3 pubblicati, la disputa chiama `/validate`; il verdetto formale entra nel prompt del compito, e il modello lo spiega con citazioni senza contraddirlo. Su `Unknown`, timeout o assenza di claim, il verdetto è quello del modello sui passaggi recuperati, e la risposta lo dichiara.

4. **Il resto dell'«arbitro» esce dal codice**, con la stessa ragione di #4386 (non raggiungibile dal web, e un secondo percorso verso lo stesso verdetto): le due rotte di `ArbitroAgentEndpoints.cs` e le due admin collegate (`ArbitroAdminEndpoints.cs`, `/arbitro/beta`), `ValidateMoveCommand`/`Handler`, `SubmitValidationFeedbackCommand`, `ArbitroAgentService`, `MoveValidationDomainService`, e la tabella del feedback in expand → contract (#4403). Il Python `arbitro_agent.py` resta sostituito dal validatore puro, come già stabilito da ADR-092.

5. **Il selettore della chat diventa una scelta d'intento**, che cambia la forma della risposta e non l'agente:
   - `auto` — default, la risposta di oggi;
   - `tutor` — spiega la regola passo passo, con un esempio di gioco;
   - **risposta secca** — una riga e la citazione, pensata per il tavolo (prende il posto di `decisore`, che il dominio accetta e nessuno offre).

   `arbitro` esce (l'arbitrato è nella partita), `stratega` e `narratore` escono (consigli di gioco e ambientazione non stanno nel regolamento: una pipeline che risponde con fonti non ha niente da citare). Il dominio e il frontend hanno **lo stesso** elenco, la pipeline legge l'intento e applica la sezione del profilo corrispondente. I valori già salvati fuori elenco si leggono come `auto` fino al contract (expand → contract, ADR-094).

## Consequences

**Positive**: una sola identità anche nel linguaggio del prodotto; la promessa «arbitro AI» atterra dove c'è una disputa vera; il selettore smette di promettere un comportamento che non esiste e smette di far fallire `stratega`/`narratore`; il validatore Python ha un consumatore raggiungibile; esce codice che nessuno chiama.

**Negative**: fuori da una partita non si apre una disputa — in chat si fa una domanda; ogni intento è una sezione di prompt in più da versionare e da valutare (domande di riferimento per intento, #4170); il validatore aspetta la fetta 8 per avere il suo consumatore; il seguito sociale richiede interfaccia nuova, non solo backend.

**Neutral**: il meccanismo house rule di AgentMemory non cambia; l'assistente in partita (#4238) resta il posto dei punteggi e dei turni.

## Alternatives considered

- **Un agente arbitro con profilo proprio**: contraddice ADR-094, divide versioni, costi e valutazione.
- **Arbitrato anche in chat**: scartato. In chat mancano la situazione di partita e il seguito (voto, house rule); sarebbe un verdetto senza conseguenze.
- **Il validatore come strumento dell'assistente in partita** (`valida_mossa` accanto a `cerca_regole`): possibile più avanti; oggi creerebbe un secondo percorso verso lo stesso verdetto.
- **Togliere il selettore**: scartato. `tutor` e la risposta secca cambiano la forma in un modo utile a chi impara e a chi è al tavolo.

## Effetti su documenti e issue esistenti

- **ADR-092**: la decisione 4 (punto di innesto C#) passa alla disputa sulla pipeline; la decisione 5 (ritiro del doppione C#) si estende a tutta la rotta `/agents/arbitro/*`.
- **Piano `docs/superpowers/plans/2026-10-08-arbiter-validate-endpoint.md`**: i Task 1–3 (kernel Python, `/validate`, client C#) restano; il Task 4 è sospeso fino alla fetta 8; il Task 6 verifica tramite una disputa.
- **#4171 (fetta 8)**: la disputa porta la sezione del compito, lo strumento del validatore, la convergenza delle due dispute e l'interfaccia del seguito.
- **#4403**: via l'arbitro delle mosse e il suo beta (decisione 4), con la tabella `knowledge_base.arbitro_validation_feedback` in expand → contract.
- **#4404**: il selettore della chat a intenti (decisione 5).
- **ADR-094**: la questione `AgentType` che lasciava aperta è chiusa qui.

## Verifica

- Nessuna rotta `/agents/arbitro/*`, e `git grep -nE "ArbitroAgentService|MoveValidationDomainService|ValidateMoveCommand" apps/api` → nessun risultato.
- **Rosso prima**: la stessa domanda con intento `auto` e `tutor` produce lo stesso prompt (oggi la pipeline ignora l'intento); dopo, due prompt diversi, con la sezione del profilo come unica differenza.
- Un test fissa che l'elenco degli intenti del dominio e quello del frontend coincidono.
- Una disputa su un gioco indicizzato cita solo pagine dei passaggi recuperati; su un gioco con claim v3, il verdetto formale compare nella risposta e non è contraddetto.
