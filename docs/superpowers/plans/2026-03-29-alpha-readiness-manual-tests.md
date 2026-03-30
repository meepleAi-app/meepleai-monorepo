# MeepleAI Alpha Readiness — Sequenza di Test Manuali

**Data:** 2026-03-29
**Scopo:** Validare che l'app sia pronta per il testing alpha da parte di utenti esterni
**Ambiente target:** Staging (`https://meepleai.app`)
**Durata stimata:** ~3–4 ore (tester singolo)

---

## Legenda Priorità

| Simbolo | Priorità | Significato |
|---------|----------|-------------|
| 🔴 P0 | **Bloccante** | L'alpha NON può partire se fallisce |
| 🟠 P1 | **Alta** | Deve funzionare prima dell'alpha |
| 🟡 P2 | **Media** | Importante ma non blocca l'alpha |
| 🟢 P3 | **Bassa** | Nice-to-have, non blocca |

---

## Prerequisiti

Prima di iniziare, assicurarsi di avere:
- [ ] Accesso all'ambiente staging (`https://meepleai.app`)
- [ ] Un account **Admin/SuperAdmin** attivo su staging
- [ ] Un file PDF di regole di un gioco da tavolo (es. Catan, Ticket to Ride) pronto per l'upload
- [ ] Un secondo indirizzo email per testare la registrazione
- [ ] Un dispositivo mobile o browser in modalità responsive per i test mobile
- [ ] Accesso a una inbox email per verificare le email transazionali

---

## SEZIONE 1 — AUTENTICAZIONE 🔴 P0

### AUTH-001 — Registrazione nuovo utente

**Pre-condizioni:** Nessuna sessione attiva
**Priorità:** 🔴 P0

1. Navigare su `https://meepleai.app`
2. Cliccare su "Register" / "Sign Up"
3. Inserire un'email valida non ancora usata
4. Inserire una password che rispetti i requisiti (es. min 8 caratteri)
5. Cliccare "Register"

**Risultato atteso:**
- ✅ Redirect alla pagina di verifica email o dashboard
- ✅ Nessun errore visibile
- ✅ Se richiesta verifica email: email ricevuta nella inbox entro 2 minuti

---

### AUTH-002 — Verifica email

**Pre-condizioni:** Account registrato, email di verifica ricevuta
**Priorità:** 🔴 P0

1. Aprire l'email di verifica
2. Cliccare sul link di verifica
3. Verificare il redirect all'app

**Risultato atteso:**
- ✅ Pagina "Verifica completata" o redirect alla dashboard
- ✅ Account risulta verificato

---

### AUTH-003 — Login con credenziali valide

**Pre-condizioni:** Account verificato esistente
**Priorità:** 🔴 P0

1. Navigare su `/login`
2. Inserire email e password corretti
3. Cliccare "Login"

**Risultato atteso:**
- ✅ Redirect alla dashboard `/dashboard`
- ✅ Avatar/nome utente visibile in header
- ✅ Nessun errore

---

### AUTH-004 — Login con credenziali errate

**Pre-condizioni:** Nessuna
**Priorità:** 🔴 P0

1. Navigare su `/login`
2. Inserire email valida ma password sbagliata
3. Cliccare "Login"

**Risultato atteso:**
- ✅ Messaggio di errore chiaro (es. "Credenziali non valide")
- ✅ Non viene effettuato il login
- ✅ Nessun crash o errore 500

---

### AUTH-005 — Logout

**Pre-condizioni:** Sessione attiva
**Priorità:** 🔴 P0

1. Essere loggati
2. Cliccare su avatar/menu utente
3. Cliccare "Logout"

**Risultato atteso:**
- ✅ Redirect alla pagina di login
- ✅ Accedendo a `/dashboard` si viene reindirizzati al login
- ✅ Cookie/sessione invalidati

---

### AUTH-006 — Reset password

**Pre-condizioni:** Account esistente
**Priorità:** 🟠 P1

