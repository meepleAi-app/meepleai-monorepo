# Guida Admin - Gestione Richieste di Condivisione

**Manuale operativo per la revisione e approvazione delle richieste di condivisione giochi**

## Panoramica

Come admin MeepleAI, sei responsabile della revisione delle richieste di condivisione
giochi dalla libreria utente al catalogo condiviso. Il tuo lavoro garantisce la qualità
e l'accuratezza del catalogo community.

### Responsabilità Admin

- ✅ **Quality Assurance**: Verificare accuratezza e completezza delle richieste
- ✅ **Content Moderation**: Prevenire spam, duplicati, contenuti inappropriati
- ✅ **User Support**: Fornire feedback costruttivo per migliorare i contributi
- ✅ **Workflow Management**: Gestire lock di revisione e tempistiche

---

## Dashboard Admin

### Accesso

Naviga a **Admin Panel → Richieste Condivisione** dalla sidebar amministrativa.

### Overview Dashboard

La dashboard mostra:

**Metriche Principali**:
- 🔢 **Richieste Pendenti**: In attesa di revisione
- 🔵 **In Revisione**: Con lock attivo (totali + tue)
- 🟠 **Modifiche Richieste**: In attesa di correzioni utente
- ⏰ **Urgenti**: In attesa da oltre 48 ore

**Le Tue Revisioni Attive**:
- Lista delle richieste per cui detieni un lock
- Tempo rimanente per ciascun lock
- Link rapido per completare la revisione

### Filtri e Ricerca

**Filtri Disponibili**:
- **Stato**: Pending, InReview, ChangesRequested, All
- **Tipo Contribuzione**: NewGame, AdditionalContent
- **Data Creazione**: Range personalizzato
- **Contributore**: Filtra per username

**Ricerca Full-Text**:
Cerca nel titolo gioco e note utente (es. "Catan strategia")

**Ordinamento**:
- Data creazione (più vecchie prime - **default**)
- Data creazione (più recenti prime)
- Stato
- Username contributore

> 💡 **Best Practice**: Prioritizza richieste pendenti da oltre 48 ore per mantenere
> tempi di risposta rapidi e soddisfazione utenti.

---

## Processo di Revisione

### Workflow Standard

```
1. Seleziona richiesta → 2. Acquisisci lock → 3. Valuta contenuto → 4. Decidi → 5. Lock rilasciato
```

### 1. Selezionare una Richiesta

Dalla dashboard, clicca su una richiesta per aprire la vista dettaglio.

**Informazioni Disponibili**:
- Dati del gioco sorgente (dalla user library)
- Note dell'utente
- Documenti allegati (con preview)
- Profilo contributore (badge, stats, storico)
- Storico revisioni precedenti (se ChangesRequested)

### 2. Acquisire il Lock di Revisione

Prima di iniziare la revisione, clicca **"Inizia Revisione"**.

**Sistema di Lock**:
- ⏱️ **Durata**: 30 minuti (configurabile)
- 🔒 **Esclusivo**: Solo 1 admin alla volta per richiesta
- ♻️ **Auto-release**: Il lock scade automaticamente se non completi in tempo
- 🔓 **Rilascio manuale**: Puoi rilasciare il lock senza decidere

**Conflitti**:
Se un altro admin sta già revisionando, vedrai:
```
⚠️ Richiesta in revisione da [AdminName]
   Lock scade: 23/01/2026 15:30
   Puoi procedere quando il lock viene rilasciato
```

> ⚠️ **Importante**: Il lock di 30 minuti è un tempo buffer. Cerca di completare
> le revisioni entro 15-20 minuti per efficienza.

### 3. Valutare la Richiesta

Usa questa checklist sistematica:

#### ✅ Verifica Titolo

- [ ] Ortografia corretta
- [ ] Nome ufficiale italiano (se disponibile)
- [ ] Nessuna abbreviazione non standard
- [ ] Formato consistente (es. "I Coloni di Catan", non "Coloni di Catan")

