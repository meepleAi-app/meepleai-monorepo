# Recepimento review legale IP — ToS Mechanic Extractor

**Evidenza:** review legale IP richiesta dal prereq **PR-2** (issue #2954, gate ADR-051 §Follow-up).
**Data review:** 2026-07-15 · **Base:** `tos-ip-review-package.md` + `tos-adr051-redlines-draft.md` + ToS live.
**Esito:** materiale valutato "di buon livello, approccio prudente"; 10 correzioni indicate, alcune **obbligatorie prima del rilascio pubblico**.

> ⚠️ **Nota di scopo.** Il consulente, in chiusura, raccomanda comunque **una review legale professionale finale** sui documenti prima del rilascio pubblico. Questo documento **recepisce** le sue indicazioni in clausole concrete, ma **non sostituisce** quella review finale né costituisce consulenza legale interna. Le formulazioni proposte vanno confermate nel testo esatto prima della pubblicazione.

## Matrice di rischio (dal consulente)
| Area | Rischio | Mitigazione principale |
|---|---|---|
| Copyright UE | Moderato | limitare la pubblicazione di contenuti derivati; clausole IP prudenti |
| DSA | Basso-Moderato | completare la procedura notice-and-action |
| DMCA | Moderato | procedura di notifica più completa (counter-notice, ecc.) |
| Contestazioni editori | Moderato | mitigato da review umana + takedown |

---

## Correzioni recepite (10 punti → clausole concrete)

### 1. 🔴 Licenza limitata (obbligatorio) — §5
**Indicazione:** evitare qualsiasi frase interpretabile come "abbiamo il diritto di usare qualsiasi PDF caricato". La licenza deve essere **limitata, non esclusiva, revocabile ove possibile, solo per erogare il servizio**.
**Clausola finale proposta (§5, IT):**
> Caricando un contenuto, l'utente **dichiara e garantisce** di avere il diritto di caricare tale materiale e concede a MeepleAI **esclusivamente una licenza limitata, non esclusiva e revocabile ove possibile, strettamente necessaria all'erogazione del servizio** (elaborazione del contenuto per l'assistenza personale e, ove applicabile, generazione delle schede di comprensione di cui al §5-bis). La licenza non conferisce a MeepleAI alcun diritto di sfruttamento del contenuto al di fuori di tali finalità.

### 2. 🔴 Nessun "fair use" (obbligatorio) — §7 / premesse
**Indicazione:** **NON** fondare il modello sul *fair use* americano (imprevedibile, è difesa processuale, utenza soprattutto UE). Fondare su: **assenza di copia sostanziale, trasformazione, riformulazione, citazione limitata, diritto di citazione ove applicabile, estrazione di informazioni funzionali**.
**Azione:** rimuovere ogni riferimento a "fair use" da ToS e redlines; sostituire con la formulazione sopra. *(Nota: il package §2 e le domande vanno letti come istruttoria, non come base giuridica: il fair use non è più un pilastro del modello.)*

### 3. 🔴 "25 parole" fuori dal ToS (obbligatorio) — §5-bis
**Indicazione:** non scrivere una soglia numerica nel contratto (il giorno di una citazione di 27 parole sembrerebbe una violazione del proprio contratto). Nel ToS usare **"brevi citazioni strettamente necessarie all'attribuzione"**. Le 25 parole restano in doc tecnica / ADR / controlli automatici.
**Stato:** la bozza redlines **già** usa "citazioni brevi" senza numero → confermato. Nessun "25" nel ToS.

### 4. Takedown più completo — policy + §5
**Indicazione:** aggiungere alla procedura: **counter-notice**, **procedura di ripristino**, **identificazione minima del richiedente**, **dichiarazione di buona fede**, **dichiarazione di titolarità dei diritti**. Inoltre: **nominare un referente copyright stabile** e **mantenere un registro interno dei takedown**.
**Azione:** aggiornare `takedown-policy.md` con questi elementi + il ToS §5 che vi rimanda. → Vedi §9 (DSA) per la sovrapposizione.

### 5. Unificazione dominio email — **DECISO: `takedown@meepleai.app`**
**Indicazione:** unificare i canali sullo stesso dominio.
**Decisione utente (2026-07-15):** il canale takedown ufficiale è **`takedown@meepleai.app`** — già implementato nel ToS §5 (PR #2984), coerente con `takedown-policy.md`, ADR-051, la pagina `/legal/takedown` e `TakedownRequestForm`.
**Residuo (non bloccante per il takedown):** `legal@`/`privacy@`/`support@` restano su **.com** e `abuse@` (User-Agent BGG) su **.app** → l'incoerenza di dominio segnalata dal consulente **persiste sugli altri canali**. Per la coerenza totale raccomandata, unificare anche gli altri su **.app** in un intervento separato; il canale takedown è comunque risolto.

### 6. 🔴 Card pubbliche (obbligatorio) — §5-bis
**Indicazione:** clausola chiara: MeepleAI **può pubblicare card derivate**, **non pubblica il PDF**, la pubblicazione è **soggetta a revisione umana**, l'editore **può chiederne la rimozione**.
**Stato:** la bozza §5-bis **già** copre; confermato dal consulente ("va nella direzione corretta"). Corregge la §5 attuale ("solo uso personale", non più vera).

### 7. Copyright non assoluto — §7
**Indicazione:** evitare frasi troppo assolute. Formulazione consigliata:
> In molte giurisdizioni le meccaniche di gioco, considerate come idee o procedure funzionali, **possono non essere protette** dal diritto d'autore, mentre la **specifica espressione testuale** del regolamento **resta normalmente protetta**.
**Azione:** sostituire la formulazione precedente ("le meccaniche… non costituiscono di per sé oggetto di privativa" — troppo assoluta) con questa.

### 8. Review umana enfatizzata — §5-bis
**Indicazione:** enfatizzare (è una delle difese legali migliori). Il ToS deve dire chiaramente: la pubblicazione **non è automatica**; ogni card è **verificata**; MeepleAI **può rifiutarne la pubblicazione, modificarla, rimuoverla**.
**Clausola finale proposta (aggiunta a §5-bis):**
> La pubblicazione di una scheda **non è automatica**: ogni scheda è sottoposta a **verifica umana** prima della pubblicazione. MeepleAI si riserva il diritto di **rifiutare, modificare o rimuovere** una scheda in qualsiasi momento, a propria discrezione.

### 9. DSA — procedura notice-and-action — policy
**Indicazione:** aggiungere almeno: **punto di contatto unico**, procedura **notice-and-action**, **possibilità di contestazione**, **tempi di gestione**, **registro interno**.
**Azione:** completare `takedown-policy.md` (già vicina) con questi elementi procedurali + esplicitare il punto di contatto unico (coerente con la decisione dominio §5).

### 10. 🔴 Nuova clausola finalità informative (la più importante) — §5-bis / §7
**Indicazione:** aggiungere una clausola nuova:
> MeepleAI **non intende sostituire, distribuire o riprodurre i regolamenti originali**. Le schede generate hanno **finalità esclusivamente informative e descrittive** delle meccaniche di gioco e **non costituiscono una riproduzione del regolamento originale**.
**Azione:** inserire come clausola dedicata (chiarisce l'intento del servizio — riduce sensibilmente il rischio).

---

## Punti che richiedono decisione prima dell'implementazione
1. ~~Dominio email~~ — ✅ **deciso**: canale takedown = `takedown@meepleai.app` (§5). Unificazione degli altri canali su `.app` = intervento opzionale separato.
2. **Re-consenso** utenti esistenti (package domanda 8) — il consulente non l'ha escluso; la modifica §5 è sostanziale → valutare versioning del consenso ToS (esiste già per l'AI, §6).
3. **Review professionale finale** — raccomandata dal consulente prima del rilascio pubblico.

## Checklist implementazione (dopo le decisioni sopra)
- [ ] `it.json` + `en.json` — riscrivere §5 (licenza limitata + card pubbliche), aggiungere §5-bis (card + review umana + finalità informative + citazioni brevi), correggere §7 (copyright non assoluto, **no fair use**), estendere manleva/limitazioni. Parità chiavi IT/EN (`i18n-legal-keys.test.ts`).
- [ ] `page.tsx` — `TERMS_SECTIONS` (nuova sezione) + `lastUpdated` → nuova data.
- [ ] `takedown-policy.md` — counter-notice, ripristino, identificazione richiedente, buona fede, titolarità, referente copyright, registro interno, punto di contatto unico (DSA).
- [ ] decisione dominio email applicata a tutti i canali.
- [ ] valutare re-consenso versionato.
- [ ] review legale professionale finale sui testi.

---

*Recepimento interno della valutazione legale ricevuta il 2026-07-15. Le clausole proposte vanno confermate nel testo esatto dalla review professionale finale prima della pubblicazione.*
