# UI Actions & Flows per Ruolo Utente

> **Data**: 2026-03-21 | **Fonte**: Codebase analysis (frontend + backend)

## Modello Ruoli & Tier

### Ruoli (RBAC)
| Ruolo | Livello | Eredita da |
|-------|---------|------------|
| **User** | 0 | - |
| **Editor** | 1 | User |
| **Creator** | 1 | User |
| **Admin** | 2 | Editor |
| **SuperAdmin** | 3 | Admin |

### Tier (Subscription)
| Tier | Limiti Giochi | Storage | Upload PDF/mese | Share Requests/mese |
|------|--------------|---------|-----------------|---------------------|
| **Free** | 50 | 100 MB | Base | 5 |
| **Normal** | 100 | 500 MB | Medio | 10 |
| **Premium/Pro** | 500 | 5 GB | Alto | 15 |
| **Enterprise** | Illimitato | Illimitato | Illimitato | Illimitato |

---

## Legenda

- **Contesto**: Dove si trova l'elemento UI (pagina/sezione)
- **Elemento UI**: Componente interattivo (bottone, form, dialog, toggle, ecc.)
- **Action**: Operazione API/client triggerata
- **Descrizione**: Cosa fa l'azione
- **Flow**: Sequenza completa dell'interazione

---

## 1. PUBBLICO (Anonimo - Nessuna autenticazione)

### Pagine Pubbliche

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/` (Home) | CTA "Inizia Gratis" | Navigate → `/register` | Porta alla registrazione | Click → Redirect `/register` → Form registrazione |
| `/` (Home) | CTA "Scopri di più" | Navigate → `/how-it-works` | Mostra come funziona | Click → Scroll/Navigate a sezione info |
| `/discover` | Search bar | `api.games.getAll({search})` | Cerca giochi nel catalogo pubblico | Input → Debounce → API call → Lista risultati |
| `/discover` | Filtri (categoria, giocatori, tempo) | `api.games.getAll({filters})` | Filtra catalogo giochi | Select filtro → API call → Lista aggiornata |
| `/discover` | Card gioco → Click | Navigate → `/discover/[gameId]` | Dettaglio gioco pubblico | Click card → Navigate → Load dettaglio |
| `/discover/[gameId]` | Tab Reviews | `api.games.getReviews(gameId)` | Visualizza recensioni | Click tab → API call → Lista recensioni |
| `/discover/[gameId]` | Tab FAQ | `api.games.getFaqs(gameId)` | Visualizza FAQ del gioco | Click tab → API call → Lista FAQ |
| `/contact` | Form contatto | `api.contact.submit({...})` | Invia messaggio di contatto | Compila form → Submit → API call → Conferma |
| `/library/shared/[token]` | Vista libreria condivisa | `api.shareLinks.getSharedLibrary(token)` | Visualizza libreria condivisa via link | Navigate con token → API call → Vista read-only |
| `/accept-invite` | Bottone "Accetta Invito" | `api.invitations.acceptInvite(token)` | Accetta invito registrazione | Click → Redirect login/register con invite token |
| Navbar | Link navigazione | Navigate | Naviga tra pagine pubbliche | Click → Client-side routing |
| Footer | Link social/policy | Navigate/External | Link a policy e social | Click → Navigate |

### Autenticazione

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/login` | Form email/password | `api.auth.login({email, password})` | Login con credenziali | Compila → Submit → API → Set cookie → Redirect `/dashboard` |
| `/login` | Bottone Google OAuth | `api.auth.login()` (OAuth) | Login con Google | Click → OAuth popup → Callback → Set cookie → Redirect |
| `/login` | Bottone Discord OAuth | `api.auth.login()` (OAuth) | Login con Discord | Click → OAuth popup → Callback → Set cookie → Redirect |
| `/login` | Bottone GitHub OAuth | `api.auth.login()` (OAuth) | Login con GitHub | Click → OAuth popup → Callback → Set cookie → Redirect |
| `/login` | Link "Password dimenticata" | Navigate → `/reset-password` | Avvia reset password | Click → Navigate → Form email |
| `/register` | Form registrazione | `api.auth.register({email, password, displayName})` | Registrazione nuovo utente | Compila → Submit → API → Email verifica → Redirect `/verification-pending` |
| `/reset-password` | Form email | `api.auth.requestPasswordReset(email)` | Richiede reset password | Submit → API → Email con link → Conferma UI |
| `/reset-password` | Form nuova password (con token) | `api.auth.confirmPasswordReset(token, newPassword)` | Conferma nuova password | Compila → Submit → API → Redirect `/login` |
| `/verify-email` | Auto-verifica (token URL) | `api.auth.verifyEmail(token)` | Verifica email da link | Load pagina → Auto API call → Redirect `/verification-success` |
| `/oauth-callback` | Auto-processo | `api.auth.handleOAuthCallback()` | Processa callback OAuth | Auto → Valida token → Set session → Redirect |
| `/login` (2FA) | Input codice 2FA | `api.auth.verify2FALogin(sessionToken, code)` | Verifica secondo fattore | Input 6 cifre → Submit → API → Set cookie → Redirect |

---

## 2. USER (Autenticato - Tier Free/Normal/Premium)

> Tutte le azioni di questa sezione sono disponibili a User, Editor, Admin e SuperAdmin.
> Le differenze di **tier** (Free/Normal/Premium) sono indicate dove rilevanti.

