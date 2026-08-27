# Ripresa — prompt per una nuova sessione

Copia il blocco qui sotto come primo messaggio. Contiene tutto il necessario senza rileggere le
conversazioni precedenti.

> La fase di **ricognizione** è chiusa (95% di copertura, 26 issue aperte): è documentata in
> [README.md](./README.md). Questo prompt serve a proseguire la fase di **correzione**:
> 20 issue chiuse e mergiate, 8 aperte.

---

```
Prosegui la correzione dei difetti del Full Feature Audit. Venti issue sono chiuse e mergiate in
main-dev; otto restano. NON ricominciare l'audit e NON rigenerare l'inventario.

## Ambiente

cd infra && make dev, poi `docker start meepleai-web` (resta in stato Created).
Credenziali in infra/secrets/admin.secret: usa badsworm@gmail.com con SEED_BADSWORM_PASSWORD
(quella di admin@meepleai.app NON corrisponde al DB). Il database si chiama meepleai_staging
anche in locale, e ha 8 schemi.

**Prima di sondare, verifica da che codice e' costruita l'immagine API.** Ci ho perso tempo due
volte: se le migration nel DB non coincidono con quelle nel sorgente, l'immagine e' vecchia e stai
misurando codice che non esiste piu'.

    MSYS_NO_PATHCONV=1 docker exec meepleai-postgres psql -U meepleai -d meepleai_staging \
      -tAc 'SELECT count(*) FROM "__EFMigrationsHistory";'
    ls apps/api/src/Api/Infrastructure/Migrations/*.cs | grep -vc "Designer\|Snapshot"

I due numeri devono coincidere (18 al 2026-08-27). Su Git Bash anteponi sempre MSYS_NO_PATHCONV=1
ai comandi con argomenti che iniziano per `/`.

## Le otto issue rimaste

| # | Cosa | Nota |
|---|---|---|
| #3846 | Upload PDF con storage S3: 200 ma l'elaborazione fallisce | la piu' concreta rimasta |
| #3850 | "Informazione non disponibile" in inglese dentro conversazioni italiane | piccola |
| #3838 | L'audit di sicurezza registra solo i login | mancano logout, 2FA, cambio ruolo |
| #3873 | IsAdmin() come guardia sbaglia in DUE direzioni opposte | autorizzazione, non meccanica |
| #3866 | 14 file di test su 20 non riproducono NoTracking | la lacuna che ha lasciato passare 5 difetti |
| #3840 | scores/confirm registrata due volte | serve una decisione sullo scoring live |
| #3853 | 16 schemi Zod paginati non raggiunti dal confronto per nome | eseguirli contro risposte reali |
| #3836 | Il gruppo /badges non esiste lato backend | esporne meta' e' decidere il prodotto |

Le ultime tre non sono correzioni: sono decisioni. Portale al proprietario invece di sceglierle tu.

## Il metodo che ha reso di piu'

**Fai elencare i difetti a un test, non cercarli a mano.** Su #3847 un test che invoca tutti i 755
validatori con un corpo vuoto ne ha trovati 29 in una passata; ventiquattro li ha risolti una riga
sola. Stesso schema per #3836 (4 dialog senza titolo su 116) e #3835 (2 chiamate senza prefisso su
270). Quando un difetto ha una forma riconoscibile, scrivi il setaccio.

**Verifica dal vivo, e ricostruisci l'immagine prima.** Tre issue su ventidue si sono rivelate
non-difetti proprio cosi' (#3834, #3854, #3851).

**Prova a smentire il tuo stesso test.** Ne ho scritti tre che passavano ANCHE sul codice rotto.
Prima di fidarti: ripristina il file originale, rilancia, pretendi il rosso.

## Sette trappole pagate care

1. **Il contesto di test traccia, la produzione no.** Senza
   `UseQueryTrackingBehavior(NoTracking)` nel contesto di test, un'intera famiglia di difetti e'
   invisibile. E' #3866, e ha lasciato passare #1627, #1633, #2804, #3564, #3858.

2. **Il build Release cade dove il Debug passa.** Rendere null-safe una lettura (`x?.Count ?? 0`)
   informa il compilatore che la proprieta' e' nullable, e da li' OGNI uso a valle diventa CS8604.
   E il publish locale e' incrementale: mente. Serve
   `dotnet clean -c Release && dotnet publish --no-incremental`.

3. **Le risposte QA sono in cache.** `/agents/qa` accetta `?bypassCache=true`. Senza, una risposta
   identica al carattere sembra determinismo del modello ed e' invece la voce vecchia: ho creduto
   per qualche minuto che una correzione non funzionasse.

4. **Correla i log per RequestId, non per vicinanza.** Attribuendo a sei endpoint la prima
   eccezione che vedevo, avevo dato a tutti la stessa causa. Erano quattro diverse.

5. **La baseline dei test di integrazione invecchia.** Confronta i NOMI dei falliti su un commit
   precedente, non i conteggi. Su Windows non usare `git worktree add` per questo: fallisce a meta'.
   Con l'albero pulito basta `git checkout <sha>`.

6. **Un elenco privato si duplica male.** Tre vocabolari di tier divergenti (#3842) esistevano
   perche' quello del dominio era `private`. Esporlo e' costato una riga.

7. **Non stampare mai `printenv` senza filtrare i valori.** Filtra per nome, stampa solo le chiavi.

## Come lavorare

Un branch per issue da main-dev (`git config branch.<nome>.parent main-dev`), test che fallisce
prima, correzione, verifica dal vivo, PR con la causa spiegata, merge, chiusura dell'issue con quello
che hai trovato — inclusi gli errori della diagnosi originale: due issue su ventidue erano sbagliate,
e dirlo vale piu' che correggerle in silenzio.

Lascia l'ambiente come l'hai trovato: nessun dato di prova, nessun branch residuo.
```

---

## Nota per chi riprende

Le sette trappole non sono pedanteria: ognuna ha prodotto lavoro da buttare. La seconda ha fatto
fallire un build che i controlli locali dichiaravano verde; la terza mi ha quasi fatto annullare una
correzione che funzionava.
