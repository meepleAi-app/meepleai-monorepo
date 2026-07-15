# Pacchetto per Review Legale IP — Terms of Service (Mechanic Extractor)

**Destinatario:** Consulente legale IP esterno
**Committente:** MeepleAI — Trust & Legal
**Gate:** Follow-up obbligatorio ADR-051 (§ Follow-up obbligatori) — *ToS review con consulenza legale IP entro M2, prima del lancio delle card pubbliche login-gated.*
**Data pacchetto:** 2026-07-15
**Stato:** Bozza per review — nessuna consulenza legale ricevuta a oggi

> **Scopo del documento.** Ridurre il lavoro del consulente a una review mirata: fornire il contesto tecnico e di prodotto, isolare le clausole da validare e formulare domande precise. **Questo documento non è consulenza legale** e non esprime valutazioni giuridiche: le premesse legali riportate al § 2 sono le assunzioni interne su cui è stata costruita la feature e sono esse stesse oggetto di validazione.

---

## 1. Contesto business

Il **Mechanic Extractor** è una funzione in cui un modello AI legge un regolamento di gioco da tavolo in formato PDF — caricato dall'utente — e ne produce una **comprehension card**: una sintesi delle meccaniche del gioco. Ogni affermazione ("claim") prodotta dall'AI è **riformulata in parole originali** (non è trascrizione né traduzione fedele del testo) e riporta almeno una **citazione** con il numero di pagina del manuale e una `quote` testuale di **massimo 25 parole** a scopo di attribution. Nessuna card viene pubblicata senza **approvazione umana esplicita** di un amministratore (review gate per-claim). Le card approvate diventano **pubbliche ma accessibili solo previo login** (login-gated) alla URL `/games/{id}/card`. Il PDF sorgente non viene mai ripubblicato. Un editore può richiedere la rimozione di una card tramite takedown, con effetto immediato via flag `is_suppressed`.

---

## 2. Premesse legali su cui si fonda la feature

Le seguenti assunzioni — tratte da ADR-051 (§ Context / § Decision) e ADR-059 (§ Context) — sono le fondamenta di progettazione della feature. **Vanno confermate, corrette o circostanziate dal consulente.**

1. **Le regole funzionali (fatti) non sono tutelate da copyright.** Solo l'*espressione testuale* del manuale è protetta; le meccaniche e le regole in quanto fatti non lo sono. Riferimento assunto: *Feist Publications v. Rural Telephone Service*, 499 U.S. 340 (1991) — negli US i fatti non sono copyrightable; in ambito UE la "thin copyright" / diritto sui generis sul database copre selezione e disposizione, non i singoli fatti.
2. **Diritto di citazione.** Quote brevi con attribution sono ammesse dal diritto di citazione (art. 70 L. 633/1941 in Italia) e, negli US, dalla dottrina del *fair use* (17 U.S.C. § 107). Il cap a 25 parole è stato scelto internamente come **standard editoriale conservativo**, non come soglia legale certa (vedi rischio residuo al § 3.b).
3. **Responsabilità di upload sull'utente.** Il modello si fonda sull'assunto che l'utente carichi un PDF di cui possiede una copia legittima e che la responsabilità civile per l'upload ricada sull'utente. ADR-051 (§ Rischi residui) segnala esplicitamente che la dichiarazione dell'utente copre il solo lato civile e che, in caso di upload consapevole di materiale piratato, MeepleAI *potrebbe* essere co-obbligata a un dovere di vigilanza — punto da chiarire con la review legale.

---

## 3. Clausole ToS da validare

Il ToS **esiste** (vedi § 3.0 per lo stato attuale). Per ciascun punto è indicato lo stato di copertura corrente e cosa il consulente deve confermare o riformulare.

### 3.0 Stato attuale del ToS