### Dashboard

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/dashboard` | Cards metriche | `api.dashboard.getMetrics()` | Mostra statistiche utente | Load → API call → Render metriche (giochi, sessioni, agenti) |
| `/dashboard` | Widget crediti/uso | `api.tiers.getUserTier()` | Mostra tier e crediti rimanenti | Load → API call → Render barra uso |
| `/dashboard` | Link "Upgrade" | Navigate → `/pricing` | Mostra piani disponibili | Click → Navigate → Pricing page |
| `/dashboard` | Widget attività recente | `api.auth.getUserActivity()` | Mostra timeline attività | Load → API call → Render timeline |
| `/dashboard` | Search globale | `api.games.getAll({search})` | Ricerca giochi dalla dashboard | Input → Debounce → API → Dropdown risultati |

### Libreria Giochi

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/library` | Bottone "Aggiungi Gioco" | Dialog → `api.library.addGame({gameId})` | Aggiunge gioco alla libreria | Click → Dialog ricerca → Seleziona → API call → Aggiorna lista |
| `/library` | Bottone "Rimuovi" (su card) | Confirm → `api.library.removeGame(gameId)` | Rimuove gioco dalla libreria | Click → Dialog conferma → API call → Aggiorna lista |
| `/library` | Checkbox selezione multipla → "Rimuovi selezionati" | `api.library.bulkRemoveGames([ids])` | Rimozione bulk dalla libreria | Seleziona → Click bulk action → Confirm → API call |
| `/library` | Toggle Preferito (cuore su card) | `api.library.updateGame({isFavorite})` | Segna/rimuovi dai preferiti | Click → API call → Toggle icona |
| `/library` | Drag & Drop cards | `api.library.reorderGames([ids])` | Riordina giochi in libreria | Drag → Drop → API call → Nuovo ordine |
| `/library` | Filtri (preferiti, genere, giocatori) | Client-side filter | Filtra vista libreria | Select → Filtra lista client-side |
| `/library` | Search bar | Client-side search | Cerca nella propria libreria | Input → Filtra lista client-side |
| `/library` | Tab "Wishlist" | `api.wishlist.getAll()` | Mostra lista desideri | Click tab → API call → Render wishlist |
| `/library` | Bottone "Aggiungi a Wishlist" | `api.wishlist.add(gameId)` | Aggiunge a wishlist | Click → API call → Aggiorna wishlist |
| `/library` | Bottone "Rimuovi da Wishlist" | `api.wishlist.remove(gameId)` | Rimuove da wishlist | Click → API call → Aggiorna wishlist |
| `/library/[gameId]` | Tab dettaglio | `api.games.getById(gameId)` | Dettaglio gioco con info complete | Navigate → API call → Render tabs |
| `/library/[gameId]` | Tab "Regole" | `api.games.getRuleSpec(gameId)` | Visualizza specifica regole | Click tab → API call → Render markdown |
| `/library/[gameId]` | Tab "FAQ" | `api.games.getFaqs(gameId)` | FAQ del gioco | Click tab → API call → Lista FAQ |
| `/library/[gameId]` | Tab "Strategie" | `api.games.getStrategies(gameId)` | Strategie suggerite | Click tab → API call → Lista strategie |
| `/library/[gameId]` | Tab "Sessioni" | `api.games.getGameSessions(gameId)` | Storico sessioni del gioco | Click tab → API call → Lista sessioni |
| `/library/[gameId]` | Bottone "Condividi" | `api.shareRequests.createShareRequest({gameId})` | Condividi gioco con altro utente | Click → Dialog → Seleziona utente → API call |
| `/library/[gameId]` | Bottone "Chiedi all'AI" | Navigate → `/library/games/[gameId]/agent` | Apri chat con agente AI del gioco | Click → Navigate → Chat interface |

**Limiti per Tier:**
| Azione | Free | Normal | Premium |
|--------|------|--------|---------|
| Max giochi in libreria | 50 | 100 | 500 |
| Share requests/mese | 5 | 10 | 15 |
| Storage documenti | 100 MB | 500 MB | 5 GB |

### Chat & AI Agent

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/chat` | Bottone "Nuovo Thread" | `api.chat.createThread({gameId, title})` | Crea nuova conversazione AI | Click → Dialog (seleziona gioco) → API call → Redirect a thread |
| `/chat` | Lista thread (sidebar) | `api.chat.getThreads()` | Lista conversazioni esistenti | Load → API call → Render lista thread |
| `/chat` | Click su thread | `api.chat.getMessages(threadId)` | Carica messaggi del thread | Click → API call → Render messaggi |
| `/chat` | Input messaggio + Invio | `api.chat.addMessage(threadId, {content})` | Invia messaggio all'agente AI | Type → Enter → API call (SSE stream) → Render risposta progressiva |
| `/chat` | Bottone "Modifica messaggio" | `api.chat.updateMessage(threadId, msgId, {content})` | Modifica messaggio inviato | Click edit → Modifica → Save → API call |
| `/chat` | Bottone "Elimina messaggio" | `api.chat.deleteMessage(threadId, msgId)` | Elimina un messaggio | Click → Confirm → API call → Rimuovi da lista |
| `/chat` | Bottone "Esporta Chat" | `api.chat.exportChat(threadId, format)` | Esporta conversazione (PDF/CSV) | Click → Select formato → API call → Download file |
| `/chat` | Bottone "Condividi Thread" | `api.shareLinks.createShareLink(threadId)` | Genera link condivisione | Click → API call → Mostra URL copiabile |
| `/chat` | Bottone "Revoca Link" | `api.shareLinks.revokeShareLink(linkId)` | Revoca link condivisione | Click → API call → Link disabilitato |
| `/chat` | Bottone "Chiudi Thread" | `api.chat.closeThread(threadId)` | Chiude il thread | Click → API call → Thread archiviato |
| `/chat` | Bottone "Riapri Thread" | `api.chat.reopenThread(threadId)` | Riapre thread chiuso | Click → API call → Thread riattivato |
| `/chat` | Switch agente | `api.chat.switchThreadAgent(threadId, agentId)` | Cambia agente nel thread | Select → API call → Nuovo agente attivo |
| `/library/games/[gameId]/agent` | Chat con agente specifico | `api.agents.invoke(agentId, {messages})` | Interagisce con AI del gioco | Input → API call (SSE) → Risposta streaming |
| `/library/games/[gameId]/agent` | Commento su regola | `api.chat.addCommentToMessage(threadId, msgId, text)` | Commenta su specifica regola citata | Click quote → Input → API call |

### Sessioni di Gioco

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/sessions` | Bottone "Nuova Sessione" | Dialog → `api.sessions.create({gameId, players})` | Crea nuova sessione di gioco | Click → Dialog (gioco + giocatori) → API call → Redirect a sessione |
| `/sessions` | Lista sessioni attive | `api.sessions.getAll()` | Mostra sessioni attive/completate | Load → API call → Render lista |
| `/sessions` | Card sessione → Click | `api.sessions.getById(sessionId)` | Dettaglio sessione | Click → Navigate → Load dettaglio |
| Sessione attiva | Bottone "Completa" | `api.sessions.complete(sessionId, {scores})` | Termina sessione con punteggi | Click → Dialog punteggi → API call → Sessione completata |
| Sessione attiva | Bottone "Pausa" | `api.sessions.pauseSession(sessionId)` | Mette in pausa la sessione | Click → API call → Stato aggiornato |
| Sessione attiva | Bottone "Riprendi" | `api.sessions.resumeSession(sessionId)` | Riprende sessione in pausa | Click → API call → Stato aggiornato |
| Sessione attiva | Bottone "Archivia" | `api.sessions.archive(sessionId)` | Archivia sessione senza completarla | Click → Confirm → API call |
| Sessione attiva | Input punteggio | `api.sessionTracking.recordScore(sessionId, {playerId, score})` | Registra punteggio giocatore | Input → API call → Aggiorna classifica |
| Sessione attiva | Bottone "Tira Dadi" | `api.sessionTracking.rollDice(sessionId, count)` | Tira dadi virtuali | Click → API call → Mostra risultato animato |
| Sessione attiva | Bottone "Pesca Carta" | `api.sessionTracking.drawCards(sessionId, count)` | Pesca carte dal mazzo | Click → API call → Mostra carte |
| Sessione attiva | Bottone "Mescola Mazzo" | `api.sessionTracking.shuffleDeck(sessionId)` | Mescola il mazzo | Click → API call → Conferma |
| Sessione attiva | Textarea note | `api.sessionTracking.addNote(sessionId, text)` | Aggiunge nota alla sessione | Type → Auto-save → API call |
| Sessione attiva | Timer start/stop | `api.sessionTracking.startTimer()` / `pauseTimer()` | Gestisce timer sessione | Click → API call → Timer UI aggiornato |
| Sessione attiva | Bottone "Aggiungi Giocatore" | `api.sessions.addPlayer(sessionId, playerId)` | Aggiunge giocatore alla sessione | Click → Select giocatore → API call |
| Sessione attiva | Bottone "Rimuovi Giocatore" | `api.sessions.removePlayer(sessionId, playerId)` | Rimuove giocatore | Click → Confirm → API call |
| Sessione attiva | Join via codice | `api.liveSessions.joinSession(sessionCode)` | Entra in sessione tramite codice | Input codice → API call → Redirect a sessione |

