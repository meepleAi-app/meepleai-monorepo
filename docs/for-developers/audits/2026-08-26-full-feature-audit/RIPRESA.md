# Ripresa dell'audit — prompt per una nuova sessione

Copia il blocco qui sotto come primo messaggio di una nuova sessione. Contiene tutto ciò che serve
per riprendere senza rileggere la conversazione precedente.

---

```
Riprendi il Full Feature Audit del prodotto. È già al 95%: NON ricominciare da capo, NON rigenerare
l'inventario, NON riscrivere gli strumenti — esistono e funzionano.

## Dove sta tutto

- Branch: `feature/full-feature-audit` (parte da `main-dev`, PR #3837 aperta)
- Tracker: `docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv` — 1725 righe,
  una per ogni (rotta × ruolo) e ogni endpoint. È la fonte di verità sulla copertura.
- Report: `README.md` nella stessa cartella + una scheda per ondata (`wave-*.md`)
- Spec e piano: `docs/for-developers/specs/2026-08-26-full-feature-audit-{design,plan}.md`
- Strumenti: `apps/web/scripts/audit/` (con test in `__tests__/`, 125 test verdi)

## Stato

| Stato nel tracker | Righe |
|---|---|
| ✅ verificato | 1322 |
| ⚠️ finding da aprire | 150 |
| ⚠️ finding da triagare | 96 |
| ⬜ non coperto | 86 |
| 🚫 non eseguito (irreversibile) | 71 |

26 issue aperte, tutte con causa accertata. Due P0 già corretti e in PR: #3832 e #3841.

## Prima di qualsiasi cosa: prepara l'ambiente

1. Stack: `cd infra && make dev`, poi **`docker start meepleai-web`** — il container web resta in
   stato `Created` e non parte da solo.
2. Credenziali: da `infra/secrets/admin.secret`. Usa `badsworm@gmail.com` con
   `SEED_BADSWORM_PASSWORD` come admin: la password di `admin@meepleai.app` NON corrisponde al DB
   locale. Utente semplice: `test@meepleai.com` con `SEED_TEST_PASSWORD`.
3. Cookie per le sonde: rigenerali in `apps/web/audit-results/cookies.json` come
   `{"admin":"meepleai_session=…","user":"meepleai_session=…"}`.
4. Database: si chiama **`meepleai_staging`** anche in locale. Le colonne sono PascalCase in alcune
   tabelle (`users."Id"`) e snake_case in altre (`shared_games.id`). Non esiste una tabella `games`:
   il catalogo è `shared_games`. Esistono 8 schemi, non solo `public`.
5. **Verifica da che codice è costruita l'immagine API prima di sondarla.** Durante l'audit il
   container girava codice di ~3 settimane prima; me ne sono accorto solo ricostruendolo. Il
   controllo costa un comando — se le migration nel DB non coincidono con quelle in
   `apps/api/src/Api/Infrastructure/Migrations/`, l'immagine è vecchia:

   ```bash
   MSYS_NO_PATHCONV=1 docker exec meepleai-postgres psql -U meepleai -d meepleai_staging      -tAc 'SELECT count(*) FROM "__EFMigrationsHistory";'   # deve dare 17
   ```

   Al 2026-08-27 l'ambiente è allineato: immagine ricostruita da `main-dev`, DB riparato,
   17/17 migration applicate.

## Gli strumenti, e come si usano

Tutti da `apps/web`, tutti con `MSYS_NO_PATHCONV=1` su Git Bash (altrimenti gli argomenti che
iniziano con `/` vengono convertiti in path Windows e la regex si rompe in silenzio):

```bash
MSYS_NO_PATHCONV=1 pnpm exec tsx scripts/audit/probe-reads.ts "." <Contesto>
MSYS_NO_PATHCONV=1 pnpm exec tsx scripts/audit/probe-mutations.ts <Contesto>
pnpm exec tsx scripts/audit/mark-verified.ts <file.jsonl>   # riporta le verifiche manuali nel tracker
pnpm exec tsx scripts/audit/main-inventory.ts               # rigenera l'inventario (raro)
pnpm exec tsx scripts/audit/main-report.ts                  # dal crawl al report + stati
```

Crawler e verifiche di interazione (Playwright, config dedicata senza auth bypass):

```bash
pnpm exec playwright test --config=playwright.audit.config.ts --grep "ruolo"        # crawler
pnpm exec playwright test --config=playwright.audit.config.ts --grep "Interazioni"  # click reali
```

## Cosa resta, in ordine di valore

1. **Le 96 righe "da triagare"** sono in larga parte artefatti dei parametri, non difetti: navigare
   `/library/private/[id]` con l'id di un gioco *condiviso* dà 404 legittimo. Vanno lette una per
   una — l'evidenza è nella colonna accanto — e riclassificate.
2. **Le 86 scoperte** richiedono entità che l'ambiente non ha (collezioni di documenti, campagne,
   job di coda). Crearle via API sblocca i parametri e le rende verificabili: è così che sono
   passate da 187 a 86.
3. **Le 71 irreversibili** — riavvii di servizi, migrazione storage, rotazione chiavi, cancellazione
   di backup — vanno su un ambiente sacrificabile, mai su questo.
4. **Aree già coperte a livello L1 ma mai esercitate**: KbQuality, BusinessSimulations, Gamification.
   Il livello L1 dice solo che l'endpoint non esplode.

## Il filo conduttore emerso, da tirare

**Gli aggregati con collezioni figlie falliscono in scrittura, ciascuno per una ragione diversa.**
Quattro casi finora: toolkit (#3854, doppia mappatura fra `game_toolkit.toolkits` e
`public."GameToolkits"`), toolbox (#3857, persistenza corretta ma 0 righe aggiornate), live-session
(#3851, campo `players` ignorato), entity-link (#3858, DELETE che risponde 204 senza cancellare).
Vale la pena verificare gli altri aggregati con lo stesso schema.

## Cinque trappole che mi sono costate tempo — non ripeterle

1. **Una sessione che scade a metà passata rende conformi centinaia di righe.** Ogni 401 veniva
   letto come «autorizzazione applicata»: 232 righe su 711 risultavano verificate senza essere state
   provate. La sonda ora si ferma dopo cinque 401 consecutivi, ma controlla comunque il conteggio.
2. **La lista di esclusione per parole chiave non basta.** `DELETE /users/me` non contiene nessun
   verbo pericoloso e cancella l'account con cui stai lavorando. C'è ora una regola sui percorsi che
   agiscono sul soggetto autenticato.
3. **I criteri automatici sull'interfaccia bocciano pagine integre.** `/library` comunica lo stato
   vuoto con i contatori, non con la parola «vuota». Guarda la pagina prima di scrivere il criterio.
4. **Un 400 può nascondere un 500.** `library/games/batch-status` risponde 400 senza parametri e 500
   fornendoli: fermarsi al 400 lo archivia come «parametro mancante» e il difetto resta.
5. **Non stampare mai `printenv` senza filtrare i valori.** Ho esposto credenziali S3 reali nel
   transcript. Filtra per nome, stampa solo le chiavi.

## Come lavorare

Parti sempre dalle chiamate API, poi usa i click dove l'API non arriva — il campo di ricerca
readonly di `/games` (#3848) nessuna chiamata lo avrebbe trovato. Per ogni difetto: isola la causa
nei log **correlando per `RequestPath`** (la prima eccezione nei log è spesso un job di sottofondo
che non c'entra), apri una issue con riproduzione e Definition of Done, e riporta la verifica nel
tracker con `mark-verified`.

Prima di dichiarare un difetto, prova a smentirlo: metà delle mie difformità iniziali erano payload
sbagliati miei. E quando i fatti smentiscono una diagnosi già scritta, correggila — è successo con
#3854, dove avevo attribuito a `xmin` quella che era una doppia mappatura.

Lascia l'ambiente come l'hai trovato: 8 utenti, 135 PDF, nessuna sessione o toolkit di prova.
```

---

## Nota per chi riprende

Il prompt sopra è deliberatamente prescrittivo sulle trappole. Non è pedanteria: ognuna di quelle
cinque ha prodotto lavoro da buttare o, nel caso del `DELETE /users/me`, ha rischiato di cancellare
l'account usato dall'audit.