**Esempi**:
```
✅ "I Coloni di Catan - Edizione Italiana"
❌ "Catan ITA"
❌ "The Settlers of Catan"
```

#### ✅ Verifica Descrizione

- [ ] Almeno 50 caratteri (target: 100-200)
- [ ] Descrive meccaniche principali
- [ ] Indica target audience
- [ ] Nessun contenuto promozionale o spam
- [ ] Nessun spoiler narrativo

**Red Flags**:
- Descrizioni copiate da Amazon/BGG (copyright)
- Link esterni o autopromozione
- Linguaggio offensivo o inappropriato
- Descrizioni in lingue diverse dall'italiano

#### ✅ Verifica Metadati

- [ ] Range giocatori realistico (confronta con BGG)
- [ ] Tempo di gioco veritiero (non solo min-max scatola)
- [ ] Complessità appropriata (1=party, 5=wargame pesante)

**Riferimenti**:
- BoardGameGeek per dati ufficiali
- Community feedback per complessità percepita

#### ✅ Verifica Documenti

- [ ] File leggibili e non corrotti
- [ ] Contenuto pertinente (regolamenti, reference)
- [ ] Nessuna violazione copyright (scansioni illegali)
- [ ] Lingua italiana o universale (icone)

**Documenti Accettabili**:
- ✅ Regolamenti ufficiali tradotti dall'editore
- ✅ Schede di riferimento create dall'utente
- ✅ FAQ ufficiali o community-approved
- ❌ Scansioni non autorizzate
- ❌ Contenuti pirata

#### ✅ Verifica Duplicati

**Prima di approvare**, verifica che il gioco non sia già nel catalogo:

1. Usa la ricerca nel catalogo condiviso
2. Cerca per titolo, BGG ID, e varianti del nome
3. Se trovi un match esatto, **rifiuta** con riferimento all'ID esistente

**Casi Edge**:
- Espansioni diverse dello stesso gioco base → **Approva** come giochi separati
- Edizioni diverse (ITA vs ENG) → **Approva** se entrambe rilevanti
- Stesso gioco con regolamenti diversi → Valuta caso per caso

### 4. Prendere una Decisione

Hai 4 opzioni disponibili:

#### ✅ Approva

**Quando usare**:
- Richiesta completa e corretta
- Tutti i criteri soddisfatti
- Nessuna modifica necessaria

**Azioni**:
1. Clicca **"Approva"**
2. Inserisci note amministrative (obbligatorie, min 10 caratteri)
3. Opzionale: Modifica titolo/descrizione se necessario
4. Opzionale: Seleziona subset di documenti da pubblicare
5. Conferma

**Risultato**:
- Gioco creato nel catalogo condiviso
- Badge assegnati automaticamente al contributore
- Email di conferma inviata all'utente
- Lock rilasciato automaticamente

**Esempio Note Admin**:
```
✅ "Ottima richiesta, approvata senza modifiche. Benvenuto nel catalogo!"
✅ "Approvata. Ho corretto il titolo usando il nome ufficiale italiano."
```

#### ✅ Approva con Modifiche

**Quando usare**:
- Richiesta valida ma con piccole imperfezioni
- Correzioni rapide che non richiedono input utente
- Migliorie editoriali (titolo, descrizione)

**Modifiche Permesse**:
- **Title Override**: Correggere titolo (typo, nome ufficiale)
- **Description Override**: Migliorare descrizione (grammatica, chiarezza)
- **Selected Documents**: Pubblicare solo subset di documenti validi

**Best Practice**:
- Spiega nelle note admin cosa hai modificato
- Usa override solo per migliorie, non stravolgimenti
- Se servono modifiche sostanziali, usa "Richiedi Modifiche"

**Esempio**:
```
Titolo Originale: "Catam"
Title Override: "I Coloni di Catan"
Note Admin: "Approvata con correzione typo nel titolo"
```