| Elemento | Stato |
|---|---|
| Pagina ToS pubblica | ✅ **Esiste** — route `apps/web/src/app/(public)/terms/page.tsx`, renderizzata via `LegalPageLayout` (componente `apps/web/src/components/legal/`) |
| Contenuto | Chiavi locale `legal.terms.sections` in `apps/web/src/locales/it.json` (righe ~849+) e `en.json` (righe ~752+); bilingue IT/EN |
| Ultimo aggiornamento dichiarato | **2026-03-09** (parametro `lastUpdated` in `page.tsx`) — **anteriore ad ADR-051 (2026-04-23)** |
| Struttura | 12 sezioni: Accettazione · Descrizione servizio · Freemium · Account · **Contenuti utente (§5)** · **Uso AI (§6)** · **Proprietà intellettuale (§7)** · Limitazioni responsabilità (§8) · Terminazione · Modifiche · **Legge applicabile (§11)** · Contatti |
| Legge applicabile dichiarata | §11 — leggi italiane e UE, foro competente **Milano** |

> ⚠️ **Gap trasversale.** Il ToS è **datato 2026-03-09, cioè precedente ad ADR-051 (2026-04-23)**. Non menziona in alcun punto: le comprehension card pubblicate, l'obbligo di riformulazione, il quote cap a 25 parole, né la pipeline AI-on-PDF del Mechanic Extractor. Il testo attuale copre il caso "utente carica PDF per uso personale", **non** il caso "MeepleAI pubblica card derivate dal manuale e visibili ad altri utenti loggati". Questo è il disallineamento centrale da colmare.

### 3.a — L'utente dichiara di possedere una copia legittima del PDF

- **Stato: PRESENTE.** §5 ("Contenuti Caricati dagli Utenti") contiene la **Certificazione dei diritti d'autore**: l'utente "dichiara e garantisce di avere il diritto di utilizzare tali contenuti"; è esplicitato che "MeepleAI non è responsabile per la verifica della titolarità dei diritti".
- **Da validare:** se la formula dichiarativa è sufficiente a trasferire la responsabilità civile e se il disclaimer di non-verifica regge (vedi domanda 1).

### 3.b — Riformulazione obbligatoria + quote cap 25 parole (standard editoriale)

- **Stato: ASSENTE dal ToS.** Nessuna sezione menziona la riformulazione obbligatoria né il cap a 25 parole. Questi vincoli sono oggi implementati **solo a livello tecnico** (ADR-051 vincoli T1/T2) e documentati negli ADR, ma non trasposti nel ToS o in una policy editoriale pubblica.
- ADR-051 (§ Rischi residui) qualifica il cap a 25 parole come "standard editoriale conservativo, **non** una soglia legale hard; un giudice potrebbe valutare diversamente in dispute".
- **Da validare:** se e come descrivere questo standard nel ToS / in una policy editoriale, evitando che diventi un'auto-dichiarazione di soglia legale vincolante (vedi domande 2 e 3).

### 3.c — Attribution editore + takedown policy

- **Stato: PARZIALE con incoerenza.**
  - §7 ("Proprietà Intellettuale") dichiara che "i regolamenti dei giochi elaborati rimangono di proprietà dei rispettivi editori" — attribution di principio presente.
  - §5 include una clausola **Conformità DMCA** che indirizza le notifiche di takedown a **`legal@meepleai.com`**.
  - Esiste una **takedown policy dedicata** (`docs/legal/takedown-policy.md`) e una **pagina pubblica** `/legal/takedown` (non login-gated), con SLA (presa in carico ≤ 3 gg lavorativi, risoluzione ≤ 10 gg) e meccanismo di soppressione immediata.
  - ⚠️ **Incoerenza dei contatti:** il ToS punta a `legal@meepleai.com`; la takedown policy e ADR-051 usano **`takedown@meepleai.app`**; ADR-059 usa **`abuse@meepleai.app`**. Divergono sia l'indirizzo sia il **dominio (.com vs .app)**. Il ToS non contiene alcun rimando alla pagina `/legal/takedown`.