### Game Night

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/game-nights` | Bottone "Crea Game Night" | Dialog → `api.gameNights.create({title, date, ...})` | Crea evento serata giochi | Click → Form (titolo, data, giochi) → API call → Redirect |
| `/game-nights` | Lista game night | `api.gameNights.getAll()` | Mostra serate pianificate | Load → API call → Render lista |
| Game Night dettaglio | Bottone "Invita" | `api.invitations.sendInvitation(userId, nightId)` | Invita utente alla serata | Click → Search utente → API call → Invito inviato |
| Game Night dettaglio | Bottone "Accetta/Rifiuta" | `api.invitations.rsvp(invitationId, {status})` | RSVP all'invito | Click Accept/Decline → API call → Stato aggiornato |
| Game Night dettaglio | Bottone "Modifica" | `api.gameNights.update(nightId, {...})` | Modifica dettagli serata | Click → Form precompilato → Save → API call |
| Game Night dettaglio | Bottone "Elimina" | `api.gameNights.delete(nightId)` | Cancella serata | Click → Confirm → API call |
| Game Night dettaglio | "Aggiungi gioco a playlist" | `api.playlists.addGame(playlistId, gameId)` | Aggiunge gioco alla scaletta | Click → Select gioco → API call |
| Game Night dettaglio | Drag & drop playlist | `api.playlists.reorderGames(playlistId, [ids])` | Riordina scaletta giochi | Drag → Drop → API call |
| Game Night dettaglio | Ricerca BGG | `api.gameNightBgg.searchBgg(query)` | Cerca giochi su BGG per la serata | Input → API call → Risultati BGG |

### Giocatori

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/players` | Bottone "Aggiungi Giocatore" | Dialog → `api.players.create({name})` | Crea profilo giocatore | Click → Form → API call → Aggiorna lista |
| `/players` | Card giocatore → Click | Navigate → dettaglio | Visualizza stats giocatore | Click → Load stats e storico |
| `/players` | Bottone "Modifica" | Dialog → `api.players.update(playerId, {...})` | Modifica profilo giocatore | Click → Form → Save → API call |
| `/players` | Bottone "Rimuovi" | Confirm → `api.players.delete(playerId)` | Soft-delete giocatore (isActive=false) | Click → Confirm → API call |

### Knowledge Base & Upload

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/knowledge-base` | Vista documenti | `api.knowledgeBase.getStatus()` | Stato RAG e documenti | Load → API call → Render stato |
| `/upload` | Area drop/click file | `api.pdf.uploadPdf(file)` | Carica PDF regolamento | Drop/Click → Select file → API call → Progress bar |
| `/upload` | Progress bar upload | `api.pdf.getProcessingProgress(pdfId)` | Monitora elaborazione PDF | Poll periodico → Aggiorna barra → Completamento |
| `/upload` | Bottone "Annulla" | `api.pdf.cancelProcessing(pdfId)` | Cancella elaborazione in corso | Click → API call → Stop processing |
| `/upload` | Lista PDF caricati | `api.pdf.getAllPdfs()` | Mostra PDF caricati | Load → API call → Render lista |
| `/upload` | Bottone "Elimina PDF" | `api.pdf.deletePdf(pdfId)` | Elimina PDF caricato | Click → Confirm → API call |

**Limiti per Tier:**
| Azione | Free | Normal | Premium |
|--------|------|--------|---------|
| Upload PDF/mese | Base | Medio | Alto |
| Dimensione file max | Limitato | Medio | Grande |
| Storage totale | 100 MB | 500 MB | 5 GB |

### Discover & Catalogo

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/discover` | Grid/Lista giochi | `api.games.getAll({page, sort, filters})` | Esplora catalogo giochi | Load → API call → Render grid con paginazione |
| `/discover` | Bottone "Aggiungi alla Libreria" | `api.library.addGame({gameId})` | Aggiunge gioco alla propria libreria | Click → API call → Feedback "Aggiunto!" |
| `/discover` | Ordinamento (rating, nome, anno) | `api.games.getAll({sort})` | Riordina catalogo | Select → API call → Lista riordinata |
| `/discover/[gameId]` | Tab "Giochi Simili" | `api.games.getSimilarGames(gameId)` | Mostra giochi simili | Click tab → API call → Render suggerimenti |
| `/discover/[gameId]` | Bottone "Scrivi Recensione" | Dialog → `api.games.createReview(gameId, {...})` | Scrive recensione | Click → Form (rating + testo) → Submit → API call |