#### 🔄 Richiedi Modifiche

**Quando usare**:
- Richiesta incompleta o con problemi
- Necessità input/correzioni dall'utente
- Documenti mancanti o da migliorare

**Processo**:
1. Clicca **"Richiedi Modifiche"**
2. Scrivi feedback dettagliato per l'utente (obbligatorio, min 20 caratteri)
3. Opzionale: Aggiungi note admin interne
4. Conferma

**Risultato**:
- Stato cambia a ChangesRequested
- Email con feedback inviata all'utente
- Lock rilasciato automaticamente
- Utente può modificare e reinviare

**Feedback di Qualità**:

❌ **Scarso**:
> "Descrizione insufficiente"

✅ **Buono**:
> "La descrizione è troppo breve. Per favore aggiungi:
> - Panoramica delle meccaniche di gioco principali
> - Target audience consigliato (famiglie, esperti, etc.)
> - Cosa rende questo gioco interessante per la community
>
> Esempi di descrizioni buone: vedi giochi #123, #456 nel catalogo"

**Tone**:
- Professionale e costruttivo
- Specifico e actionable
- Cortese e incoraggiante

#### ❌ Rifiuta

**Quando usare** (solo casi gravi):
- Spam o contenuto inappropriato
- Violazioni copyright evidenti
- Duplicato esatto già nel catalogo
- Richiesta manifestamente non valida

**Processo**:
1. Clicca **"Rifiuta"**
2. Specifica la motivazione (obbligatoria, min 20 caratteri)
3. Opzionale: Note admin interne
4. Conferma

**Risultato**:
- Stato cambia a Rejected
- Email con motivazione inviata all'utente
- Lock rilasciato automaticamente
- Utente può creare nuova richiesta

**Motivazioni Chiare**:

✅ **Buone**:
```
"Rifiutato: Il gioco 'Catan' è già presente nel catalogo con ID #12345.
 Verifica sempre usando la ricerca prima di condividere."

"Rifiutato: I documenti allegati sono scansioni non autorizzate di materiale
 protetto da copyright. Puoi allegare solo documenti di cui hai i diritti."
```

❌ **Evita**:
```
"Rifiutato: non va bene"
"Rifiutato: riprova"
```

> 💡 **Filosofia**: Usa "Rifiuta" raramente. Preferisci "Richiedi Modifiche" per
> guidare l'utente verso un contributo accettabile.

#### 🔓 Rilascia Lock

**Quando usare**:
- Hai iniziato per errore
- Necessiti più tempo per valutare
- Interruzione imprevista

**Processo**:
1. Clicca **"Rilascia Revisione"**
2. Conferma

**Risultato**:
- Lock rilasciato immediatamente
- Richiesta torna a Pending o ChangesRequested
- Disponibile per altri admin

---

## Gestione Review Lock

### Acquisire un Lock

**Requisiti**:
- Richiesta in stato Pending o ChangesRequested
- Nessun altro admin ha un lock attivo
- Hai ruolo Admin

**Comportamento**:
- Stato cambia a InReview
- Timer di 30 minuti inizia
- Richiesta riservata esclusivamente a te

### Estendere un Lock

Se hai bisogno di più tempo:

1. Completa la decisione entro il timeout (preferred)
2. Oppure rilascia e ri-acquisici il lock in un secondo momento

> ⚠️ **Non supportato**: L'estensione manuale del lock non è disponibile per design.
> Questo previene blocchi indefiniti.

### Lock Scaduti

**Cosa succede**:
- Background job rilascia automaticamente lock scaduti ogni ora
- Richiesta torna a stato precedente (Pending o ChangesRequested)
- Notifica interna generata per tracking

**Se il tuo lock scade**:
- La tua decisione parziale viene scartata
- Puoi ri-acquisire il lock e ricominciare
- Nessuna penalità o conseguenza