1. Andare su `/login`
2. Cliccare "Forgot password"
3. Inserire l'email dell'account
4. Cliccare "Send reset link"
5. Aprire l'email ricevuta e cliccare il link
6. Inserire una nuova password
7. Confermare la nuova password
8. Cliccare "Reset password"
9. Fare login con la nuova password

**Risultato atteso:**
- ✅ Email di reset ricevuta entro 2 minuti
- ✅ Il link funziona e porta al form di reset
- ✅ Dopo il reset, login con la nuova password funziona

---

### AUTH-007 — Protezione route autenticate

**Pre-condizioni:** Nessuna sessione
**Priorità:** 🔴 P0

1. Senza essere loggati, navigare direttamente a `/dashboard`
2. Navigare direttamente a `/library`
3. Navigare direttamente a `/chat`

**Risultato atteso:**
- ✅ Redirect a `/login` per tutte le route protette
- ✅ Nessuna pagina mostra dati utente senza autenticazione

---

### AUTH-008 — Persistenza sessione

**Pre-condizioni:** Account loggato
**Priorità:** 🟠 P1

1. Effettuare login
2. Chiudere il tab del browser
3. Riaprire l'URL dell'app

**Risultato atteso:**
- ✅ Sessione ancora attiva (non richiede nuovo login)
- ✅ La dashboard si carica correttamente

---

## SEZIONE 2 — NAVIGAZIONE E DASHBOARD 🔴 P0

### NAV-001 — Dashboard principale

**Pre-condizioni:** Utente loggato
**Priorità:** 🔴 P0

1. Effettuare login e osservare la dashboard
2. Verificare che tutti i blocchi principali siano visibili
3. Cliccare su ogni item del menu di navigazione principale

**Risultato atteso:**
- ✅ Dashboard si carica senza errori
- ✅ Nessun errore 404 nei link di navigazione
- ✅ Voci disabilitate in alpha mode NON sono visibili (es. "Players" non deve comparire)
- ✅ Layout corretto sia desktop che mobile

---

### NAV-002 — Navigazione mobile

**Pre-condizioni:** Utente loggato, ridimensionare browser a 375px o usare devtools
**Priorità:** 🟠 P1

1. Ridimensionare il browser a larghezza mobile (375px)
2. Verificare che la navigazione sia utilizzabile
3. Aprire/chiudere il menu mobile
4. Navigare tra le sezioni principali

**Risultato atteso:**
- ✅ Menu hamburger/bottom bar funzionante
- ✅ Testo non troncato in modo illeggibile
- ✅ Bottoni abbastanza grandi per il touch
- ✅ Nessun overflow orizzontale

---

## SEZIONE 3 — CATALOGO GIOCHI 🔴 P0

### GAMES-001 — Browsing catalogo

**Pre-condizioni:** Utente loggato
**Priorità:** 🔴 P0

1. Navigare alla sezione giochi/catalogo
2. Verificare che la lista giochi si carichi
3. Scorrere la lista

**Risultato atteso:**
- ✅ Lista di giochi visibile con immagini, titoli e info base
- ✅ Loading state visibile durante il caricamento
- ✅ Almeno alcuni giochi presenti nel catalogo

---

### GAMES-002 — Ricerca giochi

**Pre-condizioni:** Catalogo con almeno alcuni giochi
**Priorità:** 🔴 P0

1. Usare la barra di ricerca nel catalogo
2. Cercare "Catan" (o un titolo presente nel catalogo)
3. Cercare una stringa che non esiste (es. "xyzabc123")

**Risultato atteso:**
- ✅ Risultati filtrati correttamente
- ✅ Ricerca con risultato vuoto mostra messaggio appropriato (non crash)
- ✅ Ricerca reattiva (no lag eccessivo)

---

### GAMES-003 — Dettaglio gioco

**Pre-condizioni:** Almeno un gioco nel catalogo
**Priorità:** 🔴 P0