### Agenti AI

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/agents` | Lista agenti | `api.agents.getAll()` | Mostra agenti disponibili | Load → API call → Render lista agenti |
| `/agents` | Bottone "Crea Agente" | Dialog → `api.agents.create({name, gameId, typologyId})` | Crea nuovo agente AI | Click → Form → Select gioco + tipologia → API call |
| `/agents` | Card agente → "Testa" | `api.agents.invoke(agentId, {messages})` | Testa agente in chat | Click → Chat UI → Input → SSE stream risposta |
| `/agents` | Bottone "Configura" | Dialog → `api.agents.updateConfig(agentId, {...})` | Modifica configurazione agente | Click → Form config → Save → API call |
| `/agents` | Bottone "Elimina Agente" | Confirm → `api.agents.delete(agentId)` | Elimina agente | Click → Confirm → API call |
| `/toolkit` | Genera toolkit da KB | `api.gameToolkit.generateToolkitFromKb(gameId)` | Genera toolkit AI dal knowledge base | Click → API call (lungo) → Toolkit generato |

**Limiti per Tier:**
| Azione | Free | Normal | Premium |
|--------|------|--------|---------|
| Slot agenti | 3 | Medio | Illimitato |
| Query AI/mese | Limitato | Medio | Alto |

### Profilo & Impostazioni

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/profile` | Form nome/email | `api.auth.updateProfile({displayName, email})` | Aggiorna profilo | Modifica → Save → API call → Conferma |
| `/profile` | Upload avatar | `api.auth.uploadAvatar(file)` | Cambia foto profilo | Click → Select file → API call → Preview aggiornata |
| `/profile` | Toggle tema (dark/light/system) | `api.auth.updatePreferences({theme})` | Cambia tema UI | Click → API call → UI si aggiorna |
| `/profile` | Select lingua | `api.auth.updatePreferences({language})` | Cambia lingua interfaccia | Select → API call → Refresh UI |
| `/profile` | Toggle notifiche email | `api.auth.updatePreferences({emailNotifications})` | Abilita/disabilita email | Toggle → API call |
| `/profile` | Toggle visibilità profilo | `api.auth.updatePreferences({showProfile})` | Profilo pubblico/privato | Toggle → API call |
| `/profile` | Toggle visibilità attività | `api.auth.updatePreferences({showActivity})` | Attività pubblica/privata | Toggle → API call |
| `/profile` | Toggle visibilità libreria | `api.auth.updatePreferences({showLibrary})` | Libreria pubblica/privata | Toggle → API call |
| `/profile` | Slider data retention | `api.auth.updatePreferences({dataRetentionDays})` | Periodo conservazione dati | Slide → API call |
| `/profile` | Bottone "Cambia Password" | Dialog → `api.auth.changePassword(current, new)` | Cambia password | Click → Form → Submit → API call |
| `/profile` | Bottone "Abilita 2FA" | `api.auth.enable2FA()` | Attiva autenticazione a due fattori | Click → API call → Mostra QR code → Verifica codice |
| `/profile` | Bottone "Disabilita 2FA" | `api.auth.disable2FA()` | Disattiva 2FA | Click → Confirm → Input codice → API call |
| `/profile` | Bottone "Crea API Key" | `api.auth.createApiKey()` | Genera API key personale | Click → API call → Mostra key (una volta) |
| `/profile` | Bottone "Revoca API Key" | `api.auth.revokeApiKey(keyId)` | Revoca API key | Click → Confirm → API call |
| `/profile` | Lista sessioni attive | `api.auth.getActiveSessions()` | Mostra sessioni login attive | Load → API call → Render lista |
| `/profile` | Bottone "Revoca Sessione" | `api.auth.revokeSession(sessionId)` | Termina sessione specifica | Click → API call → Sessione terminata |
| `/profile` | Bottone "Revoca Tutte" | `api.auth.revokeAllSessions()` | Termina tutte le sessioni | Click → Confirm → API call → Logout |

### Badge & Gamification

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/badges` | Griglia badge | `api.badges.getAll()` | Mostra tutti i badge disponibili/ottenuti | Load → API call → Render griglia |
| `/badges` | Bottone "Reclama" (su badge sbloccato) | `api.badges.claimBadge(badgeId)` | Reclama badge guadagnato | Click → API call → Animazione ottenimento |
| `/badges` | Toggle mostra badge | `api.badges.toggleBadgeDisplay(badgeId)` | Mostra/nascondi badge sul profilo | Toggle → API call |
| `/badges` | Tab "Classifica" | `api.badges.getLeaderboard()` | Mostra classifica gamification | Click tab → API call → Render classifica |

### Notifiche

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| Header | Campanella notifiche | `api.notifications.getAll()` | Mostra notifiche recenti | Click → Dropdown → API call → Lista notifiche |
| Header | Badge conteggio | SSE stream | Conteggio notifiche non lette in real-time | Auto → SSE → Aggiorna badge |
| Dropdown notifiche | Click su notifica | `api.notifications.markNotificationRead(id)` | Segna come letta + naviga | Click → API call → Navigate a target |
| `/notifications` | Lista completa notifiche | `api.notifications.getAll()` | Tutte le notifiche con paginazione | Load → API call → Render lista completa |

### Pricing & Upgrade

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/pricing` | Card piano Free | Info | Mostra limiti piano Free | Render statico |
| `/pricing` | Card piano Pro | Navigate/CTA | Mostra vantaggi Pro e prezzo | Click "Upgrade" → Flow pagamento |
| `/pricing` | Card piano Enterprise | CTA "Contattaci" | Piano enterprise personalizzato | Click → Navigate a contatto |