### Forzare Rilascio Lock (Super Admin)

Solo Super Admin possono forzare il rilascio di lock di altri admin:

**Quando usare**:
- Admin inattivo con lock bloccato da ore
- Emergenze operative
- Lock bug o problemi tecnici

**Processo**:
1. Admin Panel → Lock Management
2. Trova il lock bloccato
3. **"Force Release"** con motivazione obbligatoria
4. Lock rilasciato e admin originale notificato

---

## Criteri di Valutazione Dettagliati

### Qualità del Contenuto

**Titolo (Weight: 25%)**:
```
Criteri:
✅ Nome ufficiale italiano (se disponibile)
✅ Ortografia corretta
✅ Formato consistente con catalogo esistente
❌ Abbreviazioni non standard
❌ Nomi in inglese quando esiste traduzione

Score: Pass/Fail (blocca se fail)
```

**Descrizione (Weight: 35%)**:
```
Criteri:
✅ Lunghezza ≥ 100 caratteri (target: 150-300)
✅ Descrive meccaniche principali
✅ Indica target audience
✅ Spiega perché il gioco è interessante
❌ Testo generico o marketing
❌ Contenuti copiati senza attribuzione
❌ Linguaggio inappropriato

Score: 1-5
- 5: Eccezionale, pronta per pubblicazione
- 4: Buona, piccole migliorie opzionali
- 3: Accettabile, richiedi miglioramenti se tempo
- 2: Insufficiente, richiedi modifiche
- 1: Inaccettabile, rifiuta o richiedi riscrittura completa
```

**Metadati (Weight: 20%)**:
```
Criteri:
✅ Range giocatori realistico (verifica vs BGG)
✅ Tempo gioco veritiero (non solo box)
✅ Complessità appropriata per il gioco
❌ Dati palesemente errati
❌ Complessità 5 per party game o viceversa

Score: Pass/Fail (blocca se fail)
```

**Documenti (Weight: 20%)**:
```
Criteri:
✅ File leggibili e ben scansionati
✅ Contenuto pertinente
✅ Lingua italiana o universale
❌ Copyright violations
❌ File corrotti o illeggibili
❌ Contenuti off-topic

Score: 0-5 (0 se assenti, accettabile)
```

### Decision Matrix

| Titolo | Descrizione | Metadati | Documenti | Decisione |
|--------|-------------|----------|-----------|-----------|
| Pass | 4-5 | Pass | 3-5 | ✅ Approva |
| Pass | 3 | Pass | 0-5 | ✅ Approva o 🔄 Richiedi miglioramenti |
| Pass | 2 | Pass | Any | 🔄 Richiedi modifiche |
| Fail | Any | Any | Any | ❌ Rifiuta o 🔄 Richiedi modifiche |
| Pass | Any | Fail | Any | 🔄 Richiedi correzione metadati |
| Pass | Any | Pass | Fail | ❌ Rifiuta (copyright) |

---

## Casi d'Uso Comuni

### Caso 1: Richiesta Perfetta (Quick Approval)

**Scenario**: Titolo corretto, descrizione eccellente, metadati accurati, regolamento PDF italiano allegato.

**Azione**:
```
1. Acquisisci lock
2. Verifica rapidamente tutti i criteri (2-5 min)
3. Approva con note: "Ottima richiesta, approvata immediatamente. Grazie per il contributo!"
```

**Tempo**: ~5 minuti

### Caso 2: Buona Richiesta con Typo

**Scenario**: Tutto corretto ma titolo ha un typo ("Catam" invece di "Catan").

**Azione**:
```
1. Acquisisci lock
2. Valuta contenuto (ok)
3. Approva con Title Override
   - titleOverride: "I Coloni di Catan"
   - adminNotes: "Approvata con correzione typo nel titolo"
```

**Tempo**: ~7 minuti

### Caso 3: Descrizione Insufficiente

