# #3878 — spec-panel verdict: la UI di progresso dell'upload PDF

**Issue**: [#3878](https://github.com/meepleAi-app/meepleai-monorepo/issues/3878) — l'utente non vede mai il fallimento di un upload PDF; la UI di progresso esiste ed è spenta
**Origine**: scorporata da [#3846](https://github.com/meepleAi-app/meepleai-monorepo/issues/3846) (F-7 del [verdetto precedente](./2026-08-27-issue-3846-spec-panel-verdict.md)), a sua volta dall'ondata 2 del [Full Feature Audit](../audits/2026-08-26-full-feature-audit/README.md)
**Data**: 2026-09-07
**Metodo**: `/sc:spec-panel` — modalità critique (Wiegers · Adzic · Nygard · Fowler · Crispin · Hightower · Cockburn)
**Stato**: verdetto emesso, DoD proposta. Il fix non è implementato.
**Issue correlate**: [#3850](https://github.com/meepleAi-app/meepleai-monorepo/issues/3850) (messaggi in inglese in contesto italiano) · [#3491](https://github.com/meepleAi-app/meepleai-monorepo/issues/3491) (precedente sul modo di testare una page-client)

---

## 1. Verdetto in una riga

La DoD è **internamente contraddittoria**: il suo primo punto — «attivare il componente esistente così
com'è» — rende irraggiungibile il terzo — «un upload che fallisce lascia sullo schermo un messaggio che
l'utente può leggere».

Il percorso di errore **smonta** la UI che contiene il messaggio e scrive la causa in un campo di stato
che nessun ramo di render legge. Eseguendo la DoD alla lettera, l'utente ottiene un'animazione in più e
lo stesso silenzio di oggi.

## 2. Rettifica: il difetto non è che la UI è spenta

La issue afferma che «il segnale esiste già su tutta la catena, ed è solo spento». La prima metà è vera:
backend, componente e test esistono. La seconda no — fra il componente e la pagina il segnale **si
perde**, e accendere il flag non lo ricongiunge.

```
componente   ProcessingProgress.tsx:149   currentStep === Failed && errorMessage  ->  onError(msg)

pagina       upload-client.tsx:161        handleProcessingError(error):
             upload-client.tsx:162          setShowProcessingProgress(false)   <- smonta chi mostra il messaggio
             upload-client.tsx:163          dispatch({ type: 'PROCESSING_ERROR', error })

reducer      useWizard.ts:55-59           -> state.processingError = error
                                             state.processingStatus = 'failed'

render       upload-client.tsx:335        { wizardState.error && ... }        <- campo DIVERSO
             upload-client.tsx:—          processingError non compare in nessun ramo di render
```

`processingError` è scritto e mai letto. `PROCESSING_ERROR` non tocca `error`, che è popolato solo da
`{type:'ERROR'}`. Il risultato osservabile, a flag acceso:

1. la barra di progresso appare e avanza;
2. al fallimento **sparisce**;
3. non compare alcun messaggio;
4. il bottone «Parse PDF» resta disabilitato per sempre
   (`disabled={parsing || wizardState.processingStatus !== 'completed'}`, `upload-client.tsx:468`),
   senza dire perché.

L'utente resta fermo allo step 2 davanti a un bottone grigio. È lo stesso esito di oggi, con più
movimento.

**Corollario utile**: la correzione centrale è piccola — rendere `processingError` e non smontare il
componente al fallimento. Ma finché la DoD parla di *accensione* e non di *ricongiungimento*, nessuno la
scriverà: si limiterà a definire la variabile d'ambiente e a dichiarare chiuso il punto.

## 3. Findings del panel

| # | Sev | Esperto | Finding |
|---|---|---|---|
| F-1 | 🔴 | Wiegers | La DoD è contraddittoria: il punto 1 rende irraggiungibile il punto 3 (§2) |
| F-2 | 🔴 | Adzic | «Un PDF che fallisce» non è una condizione: sono **quattro** modi di fallire con quattro esiti UI diversi, e la DoD ne copre uno |
| F-3 | 🟡 | Fowler | Lo stesso flag accende una **seconda** superficie che la issue non nomina: `UploadProgressTracker` allo step 4 (`upload-client.tsx:578`) |
| F-4 | 🟡 | Fowler/Hightower | Il componente è fuori dal design system (colori letterali in stili inline) e nessun gate se ne accorge: in tema scuro è illeggibile |
| F-5 | 🟡 | Crispin | 27 test verdi, tutti sul componente **isolato**: nessuno monta la pagina, cioè il lato del confine dove sta il difetto |
| F-6 | 🟡 | Hightower | Il precedente citato per il build arg è il percorso sbagliato: gli innesti reali sono tre, e uno è il workflow di deploy |
| F-7 | 🟢 | Cockburn/Wiegers | Lingua dei messaggi e attore della funzione non sono decisi |

### F-2 in dettaglio — i quattro modi di fallire

| # | Come fallisce | Cosa fa il codice | Cosa vede l'utente |
|---|---|---|---|
| **A** | `Failed` **con** `errorMessage` | `onError` → la pagina smonta il componente (F-1) | **niente** |
| **B** | `Failed` **senza** `errorMessage` | `onError` non parte (`&& transformedProgress.errorMessage`, `ProcessingProgress.tsx:149`); il banner interno richiede anch'esso `errorMessage` (`:505`) | barra rossa a 0% e «Processing status: Failed», **senza causa** |
| **C** | fallimento **prima** che la pipeline scriva `Failed` — il caso di #3846: lo storage | nessuno stato terminale, il polling continua | 10 minuti di attesa, poi «Processing timeout exceeded» |
| **D** | errore di rete durante il polling | `setNetworkError`, il componente **resta montato** | banner arancione — **l'unico dei quattro che funziona** |

Che **B** sia raggiungibile lo dice il contratto: `errorMessage: z.string().nullable().optional()`
(`pdf.schemas.ts:132`). Che **C** sia raggiungibile lo dice l'origine stessa di questa issue: in #3846
l'upload rispondeva 200 e falliva a valle.

Il backend copre bene il caso A: `UploadPdfCommandHandler.Processing.cs` chiama
`UpdateProgressAsync(..., ProcessingStep.Failed, ..., errorMessage, ...)` in quattro punti (`:258`,
`:278`, `:601`, `:1106`). Ciò che manca non è la produzione del segnale: è il suo consumo.

**Un quinto percorso cade nella stessa strettoia, ed è quello che rende il difetto evidente**:
l'annullamento richiesto dall'utente scrive anch'esso `Failed`, con messaggio «Processing cancelled by
user» (`:1074`). Il polling lo legge, `errorMessage` è valorizzato, parte `onError` — e la pagina smonta
il componente. Cioè: **il bottone «Cancel Processing» del componente fa sparire il componente senza
alcuna conferma a schermo**, per il percorso che il componente stesso offre e che i suoi test coprono
con sette casi. È la dimostrazione più economica di F-1: non serve un PDF che fallisce davvero per
riprodurlo.

Esempio eseguibile per il caso oggi scoperto:

```gherkin
Dato   un upload il cui stato diventa Failed senza errorMessage
Quando il polling riceve quello stato
Allora la pagina mostra un messaggio di fallimento con il documentId
E      il messaggio resta leggibile dopo la fine del polling
E      il bottone Parse, disabilitato, dichiara il motivo
```

### F-3 — la superficie non dichiarata

`upload-client.tsx:578` monta `UploadProgressTracker` (`components/ui/admin/upload-progress-tracker`)
sotto **lo stesso** `enableProcessingProgress`, allo step 4. Due componenti, due percorsi di dati, un solo
interruttore. La issue ne discute uno.

Rivederne uno e accendere l'altro alla cieca è il modo di mandare in staging un componente che nessuno ha
guardato — che è esattamente ciò che la issue dice di voler evitare.

### F-4 — fuori dal design system, sotto il radar dei gate

`ProcessingProgress.tsx` è interamente `CSSProperties` inline con colori letterali: `#f9fafb`,
`backgroundColor: 'white'`, `#333`, `#0070f3`, `#e0e0e0`, `#d93025`.

Il progetto impone token semantici e temizzazione `[data-theme="light|dark"]`. L'ESLint
`local/no-hardcoded-color-utility` è **error**, ma ispeziona le utility Tailwind nel `className`: uno
stile inline non è una utility e gli passa sotto. È il motivo per cui un componente non conforme ha
convissuto per mesi con un gate bloccante — il gate non era cieco, era rivolto altrove.

In tema scuro il pannello è un rettangolo chiaro con testo `#333` su fondo scuro. **Questo è il costo
reale dell'opzione «rifarlo prima»**, e la issue non lo nomina: chiede di scegliere fra due opzioni senza
dire cosa distingue il prezzo dell'una da quello dell'altra.

Nota minore sulla stessa superficie: il dialogo di conferma dell'annullamento è un `<div role="dialog"
aria-modal="true">` costruito a mano (`:532`), senza focus trap né chiusura da `Esc`. Non è un problema di
gate — appare solo dopo un click, quindi axe su pagina statica non lo incontra — ma di utente che ci
finisce dentro con la tastiera.

### F-5 — perché 27 test verdi non hanno colto nulla

`ProcessingProgress.test.tsx` contiene fra gli altri *«calls onError when processing fails with error
message»* e *«displays error message when processing fails»*. Passano entrambi, ed è corretto che passino:
**il componente fa il suo lavoro**. Il difetto vive nel contratto fra `onError` e il suo chiamante, e il
chiamante non è montato da nessun test.

> È una variante dello schema già visto su questo repo — un test verde su codice rotto — ma più
> insidiosa: qui il test non è sbagliato, è *dalla parte sbagliata del confine*.

Il test che chiude questa issue deve montare `UploadClient`, simulare un `Failed`, e asserire **testo
visibile all'utente**. Con MSW e senza mockare la pagina (precedente #3491), e va **preteso rosso** prima
della correzione.

### F-6 — i tre innesti reali del build arg

La issue indica come precedente `NEXT_PUBLIC_CSP_ALLOW_LOCAL_BLOB` in `infra/compose.e2e-storage.yml`.
Quello è il **consumatore** e2e locale, non il punto in cui il pattern è definito. Il commento in
`apps/web/lib/security/csp.js:49` lo dice: «via the `NEXT_PUBLIC_CSP_ALLOW_LOCAL_BLOB` *build arg*
(Dockerfile + …)».

| Dove | Riga | Cosa serve |
|---|---|---|
| `apps/web/Dockerfile` | 51-65 | `ARG` + `ENV` — il pattern esiste già, sono due righe |
| `.github/workflows/deploy-staging.yml` | 551 | il blocco `build-args:` dell'immagine web (oggi tre variabili) |
| `infra/compose*.yml` | — | il percorso di sviluppo locale |

Un requisito che non dice dove si applica si verifica male: senza il secondo innesto, il flag è acceso in
locale e spento su staging, con un sintomo indistinguibile dal flag lasciato a `false` — la trappola che
la issue stessa segnala.

### F-7 — lingua e attore

Le stringhe sono interamente in inglese («PDF Processing Progress», «Estimated time remaining», «Cancel
Processing») su una pagina che contiene già «Vai alla Queue →» (`upload-client.tsx:591`). È il gemello
esatto di **#3850**, aperta nello stesso backlog: accendere questa UI allarga quel difetto invece di
lasciarlo dov'è.

Sull'attore: la issue dice che l'accensione «cambia cosa vede ogni utente dopo un upload». La rotta è
`(authenticated)/upload` — ogni utente autenticato, non solo gli amministratori — ma la pagina contiene
già rami `isAdminOrAbove`. Va detto per chi è questa UI, perché è la domanda che decide il tono dei
messaggi: un amministratore vuole la causa tecnica, chi carica un manuale vuole sapere se riprovare.

## 4. Scorecard della specifica

| Dimensione | Voto | Nota |
|---|---|---|
| Chiarezza | 8/10 | ben scritta, evidenza citata, comando di verifica incluso |
| Onestà epistemica | 8/10 | dichiara ciò che è deciso e ciò che non lo è; scorporare da #3846 è stato corretto |
| Accuratezza della causa | 3/10 | il difetto centrale (§2) non è nominato: si ferma un livello sopra |
| Completezza | 4/10 | manca metà della superficie del flag (F-3) e il costo del DS (F-4) |
| Testabilità della DoD | 3/10 | «un PDF che fallisce davvero» copre 1 caso su 4 (F-2) |
| Coerenza interna | 2/10 | il punto 1 rende irraggiungibile il punto 3 (F-1) |
| **Complessivo** | **4,7/10** | buona ricognizione, spec di fix non ancora eseguibile |

Il merito va detto per intero: la issue ha scoperto che il segnale esiste ed è spento, ha verificato con
un comando che la variabile non è definita in alcun ambiente, e ha avuto ragione a separare una decisione
di prodotto da un fix di storage. Si è fermata dove il segnale si perde.

## 5. DoD proposta

- [ ] Il fallimento dell'elaborazione lascia sullo schermo un messaggio leggibile, **persistente dopo la
      fine del polling**, per tutti e quattro i modi di fallire A/B/C/D (F-2)
- [ ] `wizardState.processingError` è reso in un ramo di render; il bottone «Parse PDF» disabilitato
      dichiara il motivo (F-1)
- [ ] Decisa e documentata la sorte di `UploadProgressTracker`, che condivide lo stesso flag (F-3)
- [ ] Il componente usa i token semantici e regge sia il tema chiaro sia lo scuro (F-4)
- [ ] I messaggi sono nella lingua della pagina, coerentemente con #3850 (F-7)
- [ ] `NEXT_PUBLIC_ENABLE_PROGRESS_UI` fornita nei tre innesti: `apps/web/Dockerfile` (ARG+ENV),
      `deploy-staging.yml:551` (`build-args`), compose di sviluppo (F-6)
- [ ] L'annullamento richiesto dall'utente produce una conferma a schermo, non la sparizione del pannello
      (F-2, quinto percorso)
- [ ] Un test che monta `UploadClient`, simula `Failed` e asserisce testo visibile — **verificato rosso**
      prima della correzione (F-5)
- [ ] Verificato dal vivo su staging su almeno gli scenari **A** e **C**; l'annullamento serve come
      riproduzione rapida in sviluppo, non sostituisce la verifica su un fallimento reale

I tre punti «Da decidere» della issue originale restano decisioni del proprietario, ma due si riformulano:
non «attivare o rifare» (F-4 dice cosa costa rifare), e non «cosa mostrare sui fallimenti terminali»
(F-2 dice quali sono).

## 6. Nota implementativa

**Opzione raccomandata — ricongiungere prima, accendere dopo.** L'ordine importa: rendere
`processingError` e non smontare il componente al fallimento sono modifiche piccole e verificabili a flag
spento, con il test di F-5 a dimostrarle. Accendere il flag diventa allora un cambiamento di
configurazione su codice già provato, non un salto nel buio. L'inverso — accendere e poi correggere —
mette in staging il difetto §2 nel frattempo.

**Opzione scartata — riscrivere il componente prima di ricongiungerlo.** La conformità al design system
(F-4) è dovuta, ma è indipendente dal difetto: rifarlo prima significa riscrivere anche il percorso di
errore che oggi non sappiamo ancora leggere correttamente, e ripartire da 27 test da riscrivere. Il DS si
sistema nello stesso branch, dopo che il ricongiungimento ha un test verde a difenderlo.

**Nota su C.** Il caso C — fallimento a monte dello stato terminale — non si corregge nel frontend. Se
la pipeline non scrive mai `Failed`, l'unica difesa del client è il timeout dei 10 minuti, che è
un'attesa, non un messaggio. Vale la pena decidere se `MAX_POLL_DURATION_MS` debba tradursi in un errore
esplicito («non abbiamo ricevuto aggiornamenti: lo stato è ignoto») invece che in una riga arancione che
invita a ricaricare.

## 7. Verifiche eseguite

```bash
# il flag non e' definito in nessun ambiente (conferma della issue)
rg -n "NEXT_PUBLIC_ENABLE_PROGRESS_UI" infra/ apps/web/

# il campo scritto dal reducer non e' letto da nessun ramo di render
rg -n "processingError|wizardState\.error" "apps/web/src/app/(authenticated)/upload/upload-client.tsx"

# le due superfici sotto lo stesso flag
rg -n "enableProcessingProgress|ProcessingProgress|UploadProgressTracker" \
  "apps/web/src/app/(authenticated)/upload/upload-client.tsx"

# il backend popola l'errore sul percorso A
rg -n "ProcessingStep\.Failed" \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs

# il precedente del build arg: Dockerfile-first, non compose-first
rg -n "NEXT_PUBLIC_CSP_ALLOW_LOCAL_BLOB" infra/ apps/web/ --glob '!node_modules'
rg -n "build-args" -A 5 .github/workflows/deploy-staging.yml

# i test esistenti: 27, tutti sul componente isolato
rg -c "it\(" apps/web/src/components/progress/__tests__/ProcessingProgress.test.tsx
```
