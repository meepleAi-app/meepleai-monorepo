# ADR-099 — I `.secret` cifrati nel repository con SOPS + age

**Date**: 2026-10-16
**Status**: Accepted — scelta del committente il 2026-10-16, su proposta («si» a SOPS + age dopo il confronto con un vault)
**Related**: #4427 (la sorgente di verità era staging) · #4428 (workflow di staging sospesi) · ADR-054 (strategia multi-branch)

---

## Context

Fino al 2026-10 la sorgente di verità dei segreti era **il server staging**:
- `make secrets-sync` (`infra/scripts/sync-secrets.sh`) faceva `scp` dei `*.secret` **da staging verso il locale**;
- `deploy-staging.yml` faceva `source` dei file già presenti sul server;
- nessuna pipeline li scriveva altrove.

Dalla fine di settembre 2026 il server non è raggiungibile (IP bloccato da Hetzner), e il 2026-10-15 il committente ha confermato che per ora non c'è uno staging. Ne seguono tre fatti, misurati quel giorno:

- I `.secret` della macchina di sviluppo sono **l'unica copia**. Se la macchina si perde, si perdono con lei.
- Un checkout nuovo con `make secrets-setup` riceve solo i placeholder dei `.secret.example`. E un placeholder viene letto come un valore, non come un'assenza.
- `make secrets-sync` non può più funzionare.

Il committente è l'unico operatore, insieme alle sessioni AI sulla sua macchina. Non ha un abbonamento Azure e non vuole aprirne uno adesso.

## Decision

1. **La sorgente di verità sono i file cifrati nel repository**: `infra/secrets/sops/<nome>.secret.enc`, uno per ogni `.secret`, cifrati con [SOPS](https://github.com/getsops/sops) in formato dotenv verso un destinatario [age](https://age-encryption.org). I nomi delle chiavi restano leggibili; valori e commenti sono cifrati. I destinatari stanno in `.sops.yaml`, nella radice del repository.

2. **I `.secret` in chiaro restano dove l'API e compose li cercano** (`infra/secrets/<nome>.secret`) e restano ignorati da git. `infra/scripts/secrets-sops.sh` passa da una forma all'altra:
   - `make secrets-sync` decifra;
   - `make secrets-encrypt` cifra i file cambiati;
   - `make secrets-status` mostra le differenze.

   Il vecchio `scp` resta come `make secrets-pull-staging`.

3. **Lo script è conservativo, per costruzione:**
   - `decrypt` non sovrascrive un `.secret` locale diverso dal cifrato, a meno di `--force`. Sovrascrive invece un placeholder, così `make secrets-setup && make secrets-sync` su un checkout nuovo dà i valori veri.
   - `encrypt` salta i placeholder e **non ricifra un file invariato**. SOPS genera chiave dati e IV nuovi a ogni esecuzione: ricifrare tutto renderebbe diverso ogni file a ogni giro, e il diff smetterebbe di dire quale segreto è cambiato.

4. **Nessun valore in chiaro entra nel repository.** Ci sono tre guardie:
   - il pre-commit rifiuta un `.secret` (o un `.secret.bak`) in stage, e controlla il blob in stage di ogni file sotto `infra/secrets/sops/`;
   - la CI (`dev-fast`, job «Infra Scripts») ripete lo stesso controllo, che non richiede la chiave, per chi salta gli hook;
   - `secrets-sops.bats` prova il round-trip con `sops` e `age` veri.

5. **La chiave privata age è l'unico segreto critico e non entra mai nel repository.** Sta in `%AppData%\sops\age\keys.txt` su Windows o in `~/.config/sops/age/keys.txt` su Linux, oppure dove indica `SOPS_AGE_KEY_FILE`. Il committente la conserva **in due copie**: nel password manager e offline.

6. **La CI non decifra.** Continua a usare i placeholder, come prima di questa ADR: nessuna chiave nei secret di GitHub Actions finché un workflow non ne ha bisogno.

## Alternatives considered

| | perché no, oggi |
|---|---|
| Vault gestito (Bitwarden Secrets Manager, 1Password, Infisical, Doppler) | un account esterno e una dipendenza di rete a ogni `secrets-sync`. Rotazione e revoca per persona sono il loro punto di forza, ma oggi c'è un solo operatore |
| Azure Key Vault | richiede un abbonamento Azure, che il committente non vuole aprire adesso. Resta aperto: vedi Consequences |
| Lasciare tutto com'è con un backup manuale | non risolve un checkout nuovo, e un backup a mano invecchia in silenzio |
| `git-crypt` | cifra il file intero: il diff non dice quale chiave è cambiata, e la chiave simmetrica condivisa è più difficile da revocare |

## Consequences

- **Rischio concentrato.** Se la chiave age si perde, tutti i file cifrati diventano illeggibili. Se viene esposta, sono esposti tutti i valori, anche quelli vecchi nella storia di git. In quel caso non basta cambiare chiave: vanno **ruotati i segreti**.
- **Il round-trip non conserva le righe vuote** del formato dotenv; tutto il resto torna identico (provato con sops 3.13.3). Lo script confronta i file ignorando le righe vuote.
- **Al ritorno di un server deployato** (sotto condizione, #4428): il deploy legge i segreti decifrando questi file con una chiave age presente sull'host o nei secret della pipeline, invece di affidarsi a file preesistenti sull'host. Il server smette di essere la sorgente di verità.
- **Azure resta un'aggiunta, non una migrazione.** SOPS accetta più destinatari: un giorno si può aggiungere una chiave di Azure Key Vault in `.sops.yaml` accanto ad age, senza cambiare il formato dei file.
- **Sotto condizione.** Se la segnalazione di abuso di Hetzner indicasse una compromissione del server, i valori cifrati qui erano già esposti: andrebbero ruotati e cifrati di nuovo.