- **Da validare:** unificazione dei canali di contatto; sufficienza del meccanismo notice-and-takedown via email/form (vedi domande 4 e 5).

### 3.d — Responsabilità / manleva

- **Stato: PRESENTE.** §5 contiene una clausola di **Manleva** ("l'utente si impegna a manlevare e tenere indenne MeepleAI da qualsiasi reclamo, danno o spesa derivante da violazioni del diritto d'autore relative ai contenuti caricati"). §8 ("Limitazioni di Responsabilità") fornisce il servizio "così com'è" ed esclude danni indiretti/consequenziali.
- **Da validare:** se la manleva copre anche il rischio derivante dalla **pubblicazione delle card derivate** (non solo dall'upload dell'utente) e se regge la limitazione di responsabilità per il contenuto AI-generato pubblicato (vedi domande 6 e 7).

---

## 4. Domande specifiche per il consulente

1. **Trasferimento di responsabilità via dichiarazione.** La clausola di ownership-upload (§5, "Certificazione dei diritti d'autore") è sufficiente a trasferire la responsabilità civile per l'upload all'utente? È adeguato il disclaimer di non-verifica ("MeepleAI non è responsabile per la verifica della titolarità"), o l'ordinamento IT/UE impone a MeepleAI un **dovere di verifica attiva** o di vigilanza (in particolare quando la card derivata è poi pubblicata)?

2. **Tenuta del quote cap 25 parole.** Il cap di 25 parole per le citazioni testuali regge come uso lecito (citazione ex art. 70 L. 633/1941 IT e fair use 17 U.S.C. § 107 US) in un contesto **commerciale** e **login-gated**? La natura commerciale del servizio (modello freemium) e l'accesso previo login modificano la valutazione rispetto a un uso puramente informativo/gratuito?

3. **Standard editoriale vs soglia legale.** È opportuno enunciare il cap di 25 parole e l'obbligo di riformulazione nel ToS o in una policy editoriale pubblica, o è preferibile **non fissare una soglia numerica** in un documento legale (per non auto-vincolarsi a un limite che un giudice potrebbe considerare superato o inadeguato)? Quale formulazione consigliate?

4. **Sufficienza del notice-and-takedown.** La procedura di takedown attuale (form pubblico `/legal/takedown` + email, con SLA 3/10 gg e soppressione immediata precauzionale) soddisfa i requisiti di un meccanismo **notice-and-takedown** valido in ambito UE (Digital Services Act / responsabilità hosting provider) e negli US (safe harbor DMCA § 512, ove applicabile)? Mancano elementi formali (es. counter-notice, designazione di un agente DMCA, contenuti minimi obbligatori della notifica)?

5. **Coerenza e formalizzazione dei canali.** Va unificato il canale di contatto per takedown/abuse (attualmente triplice e su domini divergenti: `legal@meepleai.com` nel ToS, `takedown@meepleai.app` nella policy, `abuse@meepleai.app` in ADR-059)? Quale indirizzo/dominio deve essere quello ufficiale e come va richiamato nel ToS?

6. **Estensione della manleva alla pubblicazione.** La clausola di manleva (§5) e la limitazione di responsabilità (§8) coprono anche il rischio derivante dalla **pubblicazione delle comprehension card derivate** (contenuto generato da MeepleAI, non dall'utente), o coprono solo l'upload? Serve una clausola distinta che disciplini la responsabilità per il contenuto AI-generato pubblicato?

7. **Responsabilità per contenuto AI errato.** Il disclaimer sull'AI (§6: risposte generate automaticamente, "possono contenere errori", invito a verificare sul regolamento) è sufficiente a limitare la responsabilità per **inesattezze lesive** contenute in una card pubblicata (es. una card che danneggia la reputazione dell'editore o induce in errore i giocatori)?