**Scenario**: Descrizione troppo breve ("Gioco di strategia"), resto ok.

**Azione**:
```
1. Acquisisci lock
2. Richiedi modifiche con feedback:
   "La descrizione è troppo breve. Aggiungi:
    - Quali sono le meccaniche principali? (es. piazzamento, carte, dadi)
    - Per chi è consigliato? (famiglie, esperti, bambini)
    - Cosa lo rende speciale rispetto ad altri giochi simili?

    Vedi esempi di buone descrizioni nei giochi #123, #456."
```

**Tempo**: ~10 minuti

### Caso 4: Duplicato Esistente

**Scenario**: Il gioco è già nel catalogo con ID #789.

**Azione**:
```
1. Acquisisci lock
2. Cerca nel catalogo (trova duplicato)
3. Rifiuta con motivazione:
   "Il gioco è già presente nel catalogo condiviso con ID #789.
    Prima di condividere, usa la ricerca per verificare che il gioco non esista già.
    Puoi contribuire a giochi esistenti aggiungendo FAQ o errata nelle loro pagine."
```

**Tempo**: ~8 minuti

### Caso 5: Documenti con Copyright Issues

**Scenario**: Allegata scansione non autorizzata di regolamento protetto.

**Azione**:
```
1. Acquisisci lock
2. Identifica violazione copyright
3. Rifiuta con motivazione:
   "I documenti allegati contengono scansioni non autorizzate di materiale protetto da copyright.
    Puoi condividere solo:
    - Regolamenti ufficiali forniti dall'editore gratuitamente
    - Documenti di cui possiedi i diritti
    - Reference sheets create da te

    Rimuovi i documenti problematici e ricrea la richiesta."
```

**Tempo**: ~10 minuti

### Caso 6: Richiesta di Bassa Qualità Sistemica

**Scenario**: Utente ha già 3 richieste rifiutate per spam/low quality.

**Azione**:
```
1. Acquisisci lock
2. Controlla storico contributore
3. Se pattern di spam evidente:
   - Rifiuta con avviso
   - Segnala al team per possibile ban
4. Se contributore nuovo in buona fede:
   - Richiedi modifiche con guida dettagliata
```

---

## Gestione Rate Limits

### Visualizzare Limiti Utente

Nella vista dettaglio richiesta, sezione **"Contributor Profile"**:

```
Rate Limit Status:
- Tier: Free
- Limit: 3/mese
- Used: 2/3
- Remaining: 1
- Resets: 01/02/2026
- Override: Nessuno
```

### Configurare Override

Per casi speciali (contest, eventi, utenti premium temporanei):

**Processo**:
1. Vai a **Admin → Rate Limit Management**
2. Cerca l'utente per email o username
3. Clicca **"Add Override"**
4. Configura:
   - **New Limit**: Numero (o -1 per unlimited)
   - **Expires At**: Data scadenza (opzionale)
   - **Reason**: Motivazione obbligatoria
5. Salva

**Esempi Override**:
```
✅ "Contest Gennaio 2026: unlimited submissions fino al 31/01"
✅ "Contributor esperto: aumentato a 20/mese per 3 mesi"
✅ "Beta tester: unlimited per testing periodo 15-30 gennaio"
```

**Audit**:
Tutti gli override sono loggati con:
- Chi ha creato (admin ID)
- Quando (timestamp)
- Perché (reason)
- Scadenza

### Revoca Override

1. Admin → Rate Limit Management → Active Overrides
2. Trova l'override da revocare
3. **"Revoke"** con motivo opzionale
4. Utente torna al limite tier standard

---

## Metriche e KPI

### Dashboard Metriche Admin

**KPI Primari** (target organizzazione):
- ⏱️ **Tempo Medio Revisione**: < 48 ore dalla creazione
- ✅ **Tasso Approvazione**: 60-80% (troppo alto = lax, troppo basso = strict)
- 🔄 **Tasso Modifiche Richieste**: 15-25% (sano bilanciamento)
- ❌ **Tasso Rifiuto**: < 10% (solo casi gravi)