### Altre Pagine Utente

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/play-records` | Lista sessioni recenti | `api.playRecords.getAll()` | Storico partite giocate | Load → API call → Render lista |
| `/onboarding` | Wizard step-by-step | `api.onboarding.completeStep(step)` | Guida primo utilizzo | Step 1 → ... → Step N → Complete |
| `/onboarding` | Bottone "Salta" | `api.onboarding.skip()` | Salta onboarding | Click → API call → Redirect dashboard |
| `/versions` | Changelog | Render statico | Mostra versioni e novità | Load → Render markdown |
| `/chess` | Scacchiera interattiva | Client-side + `api.agents.invoke()` | Gioca a scacchi con AI | Move → API call → AI risponde → Render mossa |
| `/n8n` | Iframe n8n | Navigate | Accede a workflow n8n | Load → Render iframe |
| `/pipeline-builder` | Builder drag & drop | Client-side state | Costruisce pipeline workflow | Drag → Drop → Connect → Save |

---

## 3. EDITOR (Ruolo Editor - Eredita tutto da User)

> Accesso aggiuntivo: creazione/gestione proposte agenti, editing contenuti

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/editor` | Dashboard editor | Load → API call | Mostra overview proposte e contenuti | Load → Render metriche editor |
| `/editor/dashboard` | Stats proposte | `api.agents.getProposals()` | Statistiche proposte agenti | Load → API call → Render stats |
| `/editor/agent-proposals` | Lista proposte | `api.agents.getProposals()` | Mostra tutte le proposte agente | Load → API call → Render lista |
| `/editor/agent-proposals/create` | Form creazione proposta | `api.agents.createProposal({name, gameId, typologyId, ...})` | Crea nuova proposta agente | Compila form → Submit → API call → Redirect a proposta |
| `/editor/agent-proposals/[id]/edit` | Form modifica proposta | `api.agents.updateProposal(id, {...})` | Modifica proposta esistente | Load → Modifica → Save → API call |
| `/editor/agent-proposals/[id]/test` | Chat test agente | `api.agents.invoke(agentId, {messages})` | Testa proposta agente in chat | Input → API call (SSE) → Risposta |
| `/editor/agent-proposals/[id]` | Bottone "Invia per Approvazione" | `api.agents.submitForApproval(proposalId)` | Sottomette proposta per review | Click → Confirm → API call → Stato "In Review" |
| Libreria (editor) | Bottone "Modifica Regole" | `api.games.acquireEditorLock(gameId)` → Edit | Modifica specifica regole gioco | Click → Lock acquisito → Editor markdown → Save → Release lock |
| Libreria (editor) | "Aggiungi FAQ" | `api.games.addFaq(gameId, {question, answer})` | Aggiunge FAQ al gioco | Click → Form → Submit → API call |
| Libreria (editor) | "Modifica FAQ" | `api.games.updateFaq(gameId, faqId, {...})` | Modifica FAQ esistente | Click → Form precompilato → Save → API call |
| Libreria (editor) | "Elimina FAQ" | `api.games.deleteFaq(gameId, faqId)` | Elimina FAQ | Click → Confirm → API call |
| Libreria (editor) | "Aggiungi Errata" | `api.games.addErrata(gameId, {title, content})` | Aggiunge errata corrige | Click → Form → Submit → API call |
| Libreria (editor) | "Aggiungi House Rule" | `api.games.addHouseRule(gameId, {...})` | Aggiunge regola casa | Click → Form → Submit → API call |

---

## 4. ADMIN (Ruolo Admin - Eredita tutto da Editor)

> Accesso aggiuntivo: dashboard admin completa, gestione utenti, configurazione sistema

### Admin Overview

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/overview` | Dashboard metriche | `api.admin.getDashboardMetrics()` | Panoramica sistema | Load → API calls parallele → Render widgets |
| `/admin/overview/activity` | Feed attività | `api.admin.getActivityFeed()` | Timeline attività globale | Load → API call → Render feed |
| `/admin/overview/system` | Health check | `api.admin.getServiceHealth()` | Stato salute servizi | Load → API call → Render indicatori verde/rosso |

### Admin - Gestione Giochi

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/shared-games/all` | Tabella giochi | `api.sharedGames.getAll({page, search, sort})` | Lista completa catalogo condiviso | Load → API call → Render tabella paginata |
| `/admin/shared-games/new` | Wizard creazione gioco | `api.games.create({...})` | Crea nuovo gioco da zero | Multi-step wizard → API call → Gioco creato |
| `/admin/shared-games/import` | Import da BGG | `api.bgg.searchBgg(query)` → `api.bgg.getGameDetails(bggId)` → create | Importa gioco da BoardGameGeek | Search → Select → Review dati → Confirm → API call |
| `/admin/shared-games/import` | Bulk import CSV | `api.admin.bulkImportGames(file)` | Importazione massiva da CSV | Upload CSV → Validazione → API call → Report risultati |
| `/admin/shared-games/categories` | Gestione categorie | CRUD categorie | Crea/modifica/elimina categorie giochi | Click → Form → API call |
| Admin gioco | Bottone "Modifica" | `api.games.update(gameId, {...})` | Modifica dettagli gioco | Click → Form precompilato → Save → API call |
| Admin gioco | Bottone "Elimina" | `api.games.delete(gameId)` | Elimina gioco dal catalogo | Click → Confirm → API call → Soft delete |
| Admin gioco | "Arricchisci da BGG" | `api.bgg.enrichFromBgg(gameId)` | Aggiorna dati da BGG | Click → API call → Dati aggiornati |

