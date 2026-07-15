# Bozza redlines ToS — allineamento ad ADR-051 (Mechanic Extractor)

**Stato:** 🔴 **BOZZA TECNICA — DA VALIDARE LEGALMENTE PRIMA DELLA PUBBLICAZIONE.** Non applicare a `it.json`/`en.json`/`page.tsx` finché il consulente IP non ha validato le clausole. Companion di [`tos-ip-review-package.md`](./tos-ip-review-package.md).
**Data:** 2026-07-15 · **Preparato da:** MeepleAI Trust & Legal (istruttorio, non consulenza legale)
**Base:** ToS attuale (v. 2026-03-09, 12 sezioni) · ADR-051 · ADR-059 · `takedown-policy.md`

> **Come leggere.** Per ogni modifica: *testo attuale* → *testo proposto* + **razionale** (fonte interna) + eventuale **[DA VALIDARE]** che rimanda alla domanda del package (§4). I `[DA VALIDARE]` NON sono decisi qui: sono i punti giuridici che spettano al consulente. Il testo proposto è redazionale, non un parere legale.

---

## A. Sintesi compliance (panel review)

Priorità dei gap, dal più al meno critico:

1. 🔴 **CRITICO — §5 dichiara il falso post-ADR-051.** Il testo attuale afferma: *«I contenuti vengono elaborati esclusivamente per il tuo uso personale e non vengono condivisi con altri utenti senza il tuo consenso.»* Ma le **comprehension card** derivate dal manuale sono **pubblicate e visibili ad altri utenti** (login-gated). La clausola è **fattualmente inesatta** per la feature live → rischio di dichiarazione contrattuale non veritiera. *(Nygard: "il documento descrive un sistema che non è più quello in produzione".)*
2. 🔴 **CRITICO — nessuna clausola sulla generazione/pubblicazione di contenuti derivati.** Il ToS non disciplina che MeepleAI **produce e pubblica** contenuto derivato dal manuale (riformulazione + citazione). Manleva (§5) e disclaimer AI (§6) coprono *upload* e *risposte in chat*, **non** la pubblicazione delle card. *(Wiegers: "requisito non tracciato: la responsabilità del contenuto pubblicato da MeepleAI, non dall'utente".)*
3. 🟡 **MAJOR — incoerenza canali takedown/DMCA.** ToS → `legal@meepleai.com`; pagina Takedown + ADR-051 → `takedown@meepleai.app`; **domini divergenti** (.com vs .app). Il ToS non rimanda alla pagina `/legal/takedown`. *(Hightower: "canale di contatto operativo non deve avere due indirizzi su due domini".)*
4. 🟡 **MAJOR — disallineamento temporale + re-consenso.** ToS datato 2026-03-09, anteriore ad ADR-051 (2026-04-23): non menziona il Mechanic Extractor né le card. La modifica è **sostanziale** → valutare re-consenso versionato (già esiste il meccanismo per l'AI, §6).
5. 🟢 **MINOR — refuso §7** («funzionalità e funzionalità») + **fair use assente** dal ToS (descritto solo nella pagina Takedown).
6. 🟢 **MINOR — clausola "fonti pubblicamente disponibili"** richiesta da ADR-059 §8.5.6 (catalogo) — trattabile nella stessa revisione.

---

## B. Redlines per sezione

### §5 — "Contenuti Caricati dagli Utenti" — 🔴 riscrittura sostanziale

**Attuale (estratto critico):**
> Caricando PDF o altri contenuti su MeepleAI, dichiari e garantisci di avere il diritto di utilizzare tali contenuti e di concedere a MeepleAI una licenza limitata per elaborarli al fine di fornirti il servizio. **I contenuti vengono elaborati esclusivamente per il tuo uso personale e non vengono condivisi con altri utenti senza il tuo consenso.**

**Proposto:**
> Caricando PDF o altri contenuti su MeepleAI, dichiari e garantisci di avere il diritto di utilizzare tali contenuti e di concedere a MeepleAI una licenza limitata per elaborarli al fine di fornire il servizio. L'elaborazione avviene per due finalità distinte: (a) l'**assistenza personale** (risposte alle tue domande sulle regole), riservata al tuo uso; e (b), quando applicabile, la generazione di **schede di comprensione delle meccaniche** ("comprehension card") — vedi §5-bis — che, previa **revisione e approvazione umana**, possono essere rese **accessibili ad altri utenti autenticati**. Il PDF originale che carichi **non viene mai ripubblicato** né reso disponibile ad altri utenti.

- **Razionale:** ADR-051 §Context/Decision (card pubbliche login-gated; PDF sorgente mai ripubblicato). Corregge il gap CRITICO A.1.
- **[DA VALIDARE — domanda 8]:** modifica sostanziale → serve re-consenso degli utenti esistenti? Il meccanismo di versioning del consenso (oggi §6, solo AI) va esteso al ToS?

**Certificazione dei diritti d'autore** — invariata nel principio; **[DA VALIDARE — domanda 1]** se la sola dichiarazione trasferisce la responsabilità civile e se il disclaimer di non-verifica regge quando il contenuto derivato è poi pubblicato. Allineare inoltre alla versione EN, più ampia (aggiunge la non-infrazione di diritti di terzi).

**Manleva** — **estendere alla pubblicazione:**
> …l'utente si impegna a manlevare e tenere indenne MeepleAI da qualsiasi reclamo, danno o spesa derivante da (i) violazioni di diritti di proprietà intellettuale di terzi relative ai contenuti caricati, **e (ii) la conseguente elaborazione e pubblicazione delle schede di comprensione derivate da tali contenuti**.
- **[DA VALIDARE — domanda 6]:** l'estensione (ii) è opponibile? La responsabilità per il contenuto *generato da MeepleAI* può essere manlevata dall'utente, o resta in capo a MeepleAI?

**Conformità DMCA / takedown** — **unificare il canale:**
> Le notifiche di rimozione per violazione del diritto d'autore possono essere inviate tramite la pagina [Richiesta di rimozione](/legal/takedown) o all'indirizzo **`[CANALE UFFICIALE — DA CONFERMARE]`**.
- **[DA VALIDARE — domande 4 e 5]:** (a) quale mailbox/dominio è quello ufficiale (`takedown@meepleai.app` come da policy/ADR-051, oppure `legal@meepleai.com` come da resto del sito .com)? (b) La procedura notice-and-takedown attuale è sufficiente per DSA (UE) e DMCA §512 (US)? Servono counter-notice, agente designato, contenuti minimi della notifica?

---

### §5-bis (NUOVA) — "Schede di comprensione delle meccaniche" — 🔴 nuova clausola

**Proposto:**
> MeepleAI può generare, a partire da un regolamento caricato, una **scheda di comprensione delle meccaniche** del gioco. Tali schede: (a) sono **riformulate in parole originali** e non riproducono il testo del manuale; (b) riportano **citazioni brevi** della fonte con l'indicazione della pagina, a solo scopo di **attribuzione**; (c) sono pubblicate **solo dopo revisione e approvazione umana** di un amministratore; (d) restano soggette a **rimozione su richiesta** dell'editore (vedi §5). Il copyright del testo originale del manuale resta dei rispettivi editori.

- **Razionale:** ADR-051 §Policy (riformulazione obbligatoria, citazione con attribution, review gate, takedown) + §Attribution UI.
- **[DA VALIDARE — domande 2 e 3]:** enunciare o meno una **soglia numerica** (il cap tecnico interno è 25 parole)? ADR-051 la qualifica come "standard editoriale conservativo, non soglia legale". Il consulente decide se scriverla nel ToS (rischio auto-vincolo) o mantenerla come parametro editoriale interno. → Il testo proposto usa deliberatamente **"citazioni brevi"** senza numero, in attesa del parere.

---

### §6 — "Utilizzo dell'Intelligenza Artificiale" — 🟡 estensione

**Aggiungere** al disclaimer di accuratezza (oggi riferito alle risposte in chat) un rimando esplicito alle card:
> Le stesse avvertenze di possibile inesattezza si applicano alle schede di comprensione (§5-bis): pur soggette a revisione umana, **possono contenere errori** e non sostituiscono la consultazione del regolamento originale.
- **Razionale:** coerenza col gap A.2. **[DA VALIDARE — domanda 7]:** il disclaimer basta a limitare la responsabilità per inesattezze *lesive* in una card **pubblicata** (danno reputazionale editore / induzione in errore)?
- **Nota fedeltà:** la EN di §6 già cita "or add new providers" e "Data stays within our infrastructure" assenti in IT → allineare le due lingue.

---

### §7 — "Proprietà Intellettuale" — 🟢 fix + integrazione

- **Fix refuso:** «tutti i suoi contenuti originali, funzionalità e funzionalità» → «tutti i suoi contenuti originali, funzionalità e servizi».
- **Integrazione (fair use / citazione), testo proposto in aggiunta:**
> Le schede di comprensione (§5-bis) utilizzano brevi citazioni del regolamento a scopo di attribuzione, nell'esercizio del diritto di citazione e degli usi leciti previsti dalla legge applicabile. Le meccaniche e le regole di gioco, in quanto fatti, non costituiscono di per sé oggetto di privativa; è tutelata la sola espressione testuale, che resta degli editori.
- **[DA VALIDARE — premesse §2 del package]:** conferma delle assunzioni (fatti non copyrightable; diritto di citazione in contesto **commerciale + login-gated**).

---

### §8 — "Limitazioni di Responsabilità" — 🟡 estensione

**Aggiungere** che le limitazioni coprono anche il contenuto derivato pubblicato:
> Le presenti limitazioni si applicano anche ai contenuti generati automaticamente e pubblicati da MeepleAI, comprese le schede di comprensione, nei limiti consentiti dalla legge applicabile.
- **[DA VALIDARE — domanda 7]:** tenuta della limitazione per contenuto AI-generato pubblicato.

---

### §11 — "Legge Applicabile" — 🟡 nota internazionale

- Attuale: legge IT/UE, foro Milano. **[DA VALIDARE — domanda 9]:** poiché la feature ha esposizione anche US (fair use, editori USA) e le card sono accessibili a utenti in altre giurisdizioni, la scelta di foro/legge è opponibile o serve una disciplina per l'utenza internazionale?

---

### §12 — "Contatti" + coerenza dominio — 🟡

- **[DA VALIDARE — domanda 5]:** definire il **dominio ufficiale** (il sito usa `meepleai.com`, il takedown `meepleai.app`) e allineare tutti gli indirizzi (`legal@`, `privacy@`, `support@`, `takedown@`) su un unico dominio. Correzione **puramente redazionale ma bloccante** per la coerenza del documento legale.

---

## C. Clausola cross-feature (ADR-059) — 🟢 opzionale nella stessa revisione

**[DA VALIDARE — domanda 10]:** ADR-059 §8.5.6 richiede una clausola su **"fonti dati pubblicamente disponibili"** per il catalogo (Wikidata-primary + BGG-fallback). Bozza di aggancio:
> Alcune informazioni di catalogo (titoli, metadati) provengono da **fonti dati pubblicamente disponibili**; MeepleAI ne cura selezione e presentazione e gestisce le richieste di rettifica/rimozione secondo le procedure indicate.
Da valutare se trattarla nella stessa revisione o separatamente.

---

## D. Note implementative (SOLO dopo validazione legale)

Quando il consulente ha validato/corretto le clausole, l'implementazione tocca:
- `apps/web/src/locales/it.json` **e** `apps/web/src/locales/en.json` — chiavi `legal.terms.sections.*` (aggiungere `mechanicCards` per §5-bis; il test `i18n-legal-keys.test.ts` **richiede parità di chiavi IT/EN**).
- `apps/web/src/app/(public)/terms/page.tsx` — array `TERMS_SECTIONS` (inserire la nuova sezione nell'ordine) **e** `lastUpdated={new Date('2026-03-09')}` → nuova data di pubblicazione.
- Meccanismo di **re-consenso**/versioning se il legale lo richiede (§8 del package).
- Unificazione dominio/mailbox: `it.json`/`en.json` (`legal.terms`, `legal.takedown`, `legal.privacy`, `pages.contact`) + eventuali metadata SEO `.com` in `page.tsx`.

## E. Riepilogo `[DA VALIDARE]` → domande del package
| # | Punto | Domanda package |
|---|---|---|
| §5 | trasferimento responsabilità via dichiarazione | 1 |
| §5-bis | soglia citazione (25 parole sì/no nel ToS) | 2, 3 |
| §5 | canale + sufficienza notice-and-takedown | 4, 5 |
| §5 | estensione manleva alla pubblicazione | 6 |
| §6/§8 | responsabilità contenuto AI pubblicato | 7 |
| §5/§5-bis | re-consenso per modifica sostanziale | 8 |
| §11 | giurisdizione internazionale | 9 |
| §C | clausola fonti pubbliche (ADR-059) | 10 |

---

*Bozza istruttoria. Nessuna clausola qui proposta è validata legalmente; i `[DA VALIDARE]` sono vincolanti — non pubblicare senza opinione scritta del consulente.*
