# Ondate 3–5 e sonde sistematiche

> **Contesti**: tutti quelli non ancora coperti — KnowledgeBase, SessionTracking, GameToolkit, AgentMemory, KbQuality, GameToolbox, UserNotifications, BusinessSimulations, DatabaseSync, EntityRelationships, Gamification, Testing
> **Ambiente**: locale, `make dev` · **Data**: 2026-08-26/27

## Approccio

Le prime due ondate hanno verificato a mano, un endpoint alla volta. Con **1098 righe ancora
scoperte** quel ritmo non arrivava in fondo, quindi il metodo cambia: due sonde che percorrono
sistematicamente l'inventario, e la verifica manuale riservata a ciò che le sonde segnalano.

| Sonda | Cosa fa | Livello |
|---|---|---|
| `probe-reads` | Chiama ogni lettura con **entrambi i ruoli** e confronta l'esito col ruolo atteso | L2 |
| `probe-mutations` | Chiama ogni mutazione con corpo vuoto e id inesistente | **L1** |

`probe-mutations` non dimostra che una mutazione faccia la cosa giusta — quello richiede di
costruire lo stato e verificarne l'effetto. Dimostra che l'endpoint **esiste, autorizza e valida**,
e soprattutto scova i 500. Le righe che copre restano quindi a livello L1, ed è dichiarato nel
tracker.

### Due regole di sicurezza, e perché non bastavano

1. **Id inesistente** per PUT, PATCH e DELETE: si prova il comportamento senza toccare dati reali.
   Il 404 atteso è anche un'informazione — il caso "risorsa assente" è quello che si dimentica di
   gestire.
2. **Parole vietate** nel path (`restart`, `purge`, `migrate`, `rotate`, `bulk`, `backup`…): 71
   endpoint non sono stati eseguiti e sono marcati `🚫 non eseguito (irreversibile)`.

La seconda regola **non ha coperto `DELETE /api/v1/users/me`**: nessuna parola pericolosa nel path,
effetto massimamente distruttivo. La sonda lo ha eseguito con la sessione dell'audit. È fallito con
500 prima di completare — nessun account è stato perso — ma ha **revocato la sessione**, e la regola
è stata estesa ai percorsi che agiscono sul soggetto autenticato.

## Un terzo dei risultati era da buttare

A metà della prima passata la sessione è caduta. Da quel punto ogni chiamata ha ricevuto **401**,
che il giudizio leggeva come "autorizzazione applicata", cioè conforme: **232 righe su 711** sono
state marcate verificate senza essere mai state provate.

Le 232 righe sono state riportate a `⬜ non coperto` e rieseguite con sessione fresca. Alla sonda è
stato aggiunto un controllo: cinque 401 consecutivi interrompono la passata dichiarando il motivo,
invece di produrre centinaia di falsi conformi.

## Risultati

| | Letture | Mutazioni |
|---|---|---|
| Provate | 200 | 711 |
| Conformi | 166 | 622 |
| Difformi | 34 | 30 |
| Saltate (parametro non risolvibile / sicurezza) | 83 | 71 |

### I 500 raccolti

Trenta mutazioni rispondono 500 su richiesta malformata invece di 400, 404, 415 o 422
([#3847](https://github.com/meepleAi-app/meepleai-monorepo/issues/3847)). Le cause accertate su un
campione ricadono tutte nello stesso schema: **il validatore esplode invece di segnalare**.

| Endpoint | Dove |
|---|---|
| `POST /api/v1/game-sessions` | `CreateSessionCommandValidator:20-25` — `.Must(p => p.Count <= 20)` su `Participants` **senza `.NotNull()`** ([#3849](https://github.com/meepleAi-app/meepleai-monorepo/issues/3849)) |
| `POST /api/v1/private-games` | `AddPrivateGameCommandValidator` |
| `GET /api/v1/admin/kb/pipeline/health` | `ValidationException` non mappata: 500 invece di 400 |

Fra le letture, i 500 si riconducono a quattro famiglie già tracciate (#3839, #3843, #3845, #3833).

## Interfaccia: dove le chiamate API non arrivano

Il crawler apre le pagine; queste verifiche **agiscono**. Sei casi provati, uno solo difforme — ma
è un difetto che nessuna chiamata API avrebbe trovato.

### 🔍 Il campo di ricerca di `/games` non si può usare — [#3848](https://github.com/meepleAi-app/meepleai-monorepo/issues/3848)

```
placeholder:   "Cerca giochi, agenti, toolkit…"
readOnly:      true
aria-disabled: "true"
```

Visibile, con un placeholder che invita a cercare, e non accetta testo; il click va in timeout. La
pagina si carica senza errori, senza richieste fallite, con la console pulita: dalla navigazione
sembra sana.

### Tre segnalazioni che erano difetti dei miei criteri

| Segnalazione iniziale | Realtà |
|---|---|
| `/library` non mostra voci né stato vuoto | Mostra intestazione, contatori (`0 Giochi totali`) e "+ Aggiungi gioco". Lo stato vuoto è comunicato dai **numeri**, non da una frase |
| `/profile` non ha un pulsante di salvataggio | Ha "Modifica" e le schede Panoramica/Impostazioni: il salvataggio sta lì dentro |
| `/library` letta da `<main>` risulta priva di titolo | Il titolo e l'azione principale stanno **fuori** dal landmark: leggere solo `<main>` bocciava una pagina integra |

Tre falsi positivi su sei casi. I criteri automatici sull'interfaccia sono fragili: hanno bisogno
di essere calibrati su ciò che la pagina fa davvero, non su ciò che ci si aspetta di trovarci.

L'ultima riga resta però un'osservazione di accessibilità: chi usa lo skip-link per saltare a
`main` non raggiunge il titolo della pagina.

## Copertura raggiunta

| Stato | Righe |
|---|---|
| ✅ verificato | 1322 |
| ⚠️ finding | 192 |
| 🚫 non eseguito (irreversibile) | 71 |
| ⬜ non coperto | 140 |
| **Totale** | **1725** |

Le 140 residue richiedono entità che l'ambiente non ha (collezioni di documenti, campagne, job di
coda) o parametri che nessun dato di prova produce.
