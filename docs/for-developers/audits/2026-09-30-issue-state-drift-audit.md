# Issue state drift — quando lo stato dichiarato non corrisponde al codice

**Data**: 2026-09-30 · **Perimetro**: le issue aperte del repo + le chiuse dal 2026-08-01 · **HEAD**: `a5cf5e151`

## Che cosa cercava

Issue il cui **stato dichiarato non corrisponde al codice**, in entrambe le direzioni: chiuse ma
ancora rotte, fix parziali, residui dichiarati e mai tracciati; e all'opposto aperte ma gia'
risolte, conteggi decaduti, premesse morte.

## Come e' stato condotto

Quattro fasi, ognuna con un agente per unita' di lavoro:

| fase | unita' | esito |
|---|---|---|
| Triage | 1 agente per issue aperta, lotti da 3 per le chiuse | 174/174 esaminate |
| Verifica | 1 agente per sospetto, con repro eseguibile obbligatorio | 223 verdetti, 207 confermati |
| Refutazione | 1 agente per issue, 3 lenti sui P1/P2 (evidenza · copertura · intenzionalita'), 1 sui P3 | 189 sottoposti, **25 caduti** |
| Sintesi | 1 agente, effort massimo | questo documento |

La leva piu' produttiva del triage e' stata imporre la **rimisura** dei conteggi invece della loro
lettura: questo repo intitola le issue con numeri (`38 voci`, `30 chiamate`, `117 link`), e un numero
nel titolo invecchia da solo.

## Che cosa e' gia' stato corretto

Tre difetti emersi dall'audit sono stati chiusi lo stesso giorno, tutti e tre con una guardia nuova
provata **in negativo** prima di essere accettata:

| PR | difetto | perche' era sopravvissuto |
|---|---|---|
| [#3975](https://github.com/meepleAi-app/meepleai-monorepo/pull/3975) | hub `/settings` irraggiungibile, in ciclo di redirect | le guardie risolvevano gli href sull'albero dei file, mai sui redirect di config |
| [#3980](https://github.com/meepleAi-app/meepleai-monorepo/pull/3980) | 13 alert interrogavano metriche che nessuno espone | il gate cercava il nome C# grezzo: un *terzo* nome gli era invisibile |
| [#3981](https://github.com/meepleAi-app/meepleai-monorepo/pull/3981) | la cache RAG serviva le house rule di un utente agli altri | i test giravano sul percorso senza cache, che non e' quello di default |

Hanno la stessa firma, ed e' la firma che questo audit censisce: **un controllo che esiste, e' verde,
e non guarda dove il difetto vive**.

---

# Esito

## 1. Il verdetto in tre righe

Dopo l'esame ostile restano **164 finding su 93 issue**; **25 sono caduti**, e due issue (#3490, #3585) hanno perso tutto ciò che le riguardava, uscendo pulite dal registro dello scarto (`comm -13` sugli elenchi di issue distinte; totale toccato: 95). Il tasso di caduta è **25/189 = 13,2%**.

Il dato interessante non è la percentuale ma la sua composizione: **nove** dei venticinque sono caduti perché un fratello sulla stessa issue diceva già la stessa cosa (avrebbero prodotto issue gemelle), **due** perché il codice era cambiato alle 11:30 di stamattina, e il precedente più istruttivo è fuori campione — l'unico finding arrivato agli scettici prima di questa fase (#3954, «30 chiamate → 28») è stato ribaltato, e applicarlo avrebbe cancellato la traccia di un difetto vivo. La fase mancante non stava rifinendo: stava intercettando un errore su otto.

---

## 2. Che cosa è sopravvissuto, per gravità

Premessa che cambia il conto delle azioni: **164 finding non sono 164 difetti**. 44 issue portano più di un sopravvissuto, una ne porta sette (#3901), otto ne portano quattro (`cut -d' ' -f1 surv.txt | sort | uniq -c | awk '$1>1'`). Diversi sopravvissuti lo dichiarano da sé — #3891 («è lo stesso finding scritto due volte — una sola issue, non due»), #3866 («subsunto dal finding 2»), #3839 («è un duplicato stretto»), #3933 («l'intestazione duplica il finding gemello»).

### Grave — il difetto è nel codice, il registro è solo la spia

**(a) Il fix per campione.** La classe di difetto è stata corretta sull'istanza riprodotta e la famiglia è rimasta: #3847 (due dei trenta endpoint del *suo stesso elenco* conservano il deref pre-validazione), #3839 (uno dei nove endpoint in 500), #3843 (`GET /api/v1/wizard/game-preview/{gameId}`, il settimo che la issue si era aggiunta da sé), #3882 (`UpdateTierStrategyAccessCommandHandler.cs:57`, candidato n.2 dei 31 elencati nel corpo), #3845 (`ConfigKey`: corretto `ShareRequestLimitConfig`, non la classe), #3846 (`DisablePayloadSigning = true` incondizionato su cinque siti), #3857/#3856/#3866 (`AsTracking()` su due file toolbox, non la passata sugli aggregati con collezioni figlie), #3852/#3842/#3835/#3836/#3844 (rotte con parametro), #3589 (`UnstructuredPdfTextExtractor` sì, `SmolDocling` no), #3636 (sette handler migrati a `ExecuteInTransactionAsync`, due vivi ancora su `BeginTransactionAsync`), #3568 (`ExtractPdfTextCommandHandler.cs:61-66` passa la stessa chiave come `fileId` e come `referenceId`).

*Causa comune, ed è una sola*: la DoD dice «passata sugli altri percorsi» e la PR tocca il sito nominato. Chi chiude misura l'ambito sulla riproduzione del bug, non sulla classe. Si ri-risolvono per classe, non per sito.

**(b) I gate che non misurano.** #3625/#3622 (esclusioni per nome: `Category=Integration&FullyQualifiedName~FrontendSdk` → 57 test, i gate ristretti a quei nomi → 0/0/0), #3707, #3498 e #3895 (lo step agganciato a un job che non gira sul ramo dove serve), #3770 (gate retrieval fermo), #3901, #3617 (health check registrato solo sotto l'override `compose.staging.tutor.yml`), #3940, #3634, #3742.

Due sotto-cause distinte e ricorrenti: il filtro seleziona per nome e il nome non esiste; oppure lo step è agganciato a un workflow che non scatta lì. La memoria di progetto ha già la regola («verde e vuoto: stesso segnale — chiedi il conteggio»); quello che manca è applicarla ai gate *nuovi*.

Ne ho verificata una variante che nessun finding contesta, e che vale come controllo di salute del metodo. L'AC di #3923 è `grep -rho "bg-card/[0-9]*" src --include=*.tsx | sort -u | wc -l` con target `= 3`. Eseguito a HEAD `a5cf5e151` restituisce **15** — non 13 (corpo), non 14 (finding). Quindici perché `[0-9]*` accetta zero cifre: la quindicesima riga è il token nudo `bg-card/`, e viene da una *regex di test* (`src/components/features/library/__tests__/LibraryHybridGrid.test.tsx:212`, `expect(wrapper?.className).toMatch(/bg-card/)`). Anche portando gli alpha a tre, quel comando non restituirà mai 3: **l'AC non è spuntabile per costruzione**.

### Medio — il registro indirizza il lavoro nella direzione sbagliata

**(c) Il censimento sbagliato usato come baseline di un criterio di accettazione.** È un solo errore propagato in quattro issue aperte — #3916 (epic: «13 alpha distinti / ~671 superfici», riusato in §12 come baseline del gate «13 valori → 3»), #3920, #3922, #3923 — ed è quindi il cluster più economico da ri-risolvere insieme. Misura di oggi (`grep -rho "bg-card/[0-9]*" src --include=*.tsx | sort | uniq -c`): gli alpha numerici sono quattordici (`5,10,15,20,25,30,40,50,60,70,80,85,90,95`) per 672 occorrenze; il corpo ne conta tredici per 671 perché omette `/25`, che compare **una volta sola**. Il `526` di `backdrop-blur` regge, ma solo sotto quella forma esatta del comando: senza `--include=*.tsx` sono 528. E c'è un `bg-card/80` in `src/lib/sessions-summary/entity-text-tokens.ts:11` che nessuna forma dell'AC vede. Adiacente per natura: #3943, dove lo stesso insieme di primitive è contato tre volte in modo diverso e uno dei nomi richiesti (`SettingsShell`) non esiste nel codice.

**(d) La diagnosi morta che sopravvive nel corpo.** #3768 porta ancora viva, alla riga 33, «Il difetto è nel corpus o nel chunking heading-aware, non nella fusione né nella codifica delle query» — affermazione che CLAUDE.md contraddice due volte (nota #3737 sulla fusione, nota #3740 sulla lingua proiettata). Stessa famiglia: #3740 (due sopravvissuti, entrambi con severity alzata), #3770 (premessa nata falsa: la run citata confrontava baseline 20260817 con snapshot 20260823, non «a parità di snapshot»). Un corpo che punta nella direzione sbagliata costa più di un corpo vuoto.

**(e) Il titolo falso su issue viva.** #3901 (sette sopravvissuti: la collection raccoglie 3124 test contro il «Test Count: 0» del titolo, ma due voci di DoD su cinque restano scoperte), #3878 (quattro sopravvissuti: la testa del corpo descrive lo stato pre-fix, superato dalle PR della issue stessa #3950/#3958), #3928 («117» è l'OR di due insiemi disgiunti), #3953, #3931, #3941, #3957.

Su #3957 ho verificato la variante peggiore, perché la raccomando in testa: `apps/web/src/config/admin-navigation.ts` **non ha alcun importer**. I soli tre riferimenti nel repo sono dentro la guardia che lo sorveglia (`src/config/__tests__/static-hrefs.test.ts`, righe 43, 53, 195, dove il file è citato come stringa: `file: 'admin-navigation.ts'`), e la nav admin viva è `ADMIN_NAV_GROUPS` in `src/components/layout/admin-nav/admin-nav-config.ts`, montata da `AdminSidebar.tsx` e `AdminSideDrawer.tsx`. La guardia difende un debito su un file che nessuno renderizza: l'unico consumatore dell'orfano è il test che lo custodisce.

### Basso ma di volume — il residuo dichiarato in chiusura e mai aperto

Il pattern è uniforme: chi chiude scrive «Resta aperto», «Follow-up residui», «Non toccato», «Cosa lascia dietro», «Cosa resta rosso, e perché non è di questa PR» — e non apre la issue. Le issue coinvolte: #2954, #3390, #3427, #3435, #3455, #3467, #3495, #3570, #3578, #3590, #3601, #3617, #3618, #3646, #3655, #3656, #3669, #3670, #3688, #3694, #3756, #3786, #3806, #3813, #3833, #3840, #3844, #3846, #3866, #3887, #3891, #3917, #3925, #3932.

Il rimedio non richiede analisi: **il residuo è già scritto, dal suo autore, nel momento in cui ne sapeva di più**. È un lavoro di copia. Tre casi vanno però estratti dal blocco, perché il rinvio punta a un bersaglio morto e il registro afferma una copertura che non esiste: #3813 → #3822 (rinvio a vuoto), #3848 → #728 (chiusa da mesi), #3887 → #3601 (chiusa).

---

## 3. Che cosa è caduto, e perché

`cut -d' ' -f2 fall.txt | sort | uniq -c` → **evidenza 14 · copertura 7 · intenzionalità 4**.

**L'evidenza ha mietuto più del doppio delle altre due messe insieme, e questo dice qualcosa sul metodo, non su questi finding.** Se la fase precedente era già un esame dell'evidenza, e la fase successiva uccide con la stessa lente oltre la metà dei caduti, allora la prima passata non stava misurando: stava confermando una conclusione già presa. Chi verifica conferma; chi refuta misura.

Dentro i 14, tre meccanismi distinti:

- **La misura non riproducibile.** #3954 è il caso limite: l'evidenza portata a sostegno era un *template*, con il placeholder letterale `<p>` al posto del comando. #3813 asserisce «i job di scrape orfani sono DUE» ed è falso. #3853 risponde a «9 pagine» con «9 rotte» — sostantivo scambiato e controprova ignorata. #3770 cita come prova una run il cui log dice l'opposto (`FAIL catan-setup — top-3 retrieval drifted`).
- **Il duplicato.** #564, #3467, #3669, #3806, #3813, #3840, #3845, #3895, #3901: nove finding che ripetono un fratello, spesso misurato meglio. Non è un difetto dei singoli, è un difetto della pipeline — più agenti hanno letto la stessa issue senza vedersi.
- **La conclusione più larga dei fatti.** #3490, #3840, #3933: i fatti reggono, l'implicazione no. #3933 è il caso didattico — le due conclusioni portanti erano refutate dal repo, e applicarle avrebbe *peggiorato* la issue.

**Copertura (7)** ha una firma diversa: il finding non ha cercato dove la cosa c'era. #3633 dichiara un'assenza senza cercarla nell'albero dei test, dove c'è — è l'errore-tipo #1 in forma pura. #448 e #3940 sono caduti perché il codice è cambiato: PR #3980 e PR #3975, entrambe mergiate **alle 11:30 di oggi** (`git log --since='2026-09-30 00:00'`), cioè prima ancora che il file dei finding fosse scritto. #3433, #3655, #3845, #3895: coperti da un altro finding o da un layer preesistente.

**Intenzionalità (4)** ha mietuto meno ed è la lente più fragile. Uccide quando il deferral è documentato: #3585 (due volte, sulla *stessa* lettura errata del commento di chiusura), #3707 (il DoD 5 cita la condizione verbatim: era nello scope dichiarato), #3851 (il commento non delega un follow-up, lo offre a condizione). Fragile perché si regge su un testo, non su una misura — la debolezza esatta che l'audit imputa ai finding. Il contro-esempio è #3891, sopravvissuto proprio perché la motivazione scritta del deferral è falsificata dalla misura. Regola che ne esce: **un deferral documentato refuta un finding solo se la motivazione del deferral è verificabile e verificata.**

---

## 4. Le severity corrette

`cut -d' ' -f2 surv.txt | sort | uniq -c` → **29 abbassate · 12 alzate · 123 invariate**. Il verso conta più del conteggio.

**Abbassate (29)** — pattern unico, tre sotto-tipi:
- il rischio è già presidiato altrove: #3735 (la severity poggiava su un rischio coperto tre volte), #3836 (il solo meccanismo nominato è chiuso e gate-coperto da tre settimane prima del crawl), #3846 (il difetto *al presente* è più piccolo della cornice);
- il residuo è reale ma il deferral è esplicito e la conseguenza è documentale: #3395, #3618, #3669, #3840, #3848, #3887, #3932;
- metà del corpo del finding è caduta: #448 (la gamba alert+gate chiusa da PR #3980), #2954 (tre su quattro), #3393, #3455, #3495, #3498, #3578, #3670, #3694, #3756, #3768, #3852, #3866, #3917, #3923.

**Alzate (12)** — dicono dove la prima passata ha sottostimato, e si dividono in due gruppi netti:
- **il difetto è vivo nel codice ed era classificato come scarto documentale** — #3740 (due volte: la lingua proiettata e la baseline non più soddisfacibile toccano la correttezza del retrieval), #3839, #3842 (404 strutturale), #3852 (test verde su uno stato irraggiungibile), #3856, #3859, #3933;
- **il registro comanda lavoro già fatto, o difende un artefatto morto** — #3901 (due volte), #3940 (l'AC era soddisfatta il giorno in cui la issue è stata aperta), #3957 (la guardia sorveglia un file senza importer).

Il secondo gruppo è quello che vale la pena notare: non è un difetto *del prodotto*, è un difetto che **fa perdere lavoro**. Qualcuno implementerà qualcosa che esiste, o manterrà un orfano.

---

## 5. Ordine di attacco

**Passo zero, prima di ogni onda: deduplicare per issue.** 44 issue portano più di un sopravvissuto e almeno quattro finding dichiarano da sé di essere gemelli. Aprire issue una-per-finding riproduce esattamente il difetto che questo audit sta censendo.

**Onda 0 — i corpi che comandano lavoro sbagliato.** Solo edit di testo. #3768 (riga 33), #3916 → #3920/#3922/#3923, #3943, #3901, #3878, #3928, #3953, #3957, #3931, #3941. Prima di tutto perché il costo è un'edit e il danno è che qualcuno esegua. Dentro l'onda c'è un vincolo di sequenza: **#3916 prima dei tre figli**, perché il §12 dell'epic è la fonte del numero che ereditano; e la correzione va scritta col comando accanto al numero, perché *il comando stesso è difettoso* (vedi §2c).

**Onda 1 — i gate che non misurano.** #3625 prima di #3622 (è #3625 a portare i numeri: 57 → 0/0/0), poi #3498 e #3895 insieme (stesso errore di wiring), poi #3901, #3770, #3617, #3940, #3707. Motivo: un gate fermo non ha costo visibile oggi e ne accumula uno ogni giorno. Dipendenza rovesciata da tenere presente: finché #3901 non riporta il conteggio reale della suite E2E, non si sa che cosa gli altri gate E2E stiano eseguendo.

**Onda 2 — i difetti vivi, per classe e non per sito.** Cinque passate: (a) deref pre-validazione sotto `Routing/` (#3847); (b) serializzazione di entità di dominio su outbox/cache (#3845, #3859); (c) `AsTracking()` sugli aggregati con collezioni figlie (#3857, #3856, #3866); (d) rotte con parametro che collassano 404→500 (#3852, #3842, #3839, #3843, #3835, #3836, #3844); (e) firma payload e chiavi R2 (#3846, #3568). L'ordinamento per classe non è estetico: la causa comune di questa intera famiglia è che la passata sulla classe *è stata dichiarata nel DoD e non eseguita*. Rifarla per sito ripete l'errore che l'ha generata.

**Onda 3 — le famiglie ereditate da un fix parziale che oggi non producono 500.** #3589 (SmolDocling), #3636 (i due handler ancora su `BeginTransactionAsync`), #3568 se non già chiuso in onda 2.

**Onda 4 — la passata di apertura issue sui residui dichiarati.** Meccanica: il paragrafo del commento di chiusura diventa il corpo della issue nuova. Per primi i tre col rinvio a bersaglio morto (#3813, #3848, #3887), perché lì il registro afferma una copertura inesistente.

**Onda 5, fuori sequenza — la catena retrieval.** #3740 → #3768 → #3770, in quest'ordine: #3740 stabilisce la lingua per candidato e la baseline; #3768 eredita da #3740 la diagnosi da correggere; #3770 non è valutabile finché il gate non riparte. Fuori sequenza perché è l'unico gruppo in cui la correzione richiede una misura sul corpus, non una lettura — e la memoria di progetto avverte che i pesi `0.7/0.3` sono tarati contro una query codificata `passage:`, quindi prefisso e taratura vanno misurati insieme.

**Assunzione dichiarata**: l'ordinamento presume che i sopravvissuti descrivano correttamente il codice oltre i punti che ho ricontrollato in prima persona (HEAD `a5cf5e151` e le tre PR di oggi; il censimento `bg-card/α`; l'orfano `admin-navigation.ts`; lo stato aperto/chiuso delle issue che metto in onda 0 e 1). Non ho rieseguito i 189 repro: ho riderivato l'aritmetica del dataset e verificato quattro punti, scelti perché reggono le raccomandazioni di testa.

---

## 6. Che cosa questo audit non ha guardato

**Ciò che nessuno ha scritto.** L'audit è ancorato al registro: per costruzione non vede i difetti senza issue. Il campione stesso dice che ce ne sono — se in #3847 due dei trenta endpoint del *suo stesso elenco* sono sopravvissuti al fix, il «fix per campione» è strutturale, e le classi che nessuno ha mai enumerato stanno fuori dal registro per definizione. Il verso da girare è l'opposto di questo: partire dal codice — `#pragma warning disable`, `[Trait("Skip", …)]`, `TODO`/`Follow-up:`, filtri per nome nei workflow, allowlist degli arch-gate — e chiedere quale issue lo copra.

**I corpi delle PR e i file di audit.** Diversi residui sopravvissuti non vivono in una issue: vivono in una PR mergiata (#3891, #3917, #3945, #3866 «cosa resta rosso») o in una nota sotto `docs/for-developers/audits/` (#3887). Quella superficie non è stata censita, ed è la più probabile: una PR mergiata non ha nessuno che la riapra.

**Gli ADR.** Ne sono stati toccati alcuni (089, 090) solo perché una issue li citava. Un ADR obsoleto ha lo stesso effetto di un corpo di issue obsoleto, con più autorità e più raggio: CLAUDE.md rimanda agli ADR come fonte, non alle issue.

**Le issue chiuse NOT_PLANNED e i duplicati chiusi.** Il campione appare concentrato su OPEN e CLOSED/COMPLETED. Una issue chiusa come duplicata di un'altra che poi si chiude senza il lavoro è invisibile a entrambe le direzioni di questo audit.

**La refutazione non è stata refutata.** Questa fase è girata una volta sola, come quella che critica. I quattro caduti sotto intenzionalità sono i candidati naturali per un terzo passaggio, perché la difesa che li ha uccisi è testuale e non misurata. Il conto però non lo giustifica: quattro finding al 13,2% fa mezzo finding recuperato in media. È un conto, non un'intuizione — e va rifatto se il campione cresce.

**La data di raccolta.** I finding sono stati raccolti ad almeno due HEAD (`6d7c15781` e `a5cf5e151`) e due sono caduti esattamente per questo, entrambi per PR mergiate lo stesso giorno. Nessuno ha ri-datato sistematicamente i restanti: chi eseguirà le onde deve rieseguire il repro **prima** di agire. È già costato due finding in ventisei minuti.

**La documentazione utente** (`docs/for-users/`) e il perimetro Python (`apps/*-service/`, toccato solo da #3668 e #3455). Hanno in comune il fatto di non avere alcun gate che le confronti col codice — cioè la condizione che ha prodotto quasi tutto ciò che è sopravvissuto qui.