1. Cliccare su un gioco dalla lista
2. Verificare la pagina di dettaglio
3. Navigare tra le tab disponibili (Rules, FAQs, Reviews, Sessions)

**Risultato atteso:**
- ✅ Pagina dettaglio si carica correttamente
- ✅ Informazioni base del gioco visibili (titolo, immagine, players, playtime)
- ✅ Tab navigabili senza errori (anche se vuote)

---

### GAMES-004 — Filtraggio giochi

**Pre-condizioni:** Catalogo con giochi
**Priorità:** 🟠 P1

1. Applicare filtro per numero di giocatori (es. 2-4)
2. Applicare filtro per durata
3. Rimuovere i filtri

**Risultato atteso:**
- ✅ Filtri applicati correttamente
- ✅ Lista aggiornata in base ai filtri
- ✅ Reset filtri funzionante

---

## SEZIONE 4 — LIBRERIA UTENTE 🔴 P0

### LIB-001 — Visualizzazione libreria

**Pre-condizioni:** Utente loggato
**Priorità:** 🔴 P0

1. Navigare su `/library`
2. Verificare le tab disponibili: Collection, Private, Wishlist
3. Verificare che la tab "Proposals" NON sia visibile (disabilitata in alpha)

**Risultato atteso:**
- ✅ Tab Collection, Private, Wishlist visibili
- ✅ Tab Proposals assente
- ✅ Libreria vuota mostra stato vuoto appropriato (non errore)

---

### LIB-002 — Aggiunta gioco alla libreria

**Pre-condizioni:** Utente loggato, catalogo con giochi
**Priorità:** 🔴 P0

1. Navigare al catalogo e aprire la pagina di un gioco
2. Cliccare "Add to Library" / "Add to Collection"
3. Navigare su `/library` → tab Collection

**Risultato atteso:**
- ✅ Il gioco appare nella libreria
- ✅ Feedback visivo di conferma (toast/notifica)
- ✅ Bottone "Add" diventa "Remove" o stato cambiato

---

### LIB-003 — Rimozione gioco dalla libreria

**Pre-condizioni:** Almeno un gioco nella libreria
**Priorità:** 🟠 P1

1. Navigare su `/library`
2. Trovare un gioco nella collezione
3. Rimuoverlo dalla libreria

**Risultato atteso:**
- ✅ Il gioco viene rimosso dalla lista
- ✅ Feedback visivo di conferma
- ✅ Lista aggiornata immediatamente

---

### LIB-004 — Aggiunta gioco alla wishlist

**Pre-condizioni:** Utente loggato, catalogo con giochi
**Priorità:** 🟠 P1

1. Aprire la pagina di un gioco dal catalogo
2. Cliccare "Add to Wishlist"
3. Navigare su `/library` → tab Wishlist

**Risultato atteso:**
- ✅ Gioco visibile nella wishlist
- ✅ Feedback visivo di aggiunta

---

### LIB-005 — Giochi privati

**Pre-condizioni:** Utente loggato
**Priorità:** 🟠 P1

1. Navigare su `/library` → tab Private
2. Creare un nuovo gioco privato con titolo e info base
3. Verificare che appaia nella lista

**Risultato atteso:**
- ✅ Form di creazione gioco privato funzionante
- ✅ Gioco privato visibile nella tab Private
- ✅ Gioco privato NON visibile nel catalogo pubblico

---

## SEZIONE 5 — UPLOAD PDF E DOCUMENT PROCESSING 🔴 P0

### PDF-001 — Upload PDF (come Admin/Editor)

**Pre-condizioni:** Utente Admin o Editor loggato, file PDF pronto
**Priorità:** 🔴 P0

1. Navigare su `/upload`
2. Selezionare o trascinare un file PDF di regole (< 50MB)
3. Associare il PDF a un gioco esistente
4. Avviare l'upload

**Risultato atteso:**
- ✅ Upload completato senza errori
- ✅ Progress bar visibile durante upload
- ✅ Documento appare nella lista con stato "Processing" o "Uploaded"
- ✅ Nessun errore 4xx/5xx