### Admin - Knowledge Base & Documenti

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/knowledge-base` | Overview KB | `api.knowledgeBase.getStatus()` | Stato generale knowledge base | Load → API call → Render metriche |
| `/admin/knowledge-base/documents` | Lista documenti | `api.documents.getAll()` | Tutti i documenti nel sistema | Load → API call → Tabella documenti |
| `/admin/knowledge-base/queue` | Coda processing | `api.pdf.getProcessingQueue()` | Documenti in coda di elaborazione | Load → Poll → Render coda con stati |
| `/admin/knowledge-base/vectors` | Collezioni vettoriali | `api.knowledgeBase.getCollections()` | Gestione collezioni embedding | Load → API call → Render collezioni |
| `/admin/knowledge-base/upload` | Upload & elabora | `api.pdf.uploadPdf(file)` | Carica e processa documento | Drop file → API call → Monitor progress |
| Admin documenti | "Reindicizza" | `api.documents.reindexDocument(docId)` | Rigenera embeddings documento | Click → API call → Progress → Completato |
| Admin documenti | "Imposta attivo per RAG" | `api.documents.setActiveForRag(docId)` | Abilita documento per ricerca RAG | Toggle → API call |
| Admin documenti | "Elimina documento" | `api.pdf.adminDeletePdf(pdfId)` | Elimina documento (admin) | Click → Confirm → API call |
| Admin documenti | "Bulk elimina" | `api.pdf.bulkDeletePdfs([ids])` | Eliminazione massiva | Seleziona → Click → Confirm → API call |
| Admin documenti | "Bulk reindicizza" | `api.pdf.bulkReindexPdfs([ids])` | Reindicizzazione massiva | Seleziona → Click → API call → Monitor progress |

### Admin - Agenti AI

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/agents` | Lista agenti | `api.agents.getAll()` | Tutti gli agenti nel sistema | Load → API call → Render tabella |
| `/admin/agents/new` | Form creazione | `api.agents.create({...})` | Crea agente con setup completo | Form multi-step → API call |
| `/admin/agents/definitions` | Definizioni agente | `api.agents.getTypologies()` | Gestione tipologie agente | Load → API call → Render lista |
| `/admin/agents/builder` | Builder visuale | Client-side state | Costruisce pipeline agente | Drag & drop → Configure → Save |
| `/admin/agents/models` | Modelli & Prompt | `api.admin.getPrompts()` | Gestione modelli e prompt di sistema | Load → API call → Render lista |
| `/admin/agents/models` | "Crea Prompt" | `api.admin.createPrompt({name, content})` | Crea nuovo prompt di sistema | Click → Form → Submit → API call |
| `/admin/agents/models` | "Modifica Prompt" | `api.admin.updatePrompt(id, {...})` | Modifica prompt esistente | Click → Form → Save → API call |
| `/admin/agents/models` | "Elimina Prompt" | `api.admin.deletePrompt(id)` | Elimina prompt | Click → Confirm → API call |
| `/admin/agents/models` | "Crea Versione" | `api.admin.createPromptVersion(promptId)` | Crea nuova versione prompt | Click → API call → Nuova versione |
| `/admin/agents/models` | "Attiva Versione" | `api.admin.activatePromptVersion(versionId)` | Attiva versione prompt | Click → API call → Versione attiva |
| `/admin/agents/chat-history` | Storico chat | `api.chatSessions.getChatSessions()` | Visualizza tutte le chat | Load → API call → Render lista |
| `/admin/agents/pipeline` | Pipeline explorer | Client-side visualization | Esplora pipeline RAG | Load → Render flowchart |
| `/admin/agents/debug` | Console debug | `api.agents.debugInvoke(...)` | Debug agente con logs dettagliati | Input → API call → Render logs + risposta |
| `/admin/agents/debug-chat` | Debug chat | `api.agents.debugChat(...)` | Chat con debug output | Input → API call (SSE) → Render con metadata |
| `/admin/agents/sandbox` | RAG sandbox | `api.sandbox.query(...)` | Testa query RAG isolatamente | Input → API call → Render chunks + scores |
| `/admin/agents/strategy` | Config strategia | `api.tierStrategy.getAll()` | Configura strategie per tier | Load → Form → Save → API call |
| `/admin/agents/chat-limits` | Limiti chat | `api.chat.updateChatHistoryLimits({...})` | Configura limiti conversazione | Form → Save → API call |
| `/admin/agents/usage` | Uso & Costi | `api.dashboard.getUsageStats()` | Dashboard costi AI | Load → API call → Render grafici |
| `/admin/agents/analytics` | Analytics AI | `api.analytics.getAIMetrics()` | Metriche performance AI | Load → API call → Render dashboard |
| `/admin/agents/metrics` | Metriche dettagliate | `api.analytics.getDetailedMetrics()` | Metriche granulari | Load → API call → Render grafici |
| `/admin/agents/wizard` | Wizard agente | Multi-step | Creazione guidata agente | Step 1→2→3 → API call → Agente creato |
| Admin proposte | "Approva Proposta" | `api.agents.approvePublication(proposalId)` | Approva proposta agente editor | Click → API call → Stato "Approved" |
| Admin proposte | "Rifiuta Proposta" | `api.agents.rejectPublication(proposalId, reason)` | Rifiuta con motivazione | Click → Dialog motivo → API call |
| Admin proposte | "Richiedi Modifiche" | `api.agents.requestChanges(proposalId, feedback)` | Richiedi modifiche a proposta | Click → Dialog feedback → API call |
| Admin tipologie | "Approva Tipologia" | `api.agents.approveTypology(typologyId)` | Approva nuova tipologia | Click → API call |
| Admin tipologie | "Rifiuta Tipologia" | `api.agents.rejectTypology(typologyId)` | Rifiuta tipologia | Click → API call |

