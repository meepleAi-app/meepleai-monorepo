# Casi d'uso lato utente

> **Cosa dice**: che cosa può fare oggi chi usa MeepleAI senza ruoli di staff: visitatore, ospite e utente registrato.
> **Come è stato ricavato**: dal codice, non dalla documentazione. Per ogni rotta si è seguito il percorso `page.tsx` → componente → hook o client API, si sono cercati i redirect in `page.tsx`, `next.config.js` e `proxy.ts`, e per ogni componente si è controllato chi lo monta.
> **Verificato su**: analisi del codice a `5c8fa5ea3` (branch `feature/issue-4138-retire-agent-slot`), poi **prova nel browser** su `main-dev` a `f8829d954`, entrambe il 2026-10-10. La prova è stata fatta sullo stack locale con immagini `api` e `web` ricostruite da quel commit, con un account `User` registrato dal browser. Gli esiti sono in [§ Verifica nel browser](#verifica-nel-browser-2026-10-10). Le righe senza quella verifica restano una lettura del codice.
> **Cosa non dice**: lo stato di un ambiente diverso da quello locale. Il percorso UI → API → DB per contesto è nel [Full Feature Audit](./audits/2026-08-26-full-feature-audit/README.md). Per sapere quanto codice c'è e dove sta, vedi l'[inventario delle feature](./feature-inventory.md), che viene generato.

## Come leggere le tabelle

| Stato | Significato |
|---|---|
| ✅ | Funziona dalla UI: la pagina c'è, è raggiungibile e chiama il backend. |
| ⚠️ | Funziona, ma con un limite scritto nella colonna Note. |
| 🚧 | Segnaposto: la UI dice «in arrivo» oppure il pulsante è disabilitato. |
| 👻 | Costruito ma irraggiungibile: il codice esiste, ma nessuna pagina monta il componente o porta alla rotta. |
| ❌ | Apparente: la UI accetta l'azione, ma l'effetto promesso non avviene. |
| ⛔ | Assente: nel frontend non c'è niente. |

**Attori**

- **Visitatore**: non ha fatto l'accesso.
- **Ospite**: non ha un account ed entra con un link o un codice.
- **Utente**: è registrato, con ruolo `User`.
- **Host**: è l'utente che ha creato la serata o la sessione.

Le pagine sotto `(authenticated)` mandano a `/login?from=` chi non ha fatto l'accesso (`lib/routing/protected-routes.ts`). Dopo il login si arriva su `/library`, non su `/dashboard` (`proxy.ts`).

**Agente AI**: c'è un solo agente, di sistema, configurato dall'admin ([ADR-094](../for-claude/architecture/adr/adr-094-single-system-agent.md)). Il gioco è un parametro della domanda, non una proprietà dell'agente. Le superfici «agente per gioco» sono state ritirate (#4138) e non compaiono qui.

---

## 1. Accesso e account — `ACC`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| ACC-01 | registrarsi con email e password | `/register` | ✅ | La modalità si decide a runtime (`RegistrationMode`). Dopo la registrazione si passa a `/verification-pending`. |
| ACC-02 | chiedere l'accesso quando la registrazione è solo su invito | `/register` | ✅ | Usa `RequestAccessForm`. Se il backend non risponde, la pagina ricade su questa modalità. |
| ACC-03 | accedere con email e password | `/login` | ✅ | |
| ACC-04 | accedere con Google, Discord o GitHub | `/login`, `/register` | ⚠️ | In `/register` i pulsanti compaiono solo se `oauthEnabled`. In `/login` invece compaiono sempre, anche con l'OAuth spento (osservato in locale con `oauthEnabled: false`). `/oauth-callback` rimanda a `/login`. |
| ACC-05 | completare l'accesso con il secondo fattore | `/login` | ✅ | `verify2FALogin` |
| ACC-06 | verificare l'email e farsi rinviare il link | `/verify-email`, `/verification-pending` | ✅ | Il rinvio ha un limite di frequenza. |
| ACC-07 | reimpostare la password dimenticata | `/reset-password` | ✅ | Dopo il reset l'accesso è automatico e si arriva su `/chat`. |
| ACC-08 | attivare l'account da un invito, impostando la password | `/setup-account?token`, `/accept-invite?token` | ⚠️ | Ci sono due flussi paralleli su endpoint diversi (`activate-account` e `accept-invitation`). Con un invito scaduto si arriva su `/invitation-expired`. |
| ACC-09 | cambiare la password dopo l'accesso | — | 👻 | `authClient.changePassword` esiste, ma nessun componente lo chiama. |
| ACC-10 | collegare o scollegare un account OAuth | — | ⛔ | |
| ACC-11 | cancellare l'account o esportare i propri dati | — | ⛔ | Nel frontend utente non c'è niente. Va confrontato con l'[informativa privacy](../legal/). |

## 2. Primo accesso e home — `HOME`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| HOME-01 | completare l'onboarding, cioè scegliere gli interessi e aggiungere un primo gioco | `/onboarding` | ⚠️ | Il wizard si completa, ma con tre limiti. (1) **La ricerca del primo gioco trova solo la prima pagina del catalogo**: `api.games.getAll` scarica 20 giochi e filtra lato client. «Azul» si trova, «Dominion» no, e nemmeno gli esempi del segnaposto (Catan, Wingspan, Ticket to Ride). (2) Al passo 1 «Continua» salva senza avanzare, mentre «Next» avanza solo dopo «Continua»; premuto prima non fa niente e non dice niente. (3) Il passo «invita un amico» è un segnaposto. Un utente nuovo non viene mandato all'onboarding: dopo la verifica email arriva su `/library`. |
| HOME-02 | vedere la home: serate in arrivo, sessioni recenti, giochi suggeriti, attività degli amici | `/dashboard` | ✅ | |
| HOME-03 | rispondere Accetta / Forse / Rifiuta a una serata direttamente dalla home | `/dashboard` | ✅ | `useRsvpGameNight` |
| HOME-04 | farsi generare dall'AI una guida alla preparazione di un gioco | `/setup` | ✅ | `api.agents.generateSetupGuide`. Non è il setup dell'account. Non è verificato da dove ci si arrivi. |

## 3. Profilo e impostazioni — `SET`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| SET-01 | caricare e ritagliare l'avatar | `/profile` | ✅ | |
| SET-02 | cambiare il nome visualizzato | `/profile`, `/settings/profile` | ✅ | |
| SET-03 | cambiare l'email | `/settings/profile` | ✅ | Il sottotitolo della sezione promette anche avatar e lingua, che lì non ci sono. |
| SET-04 | attivare la 2FA (QR e codici di backup) o disattivarla | `/settings/security` | ✅ | |
| SET-05 | vedere le sessioni attive e revocarne una o tutte | `/settings/security` | ✅ | |
| SET-06 | dare o togliere il consenso all'elaborazione AI e all'uso di provider esterni | `/settings/ai-consent` | ✅ | |
| SET-07 | scegliere lingua e tema | `/settings/preferences` | ❌ | **Il salvataggio fallisce per ogni utente**: `PUT /api/v1/users/preferences` risponde `422` perché la pagina invia `{theme, language, emailNotifications}` senza `dataRetentionDays`, che il backend esige fra 1 e 3650. Anche se si salvasse, i testi seguono la lingua del browser e per `fr` non ci sono traduzioni. L'interruttore del tema che funziona all'istante è nel menu utente. |
| SET-08 | creare, copiare e revocare chiavi API | `/settings/api-keys` | ✅ | Lo scope è fisso a `read`. |
| SET-09 | configurare i servizi | `/settings/services` | 🚧 | |
| SET-10 | vedere i propri achievement e l'attività recente | `/profile` (tab achievements, activity) | ✅ | Il redirect `/badges` → `?tab=badges` è rotto: `badges` non è un tab e si finisce sulla panoramica. |

## 4. Notifiche — `NOT`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| NOT-01 | leggere le notifiche, filtrarle per tipo e vedere solo le non lette | `/notifications` | ✅ | |
| NOT-02 | segnare come letta una notifica (aprendola) oppure tutte | `/notifications` | ✅ | |
| NOT-03 | scegliere per ogni evento i canali: email, push, in-app | `/settings/notifications` | ⚠️ | Per il push si salva solo la preferenza: `usePushNotifications`, che iscrive il browser, non è usato da nessuna pagina. |
| NOT-04 | impostare le ore di silenzio con il proprio fuso orario | `/settings/notifications` | ✅ | |
| NOT-05 | ricevere le notifiche su Slack | `/settings/notifications` | ❌ | Gli interruttori ci sono, ma non c'è il pulsante per collegare Slack. Il backend espone connect, callback e status (`SlackIntegrationEndpoints.cs`), ma il frontend non li chiama. |

## 5. Catalogo e scoperta — `CAT`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| CAT-01 | esplorare le righe di Discover (tendenze, novità, toolkit, documenti KB, contributori) e filtrarle per tipo | `/discover`, `/games?tab=discover` | ⚠️ | La ricerca e la riga eventi sono spente da costanti (`SEARCH_ENDPOINT_AVAILABLE`, `EVENTS_ENDPOINT_AVAILABLE`). |
| CAT-02 | vedere i giochi di tendenza | `/games?tab=trending` | ✅ | I tab `catalogo` e `community` sono 🚧. |
| CAT-03 | cercare nel catalogo community con filtri e ordinamento | `/shared-games` | ✅ | Funziona anche per il visitatore. I filtri restano nell'URL. |
| CAT-04 | consultare la scheda pubblica di un gioco | `/shared-games/[id]` | ✅ | È in sola lettura e l'unica azione è accedere. Ha ancora un tab «agenti» (vedi [§ Lacune](#lacune-da-decidere)). |
| CAT-05 | proporre un proprio gioco privato al catalogo community | `/library/private` | ✅ | `useCreateShareRequest` |
| CAT-06 | vedere la vetrina della libreria pubblica | `/library-public` | 🚧 | Giochi e statistiche sono scritti nel codice. |

## 6. Scheda gioco — `GAM`

Il dettaglio canonico è `/games/[id]` ([ADR-091](../for-claude/architecture/adr/adr-091-canonical-game-detail-tree.md)), con le schede info, regole, FAQ, sessioni, statistiche, chat e documenti. Il dettaglio di libreria `/library/[gameId]` ne ha un altro, con le schede info, Agente, Toolkit, house rules e partite.

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| GAM-01 | aggiungere il gioco alla propria libreria | `/games/[id]` | ⚠️ | Dopo l'aggiunta si passa a `/library/[id]`. La UI raggiungibile non mostra e non controlla il limite di libreria: `useLibraryQuota` è usato solo da componenti 👻. Non è verificato se il limite lo applichi il backend. |
| GAM-02 | aggiungere, modificare e togliere house rules | `/games/[id]` (Info) | ✅ | Solo se il gioco è in libreria. Le legge l'AI in sessione. Nella scheda house rules di `/library/[gameId]` è invece 🚧. |
| GAM-03 | avviare una nuova sessione sul gioco | `/games/[id]` | ✅ | Porta a `/sessions/new?gameId=`. |
| GAM-04 | vedere la propria percentuale di vittorie, le partite giocate e la classifica del gioco | `/games/[id]` | ✅ | `useGameLeaderboard` |
| GAM-05 | leggere le FAQ del gioco | `/games/[id]/faqs` | ⚠️ | La pagina dedicata funziona. La scheda FAQ dentro il dettaglio è sempre vuota (`faqs={[]}`), e il suo sottotitolo mostra il segnaposto crudo «Le domande più frequenti su **{title}**.». |
| GAM-06 | leggere le versioni delle regole e lo storico delle sessioni | `/games/[id]/rules`, `/games/[id]/sessions` | ✅ | Sola lettura. |
| GAM-07 | consultare la scheda meccaniche stampabile, con citazioni di pagina, e lasciare un feedback | `/games/[id]/card` | 👻 | La pagina funziona, ma non è stato trovato nessun link che ci porti. |
| GAM-08 | condividere il gioco | `/games/[id]` | ✅ | Usa `navigator.share`. |
| GAM-09 | caricare o togliere una copertina personalizzata | `/library/[gameId]` | ✅ | `useCustomCoverUpload` |
| GAM-10 | segnare il gioco come preferito | `/games/[id]` | ⚠️ | Il dettaglio mostra il preferito ma non permette di cambiarlo. Resta un toggle nella home, dentro `MeepleLibraryGameCard`; non è verificato. |

## 7. Libreria personale — `LIB`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| LIB-01 | sfogliare la propria libreria a schede (tutto, giochi, KB, sessioni, chat), cercare, filtrare, ordinare e cambiare vista | `/library` | ⚠️ | Fuori dalla scheda giochi, l'ordinamento è uno stub (`SORT_KEY_STUB`). Le card dei giochi aprono `/games/{id}`. |
| LIB-02 | aggiungere un gioco dal catalogo | `/library?action=add` | ✅ | `AddGameDrawer` → `CatalogSearchStep` |
| LIB-03 | creare un gioco privato a mano, con il PDF del regolamento opzionale | `/library?action=add`, `/library/private/add` | ✅ | |
| LIB-04 | elencare, modificare ed eliminare i propri giochi privati | `/library/private` | ✅ | |
| LIB-05 | seguire la checklist di attivazione di un gioco privato e avviare, riprendere o abbandonare una sessione live | `/library/private/[id]` | ✅ | |
| LIB-06 | gestire la wishlist: aggiungere, modificare (priorità, prezzo obiettivo, note), togliere, filtrare, vedere le statistiche | `/library/wishlist` | ⚠️ | Il selettore «aggiungi» cerca solo tra i giochi già in libreria (`AddToWishlistDialog` usa `useLibrary`). |
| LIB-07 | togliere un gioco dalla libreria, modificare le note, assegnare etichette, fare azioni in blocco | — | 👻 | Ci sono `RemoveGameDialog`, `EditNotesModal`, `LabelSelector` e `BulkActionBar`, ma nessuna pagina li monta. |
| LIB-08 | creare, modificare o revocare un link di condivisione della propria libreria | — | 👻 | `ShareLibraryModal` è montato solo nella sua story. Di conseguenza nessuna UI crea il token. |
| LIB-09 | vedere una libreria condivisa da altri | `/library/shared/[token]` | ✅ | Pagina pubblica. Nessuna UI crea i link (LIB-08). |
| LIB-10 | gestire le collezioni | — | 👻 | `useCollections` (`useCollectionStatus`, `useAddToCollection`, `useRemoveFromCollection`) non ha consumatori. |

Non risultano UI utente per valutare o recensire giochi, né per i suggerimenti di gioco.

## 8. Chat AI e knowledge base — `AI`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| AI-01 | vedere le proprie chat e l'avviso quando si avvicina il limite di sessioni del proprio piano | `/chat` | ✅ | `useChatSessionLimit` |
| AI-02 | aprire una chat scegliendo un gioco (della libreria privata o condivisa) e i suggerimenti iniziali | `/chat/new` | ⚠️ | La scheda «I miei giochi» carica solo i giochi **privati** (`/private-games`): con Azul in libreria dice «Nessun gioco trovato», e il gioco si trova solo in «Libreria condivisa». Accetta i parametri `?game=` e `?kbIds=`. |
| AI-03 | scegliere una «persona» (auto, tutor, arbitro, stratega, narratore) | `/chat/new`, `/chat/[threadId]` | ❌ | La scelta non viene inviata quando si crea il thread (`ThreadCreator.createThread` non ha il campo) e lo stream della risposta la ignora. Il cambio dentro il thread chiama `switchThreadAgent`, ma non è verificato che cambi la risposta. |
| AI-04 | fare una domanda sulle regole e ricevere una risposta in streaming con citazioni dal manuale | `/chat/[threadId]` | ❌ | **Per un utente normale, su un gioco del catalogo, la risposta è «An error occurred: Accesso RAG non autorizzato».** `RagAccessService` concede l'accesso solo se la KB del gioco è pubblica (in locale `select count(*) filter (where is_rag_public) from shared_games` → 0), se il gioco è privato dell'utente, oppure se l'utente ha dichiarato di possederlo (AI-15), cosa che l'interfaccia non permette di fare. Prova di controllo: dopo `POST /library/{id}/declare-ownership` fatto via API, la stessa domanda riceve una risposta corretta con le citazioni di pagina. Senza gioco nel thread il messaggio viene salvato e non parte nessuna richiesta di risposta. |
| AI-05 | aprire la pagina del manuale citata | `/chat/[threadId]` | ✅ | `PdfPageModal` |
| AI-06 | valutare una risposta come utile o non utile | `/chat/[threadId]` | ⚠️ | Solo se il thread ha un gioco. |
| AI-07 | rinominare o eliminare una chat | `/chat/[threadId]` | ✅ | |
| AI-08 | fare domande a voce e farsi leggere le risposte | `/chat/[threadId]` | ✅ | `useVoiceInput`, `useVoiceOutput` |
| AI-09 | chattare sulle regole dal dettaglio del gioco | `/library/[gameId]?tab=aiChat` | ⚠️ | Il gioco deve essere in libreria e avere almeno un PDF indicizzato o in elaborazione. Ha lo stesso controllo d'accesso di AI-04 (stesso `qaStream`); non è stato provato separatamente. |
| AI-10 | cercare in tutti i propri documenti e fare una domanda su più giochi | `/knowledge-base/global` | 👻 | Funziona, ma la navigazione non ci porta: non c'è in `config/navigation.ts`. |
| AI-11 | sfogliare i chunk di un documento e cercarci dentro | `/knowledge-base/[id]` | ✅ | `/kb/[id]` rimanda qui. `/knowledge-base` rimanda a `/library`. |
| AI-12 | vedere il PDF di un documento della KB | `/knowledge-base/[id]/pdf` | 🚧 | Mostra «Coming Soon» e dopo 2 secondi rimanda altrove. |
| AI-13 | vedere lo stato KB di un gioco, reindicizzarlo, eliminare un PDF | `/library/[gameId]/kb` | ⚠️ | Il pulsante «carica» non fa niente: `handleUpload` è vuoto. Apri, costo e sposta sono rinviati. |
| AI-14 | caricare il PDF del regolamento | aggiunta gioco (`KnowledgeBaseStep`), `/sessions/new` (`UploadRulesStep`), gioco privato | ✅ | C'è un limite mensile per piano (`TierLimits.MaxPdfUploadsPerMonth`). La pagina `/upload` è riservata a Editor e Admin. |
| AI-15 | dichiarare di possedere un gioco, cosa che sblocca la chat sul suo manuale | — | 👻 | `DeclareOwnershipButton` è montato solo da `MeepleLibraryGameCard`, che vive in `HomeFeed` e `LibraryPanel`, e nessuna pagina li monta. Nel browser il pulsante non compare né in `/dashboard` né in `/library` né nei dettagli del gioco. È il motivo per cui AI-04 fallisce. |

## 9. Librogame — `GBK`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| GBK-01 | vedere i propri manuali fotografati e la quota mensile di traduzione | `/gamebook` | ⚠️ | I pacchetti di crediti sono scritti nel codice (`lib/gamebook/checkout-packs.ts`) e non c'è nessun pagamento. |
| GBK-02 | fotografare un manuale e caricare le foto in blocco, seguendone lo stato | `/gamebook/upload` | ✅ | La ricerca BGG compare solo agli admin. |
| GBK-03 | creare, riprendere, rinominare ed eliminare campagne | `/library/[gameId]/play` | ✅ | |
| GBK-04 | giocare una campagna: aggiornare i progressi, aprire la chat, chiudere la campagna | `/library/[gameId]/play/[campaignId]` | ✅ | Se la campagna è legata a una serata compare una striscia. |
| GBK-05 | fotografare una pagina e riceverla divisa in segmenti e tradotta in streaming, oppure incollare il testo | `…/[campaignId]/translate` | ✅ | `?mode=manual` serve per il testo incollato. |
| GBK-06 | trasformare un paragrafo di incontro in un promemoria con glossario | `…/[campaignId]/encounter` | ✅ | Ci si arriva solo da un link dentro la traduzione. |

## 10. Serate di gioco — `GN`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| GN-01 | vedere le serate in calendario o in elenco e rispondere dalla card | `/game-nights` | ✅ | |
| GN-02 | creare una serata con il wizard: data con controllo conflitti, inviti a utenti o a email, giochi scelti o «decidiamo insieme» | `/game-nights/new` | ✅ | La bozza si conserva tra una visita e l'altra. |
| GN-03 | (host) modificare, pubblicare (cosa che invia gli inviti) o annullare la serata | `/game-nights/[id]` | ✅ | `/game-nights/:id/edit` rimanda a `?action=edit`. |
| GN-04 | rispondere Accetto / Forse / Rifiuto | `/game-nights/[id]` | ✅ | |
| GN-05 | votare un gioco candidato o ritirare il voto; l'host decide i pareggi | `/game-nights/[id]?tab=voting` | ✅ | Solo per le serate pubblicate. Vedi [ADR-074](../for-claude/architecture/adr/adr-074-voting-closure-mechanism.md). |
| GN-06 | (host) aggiungere un gioco, cosa che avvia una sessione, e chiudere la serata | `/game-nights/[id]` | ✅ | |
| GN-07 | condurre la serata dal vivo: avviare il gioco successivo, chiudere un gioco indicando il vincitore, finalizzare | `/game-nights/[id]/live` | ✅ | |
| GN-08 | vedere il riepilogo, caricare o eliminare foto (con estrazione del punteggio dalla foto), creare un link di condivisione, archiviare | `/game-nights/[id]/summary` | ✅ | |
| GN-09 | (ospite) vedere il riepilogo condiviso | `/game-nights/shared/[token]` | ✅ | |
| GN-10 | (ospite) rispondere a un invito ricevuto via email, con un nome facoltativo | `/join/event/[code]`, `/invites/[token]` | ✅ | `/invites/[token]` permette anche di annullare un rifiuto. |

## 11. Sessioni di gioco — `SES`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| SES-01 | elencare le sessioni e filtrarle per stato o testo | `/sessions` | ⚠️ | La scheda «Storico» usa `?tab=history`, ma la vista filtra su `?status=`. Non è verificato che funzioni. |
| SES-02 | creare una sessione: gioco, documenti KB, giocatori, ordine di turno, fasi | `/sessions/new` | ❌ | Il wizard (gioco → dimensioni di punteggio → giocatori → riepilogo) crea una `live-session` e porta a `/sessions/{id}`, che **mostra un errore**: «API /api/v1/sessions/{id} failed with status 404». Quella pagina, come `/scoreboard` e `/notes`, legge un aggregato diverso da quello appena creato. Funziona solo `/sessions/{id}/live`. Il wizard chiede dimensioni (nome e unità), non il tipo di punteggio. |
| SES-03 | entrare in una sessione con un codice | `/sessions/join` | ✅ | |
| SES-04 | segnare i punti con l'editor adatto al tipo di punteggio: punti, vittoria sì/no, obiettivi, classifica | `/sessions/[id]/live` | ⚠️ | `useUpdateSessionScores` ([ADR-089](../for-claude/architecture/adr/adr-089-session-scoring-ssot.md)). Su una sessione creata dal wizard la scheda Score resta su «Caricamento punteggi…» dopo 12 secondi, senza nessuna chiamata ai punteggi. La causa non è determinata. |
| SES-05 | (host) aggiungere un ospite per nome o un utente registrato | `/sessions/[id]/live` | ✅ | |
| SES-06 | scrivere note di diario | `/sessions/[id]/live` | ✅ | |
| SES-07 | fare domande all'AI sulle regole durante la partita, anche con una foto del tavolo | `/sessions/[id]/live` | ❌ | Il pannello mostra «Impossibile avviare l'assistente. Riprova più tardi.». `GET /api/v1/games/{id}/agents` risponde `200`, ma la risposta fallisce la validazione di `AgentDtoSchema`, che richiede ancora `type`, tolto dal backend in #4147 (vedi [§ Lacune](#lacune-da-decidere), punto 0). Il progetto prevede risposte ancorate al manuale ([ADR-090](../for-claude/architecture/adr/adr-090-in-session-grounded-answer-ownership.md)). L'espansione della domanda dalla foto è dietro il flag `rag.live-vision-query-expansion`, spento di default. |
| SES-08 | aprire una disputa sulle regole nella scheda «Arbitro» | `/sessions/[id]/live` | ✅ | |
| SES-09 | usare gli strumenti al tavolo: generatore casuale, risorse, segnapunti, gestore turni, note, lavagna | `/sessions/[id]/live` | ✅ | |
| SES-10 | usare il tracker di stato del proprio gioco | `/sessions/[id]/live` | ⚠️ | Esiste solo per i giochi in `FlavorRenderer.tsx` (Catan, Codenames, Paleo, Power Grid, Puerto Rico, Wingspan, Zombicide). |
| SES-11 | tenere una galleria di foto della partita | `/sessions/[id]/live` | ⚠️ | La galleria è solo locale (IndexedDB). Le istantanee Vision passano dal backend. |
| SES-12 | mettere in pausa e riprendere | `/sessions/[id]/live` | ❌ | Nel browser `/live` non mostra nessun pulsante pausa. Il codice prevede un pulsante `aria-disabled` e la sola ripresa dell'host dall'overlay. La pausa funziona nelle pagine sessione del toolkit (TK-03). |
| SES-13 | chiudere la partita e salvarla come partita registrata | `/sessions/[id]/live` | ✅ | Il backend crea la partita registrata e «Salva partita» la apre. Non è stato provato nel browser. |
| SES-14 | vedere il riepilogo di fine partita | `/sessions/[id]` | ❌ | Su una sessione creata dal wizard la pagina dà l'errore 404 di SES-02. Il codice mostra achievement di prova (`sessionSummaryFixtures`; «no backend endpoint v1»), e «Condividi» copia solo l'URL. |
| SES-15 | prendere appunti personali sulla sessione | `/sessions/[id]/notes` | ❌ | Su una sessione creata dal wizard la pagina dà l'errore 404 di SES-02. Il codice salva gli appunti solo nel `localStorage`. |
| SES-16 | vedere il tabellone dei punteggi | `/sessions/[id]/scoreboard` | ❌ | Su una sessione creata dal wizard la pagina dice «Impossibile caricare la sessione» (404, come SES-02). Il codice ordina i giocatori per ordine di turno, non per punteggio. |
| SES-17 | (host) invitare con QR o link e confermare i punteggi proposti dagli ospiti | — | 👻 | `InviteModal`, `QrInviteSheet`, `ScoreAssistant` e `ScoreProposalCard` non sono montati. `sessionInvites.createInvite` non viene mai chiamato dalla UI. |
| SES-18 | (ospite) entrare con un nome e proporre un punteggio | `/join/[token]` | ⚠️ | La pagina c'è, ma nessuna UI genera il token (SES-17). `/sessions/[id]/join?token=` fa lo stesso, però sta dietro il login. |
| SES-19 | (ospite) vedere il tabellone in sola lettura | `/join/session/[code]` | ✅ | |

## 12. Partite registrate — `REC`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| REC-01 | elencare le partite registrate e vedere le statistiche | `/play-records`, `?tab=stats` | ✅ | `/play-records/stats` rimanda alla scheda. |
| REC-02 | registrare una partita a mano: gioco, giocatori, un punteggio a testa | `/play-records/new` | ⚠️ | Si registra solo la prima dimensione di punteggio. I dati si possono precompilare da `?gameNightId`. |
| REC-03 | vedere classifica e dettaglio, aggiungere foto, ripristinare una versione precedente | `/play-records/[id]` | ✅ | |
| REC-04 | creare o revocare un link di condivisione | `/play-records/[id]` | ✅ | |
| REC-05 | modificare data, note e luogo, oppure eliminare la partita | `/play-records/[id]/edit` | ⚠️ | Giocatori e punteggi non si possono modificare. |
| REC-06 | (ospite) vedere una partita condivisa | `/play-records/shared/[token]` | ✅ | |

## 13. Toolkit — `TK`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| TK-01 | aprire il toolkit dalla barra di navigazione principale | `/toolkit` | 🚧 | Mostra «Toolkit in arrivo», ma è una delle voci di `TOP_BAR_NAV_IDS`. |
| TK-02 | usare dadi, timer, contatore e randomizzatore senza una sessione | `/toolkit/play` | ⚠️ | Il registro resta solo nel browser. |
| TK-03 | avviare una sessione toolkit da un gioco della libreria: punteggio per round o categoria, pausa, chiusura, sincronizzazione live | `/library/[gameId]/toolkit`, `…/toolkit/[sessionId]` | ✅ | Si apre da `GameTableDrawer`, `GameTableZoneTools` e `LibroGameDetailView`. |
| TK-04 | rivedere le sessioni toolkit passate e le statistiche | `/toolkit/history`, `/toolkit/stats` | ✅ | Sola lettura. |
| TK-05 | usare un modello di toolkit approvato | `/toolkit/templates` | 🚧 | Il pulsante «usa» è disabilitato. |
| TK-06 | sfogliare i toolkit della community | `/toolkits`, `/toolkits/[id]` | ⚠️ | In sola lettura. Le schede agente, KB, versioni e valutazioni sono disabilitate. `useInstallToolkit` non è usato. |
| TK-07 | configurare il toolkit di un gioco privato e farsene proporre uno dall'AI | `/library/private/[id]/toolkit/configure` | 👻 | Funziona, ma non è stato trovato nessun link che ci porti. |
| TK-08 | usare il toolkit con turni, dadi, lavagna, mazzi e contatori personalizzati | `/toolkit/[sessionId]` | 👻 | Nessuna pagina porta qui. |
| TK-09 | usare la scheda Toolbox del gioco | `/library/[gameId]?tab=toolbox` | 🚧 | `/library/[gameId]/toolbox` rimanda qui. |

## 14. Giocatori e achievement — `PLY`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| PLY-01 | vedere l'elenco dei giocatori con cui ha giocato | `/players` | ❌ | Le righe sono **nomi di giochi**: `transformStatsToItems` costruisce l'elenco da `gamePlayCounts` (`lib/players/players-filters.ts`). |
| PLY-02 | vedere il profilo, i giochi, le sessioni e le statistiche di un giocatore | `/players/[id]/…` | ❌ | Tutte le viste mostrano i dati dell'utente corrente: l'id nell'URL è in gran parte ignorato. La scheda Toolkit è un segnaposto. |
| PLY-03 | vedere i propri badge | `/players/[id]/achievements`, `/profile` | ✅ | `api.badges.getMyBadges` |
| PLY-04 | vedere la classifica globale dei badge | — | 👻 | `LeaderboardTable` (`api.badges.getLeaderboard`) non è montata. Le classifiche per gioco invece ci sono (GAM-04). |

## 15. Area pubblica — `PUB`

| ID | Il visitatore può… | Dove | Stato | Note |
|---|---|---|---|---|
| PUB-01 | leggere la landing e andare alla registrazione | `/` | ✅ | Chi ha già fatto l'accesso viene mandato su `/library`. |
| PUB-02 | leggere come funziona l'app e provare la demo delle citazioni | `/how-it-works`, `/how-it-works/game-comprehension` | ✅ | La demo usa dati di esempio fissi (Catan). |
| PUB-03 | cercare tra le FAQ e filtrarle per categoria | `/faq` | ✅ | I dati sono statici e la ricerca avviene nel browser. |
| PUB-04 | inviare un messaggio di contatto | `/contact` | ❌ | `SendContactMessageCommandHandler` scrive una riga di log e restituisce un Guid: non manda email e non salva niente. I pulsanti Twitter e Discord non portano da nessuna parte. |
| PUB-05 | chiedere la rimozione di un contenuto per copyright | `/legal/takedown` | ⚠️ | Apre una `mailto:` o copia il testo. Non c'è un endpoint. |
| PUB-06 | consultare i piani e sceglierne uno | `/pricing` | ❌ | Free, Pro e Team sono scritti nel codice. «Scegli Pro» porta a `/register?plan=pro`, ma la registrazione ignora `plan`. Non esiste nessun codice di pagamento. |
| PUB-07 | iscriversi alla lista d'attesa | `/join` | ✅ | `POST /api/v1/waitlist`. Senza risposta, la posizione mostrata ripiega su un numero fisso. |
| PUB-08 | leggere privacy, termini, cookie e note legali, in italiano o in inglese | `/privacy`, `/terms`, `/cookies`, `/legal` | ✅ | Le pagine legali hanno un loro selettore IT/EN. |
| PUB-09 | usare l'app senza connessione | `/offline` | ⚠️ | Il service worker è registrato e mostra `/offline`. Il prompt di installazione è spento (`showInstallPrompt={false}`); il browser permette comunque di installarla dal suo menu. |

## 16. Funzioni trasversali — `X`

| ID | L'utente può… | Dove | Stato | Note |
|---|---|---|---|---|
| X-01 | cercare ovunque con Cmd/Ctrl+K | ovunque | ❌ | La palette si apre, ma riceve `dataSources={{}}` (`app/providers.tsx`) e quindi non trova mai niente. |
| X-02 | passare dal tema chiaro a quello scuro | menu utente | ✅ | Solo dopo l'accesso. |
| X-03 | cambiare lingua dell'app | — | ⛔ | Esistono solo `it` ed `en`, scelte in base al browser. Fuori dalle pagine legali non c'è un selettore (vedi SET-07). |
| X-04 | vedere i propri consumi rispetto ai limiti del piano | — | 👻 | `UsageWidget`, `QuotaStatusBar`, `LibraryQuotaBadge`, `SessionQuotaBar` e `DowngradeTierModal` non sono montati. `GET /users/me/usage` non ha una UI. Le uniche quote visibili sono quella delle chat (AI-01) e quella del librogame (GBK-01). |

---

## Verifica nel browser (2026-10-10)

**Su cosa.** Stack locale (`make dev`) con le immagini `api` e `web` ricostruite da `main-dev` a `f8829d954`, perché quelle presenti erano del 2026-10-07, prima di #4138. Account `User` creato dal browser: registrazione, link di verifica letto da Mailpit, onboarding. Gli strumenti sono stati il server Playwright MCP e, dopo tre cadute del server, script Playwright autonomi.

**Le cadute erano dell'harness, non dell'app.** Una sonda con un heartbeat in pagina ogni 100 ms ha misurato un ritardo sempre sotto i 100 ms, prima e dopo le stesse azioni (digitare nella palette, attivare il push, salvare).

**Esiti**

| Esito | ID |
|---|---|
| ❌ o 👻 confermato | AI-03 (body di `POST /chat-threads` senza persona; il thread mostra «Auto (Orchestrator)»), AI-04 senza gioco, AI-13, CAT-06, NOT-05, PLY-01, PLY-02, PUB-04 (`200` e «Messaggio inviato con successo!», nessuna mail in Mailpit, solo una riga di log), PUB-05, PUB-06, SET-10, SES-12, X-01 («Azul» → «No results found.») |
| ⚠️ o 🚧 confermato | CAT-01 (ricerca «non ancora disponibile», eventi «Phase 0.5»), CAT-02, NOT-03 (push attivo di default su più eventi, permesso del browser mai chiesto), SET-03, SET-09, TK-01, TK-02 (nessuna chiamata API), il redirect `/badges` → panoramica, i redirect `/library/propose` (404) e `/library/proposals` |
| ✅ confermato | ACC-01, ACC-03, ACC-06, HOME-02, AI-05 e AI-06 (dopo la prova di controllo di AI-04), CAT-03, GAM-01 (nessun indicatore di quota visibile), `/pipeline-builder` aperto a un `User` |
| Diverso da quanto letto nel codice | SET-07 (non «salvata ma ignorata»: il salvataggio fallisce con `422`), SES-02, SES-14, SES-15 e SES-16 (errore 404 sulle sessioni create dal wizard), SES-07 (regressione dello schema agenti), HOME-01 (ricerca limitata alla prima pagina), AI-02 («I miei giochi» = solo giochi privati), AI-04 con gioco (bloccato da AI-15), ACC-04 (pulsanti OAuth sempre visibili in `/login`), GAM-05 (segnaposto `{title}` non interpolato) |
| Non provato | ACC-08, GAM-10, LIB-06, GBK-01, REC-02, REC-05, SES-01, TK-05 e TK-06 (catalogo vuoto in locale), i casi da ospite (SES-18, SES-19, GN-09, GN-10) |

**Rumore locale, non difetti del prodotto.** La CSP (`img-src 'self' data: https:`) blocca le copertine servite da MinIO su `http://localhost:9000`. All'avvio il seed ha ricaricato su MinIO i PDF che mancavano e li ha rimessi in coda di elaborazione.

## Superfici che non sono per l'utente

| Rotta | Chi | Note |
|---|---|---|
| `/upload` | Editor, Admin | Caricamento PDF a blocchi, con parse e pubblicazione delle rule spec. |
| `/editor` | Editor, Admin | Voce «Editor Regole» (`minRole: 'editor'`). |
| `/versions` | lettura per tutti gli utenti, ripristino per Editor e Admin | |
| `/pipeline-builder` | qualsiasi utente | Né il frontend né il backend controllano il ruolo: `RagPipelineEndpoints.cs` chiede solo una sessione attiva, e le pipeline vengono salvate per utente (`ListUserPipelinesQuery`). Di fatto è una funzione utente a cui la navigazione non porta (vedi sotto). |

## Lacune da decidere

Sono i casi ❌ e 👻 che hanno un effetto sull'utente. Per ciascuno c'è una scelta da fare: collegarlo o toglierlo. Nessuno è ancora tracciato da questo documento.

0. **Bloccanti emersi dalla prova nel browser**, in ordine di gravità:
   - [#4155](https://github.com/meepleAi-app/meepleai-monorepo/issues/4155): **un utente normale non può interrogare l'AI su un gioco del catalogo** (AI-04). Serve la dichiarazione di possesso (AI-15), che nessuna pagina offre, e l'errore mostrato è il messaggio grezzo «Accesso RAG non autorizzato».
   - [#4156](https://github.com/meepleAi-app/meepleai-monorepo/issues/4156): **le sessioni create dal wizard portano a una pagina d'errore** (SES-02). `/sessions/{id}`, `/scoreboard` e `/notes` leggono `/api/v1/sessions/{id}` (404), mentre il wizard crea una `live-session`.
   - [#4154](https://github.com/meepleAi-app/meepleai-monorepo/issues/4154): **regressione di #4147 su `main-dev`**. Il backend ha tolto `type` da `AgentDto`, ma `AgentDtoSchema` lo richiede ancora, quindi ogni lista di agenti fallisce la validazione. L'assistente in sessione non parte (SES-07) e la libreria perde l'agente di sistema (`/api/v1/agents?scope=my-library`, `useHybridHubItems`).
   - [#4157](https://github.com/meepleAi-app/meepleai-monorepo/issues/4157): **le preferenze non si salvano** (SET-07), con un `422` per `dataRetentionDays` mancante.
   - [#4158](https://github.com/meepleAi-app/meepleai-monorepo/issues/4158): **`api.games.getAll` ignora `search`, `page` e `pageSize`**, quindi l'onboarding trova solo la prima pagina del catalogo (HOME-01).
1. **Promesse che non vengono mantenute** (❌): la persona della chat (AI-03), la lingua salvata (SET-07), il consenso ai cookie (SET-10), Slack (NOT-05), il modulo contatti (PUB-04), i piani (PUB-06), la ricerca globale (X-01), l'elenco dei giocatori (PLY-01, PLY-02), il tabellone (SES-16), la pausa (SES-12).
2. **Flussi interrotti a metà**: un ospite può entrare in una sessione con un link (SES-18), ma nessuna UI genera il link (SES-17). Una libreria condivisa si può aprire (LIB-09), ma nessuna UI crea il link (LIB-08).
3. **Operazioni di base sulla libreria irraggiungibili**: togliere un gioco, note, etichette (LIB-07).
4. **Account**: non si può cambiare la password (ACC-09), né cancellare l'account o esportare i dati (ACC-11).
5. **Voce di navigazione principale su un segnaposto**: `/toolkit` (TK-01).
6. **Funzione senza destinatario dichiarato**: `/pipeline-builder` è aperto a ogni utente con una sessione attiva, sia nel frontend sia nel backend, ma non compare nella navigazione. Va deciso se è una funzione utente o uno strumento da riservare allo staff.
7. **Redirect obsoleti in `next.config.js`**:
   - `/library/games/:id/*` porta su `?tab=faq|reviews|rules|sessions|strategies|agent`, che non sono `GameTabId` validi, quindi si finisce su Info.
   - `/library/propose` porta a `/discover/propose`, che non esiste.
   - `/library/proposals` porta a `/discover?tab=proposals`, ma Discover non ha schede.
   - `/badges` porta a `?tab=badges`, che non è un tab del profilo.
8. **Residui dell'agente per gioco** (#4138):
   - la scheda «agenti» della scheda pubblica (CAT-04) e le schede agente di `/toolkits/[id]` (TK-06);
   - la riga «Agenti più installati» di Discover;
   - la sezione «Giochi con agente pronto» della dashboard;
   - i testi della waitlist `/join` («un esperto dedicato per ogni gioco», «Quale gioco vorresti un agente per?»).