**Tue Performance** (individuali):
- Revisioni completate questo mese
- Tempo medio per decisione
- Distribution decisioni (approve/modify/reject)
- Feedback satisfaction score (da utenti)

### Report Settimanali

Ogni lunedì mattina ricevi un digest:

```
📊 Weekly Share Request Report

New Requests: 23
Approved: 15 (65%)
Modifications Requested: 6 (26%)
Rejected: 2 (9%)

Your Activity:
- Reviews Completed: 8
- Avg Decision Time: 12 minutes
- Pending Reviews: 2

Team Performance:
- Avg Response Time: 36 hours ✅ (target: <48h)
- Backlog Size: 12 requests
- Oldest Pending: 18 hours
```

### Analisi Trend

**Admin Panel → Analytics** per visualizzare:

- **Trend settimanali**: Richieste create vs processate
- **Distribution per tipo**: NewGame vs AdditionalContent
- **Top contributori**: Chi ha più richieste approvate
- **Motivi rifiuto**: Categorizzazione per migliorare guidance

---

## Best Practices

### Efficienza

✅ **Fai**:
- Processa richieste in batch (5-10 alla volta)
- Usa template per feedback comuni
- Prioritizza richieste > 48 ore
- Rilascia lock se interrotto
- Completa decisioni entro 15-20 minuti

❌ **Evita**:
- Acquisire lock senza tempo per completare
- Lasciare richieste in InReview senza decisione
- Procrastinare richieste complicate
- Modifiche eccessive che cambiano sostanza contributo

### Qualità

✅ **Fai**:
- Fornisci feedback specifico e actionable
- Cita esempi di descrizioni buone
- Educa i contributori sui criteri
- Mantieni tono professionale e cortese
- Documenta decisioni complesse nelle note admin

❌ **Evita**:
- Feedback generico o vago
- Tono sprezzante o sarcastico
- Modifiche soggettive non giustificate
- Approvazioni "veloci" senza verifica

### Consistenza

- **Applica criteri uniformemente**: Stesso standard per tutti
- **Consulta altri admin**: Per casi edge o decisioni difficili
- **Aggiorna guidelines**: Se emergono pattern ricorrenti
- **Documenta precedenti**: Per decisioni complesse o controverse

---

## Troubleshooting

### Lock Non Si Acquisisce

**Sintomo**: Bottone "Inizia Revisione" non funziona o mostra errore 409.

**Cause**:
- Altro admin ha già un lock attivo
- Richiesta non è in stato Pending/ChangesRequested
- Problemi di rete o sessione scaduta

**Soluzioni**:
1. Verifica lo stato della richiesta
2. Controlla se c'è lock attivo (nome admin + scadenza)
3. Ricarica la pagina
4. Se persiste, segnala al team tecnico

### Non Riesco a Decidere

**Sintomo**: Lock acquisito ma bottoni Approva/Rifiuta disabilitati.

**Cause**:
- Lock scaduto nel frattempo
- Stato richiesta cambiato
- Bug UI

**Soluzioni**:
1. Ricarica la pagina
2. Verifica che il lock sia ancora attivo
3. Re-acquisici il lock se necessario
4. Segnala se persiste

### Richiesta Bloccata in InReview

**Sintomo**: Richiesta in InReview da oltre 2 ore, nessun admin visibile.

**Cause**:
- Lock scaduto ma background job non ancora eseguito (runs ogni ora)
- Admin ha abbandonato senza rilasciare
- Bug di stato

**Soluzioni**:
1. Attendi 1 ora (prossimo background job run)
2. Se urgente, contatta Super Admin per force release
3. Segnala al team tecnico se ricorrente

### Decisione Errata