8. **Disallineamento temporale del ToS.** Il ToS è datato 2026-03-09 e non menziona la feature Mechanic Extractor né le card pubbliche. Quali sezioni **nuove o modificate** sono necessarie prima del go-live delle card login-gated? È richiesto un **re-consenso** degli utenti esistenti (versionamento del consenso) a fronte della modifica sostanziale?

9. **Giurisdizione e foro.** Il ToS fissa legge italiana/UE e foro di Milano (§11). Poiché la feature ha esposizione anche di diritto US (fair use, editori statunitensi) e le card sono accessibili a utenti potenzialmente in altre giurisdizioni, la scelta di foro/legge è adeguata e opponibile, o serve una disciplina specifica per l'utenza internazionale?

10. **Clausola "fonti pubblicamente disponibili" (cross-feature).** ADR-059 (checklist pre-rollout § 8.5.6) richiede l'aggiunta al ToS di una clausola su "publicly available data sources" per il catalogo (Wikidata-primary + BGG-fallback). Questa clausola e la review del Mechanic Extractor possono/devono essere trattate nella **stessa revisione** del ToS? Ci sono interazioni tra le due?

---

## 5. Riferimenti normativi e deliverable attesi

### Riferimenti normativi

- **Legge 22 aprile 1941, n. 633** (Legge sul diritto d'autore), art. 70 — diritto di citazione.
- **17 U.S.C. § 107** — Fair Use (US).
- **17 U.S.C. § 512** — DMCA safe harbor / notice-and-takedown (US), ove applicabile.
- **Feist Publications, Inc. v. Rural Telephone Service Co.**, 499 U.S. 340 (1991) — i fatti non sono copyrightable.
- **Direttiva 96/9/CE** (EU Database Directive), art. 7 — diritto sui generis del costitutore di banca dati (rilevante per il catalogo, ADR-059).
- **Regolamento (UE) 2022/2065** (Digital Services Act) — obblighi di notice-and-action per hosting provider.
- **Regolamento (UE) 2016/679** (GDPR) — trattamento dati (nomi designer/editori; art. 17 erasure — vedi ADR-059).

### Documenti interni allegati / di riferimento

- `docs/for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md` — policy IP e vincoli tecnici T1–T8; § Follow-up obbligatori (gate ToS review M2).
- `docs/legal/takedown-policy.md` — takedown policy attiva (canali, SLA, processo interno).
- `docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md` — legal posture del catalogo; checklist pre-rollout § 8.5.6 (clausola "publicly available data sources").
- ToS attuale: `apps/web/src/app/(public)/terms/page.tsx` + chiavi `legal.terms.sections` in `apps/web/src/locales/{it,en}.json`.

### Checklist deliverable attesi dal consulente

- [ ] **Opinione scritta** che confermi, corregga o circostanzi le premesse legali del § 2 (fatti non copyrightable; diritto di citazione; responsabilità upload).
- [ ] **Risposte puntuali** alle 10 domande del § 4.
- [ ] **ToS aggiornato** (o redlines / clausole da inserire) che copra: (a) ownership-upload rafforzata; (b) disciplina della pubblicazione di card derivate e dello standard editoriale di riformulazione/citazione; (c) attribution editore + rimando alla takedown policy con canale unico; (d) manleva estesa alla pubblicazione e limitazione di responsabilità per contenuto AI-generato; (e) clausola "fonti pubblicamente disponibili" ADR-059; (f) eventuale disciplina di giurisdizione internazionale.
- [ ] **Parere sul re-consenso** degli utenti esistenti a fronte della modifica sostanziale del ToS.
- [ ] **Parere sulla sufficienza del meccanismo notice-and-takedown** (DSA / DMCA) ed eventuali elementi formali mancanti.
- [ ] **Data e firma** dell'opinione; indicazione della validità temporale del parere e degli assunti di fatto su cui si basa.

---

*Preparato internamente da MeepleAI Trust & Legal come materiale istruttorio. Non costituisce consulenza legale.*
