# ADR-093 — Confine IP per i parser esterni: file intero ammesso solo verso fornitori allowlistati

**Date**: 2026-10-08
**Status**: Accepted — decisione del committente presa nel brainstorming del 2026-10-08. Non è un parere legale: fissa la postura tecnica e i vincoli auto-applicati, come ADR-051.
**Related**: ADR-051 (Mechanic Extractor IP policy: «nessun terzo vede il PDF, DeepSeek/OpenRouter ricevono chunk») · ADR-059 (postura legale del catalogo) · ADR-003b (Unstructured) · `docs/for-developers/security/` (DPIA) · ricerca `docs/for-developers/research/2026-10-08-azure-ai-200-provider-switch-research.md` §5 (#6) · spec panel `2026-10-08-neuro-symbolic-architecture-spec-panel.md` §2.1.

---

## Context

La linea vigente, scritta in ADR-051 §Pro, è: **testo a pezzi sì, file intero no**. I fornitori esterni ricevono chunk di testo (estrazione meccaniche via DeepSeek/OpenRouter, traduzione via Claude Haiku), mai il PDF. Il parsing è interamente self-hosted: Unstructured, SmolDocling, Docnet, Tesseract.

Tre strumenti utili al prodotto o all'esame AI-200 richiedono invece il **file intero** o le pagine come immagini:

| Strumento | Cosa riceve | Perché interessa |
|---|---|---|
| LlamaParse (LlamaCloud) | PDF intero | parsing layout-aware con istruzioni; tier Agentic a credito (10.000 crediti/mese gratuiti) |
| Azure AI Document Intelligence | PDF intero o pagine | esercizio Azure; Layout model per tabelle |
| Vision OCR (`VisionOcrAdapter`, OpenRouter → Gemini) | pagine come immagini | già scritto nel repo, non registrato nel DI |

Senza una decisione, ogni adapter resta bloccato o, peggio, viene aggiunto senza che nessuno abbia guardato la clausola del fornitore.

## Decision

1. **Il file intero può essere inviato a un fornitore esterno solo se tutte le condizioni valgono**:
   - contratto o termini che escludono l'uso dei dati per addestramento (**no-training**) e prevedono la **cancellazione** dopo l'elaborazione;
   - elaborazione in **regione UE** quando il fornitore la offre, altrimenti motivazione scritta;
   - fornitore elencato nella **allowlist** di questo ADR (§Allowlist), con data di verifica dei termini;
   - trattamento dichiarato nella **DPIA** in `docs/for-developers/security/`.
2. **Il parsing è un passaggio tecnico, non una pubblicazione**: l'output del fornitore entra nella pipeline esistente (chunk, citazioni, review umana per i claim); nessun testo del manuale viene esposto all'utente oltre a quanto già permesso da ADR-051 (citazioni ≤ 25 parole, attribuzione visibile).
3. **Un adapter esterno è sempre uno stage opzionale** dietro `IPdfTextExtractor` keyed, acceso per configurazione e **spento per default**; la cascata self-hosted resta il percorso senza configurazione.
4. **Osservabilità**: ogni invio di file a un fornitore esterno produce un log strutturato con `pdfDocumentId`, fornitore, pagine inviate e costo stimato, e una metrica Prometheus con etichetta `provider`. Nessun invio silenzioso.
5. **Il Vision OCR** rientra nella stessa regola: Gemini via OpenRouter va verificato e allowlistato prima di registrare l'adapter, oppure l'adapter resta non registrato.

## Allowlist

| Fornitore | Uso | No-training | Cancellazione | Regione | Verificato il | Stato |
|---|---|---|---|---|---|---|
| — | — | — | — | — | — | **vuota**: nessun fornitore è ammesso finché la riga non viene compilata con i riferimenti ai termini |

Aggiungere una riga richiede: link ai termini, data, e PR che tocca anche la DPIA.

## Consequences

**Positive**: sblocca LlamaParse e Document Intelligence come esercizi e come stage opzionali; il Vision OCR ha una regola invece di un limbo; la postura resta tracciabile (allowlist + DPIA + metrica).

**Negative**: la verifica dei termini è lavoro umano per ogni fornitore; una clausola che cambia richiede di rivedere la riga; costo variabile per pagina da tenere sotto osservazione (metrica).

**Neutral**: nessun cambiamento per i fornitori che ricevono solo chunk, che restano regolati da ADR-051.

## Alternatives considered

- **Solo chunk, mai il file**: conferma ADR-051 e lascia tutto self-hosted. Esclude LlamaParse, Document Intelligence e il Vision OCR. Scartata per non chiudere gli esercizi Azure.
- **Dipende dalla licenza del manuale**: più preciso, ma richiede un campo licenza affidabile per ogni PDF e un gate in pipeline; rimandato a quando i metadati licenza (ADR-059, Wikidata) copriranno il catalogo.
- **Rimandare a consulenza legale**: coerente con la cautela originaria, ma blocca anche gli esercizi; questo ADR resta rivedibile a parere ricevuto.

## Verifica

- Gate di architettura: un `IPdfTextExtractor` che effettua chiamate HTTP verso host esterni deve essere keyed e non attivo nella configurazione di default (`appsettings.json`), pena test rosso.
- La metrica `meepleai_external_parser_pages_total{provider}` esiste e vale 0 con la configurazione di default.