---

### PDF-002 — Processing pipeline status

**Pre-condizioni:** PDF uploadato (PDF-001)
**Priorità:** 🔴 P0

1. Dopo l'upload, aspettare qualche minuto
2. Navigare su `/admin/knowledge-base/documents`
3. Verificare lo stato di processing del documento uploadato

**Risultato atteso:**
- ✅ Documento visibile nell'admin KB
- ✅ Stato cambia da "Uploaded" → "Extracting" → "Indexing" → "Indexed"
- ✅ Processo si completa senza stato "Failed" (entro ~5-10 min per documenti piccoli)

---

### PDF-003 — Validazione tipo file

**Pre-condizioni:** Utente Admin/Editor
**Priorità:** 🟠 P1

1. Tentare di uploadare un file non-PDF (es. `.txt`, `.jpg`)
2. Tentare di uploadare un PDF molto grande (> 50MB se disponibile)

**Risultato atteso:**
- ✅ File non-PDF: messaggio di errore chiaro, nessun upload
- ✅ File troppo grande: messaggio di errore con limite indicato
- ✅ Nessun crash dell'applicazione

---

### PDF-004 — Upload PDF su gioco privato

**Pre-condizioni:** Utente con gioco privato creato (LIB-005)
**Priorità:** 🟡 P2

1. Navigare al gioco privato
2. Caricare un PDF per quel gioco
3. Verificare che il processing avvenga correttamente

**Risultato atteso:**
- ✅ PDF associato al gioco privato
- ✅ Processing completato
- ✅ KB disponibile per chat su quel gioco

---

## SEZIONE 6 — RAG CHAT 🔴 P0

### CHAT-001 — Creazione nuovo thread di chat

**Pre-condizioni:** Utente loggato
**Priorità:** 🔴 P0

1. Navigare su `/chat`
2. Cliccare "New Chat" o "+"
3. Verificare che si apra un nuovo thread vuoto

**Risultato atteso:**
- ✅ Thread vuoto creato
- ✅ Interfaccia chat visibile con input
- ✅ Cronologia thread visibile a sinistra/lista

---

### CHAT-002 — Messaggio senza KB (query generica)

**Pre-condizioni:** Thread di chat aperto
**Priorità:** 🔴 P0

1. Nel thread di chat, inviare un messaggio generico come "Ciao, come stai?"
2. Attendere la risposta

**Risultato atteso:**
- ✅ Loading/streaming indicator visibile
- ✅ Risposta ricevuta dall'AI entro ~10 secondi
- ✅ Testo della risposta leggibile e formattato
- ✅ Nessun errore visibile

---

### CHAT-003 — Query RAG su gioco con PDF indicizzato

**Pre-condizioni:** PDF indicizzato per un gioco (PDF-002 completato con stato "Indexed")
**Priorità:** 🔴 P0

