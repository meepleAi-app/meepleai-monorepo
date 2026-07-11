# Takedown Policy — Mechanic Comprehension Cards

**Owner:** MeepleAI Trust & Legal
**Parent ADR:** [ADR-051 — Mechanic Extractor IP Policy](../for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md)
**Related issues:** #528 (public card), #529 (this policy + `/legal/takedown`)
**Status:** Active

---

## 1. Scopo e ambito

MeepleAI pubblica **comprehension card** dei giochi da tavolo: sintesi delle meccaniche
**riformulate in parole originali dall'AI** a partire dal manuale, con citazione della pagina del
regolamento (ADR-051). Il testo originale del manuale resta **copyright dell'editore**.

Questa policy descrive come chiunque — in particolare editori e detentori di diritti — può
richiedere la rimozione (takedown) di una card, e come MeepleAI la gestisce internamente. Copre le
`mechanic_cards` pubblicate visibili su `/games/{id}/card`. Non copre contenuti caricati dagli
utenti (regolati dai Terms of Service) né PDF sorgente (mai ripubblicati).

## 2. Chi può presentare una richiesta

- Il detentore dei diritti d'autore sul manuale o un suo rappresentante autorizzato.
- Chiunque segnali un contenuto che ritiene violi diritti di terzi o sia inaccurato in modo lesivo.

Non è richiesto un account MeepleAI: la pagina `/legal/takedown` è **pubblica** (route group
`(public)`, non login-gated).

## 3. Canali

| Canale | Uso |
|---|---|
| Form pubblico **`/legal/takedown`** | Percorso primario; genera una richiesta strutturata precompilata. |
| Email **`takedown@meepleai.app`** | Canale diretto / destinatario del form. |

> **Nota infra (fuori scope #529):** l'alias `takedown@meepleai.app` va configurato come task
> infrastrutturale separato prima del go-live pubblico. Fino ad allora il form indirizza comunque a
> tale alias via `mailto:`.

## 4. Cosa deve contenere una richiesta

La richiesta (template del form) deve includere:

1. **Identità e contatto** del richiedente (nome, email, ruolo/rappresentanza).
2. **Opera protetta** identificata (titolo del gioco/manuale, editore, edizione).
3. **URL della card** contestata (`/games/{id}/card`).
4. **Descrizione** del problema (violazione di copyright, inaccuratezza lesiva, altro).
5. **Dichiarazione di buona fede** e di **accuratezza** delle informazioni fornite.

## 5. Processo interno

```
Richiesta (form/email)
  → Triage Trust & Legal (registrazione, verifica completezza)
    → Valutazione (fondatezza, ambito, opera identificata)
      → Azione:
          ├─ Fondata / precauzionale → SUPPRESS card
          │     (is_suppressed=true, suppressed_reason, suppressed_at/by)
          │     → card sparisce dal pubblico (404) automaticamente
          ├─ Infondata → risposta motivata, nessuna rimozione
          └─ Incompleta → richiesta di integrazione al mittente
    → Audit log + risposta al richiedente
```

**Meccanismo tecnico di rimozione.** La soppressione imposta `is_suppressed=true` sull'aggregato
`MechanicCard`. Il filtro globale EF (`HasQueryFilter(!IsSuppressed)`) e la query pubblica
`GetActiveByGameAsync` fanno sì che la card soppressa **non sia più leggibile pubblicamente** —
`GET /api/v1/games/{gameId}/card` restituisce **404** e la pagina rende `notFound()` (verificato in
#528, test `PublishedMechanicCardEndpointIntegrationTests`). Ogni soppressione è tracciata (audit
log + colonne `suppressed_*`).

**Ruoli.** Triage e decisione: Trust & Legal. Esecuzione tecnica della soppressione: admin
autorizzato (surface admin). Nessuna azione automatica di rimozione da questa policy (l'auto-
suppression da feedback utente è separata — vedi #534).

## 6. SLA

| Fase | Target |
|---|---|
| **Presa in carico** (acknowledge al richiedente) | ≤ **3 giorni lavorativi** dalla ricezione |
| **Risoluzione** (rimozione o risposta motivata) | ≤ **10 giorni lavorativi** dalla presa in carico |
| Casi di rischio evidente | Soppressione **precauzionale immediata**, valutazione a seguire |

## 7. Ripristino e controversie

Se una richiesta viene ritenuta infondata dopo una soppressione precauzionale, o l'editore ritira la
richiesta, la card può essere ripristinata ripubblicando l'analisi (nuova versione). Le controversie
sono gestite via `takedown@meepleai.app`. Rimane impregiudicato ogni diritto di legge delle parti.

## 8. Fuori scope

- Setup dell'alias email `takedown@meepleai.app` (task infrastrutturale).
- Review dei Terms of Service con IP legal counsel (gate M2, ADR-051).
- Auto-suppression da feedback utente (#534, tracciata separatamente).
