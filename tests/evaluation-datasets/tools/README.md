# Banco di recupero (#4482)

Per ogni domanda di riferimento con una citazione verificata (`verifica.citazione`), dice se il passaggio
citato è fra i chunk che la chat recupera, e in che posizione: nel **pool** che va al reranker e nei
**finali** che la chat mette nel prompt.

Il banco chiama la sonda `POST /api/v1/admin/kb/retrieval-probe`, che fa il recupero della chat per quel
gioco (lingua dalla domanda e dall'`Accept-Language`, versione pubblicata del profilo, ricerca ibrida,
reranker) e **non chiama nessun modello linguistico**: niente costi. Chiama però il servizio di embedding e
il reranker veri, e il recupero conta nelle metriche come quello della chat.

## Come si lancia

Serve una sessione admin. Il valore del cookie `meepleai_session` va nella variabile d'ambiente
`MEEPLEAI_ADMIN_COOKIE` (oppure l'header intero, `nome=valore`, se il cookie ha un altro nome). Il banco non
lo stampa mai. Per esempio, con un login curl:

```bash
curl -s -c /tmp/jar -H 'content-type: application/json' \
  -d @/tmp/login.json http://localhost:8080/api/v1/auth/login > /dev/null   # {"email":…,"password":…}
export MEEPLEAI_ADMIN_COOKIE="$(awk '$6 == "meepleai_session" { print $7 }' /tmp/jar)"
rm /tmp/login.json /tmp/jar
```

Poi, dalla radice del repository:

```bash
node tests/evaluation-datasets/tools/recupero.mjs \
  --set tests/evaluation-datasets/reference-questions-it.json \
  --api http://localhost:8080 \
  --out recupero.json
```

- `--set` è obbligatorio e non ha un valore predefinito: il file delle domande si sposta con #4470.
- I titoli dei giochi (`game`) si risolvono con `GET /api/v1/admin/kb/vector-stats`, cioè fra i giochi
  indicizzati, per titolo esatto (maiuscole a parte). Se un titolo manca o è doppio, il banco si ferma e
  elenca i giochi disponibili: allora si passa una mappa con `--giochi mappa.json`, nella forma
  `{ "Catan": "<gameId>", "Ticket to Ride: Europe": "<gameId>" }`.
- `--gioco Catan` (ripetibile) e `--id CAT-03,CAT-19` restringono le domande.
- `--accept-language` è l'header che la chat leggerebbe (predefinito `it`): decide la lingua solo quando la
  domanda non basta a dirla.
- `--completo` mette nell'uscita la risposta intera della sonda, con il testo di tutti i chunk.
- Senza `--out` l'esito JSON va sullo standard output; l'avanzamento e la tabella di riepilogo vanno sempre
  sullo standard error.

## Cosa esce

Per ogni domanda: la categoria, la lingua del regolamento, la lingua usata per cercare e da dove viene, la
versione del profilo, come sono stati scelti i finali (`reranking`), e per `pool` e `finali`:

- `trovata` e `posizione`: la più piccola K per cui tutta la citazione sta nei primi K chunk;
- `parziale`: solo una parte dei frammenti della citazione (separati da `[…]`) è nella lista;
- per ogni frammento, la posizione, il punteggio e il chunk più vicino anche sotto la soglia (`migliore`, con
  pagina e testo), per rileggere a mano i casi mancati.

Il riepilogo conta, per gioco e per lingua del regolamento, le domande trovate nel pool e nei finali sul
totale con citazione, separando tre categorie:

- `in_ambito`: le domande sul regolamento, il dato che conta;
- `fuori_ambito`: le domande «Out of Scope», che chiedono di un termine o una meccanica che il gioco non ha;
  la citazione è il passaggio che smentisce la premessa;
- `fuori_dal_corpus`: le domande con `fuori_dal_corpus: true`, il cui dato non è nel testo indicizzato.

La lingua di un regolamento è quella della maggioranza delle sue citazioni: Catan e Ticket to Ride: Europe
risultano in inglese, gli altri in italiano.

Se la tabella segnala `FellBack`, il reranker non ha lavorato su quelle domande: i finali sono i primi della
fusione, e la misura non dice nulla del reranker.

## Il confronto e la soglia

La citazione è stata estratta dal PDF con un estrattore diverso da quello del corpus, quindi il confronto
non cerca la stringa (`confronto.mjs`):

1. normalizza i due testi: parole spezzate da un trattino a fine riga ricomposte, accenti tolti,
   minuscole, solo lettere e cifre, spazi singoli;
2. divide la citazione nei frammenti separati da `[…]` o `[...]`; un frammento sotto le 4 parole
   («Contadino») si ignora, se ce ne sono di più lunghi;
3. conta quanti trigrammi di parole del frammento stanno nel chunk (il contenimento): il frammento è nel
   chunk se il contenimento arriva a **0,7**. Una citazione con meno di 3 parole usa sequenze lunghe quanto
   lei: le sue parole devono stare di seguito;
4. se nessun chunk da solo basta, prova le coppie di chunk **consecutivi dello stesso PDF**: un passaggio
   spezzato fra due chunk vale trovato, e la coppia è segnalata.

**Perché 0,7.** Calibrazione del 2026-10-18 sui regolamenti in `data/rulebook/` di Catan, Carcassonne,
Ticket to Ride e Ticket to Ride: Europe (90 citazioni; il PDF italiano di Gloomhaven non è nel repository):
testo estratto con due estrattori (pdftotext e PyMuPDF), tagliato in finestre da 60, 120 e 250 parole.

- Ritrovate: con PyMuPDF 90 su 90 da 0,6 a 0,75; con pdftotext 87 su 90 da 0,6 a 0,7. Le tre mancate sono
  tabelle ed elenchi (`TTR-26`, `TTR-27`, `CAT-30`) che i due estrattori leggono in ordine diverso: lì il
  confronto per sequenze non può funzionare, e va letto `migliore` a mano.
- Falsi positivi: le citazioni contro il regolamento di un altro gioco nella stessa lingua non risultano
  mai trovate, a nessuna delle soglie provate (da 0,6 a 0,8). Nello stesso regolamento, tolto il passaggio
  vero, le citazioni ritrovate altrove a 0,7 sono la stessa regola ripetuta (il testo di una carta nel
  regolamento e nell'almanacco), dove «trovata» è giusto, tranne un caso misto: `CAT-28` con finestre da
  120 parole, una coppia che unisce Longest Road e una seconda copia, troncata, della regola di Largest Army.
- Il caso che decide: regole di forma parallela. «If another player has more Knight cards in play, they
  immediately receive this tile» contro il chunk di Longest Road («…more continuous roads in play, they
  immediately receive this tile») ha contenimento 0,67, e «The first player to have 3 Knight cards…»
  contro «…5 continuous roads…» 0,55. A 0,6 il primo passerebbe; a 0,7 no, senza perdere nessuna
  citazione vera rispetto a 0,6. Per lo stesso motivo le coppie sono solo di chunk consecutivi: l'unione di
  due chunk qualsiasi somma due regole parallele e supera la soglia.

La soglia si cambia con `--soglia`. I test del confronto:

```bash
node --test tests/evaluation-datasets/tools/confronto.test.mjs
```