1. Aprire un thread di chat
2. Selezionare il gioco con PDF indicizzato (se l'UI lo permette)
3. Inviare una domanda specifica sulle regole del gioco (es. "Come si configura il gioco all'inizio?")
4. Verificare la risposta

**Risultato atteso:**
- ✅ Risposta pertinente alle regole del gioco
- ✅ Citazioni delle fonti visibili (es. "Fonte: Regole Catan, pag. 3")
- ✅ Nessun errore
- ✅ Streaming della risposta funzionante (testo appare progressivamente)

---

### CHAT-004 — Lista thread e cronologia

**Pre-condizioni:** Almeno un thread con messaggi
**Priorità:** 🟠 P1

1. Navigare su `/chat`
2. Verificare che i thread precedenti siano visibili
3. Aprire un thread precedente
4. Verificare che i messaggi precedenti siano presenti

**Risultato atteso:**
- ✅ Lista thread ordinata (più recente prima)
- ✅ Cronologia messaggi preservata nel thread
- ✅ Messaggi dell'utente e dell'AI distinti visivamente

---

### CHAT-005 — Eliminazione thread

**Pre-condizioni:** Almeno un thread di chat
**Priorità:** 🟡 P2

1. Navigare su `/chat`
2. Eliminare un thread dalla lista
3. Confermare l'eliminazione

**Risultato atteso:**
- ✅ Thread rimosso dalla lista
- ✅ Conferma richiesta prima dell'eliminazione
- ✅ Thread non più accessibile

---

### CHAT-006 — Comportamento con errore LLM

**Pre-condizioni:** Utente loggato
**Priorità:** 🟠 P1

1. Inviare un messaggio molto lungo o con caratteri speciali
2. Verificare il comportamento

**Risultato atteso:**
- ✅ Se errore: messaggio di errore user-friendly, non crash
- ✅ Se fallback attivo: indicazione visiva del fallback (es. "Using backup model")
- ✅ La chat rimane utilizzabile dopo l'errore

---

## SEZIONE 7 — SESSIONI DI GIOCO 🟠 P1

### SESSION-001 — Creazione sessione

**Pre-condizioni:** Almeno un gioco nel catalogo
**Priorità:** 🟠 P1

1. Navigare su `/sessions/new`
2. Selezionare un gioco
3. Aggiungere giocatori (almeno se stesso)
4. Salvare la sessione

**Risultato atteso:**
- ✅ Sessione creata con successo
- ✅ Redirect alla pagina della sessione
- ✅ Info sessione visibili (gioco, giocatori, data)

---

### SESSION-002 — Tracking punteggi

**Pre-condizioni:** Sessione creata (SESSION-001)
**Priorità:** 🟠 P1

1. Aprire una sessione attiva
2. Navigare al tab "Scoreboard"
3. Inserire punteggi per i giocatori

**Risultato atteso:**
- ✅ Punteggi inseriti e salvati
- ✅ Classifica aggiornata in tempo reale
- ✅ Nessun errore durante salvataggio

---

### SESSION-003 — Note sessione

**Pre-condizioni:** Sessione creata
**Priorità:** 🟡 P2

1. Aprire una sessione
2. Navigare al tab "Notes"
3. Aggiungere una nota

**Risultato atteso:**
- ✅ Nota salvata correttamente
- ✅ Nota persistente (visibile dopo refresh)

---

## SEZIONE 8 — PROFILO E IMPOSTAZIONI 🟠 P1

### PROFILE-001 — Visualizzazione profilo

**Pre-condizioni:** Utente loggato
**Priorità:** 🟠 P1

1. Navigare su `/profile`
2. Verificare le informazioni del profilo

**Risultato atteso:**
- ✅ Nome utente, email visibili
- ✅ Statistiche base (se disponibili)
- ✅ Nessun errore di caricamento

---

### SETTINGS-001 — Cambio password

**Pre-condizioni:** Utente loggato
**Priorità:** 🟠 P1

1. Navigare su `/settings/security`
2. Cambiare la password (vecchia + nuova)
3. Fare logout e login con la nuova password

**Risultato atteso:**
- ✅ Password cambiata con successo
- ✅ Login con vecchia password fallisce
- ✅ Login con nuova password funziona

---

### SETTINGS-002 — Setup 2FA (TOTP)

**Pre-condizioni:** Utente loggato senza 2FA attivo
**Priorità:** 🟡 P2

1. Navigare su `/settings/security`
2. Cliccare "Enable 2FA"
3. Scansionare il QR code con un'app TOTP (Google Authenticator, Authy)
4. Inserire il codice TOTP generato
5. Fare logout e login — verificare che 2FA venga richiesto

**Risultato atteso:**
- ✅ QR code visibile e scansionabile
- ✅ 2FA abilitato dopo verifica codice
- ✅ Al login successivo: richiesto codice TOTP
- ✅ Login con codice TOTP corretto funziona

---

### SETTINGS-003 — Preferenze notifiche

**Pre-condizioni:** Utente loggato
**Priorità:** 🟡 P2

1. Navigare su `/settings/notifications`
2. Modificare alcune preferenze
3. Salvare e ricaricare la pagina

**Risultato atteso:**
- ✅ Preferenze salvate persistentemente
- ✅ Impostazioni visibili dopo refresh

---

## SEZIONE 9 — PANNELLO ADMIN 🔴 P0

> Questi test richiedono un account Admin o SuperAdmin.

### ADMIN-001 — Accesso pannello admin

**Pre-condizioni:** Account Admin/SuperAdmin
**Priorità:** 🔴 P0

1. Fare login con account admin
2. Navigare su `/admin`
3. Verificare le sezioni visibili in alpha mode

**Risultato atteso:**
- ✅ Sezioni visibili: Overview, Content (filtrato), Users
- ✅ Sezioni assenti: AI/Agents, System Monitor, Analytics
- ✅ Nessun errore di caricamento

---

### ADMIN-002 — Protezione route admin per utenti normali

**Pre-condizioni:** Account utente normale (non admin)
**Priorità:** 🔴 P0

1. Fare login come utente normale
2. Tentare di accedere direttamente a `/admin`

**Risultato atteso:**
- ✅ Redirect o pagina 403/404
- ✅ Nessun dato admin esposto

---

### ADMIN-003 — Overview admin (sistema e attività)

**Pre-condizioni:** Account Admin
**Priorità:** 🟠 P1

1. Navigare su `/admin/overview`
2. Verificare la dashboard principale
3. Cliccare su "Activity" e "System"

**Risultato atteso:**
- ✅ Dashboard caricata con metriche base
- ✅ Feed attività visibile
- ✅ Stato sistema visibile (API health, DB, Redis)

---

### ADMIN-004 — Gestione utenti

**Pre-condizioni:** Account Admin
**Priorità:** 🟠 P1

1. Navigare su `/admin/users`
2. Verificare la lista utenti
3. Cercare un utente specifico
4. Aprire il dettaglio di un utente

**Risultato atteso:**
- ✅ Lista utenti caricata
- ✅ Ricerca funzionante
- ✅ Dettaglio utente accessibile
- ✅ Nessun dato sensibile esposto in modo improprio

---

### ADMIN-005 — Inviti utenti

**Pre-condizioni:** Account Admin
**Priorità:** 🟠 P1

1. Navigare su `/admin/users/invitations`
2. Creare un nuovo invito con email di test
3. Verificare che l'email di invito arrivi

**Risultato atteso:**
- ✅ Form invito funzionante
- ✅ Email di invito ricevuta
- ✅ Link nell'email porta alla registrazione

---

### ADMIN-006 — Gestione giochi nel catalogo

**Pre-condizioni:** Account Admin
**Priorità:** 🟠 P1

1. Navigare su `/admin/content/shared-games/all`
2. Verificare la lista giochi
3. Creare un nuovo gioco con info base
4. Verificare che appaia nel catalogo pubblico

**Risultato atteso:**
- ✅ Lista giochi caricata
- ✅ Creazione gioco funzionante
- ✅ Gioco visibile nel catalogo utenti

---

### ADMIN-007 — Import gioco da BGG

**Pre-condizioni:** Account Admin
**Priorità:** 🟠 P1

1. Navigare su `/admin/content/shared-games/import`
2. Inserire un BGG ID (es. 13 = Catan)
3. Avviare l'import

**Risultato atteso:**
- ✅ Dati BGG recuperati (titolo, immagine, info)
- ✅ Gioco importato nel catalogo
- ✅ Immagine da BGG visibile

---

### ADMIN-008 — Pannello Knowledge Base

**Pre-condizioni:** Account Admin, almeno un PDF in processing
**Priorità:** 🟠 P1

1. Navigare su `/admin/knowledge-base`
2. Verificare overview dei documenti
3. Navigare su `/admin/knowledge-base/documents`
4. Verificare stato dei documenti

**Risultato atteso:**
- ✅ Lista documenti visibile con stati
- ✅ Documenti in stato "Indexed" presenti
- ✅ Nessun documento bloccato in "Failed" inspiegabilmente

---

### ADMIN-009 — Upload PDF dall'admin

**Pre-condizioni:** Account Admin
**Priorità:** 🟠 P1

1. Navigare su `/admin/knowledge-base/upload`
2. Uploadare un PDF associato a un gioco
3. Monitorare il processing

**Risultato atteso:**
- ✅ Upload funzionante dall'admin panel
- ✅ Processing avviato automaticamente
- ✅ Status aggiornato nella lista documenti

---

## SEZIONE 10 — ACCESSIBILITÀ E UX TRASVERSALE 🟡 P2

### UX-001 — Messaggi di errore API

**Pre-condizioni:** Vari stati dell'app
**Priorità:** 🟡 P2

Testare i seguenti scenari di errore:
1. Perdita di connessione internet durante un'azione (disattivare WiFi)
2. Session scaduta (attendere scadenza o cancellare cookie manualmente)

**Risultato atteso:**
- ✅ Errori di rete: messaggio user-friendly, non codice di errore tecnico
- ✅ Session scaduta: redirect al login con messaggio esplicativo
- ✅ Nessun crash bianco (blank screen)

---

### UX-002 — Loading states

**Pre-condizioni:** Vari punti dell'app
**Priorità:** 🟡 P2

1. Su connessione lenta (throttle in devtools a "Slow 3G")
2. Navigare tra le sezioni principali

**Risultato atteso:**
- ✅ Skeleton loaders o spinner visibili durante caricamento
- ✅ Nessuna pagina bianca senza indicazione
- ✅ Layout non "balza" quando i dati arrivano (CLS accettabile)

---

### UX-003 — Notifiche e feedback

**Pre-condizioni:** Varie azioni completate
**Priorità:** 🟡 P2

Verificare feedback visivo per:
1. Aggiunta gioco alla libreria
2. Salvataggio impostazioni
3. Upload file completato
4. Eliminazione elemento

**Risultato atteso:**
- ✅ Toast/notifica visibile per ogni azione importante
- ✅ Notifiche spariscono automaticamente (o hanno X per chiuderle)
- ✅ Colori appropriati: verde per successo, rosso per errore

---

### UX-004 — Navigazione da tastiera

**Pre-condizioni:** Qualsiasi pagina
**Priorità:** 🟢 P3

1. Navigare l'app usando solo Tab, Enter, Esc
2. Verificare che focus ring sia visibile
3. Verificare che modali possano essere chiuse con Esc

**Risultato atteso:**
- ✅ Ordine Tab logico
- ✅ Focus ring visibile
- ✅ Modali chiudibili con Esc

---

## SEZIONE 11 — TEST DI SICUREZZA BASE 🔴 P0

### SEC-001 — HTTPS e cookie sicuri

**Pre-condizioni:** Staging environment
**Priorità:** 🔴 P0

1. Verificare che l'URL sia HTTPS (`https://meepleai.app`)
2. Aprire DevTools → Application → Cookies
3. Verificare i cookie di sessione

**Risultato atteso:**
- ✅ Certificato SSL valido (nessun warning browser)
- ✅ Cookie di sessione con flag `HttpOnly` e `Secure`
- ✅ Nessun token esposto in LocalStorage o URL

---

### SEC-002 — Accesso cross-account

**Pre-condizioni:** Due account utente separati (User A e User B)
**Priorità:** 🔴 P0

1. Loggarsi come User A, creare un gioco privato, notare l'ID
2. Loggarsi come User B
3. Tentare di accedere al gioco privato di User A tramite URL diretto

**Risultato atteso:**
- ✅ User B non può vedere il gioco privato di User A
- ✅ Risposta 403 o 404 (non i dati di User A)

---

### SEC-003 — XSS base

**Pre-condizioni:** Form di creazione (es. nome gioco privato)
**Priorità:** 🔴 P0

1. In un campo di testo (es. nome gioco privato), inserire: `<script>alert('xss')</script>`
2. Salvare e ricaricare la pagina

**Risultato atteso:**
- ✅ Nessun alert JavaScript eseguito
- ✅ Il testo viene mostrato come testo (escaped) o ignorato
- ✅ L'input viene sanitizzato

---

## SEZIONE 12 — PERFORMANCE BASE 🟡 P2

### PERF-001 — Tempo di caricamento iniziale

**Pre-condizioni:** Browser senza cache (Incognito mode)
**Priorità:** 🟡 P2

1. Aprire l'app in incognito
2. Cronometrare il tempo fino a "First Contentful Paint"
3. Verificare con Lighthouse in DevTools

**Risultato atteso:**
- ✅ FCP < 3 secondi su connessione normale
- ✅ Score Lighthouse Performance > 50 (accettabile per alpha)
- ✅ Nessuna risorsa 404 in console

---

### PERF-002 — Console errors

**Pre-condizioni:** Utente loggato, navigare l'app
**Priorità:** 🟠 P1

1. Aprire DevTools → Console
2. Navigare attraverso: Dashboard → Library → Chat → Profile → Admin
3. Verificare gli errori in console

**Risultato atteso:**
- ✅ Nessun errore rosso (error) nelle sezioni principali
- ✅ Warning accettabili (non critici)
- ✅ Nessuna chiamata API fallita visibile in Network tab

---

## CHECKLIST RIEPILOGATIVA

### Go/No-Go Criteri per Alpha

| ID | Test | Risultato | Bloccante |
|----|------|-----------|-----------|
| AUTH-001 | Registrazione | ⬜ | 🔴 |
| AUTH-003 | Login | ⬜ | 🔴 |
| AUTH-004 | Login errato | ⬜ | 🔴 |
| AUTH-005 | Logout | ⬜ | 🔴 |
| AUTH-007 | Route protette | ⬜ | 🔴 |
| NAV-001 | Dashboard carica | ⬜ | 🔴 |
| GAMES-001 | Catalogo visibile | ⬜ | 🔴 |
| GAMES-002 | Ricerca giochi | ⬜ | 🔴 |
| GAMES-003 | Dettaglio gioco | ⬜ | 🔴 |
| LIB-001 | Libreria visibile | ⬜ | 🔴 |
| LIB-002 | Aggiunta libreria | ⬜ | 🔴 |
| PDF-001 | Upload PDF | ⬜ | 🔴 |
| PDF-002 | Processing PDF | ⬜ | 🔴 |
| CHAT-001 | Nuovo thread chat | ⬜ | 🔴 |
| CHAT-002 | Risposta AI base | ⬜ | 🔴 |
| CHAT-003 | RAG query | ⬜ | 🔴 |
| ADMIN-001 | Accesso admin | ⬜ | 🔴 |
| ADMIN-002 | Protezione admin | ⬜ | 🔴 |
| SEC-001 | HTTPS + cookie | ⬜ | 🔴 |
| SEC-002 | Isolamento dati | ⬜ | 🔴 |
| SEC-003 | XSS base | ⬜ | 🔴 |
| PERF-002 | Console errors | ⬜ | 🟠 |

**Regola:** Tutti i test 🔴 devono essere ✅ per procedere all'alpha.

---

## Note per il Tester

1. **Registrare** ogni anomalia con: screenshot, URL, browser/OS, passi per riprodurre
2. **Non** testare feature non in alpha (vedremo comportamenti undefined)
3. **Usare** sempre dati realistici (non "aaa", "test123") — gli alpha tester useranno dati reali
4. **Testare** sia su desktop (Chrome, Firefox) che mobile (iOS Safari o Android Chrome)
5. **Appuntare** il tempo impiegato per ogni azione — se > 5s è un problema di UX da segnalare

---

*Generato con `/sc:spec-panel --focus testing ultrathink` | 2026-03-29*