**Sintomo**: Hai approvato/rifiutato per errore.

**Azione**:
- ❌ **Non reversibile via UI**: Le decisioni sono finali per design
- ✅ **Contatta Super Admin**: Possono modificare stato manualmente via DB
- ✅ **Alternativa**: Crea issue su GitHub per tracking

**Prevenzione**:
- Leggi attentamente prima di confermare
- Usa "Rilascia Lock" se non sei sicuro
- Prenditi tempo adeguato per decisioni complesse

---

## Comunicazione e Feedback

### Template Feedback

Per accelerare revisioni, puoi usare questi template:

**Descrizione Insufficiente**:
```
La descrizione attuale è troppo breve. Per favore espandi includendo:

1. **Meccaniche Principali**: Quali sono le azioni core del gioco?
2. **Target Audience**: Per chi è consigliato? (famiglie, esperti, bambini, etc.)
3. **Unicità**: Cosa distingue questo gioco da altri simili?

Lunghezza target: 150-300 caratteri.

Esempi di descrizioni buone: vedi giochi #123 "Ticket to Ride" e #456 "Splendor" nel catalogo.
```

**Metadati da Verificare**:
```
I metadati sembrano non accurati. Per favore verifica:

- **Numero Giocatori**: Il range {min}-{max} è corretto? Confronta con BoardGameGeek.
- **Tempo di Gioco**: {playtime} minuti riflette la durata reale delle tue partite?
- **Complessità**: {complexity}/5 è appropriato? (1=party, 3=gateway, 5=heavy)

Usa la tua esperienza di gioco effettiva, non solo i dati della scatola.
```

**Documenti Problematici**:
```
I documenti allegati presentano problemi:

- {filename}: {issue description}

Per favore:
1. Rimuovi documenti problematici
2. Sostituisci con versioni corrette (se disponibili)
3. Reinvia la richiesta

Ricorda: Puoi allegare solo documenti di cui possiedi i diritti o che sono
pubblicamente disponibili dall'editore.
```

---

## Escalation e Supporto

### Quando Escalare

Contatta il team leadership admin per:

- **Casi complessi di copyright**: Incertezze su legalità documenti
- **Contributori problematici**: Pattern di spam o abuso ripetuto
- **Decisioni controverse**: Disaccordo tra admin su una richiesta
- **Bug o problemi tecnici**: Malfunzionamenti del sistema

### Canali di Supporto

- 💬 **Slack Admin**: #share-requests-admin (discussioni)
- 📧 **Email**: admin-support@meepleai.com (supporto tecnico)
- 📞 **Call Settimanale**: Mercoledì 15:00 (review discussioni complesse)

### Feedback sulla Procedura

Se hai suggerimenti per migliorare:

- Questo processo di revisione
- I criteri di valutazione
- Le feature della dashboard
- Le comunicazioni con gli utenti

Apri una **discussione** nel canale admin o invia proposta al team product.

---

## Appendice

### Glossary

| Termine | Definizione |
|---------|-------------|
| **Share Request** | Richiesta di condivisione gioco da user library a shared catalog |
| **Review Lock** | Lock esclusivo di 30 minuti per admin durante revisione |
| **ContributionType** | NewGame (nuovo nel catalogo) o AdditionalContent (migliora esistente) |
| **Rate Limit** | Limite mensile di richieste basato su tier utente |
| **Override** | Modifica temporanea del rate limit da parte admin |
| **Badge** | Riconoscimento assegnato automaticamente ai contributori |

### Shortcut Keyboard (Future)

| Azione | Shortcut |
|--------|----------|
| Approva | `Ctrl+Enter` |
| Richiedi Modifiche | `Ctrl+M` |
| Rifiuta | `Ctrl+R` |
| Rilascia Lock | `Esc` |

---

**Ultima revisione**: 2026-01-23
**Versione guida**: 1.0
**Maintainer**: Team Admin MeepleAI