### Admin - Gestione Utenti

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/users` | Tabella utenti | `api.admin.users.getAll()` | Lista tutti gli utenti | Load → API call → Render tabella paginata |
| `/admin/users` | Search utenti | `api.admin.users.search(query)` | Cerca utente | Input → API call → Risultati filtrati |
| `/admin/users` | "Crea Utente" | `api.admin.users.create({email, displayName, role})` | Crea nuovo utente manualmente | Click → Form → API call → Utente creato |
| `/admin/users` | "Modifica Ruolo" | `api.admin.users.changeUserRole(userId, newRole)` | Cambia ruolo utente | Click → Select ruolo → Confirm → API call |
| `/admin/users` | "Reset Password" | `api.admin.users.resetUserPassword(userId, newPwd)` | Reset password utente | Click → Dialog → API call → Password resettata |
| `/admin/users` | "Sospendi Utente" | `api.admin.users.suspendUser(userId)` | Sospende account utente | Click → Confirm → API call |
| `/admin/users` | "Riattiva Utente" | `api.admin.users.unsuspendUser(userId)` | Riattiva account sospeso | Click → API call |
| `/admin/users` | "Elimina Utente" | `api.admin.users.delete(userId)` | Elimina account utente | Click → Double confirm → API call |
| `/admin/users/invitations` | Lista inviti | `api.invitations.getAll()` | Inviti inviati e pending | Load → API call → Render lista |
| `/admin/users/invitations` | "Invia Invito" | `api.invitations.sendInvitation(email)` | Invita utente via email | Click → Input email → API call → Invito inviato |
| `/admin/users/invitations` | "Invito Bulk" | `api.admin.bulkSendInvitations([emails])` | Inviti massivi da CSV | Upload CSV → API call → Report |
| `/admin/users/access-requests` | Lista richieste | `api.accessRequests.getAccessRequests()` | Richieste di accesso pending | Load → API call → Render lista |
| `/admin/users/access-requests` | "Approva" | `api.accessRequests.approveAccessRequest(id)` | Approva richiesta accesso | Click → API call → Utente abilitato |
| `/admin/users/access-requests` | "Rifiuta" | `api.accessRequests.rejectAccessRequest(id, reason)` | Rifiuta con motivazione | Click → Dialog → API call |
| `/admin/users/access-requests` | "Approva Bulk" | `api.accessRequests.bulkApproveAccessRequests([ids])` | Approvazione massiva | Seleziona → Click → API call |
| `/admin/users/roles` | Gestione ruoli | Visualizzazione ruoli | Mostra ruoli e permessi | Load → Render tabella ruoli |
| `/admin/users/activity` | Log attività utenti | `api.admin.users.getActivity(filters)` | Timeline attività tutti gli utenti | Load → API call → Render timeline |

### Admin - Monitoraggio & Sistema

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/monitor` | Dashboard monitoring | Multi API calls | Panoramica salute sistema | Load → API calls parallele → Render dashboard |
| `/admin/monitor?tab=alerts` | Gestione alert | `api.alerts.getAll()` | Lista alert configurati | Load → API call → Render lista |
| `/admin/monitor?tab=alerts` | "Crea Alert" | `api.alerts.create({name, condition})` | Configura nuovo alert | Click → Form → API call |
| `/admin/monitor?tab=alerts` | "Elimina Alert" | `api.alerts.delete(id)` | Elimina alert | Click → Confirm → API call |
| `/admin/monitor?tab=cache` | Stato cache | `api.admin.getCacheStatus()` | Visualizza stato Redis cache | Load → API call → Render metriche |
| `/admin/monitor?tab=infra` | Infrastruttura | `api.admin.getInfraStatus()` | Stato servizi infra | Load → API call → Render status |
| `/admin/monitor?tab=command` | Command center | Vari | Centro comandi operativo | Load → Render UI comandi |
| `/admin/monitor?tab=testing` | Testing tools | Vari | Strumenti testing admin | Load → Render tools |
| `/admin/monitor?tab=export` | Bulk export | `api.admin.exportUsersToCSV()` | Export dati in CSV | Click → API call → Download file |
| `/admin/monitor?tab=email` | Config email | `api.admin.getEmailTemplates()` | Gestione template email | Load → API call → Render lista |
| `/admin/monitor?tab=email` | "Test Email" | `api.admin.sendTestEmail(email)` | Invia email di test | Input → Click → API call → Conferma |
| `/admin/monitor/operations` | Console operazioni | `api.admin.getOperations()` | Log operazioni sistema | Load → API call → Render log |
| `/admin/monitor/services` | Dashboard servizi | `api.admin.getServiceHealth()` | Health check tutti i servizi | Load → API call → Render status per servizio |
| `/admin/monitor/grafana` | Grafana embed | Iframe | Dashboard Grafana monitoring | Load → Render iframe |
| `/admin/monitor/logs` | Log viewer | `api.admin.getLogs(filters)` | Visualizzatore log sistema | Load → API call → Render logs con filtri |
| `/admin/monitor/containers` | Container dashboard | `api.admin.getContainerStatus()` | Stato container Docker | Load → API call → Render status |

### Admin - Configurazione

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/config?tab=general` | Config generale | `api.config.getAll()` | Impostazioni generali sistema | Load → API call → Render form |
| `/admin/config?tab=general` | "Salva Config" | `api.config.updateConfiguration(id, {...})` | Aggiorna configurazione | Modifica → Save → API call |
| `/admin/config?tab=limits` | Limiti sistema | `api.config.getLimits()` | Configurazione limiti globali | Load → Form → Save → API call |
| `/admin/config?tab=flags` | Feature flags | `api.config.getFeatureFlags()` | Lista feature flags | Load → API call → Render toggle list |
| `/admin/config?tab=flags` | Toggle feature flag | `api.config.updateConfiguration(flagId, {isActive})` | Abilita/disabilita feature | Toggle → API call → Stato aggiornato |
| `/admin/config?tab=flags` | "Bulk Update" | `api.config.bulkUpdateConfiguration([configs])` | Aggiornamento massivo flags | Seleziona → Click → API call |
| `/admin/config?tab=rate-limits` | Rate limits | `api.rateLimits.getRateLimits()` | Configurazione rate limiting | Load → API call → Render form |
| `/admin/config?tab=rate-limits` | "Aggiorna Rate Limit" | `api.rateLimits.updateRateLimits({endpoint, limit})` | Modifica rate limit endpoint | Form → Save → API call |
| `/admin/config/n8n` | Workflow n8n | `api.admin.getN8nWorkflows()` | Gestione workflow automazione | Load → API call → Render lista |
| `/admin/notifications/compose` | Componi notifica | `api.adminNotifications.sendManualNotification({userId, message})` | Invia notifica manuale a utente | Form → Submit → API call |

### Admin - Analytics

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/analytics?tab=overview` | Overview analytics | `api.analytics.getOverview()` | Dashboard analytics generale | Load → API call → Render grafici |
| `/admin/analytics?tab=ai-usage` | Uso AI | `api.analytics.getAIUsage()` | Metriche utilizzo AI | Load → API call → Render grafici |
| `/admin/analytics?tab=audit` | Audit log | `api.analytics.getAuditLog(filters)` | Log audit completo | Load → API call → Render tabella |
| `/admin/analytics?tab=reports` | Report | `api.analytics.getReports()` | Report generati | Load → API call → Render lista |
| `/admin/analytics?tab=api-keys` | API Keys | `api.admin.getApiKeys()` | Gestione API keys globale | Load → API call → Render tabella |
| `/admin/analytics?tab=api-keys` | "Export API Keys" | `api.admin.exportApiKeysToCSV()` | Esporta API keys in CSV | Click → API call → Download |
| `/admin/analytics?tab=api-keys` | "Import API Keys" | `api.admin.bulkImportApiKeysFromCSV(file)` | Importa API keys da CSV | Upload → API call → Report |

### Admin - Database Sync & Content

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| `/admin/database-sync` | Sync tool | `api.admin.syncDatabase()` | Sincronizzazione database | Click → API call → Progress → Completato |
| `/admin/content/email-templates` | Lista template | `api.admin.getEmailTemplates()` | Template email disponibili | Load → API call → Render lista |
| `/admin/content/email-templates` | "Crea Template" | `api.admin.createEmailTemplate({...})` | Crea nuovo template email | Click → Editor → Save → API call |
| `/admin/content/email-templates` | "Modifica Template" | `api.admin.updateEmailTemplate(id, {...})` | Modifica template | Click → Editor → Save → API call |
| `/admin/content/email-templates` | "Pubblica Template" | `api.admin.publishEmailTemplate(id)` | Pubblica template per uso | Click → Confirm → API call |
| `/admin/ui-library` | UI Library | Client-side | Showcase componenti UI | Load → Render componenti |

### Admin - Tier Management

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| Admin config | "Gestione Tier" | `api.tiers.getAll()` | Lista tier configurati | Load → API call → Render lista |
| Admin config | "Crea Tier" | `api.tiers.create({name, creditLimit})` | Crea nuovo tier | Click → Form → API call |
| Admin config | "Modifica Limiti Tier" | `api.tiers.updateTierLimits(tierId, {...})` | Aggiorna limiti tier | Form → Save → API call |
| Admin config | "Configura Strategia Tier" | `api.tierStrategy.update(id, {...})` | Imposta strategia AI per tier | Form → Save → API call |

---

## 5. SUPERADMIN (Ruolo SuperAdmin - Eredita tutto da Admin)

> Accesso esclusivo: operazioni critiche, impersonation, feature flags globali, emergency controls

| Contesto | Elemento UI | Action | Descrizione | Flow |
|----------|-------------|--------|-------------|------|
| Admin Operations | "Impersona Utente" | `api.admin.startImpersonation(userId)` | Accedi come altro utente (Level2 confirm) | Click → Double confirm → API call → Sessione impersonata |
| Admin Operations | "Termina Impersonation" | `api.admin.stopImpersonation()` | Torna al proprio account | Click → API call → Sessione ripristinata |
| Admin Operations | "Emergency: Disabilita Feature" | `api.admin.emergencyDisableFeature(featureKey)` | Disabilita feature in emergenza | Click → Level2 confirm → API call → Feature disabilitata |
| Admin Operations | "Emergency: Modalità Manutenzione" | `api.admin.enableMaintenanceMode()` | Attiva modalità manutenzione | Click → Level2 confirm → API call → Sistema in manutenzione |
| Admin Users | "Assegna SuperAdmin" | `api.admin.users.changeUserRole(userId, 'SuperAdmin')` | Promuovi utente a SuperAdmin | Click → Triple confirm → API call |
| Admin Config | Feature flags critici | `api.config.updateConfiguration(...)` con policy SuperAdmin | Gestione flags sistema critici | Toggle → Level2 confirm → API call |
| Admin Analytics | Export completo sistema | `api.admin.fullSystemExport()` | Export dati completo sistema | Click → Confirm → API call → Download |

---

## 6. MATRICE RIASSUNTIVA ACCESSI

| Funzionalità | Anonimo | Free | Normal | Premium | Editor | Admin | SuperAdmin |
|-------------|---------|------|--------|---------|--------|-------|------------|
| Catalogo giochi (lettura) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Registrazione/Login | ✅ | - | - | - | - | - | - |
| Dashboard personale | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Libreria giochi | ❌ | ✅ (50) | ✅ (100) | ✅ (500) | ✅ | ✅ | ✅ |
| Chat AI | ❌ | ✅ (limit) | ✅ (limit) | ✅ (high) | ✅ | ✅ | ✅ |
| Sessioni di gioco | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Game Night | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Upload PDF | ❌ | ✅ (limit) | ✅ (limit) | ✅ (high) | ✅ | ✅ | ✅ |
| Agenti AI | ❌ | ✅ (3 slot) | ✅ | ✅ (∞) | ✅ | ✅ | ✅ |
| Badge/Gamification | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Share requests | ❌ | ✅ (5/m) | ✅ (10/m) | ✅ (15/m) | ✅ | ✅ | ✅ |
| Toolkit AI | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Editor proposte agenti | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ |
| Editing contenuti (FAQ, regole) | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ |
| Admin Dashboard | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Gestione utenti | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Feature flags | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Monitoring sistema | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Prompt management | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Impersonation | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Emergency controls | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| SuperAdmin assignment | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

---

## 7. FLOW PRINCIPALI (End-to-End)

### Flow 1: Utente scopre e gioca un gioco
```
/discover → Cerca gioco → Click card → /discover/[id] → "Aggiungi alla Libreria"
→ /library → Seleziona gioco → "Chiedi all'AI" → /library/games/[id]/agent
→ Chat con agente → Impara regole → "Nuova Sessione" → /sessions
→ Aggiungi giocatori → Gioca (dadi, carte, punteggi) → "Completa Sessione"
→ Punteggi registrati → Badge guadagnati → /badges
```

### Flow 2: Utente organizza serata giochi
```
/game-nights → "Crea Game Night" → Titolo + Data → "Aggiungi giochi a playlist"
→ Cerca da BGG/libreria → Aggiungi → "Invita giocatori" → Cerca utenti
→ Invito inviato → Invitato riceve notifica → Accetta/Rifiuta
→ Serata: crea sessioni per ogni gioco → Registra punteggi → Completa
```

### Flow 3: Admin importa catalogo giochi
```
/admin/shared-games/import → Cerca su BGG → Seleziona giochi
→ Review dati importati → Conferma → Giochi creati nel catalogo
→ /admin/knowledge-base/upload → Carica PDF regolamenti
→ Processing automatico → Embeddings generati → RAG attivo
→ /admin/agents/new → Crea agente per gioco → Testa in sandbox
→ Pubblica agente → Utenti possono chattare
```

### Flow 4: Editor propone nuovo agente
```
/editor/agent-proposals/create → Form (nome, gioco, tipologia, config)
→ Salva bozza → /editor/agent-proposals/[id]/test → Testa in chat
→ Itera configurazione → "Invia per Approvazione"
→ Admin riceve notifica → /admin/agents → Review proposta
→ "Approva" / "Richiedi Modifiche" / "Rifiuta"
→ Se approvato: agente pubblicato nel catalogo
```

### Flow 5: SuperAdmin gestisce emergenza
```
Alert ricevuto → /admin/monitor → Verifica stato servizi
→ Identifica problema → "Emergency: Disabilita Feature" (Level2 confirm)
→ Feature disabilitata → Investiga root cause → Fix
→ "Riabilita Feature" → Verifica sistema → Notifica team
```

---

> **Note**: Questo documento riflette lo stato del codebase al 2026-03-21. I limiti tier esatti sono configurati lato backend in `UserTier.cs` e possono essere modificati via admin config.
