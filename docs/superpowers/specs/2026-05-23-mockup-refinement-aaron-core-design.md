# Mockup Refinement — Aaron Core + Cross-cutting (Approccio B)

**Date**: 2026-05-23
**Status**: design — pending implementation plan
**Author**: socratic refinement session (Cockburn / Wiegers / Adzic / Fowler / Nygard)
**Scope**: raffinamento mockup cluster libro-game (CORE Aaron) + chat full-screen (B11b) + state matrix cross-route (B18)
**Out of scope**: B11/B11a/B14/B15/B16/B17 (deferred to Phase 2-3, vedi sezione *Out of scope*)
**Related audit**: [`docs/for-developers/audits/2026-05-22-mockup-gaps.md`](../../for-developers/audits/2026-05-22-mockup-gaps.md)

---

## 1. Context

### 1.1 North-star + segmenti attivi

**North-star**: **Aaron libro-game enthusiast** (multi-segment ma Aaron prioritario).

**Segmenti attivi al 2026-05-23**:

| Segmento | Status | Pagine principali |
|---|---|---|
| Libro-game enthusiast (Aaron) | ✅ ATTIVO PRIMARY | `/library/[gameId]/play/[campaignId]/translate`, `/gamebook`, `/chat/[threadId]` |
| Boardgamer host + player | ✅ ATTIVO secondary | `/game-nights`, `/sessions/live/[sessionId]`, `/play-records` |
| Casual browser pre-registrazione | ✅ ATTIVO secondary | `/shared-games`, `/discover`, `/faq` |
| AI builder / KB curator | ⏸️ PARCHEGGIATO (strategy ambigua) | `/editor/agent-proposals/*`, `/agents/*`, `/knowledge-base/*` |

#### 1.1.1 Aaron actor variations (supporting actors)

Aaron non è isolato. Le actor variations attive influenzano i mockup:

| Variation | Comportamento | Design impact |
|---|---|---|
| **Passive co-player** (compagna/partner che osserva) | Legge lo schermo a distanza ~50cm, non tocca il telefono | • **Reader mode**: toggle font 16pt → 24pt nel translate viewer + chat (vedi sezioni 1b e 6.4)<br>• **Wake-lock**: durante `/translate`, `/play/[campaignId]`, `/chat/[threadId]` lo schermo non si auto-blocca<br>• Indicator visibile: badge 🔆 *"Schermo attivo"* con override manuale<br>• Contrasto: AA elevated (target AAA dove possibile) |
| **Phone-passing** (entrambi toccano lo stesso device, stesso account) | Aaron passa il telefono alla compagna che fotografa, lui legge la risposta, lei aggiunge parola al glossario | • **No actor attribution**: glossary entries, history, chat messages NON taggate per autore (coerente con sezione 1c `contexts[]` che ha solo `book_id`)<br>• **Session state preserved**: navigazione tra `/translate` ↔ `/chat` ↔ `/glossary` mantiene state senza re-login né reload (allinea con `?mode=manual` deeplink, sezione 1d) |

**Out of scope** (actor variations non selezionate):
- Own-device co-player con account separato (multi-user campaign sync) — feature parcheggiata
- Bambino spectator con TTS — TTS UI placeholder ma backend deferito

### 1.2 JTBD #1 di Aaron

**Tradurre paragrafo da foto** del libro fisico.

Cluster di mockup critici per il JTBD #1:
- `librogame-runthrough-translate-viewer.html` (page-mock)
- `sp6-libro-game-photo-upload.html` (camera step)
- `librogame-runthrough-glossary-editor.html` (glossary overlay)
- `sp6-libro-game-translation-viewer.jsx` + `sp6-libro-game-glossary-editor.jsx` (twin jsx)

### 1.3 Gap identificati (4 aree)

1. **Failure modes OCR/AI puntuali** — `librogame-runthrough-error-states` copre solo OCR-fail/LLM-503 generici; mancano 6 stati specifici.
2. **Loading + progress durante elaborazione** — mockup attuali non mostrano stato intermedio durante i ~17s di OCR+translate; rischio di hot-reload utente.
3. **Glossario conflict / merge / context** — modello dati attuale non supporta varianti per book; manca conflict resolution UI e bulk import.
4. **Multi-language source + manual fallback** — mockup assumono sorgente EN; nessun supporto per FR/DE/ES/IT o per manual text input quando foto fallisce.

### 1.4 Approccio selezionato — Opzione B

**Aaron-core + cross-cutting foundation** (~3 settimane mockup work):

- Cluster translate raffinato (4 sezioni 1a-1d)
- B11b chat full-screen (carry-over closure parziale #491, Aaron-relevant)
- B18 state matrix cross-route (foundation per Fase 2-3)

Trade-off accettato: boardgamer game-night gap (B11/B17) restano non coperti; US-32 ancora bloccante. Razionale: Aaron è north-star + B18 ammortizza su tutti i raffinamenti futuri.

---

## 2. Section 1a — Failure modes OCR/AI puntuali

**File target**: estensione di `admin-mockups/design_files/librogame-runthrough-error-states.html` (NO nuovo file — preserva pattern cross-cutting PR #1056).

**Route impattata**: `/library/[gameId]/play/[campaignId]/translate`

**6 stati nuovi (oltre ai 4 esistenti)**:

| Stato | Trigger | UX |
|---|---|---|
| `ocr-low-confidence` | OCR completato, `confidence < 0.7` su ≥1 regione | Inline highlight regioni dubbie (giallo) + CTA *"Ritocca foto"* / *"Procedi comunque"* |
| `photo-blur-pre-ocr` | Autofocus score < threshold (client-side, pre-upload) | Reject in-camera prima di upload + CTA *"Riprova"* (zero roundtrip server) |
| `translation-timeout` | AI translate abort dopo 20s | Banner *"Traduzione lenta — riprova o modifica manualmente"* + 2 CTA |
| `source-lang-unknown` | LLM ritorna `confidence_lang < 0.5` | Dialog *"Lingua non riconosciuta. Conferma:"* + dropdown 5 lingue |
| `network-mid-ocr` | Loss connection durante upload foto | Banner persistent *"Connessione persa — foto salvata localmente, riprova quando torni online"* |
| `quota-exhausted-mid-stream` | Token quota termina durante AI translate | Bridge a `librogame-runthrough-quota-credits` overlay + retain OCR-decoded paragraph come fallback (no token aggiuntivi consumati) |

**Pattern decisions**:
- **Retry granularity**: per `OCR-fail` → retry solo OCR step (foto in cache locale); per `LLM-503` → retry solo AI step (OCR result conservato). MAI retry-from-scratch automatico.
- **Confidence threshold**: low-confidence default `0.7` (futuro configurabile in `/settings/preferences`).
- **Fallback path**: ogni failure ha minimo 1 fallback non-distruttivo (manual input, save-and-retry, alternative provider). MAI dead-end.

**Acceptance criteria**:
- [ ] 6 nuove cellule nel `librogame-runthrough-error-states.html`
- [ ] Ogni cellula documenta: trigger condition, recovery action, fallback path
- [ ] Light + dark mode
- [ ] MOCKUPS_INDEX resta `component-mock`

---

## 3. Section 1b — Loading + progress durante elaborazione

**File target**: estensione di `admin-mockups/design_files/librogame-runthrough-translate-viewer.html`.

**Loading sequence multi-step** (4 step nominati, non % anonima):

| Step | Durata target | UI | Label IT |
|---|---|---|---|
| 1. Upload foto | 0-2s | Linear progress bar percentage + thumbnail | *"Caricamento foto…"* |
| 2. OCR (lettura) | 2-7s | Skeleton paragrafo 3-righe + shimmer-sweep | *"Sto leggendo il libro…"* |
| 3. AI translation | 7-15s | Paragrafo OCR in 60% opacity sopra + overlay tradotto token-by-token | *"Sto traducendo…"* |
| 4. Glossary check | 15-17s | Highlight progressivo parole nuove (badge ⊕) | *"Cerco parole nel glossario…"* |

**Pattern decisions**:
- **Multi-step vs single-phase**: scelto multi-step per trasparenza diagnostica.
- **Abort CTA**: visibile dal step 2 in poi. Click → torna a camera step con foto preservata per retake.
- **Skeleton sizing**: 3 righe dimensionate al paragrafo medio (~150 char), shimmer-sweep gradient.
- **Token-by-token vs final**: step 3 streaming token-by-token, allinea `useThreadMessages` pattern (PR#551).

**Variants viewport**:
- Mobile 375: step list verticale stack, paragrafo sotto, abort FAB bottom-right
- Desktop 1280: step list orizzontale top breadcrumb, paragrafo central, abort top-right

**Latency targets** (Wiegers measurability):
- Step 1 < 2s · Step 2 < 5s · Step 3 < 10s · Step 4 < 2s · Total < 17s
- Hard timeout 20s totale → trigger `translation-timeout` di sezione 1a
- Documentati nei commenti HTML head

**Reader mode + wake-lock** (per passive co-player, sezione 1.1.1):
- **Reader mode toggle**: button 📖 nel header translate-viewer, switch font body 16pt → 24pt + line-height +30% + padding maggiore. Stato persistito in `localStorage` per sessione. Visibile anche durante step di loading (skeleton scala al font scelto).
- **Wake-lock indicator**: badge 🔆 *"Schermo attivo"* visibile bottom-right durante translate viewer + play session attivi. Click → disable wake-lock con feedback toast *"Schermo torna alla normalità"*. Auto-re-enable su nuova foto/azione.
- **Contrasto AAA-targeting**: paragrafo tradotto usa coppia colore con ratio ≥7:1 (vs AA standard 4.5:1), centrato, max-width 65ch per leggibilità.

**Acceptance criteria**:
- [ ] 4 step espliciti, label IT
- [ ] Skeleton dimensionato + shimmer animato + `prefers-reduced-motion` fallback statico
- [ ] Abort visibile da step 2
- [ ] Token-by-token rendering in step 3
- [ ] Latency targets nei commenti HTML
- [ ] Reader mode toggle (16pt ↔ 24pt) persistito + applicato a skeleton + paragrafo finale
- [ ] Wake-lock indicator 🔆 con override manuale
- [ ] Contrasto paragrafo tradotto AAA-target (≥7:1)
- [ ] Light + dark + mobile + desktop

---

## 4. Section 1c — Glossario conflict / merge / context

**File target**: estensione di `librogame-runthrough-glossary-editor.html` (component-mock) + twin `sp6-libro-game-glossary-editor.jsx`.

**Modello dati proposto** (riflesso nei mockup):

```
GlossaryEntry {
  term: "sentinel"
  base_definition: "soldato di guardia"          ← primary
  contexts: [
    { book_id: "press-start", definition: null,  ← uses base
      first_seen: paragraph_id_X },
    { book_id: "rules-game", definition: "punto di osservazione strategica",  ← variant
      first_seen: paragraph_id_Y }
  ]
}
```

**4 sub-screen da aggiungere**:

| Sub-screen | Trigger | UI |
|---|---|---|
| `conflict-dialog` | OCR trova parola già nel glossario con `context_similarity < 0.85` | Modal con 3 CTA: ① *"Mantieni esistente"* (default) · ② *"Sostituisci"* · ③ *"Aggiungi variante"* |
| `bulk-import-card` | OCR trova ≥3 parole nuove non-glossario in un paragrafo | Inline card sotto traduzione: *"5 nuove parole rilevate"* + chip preview + CTA *"Aggiungi tutte"* / *"Rivedi"* (modal multi-select) |
| `variant-expansion` | Click su term con `contexts.length > 1` | Row espande mostrando N varianti con book badge + definition specifica. Collapse-by-default per density |
| `filter-strip + context-badges` | Sempre visibile in glossary index | Header con: search by term/def/book · filter by book (chip multi-select) · filter "solo con varianti" toggle · ogni term row mostra `📖 Press Start` + `📖 Rules Game` badges |

**Pattern decisions**:
- **Default conflict resolution**: auto-keep esistente se `context_similarity ≥ 0.85` (silenzioso); mostra dialog solo `< 0.85`.
- **Context similarity**: server-side via embedding cosine (riusa `IEmbeddingService`); threshold `0.85` documentato.
- **Bulk import trigger**: `≥3` parole, non `≥1` — evita card-fatigue su paragrafi corti.
- **Variant deletion**: utente può eliminare singola variante; se elimina l'ultima, term torna single-context.

**Decisione architetturale incidentale** (Fowler): il modello `GlossaryEntry` con `contexts[]` implica backend schema migration su `glossary_entries`. Il mockup *propone* il modello; l'implementazione backend è separata (issue/spec a parte, non in questo design).

**Acceptance criteria**:
- [ ] 4 sub-screen documentate
- [ ] Threshold `context-similarity = 0.85` annotato
- [ ] Bulk import threshold `≥3` annotato
- [ ] Variant expansion/collapse + filter strip + context badges
- [ ] Twin `.jsx` aggiornato di pari passo
- [ ] Light + dark + mobile + desktop
- [ ] MOCKUPS_INDEX resta `component-mock`

---

## 5. Section 1d — Multi-language source + manual fallback

**File target**: estensione di `librogame-runthrough-translate-viewer.html` + `sp6-libro-game-photo-upload.html` + twin `sp6-libro-game-translation-viewer.jsx`.

**Source language model**:

- Auto-detect via LLM su OCR result (riusa stessa call di AI translation)
- `confidence < 0.5` → trigger `source-lang-unknown` di sezione 1a (forza override)
- `confidence 0.5-0.8` → badge tap-to-confirm prima di translate
- `confidence > 0.8` → badge informativo, no friction

**Lingue supportate v1**: EN · FR · DE · ES · IT (sorgente). Target = **IT fisso** (derivato da setting profilo). Multi-target out-of-scope v1.

**3 sub-screen da aggiungere**:

| Sub-screen | Trigger | UI |
|---|---|---|
| `lang-detection-badge` | Post OCR step 2 | Header pill *"Sorgente: EN · Conferma o cambia"* (top-right). Click → dropdown override |
| `lang-override-modal` | Tap su badge | Modal radio 5 lingue + CTA *"Conferma e ritraduci"* (re-trigger step 3, skip step 2 OCR cache hit) |
| `manual-input-mode` | Triple-entry (vedi sotto) | Layout translate-viewer SENZA foto thumbnail: textarea multiline (max 2000 char + counter) · lang dropdown pre-filled last-used · book ref pre-filled current campaign · CTA submit → skip OCR, va direttamente step 3 |

**Manual mode — triple-entry discoverability** (Cockburn: actor variations):
1. CTA *"Digita manualmente"* in error state `photo-blur-pre-ocr` (sezione 1a)
2. Kebab menu `⋮` del camera step
3. Empty state quando `/translate` aperto senza foto: card *"Libro non a portata? Digita il paragrafo"*

Route invariata, query param `?mode=manual` per deeplink/share.

**Pattern decisions**:
- **Target lang fixed IT v1**: scope-cut intenzionale (multi-target = lang pairs N², complessità esponenziale).
- **5 lingue v1**: copre ~95% libri-game europei.
- **Manual mode parity**: stessi step 3-4 della sezione 1b. Unica diff = step 2 OCR sostituito da textarea.
- **Source method tracking**: ogni traduzione persistita con `source_method: "ocr" | "manual"` per analytics futuro.
- **Override re-translate**: lang change post-fact preserva OCR result (cache hit) — re-translate solo step AI. Token saving.

**Decisione architetturale incidentale** (Fowler): `?mode=manual` come query param vs sub-route `/translate/manual` — scelto **query param** perché stato `manual` è UI-only (no backend distinto) e mantiene `useThreadMessages` deeplink-friendly.

**Acceptance criteria**:
- [ ] 3 sub-screen documentate
- [ ] 5 lingue sorgente (EN, FR, DE, ES, IT) · target IT fisso v1
- [ ] Manual mode con triple-entry discoverability
- [ ] Length limit 2000 char + counter
- [ ] `source_method` tracked nello stato
- [ ] OCR result preservato su lang override (no re-OCR)
- [ ] `?mode=manual` query param per deeplink
- [ ] Twin `.jsx` aggiornato
- [ ] MOCKUPS_INDEX resta `page-mock`
- [ ] Light + dark + mobile + desktop

---

## 6. Section 2 — B11b Chat full-screen (carry-over #491)

**File target**: nuovo `admin-mockups/design_files/chat-fullscreen.html` + twin `.jsx`.
**NOTA**: NON estendere `nanolith-nav-chat-panel.html` (resta `component-mock` slide-over per cross-page quick-access).

**Route impattate**: `/chat/[threadId]` (page-mock principale) · `/chat/new` (empty state)

### 6.1 Screen B11b-1 — Desktop 3-col (1280px)

| Colonna | Width | Contenuto |
|---|---|---|
| Sidebar sx | 260px | Thread list tabs (`Tutti`/`Preferiti`/`Archiviati`) + search + new-thread CTA + filter by agent |
| Main center | ~720px | Chat stream bubble + typing indicator + composer multiline autoresize bottom |
| Sidebar dx | 260px | Context panel: agent info mini-card + cited sources list + suggested actions |

### 6.2 Screen B11b-2 — Mobile (375)

- Header sticky: ← back + thread title + agent avatar
- Message stream scroll
- Bottom composer fixed (collapse autoresize 1-row default)
- Tap header → bottom sheet context panel
- Tap citation pill → PDF viewer overlay full-screen

### 6.3 Screen B11b-3 — Empty state (`/chat/new`)

Hero *"Inizia una conversazione"* + 4 quick-starter cards **adattate per agent type**:

| Agent type | Quick-starters |
|---|---|
| Libro-game (Aaron) | *"Quali sono le regole di [game]?"* · *"Spiega questa Encounter"* · *"Riassumi paragrafo X"* · *"Lista personaggi"* |
| Boardgamer game-rules | *"Setup iniziale"* · *"Regole edge case"* · *"Strategie comuni"* · *"FAQ"* |
| Default (no agent) | CTA *"Scegli un agent"* + dropdown picker prominent |

### 6.4 Pattern decisions

- **Slide-over vs full-screen routing**: automatico per viewport. Mobile/tablet (<1024px) → sempre full-screen (slide-over diventa drawer fallback). Desktop (≥1024px) → slide-over default, toggle *"View as full-screen"* nello slide-over header per immersive mode.
- **Streaming**: token-by-token con AbortController, riusa `useThreadMessages` (PR#551). Cancel CTA visibile durante stream attivo.
- **Citations**: pill click → `sp4-citation-pdf-viewer` overlay (reuse, NO fork).
- **Attachment Aaron-specific**: foto inline apre flow translate (sezione 1b/1d) come quick-action; risposta translate ritorna come citation pill. Ponte chat ↔ translate.
- **Voice**: UI placeholder, backend integration Whisper deferita (out of scope mockup).
- **Quick-starter selection**: agent type da `agent.metadata.category` (verificare esistenza campo in `AgentDefinition` durante impl; se mancante, fallback graceful a *"Default (no agent)"* quick-starters).
- **Reader mode + wake-lock** (per passive co-player, sezione 1.1.1): stesso toggle 📖 della sezione 3 (translate viewer) — applicabile a bubble dei messaggi (font 16pt → 24pt). Stessa `localStorage` key (`reader-mode-enabled`) → coerenza cross-route Aaron. Wake-lock attivo durante chat con streaming attivo.

### 6.5 Stati di stream (Nygard — failure modes)

- `idle` — empty composer
- `streaming` — token-by-token + cancel CTA visibile
- `stream-error` — banner *"Errore stream — riprova"* + last user message conservato in composer (no perdita)
- `stream-abort` — utente click cancel → message preservato come *"[interrotto]"* badge

**Acceptance criteria**:
- [ ] 3 screen documentati (desktop 3-col, mobile, empty)
- [ ] 4 stream stati (idle/streaming/error/abort) visibili
- [ ] Citation pill → PDF overlay (sp4-citation-pdf-viewer reuse)
- [ ] Quick-starter contestualizzati per agent type
- [ ] Slide-over ↔ full-screen routing automatico per viewport
- [ ] Attachment Aaron quick-translate bridge documentato
- [ ] Voice come placeholder UI
- [ ] Reader mode toggle 📖 (16pt ↔ 24pt) coerente con sezione 1b (stessa `localStorage` key)
- [ ] Wake-lock attivo durante stream + override manuale
- [ ] Reuse: `nanolith-nav-chat-panel` (slide-over secondary) · `sp4-citation-pdf-viewer` · `sp4-agent-detail` (mini)
- [ ] Light + dark + mobile + desktop
- [ ] MOCKUPS_INDEX nuovo entry `chat-fullscreen.html` (page-mock)

---

## 7. Section 3 — B18 State Matrix cross-route

**File target**: nuovo `admin-mockups/design_files/state-matrix.html` (classificato `dev-fixture` come `01-screens.html`).

### 7.1 Route × Stati grid (8 × 5)

| Route | Segmento | Empty | Error | Loading | Permission | Offline |
|---|---|---|---|---|---|---|
| `/library/[gameId]/play/[campaignId]/translate` | Aaron CORE | "Fotografa una pagina" + camera CTA | sez 1a riuso | sez 1b 4-step skeleton | Tier-locked OCR quota | banner + last-translation cache |
| `/library/[gameId]/play` | Aaron resume | "Nessuna campagna — inizia un libro" + library CTA | "Errore caricamento — riprova" | 3-card skeleton | — | banner + campagne cached |
| `/chat/[threadId]` | Aaron + boardgamer | "Inizia conversazione" + quick-starters (sez 2) | sez 2 stream-error | typing indicator + skeleton bubble | Tier-locked chat quota | banner + msg history cached |
| `/game-nights` | boardgamer | "Crea la prima serata" + new-night CTA | "Calendar non disponibile" | calendar skeleton 7d | — | banner + serate cached |
| `/sessions/live/[sessionId]` | boardgamer | "Aspetta che host inizi" | SSE disconnect banner + reconnecting | spinner + game cover | "Solo l'host può iniziare — aspetta che la sessione si attivi" | banner — modalità local-only |
| `/shared-games` | browser | "Catalog vuoto — riprova" | "Catalogo non disponibile" | card-grid 6-skeleton | — | banner + cached results |
| `/discover` | browser cross | "Nessun consiglio ora" | "Discovery offline" | rail skeleton 3-row | — | banner + cached |
| `/notifications` | cross-cutting | "Sei in pari!" | "Errore caricamento notifiche" | list skeleton | — | banner + cached |

### 7.2 Pattern per stato (riusabili)

**Empty**: icon 96px centrale + gradient bg entity color + tagline IT route-specific + primary CTA per uscire (mai dead-end).

**Error**: riuso `librogame-runthrough-error-states` (icon + message umano + retry + report-bug). Annotato in commento HTML head per traceability.

**Loading skeleton**: 3-row card dimensionato al contenuto reale (3 game-cards per `/shared-games`, 7-day calendar per `/game-nights`, ecc.). Shimmer-sweep gradient. `prefers-reduced-motion` → fallback statico.

**Permission-denied**: 🔒 lock + tier badge *"Pro"*/*"Premium"* + tagline *"Disponibile nei piani Pro/Premium"* + primary CTA *"Esplora piani"* → `/pricing` (route esistente ma senza mockup canonico — vedi audit gap; il link sarà dead-end visivo finché B13 non viene aperto) + secondary *"Torna indietro"*.

**Offline**: banner top persistent (📡 WiFi-off + *"Offline — sto usando dati salvati"*) + cache-stale data a 80% opacity + retry inline.

### 7.3 Pattern decisions

- **Singolo matrix vs files separati**: **singolo file**. Density + visibility cross-route consistency batte file-fatigue. ~40 phone-frames in grid 8×5 scrollable.
- **Mobile-only viewport**: come `01-screens.html`. Desktop pattern emerge nei page-mock dedicati. Burden ridotto a 40 vs 80 cell.
- **Light/dark inline toggle**: button top destro swap mode. Evita 2 file paralleli.
- **Illustration economy**: ~5 illustration uniche con sub-set tematici riusabili (*"book"*: translate+play · *"speak"*: chat+notifications · *"calendar"*: game-nights+sessions-live · *"compass"*: shared-games+discover).
- **Permission-denied dummy**: solo 3 route ne hanno bisogno (translate, chat, sessions-live host-only); altre 5 = `—` (skip cell).
- **Reuse trace**: ogni cell linka via commento HTML al pattern di origine.

**Cross-segment value**: investimento mockup state-matrix ammortizza su tutti i raffinamenti futuri (Fase 2 boardgamer, Fase 3 community/wishlist). Foundation cross-cutting.

**Acceptance criteria**:
- [ ] 40 cell visibili in grid 8×5 phone-frame mobile 375
- [ ] Light + dark toggle inline
- [ ] 5 pattern per stato documentati riusabili
- [ ] Tagline IT route-specific (no placeholder generici)
- [ ] Reuse trace nei commenti HTML head per ogni pattern
- [ ] Permission-denied applicato solo a 3 route
- [ ] `prefers-reduced-motion` fallback per skeleton shimmer
- [ ] MOCKUPS_INDEX nuovo entry `state-matrix.html` come `dev-fixture`

---

## 8. Out of scope

| Brief | Razionale esclusione | Phase ripristino |
|---|---|---|
| B11 Play Records | Bloccante US-32 boardgamer, ma north-star = Aaron. Boardgamer riposizionato come secondary. | Phase 2 post-validazione demo Iter 1 |
| B11a Community follow-up | Segmento community è secondary; #492 closure false-positive non blocca Aaron. | Phase 2 |
| B14 Editor agent-proposals | Segmento AI builder parcheggiato (strategy ambigua). | Gated on AI builder strategy decision (no scheduled phase) |
| B15 Toolkit sub-pages | Toolkit non-CORE per Aaron; boardgamer secondary. | Phase 3 |
| B16 Library Wishlist | Cross-segment low-priority; impl attiva ma non bloccante. | Phase 3 |
| B17 Sessions sub-pages | Boardgamer secondary + IA nodo N7 (consolidation decision) ancora aperto. | Phase 2 con prior N7 decision |

**Nodi architetturali deferiti** (Phase 3):
- N4 — routing duplicate `/private-games/[id]` vs `/library/private/[privateGameId]`
- N6 — tabs vs routes per `/games/[id]/*`
- N7 — sessions sub-route consolidation

---

## 9. Acceptance criteria globali

- [ ] 6 sezioni mockup-work consegnate (1a, 1b, 1c, 1d, 2, 3)
- [ ] 3 file modificati: `librogame-runthrough-error-states.html`, `librogame-runthrough-translate-viewer.html`, `librogame-runthrough-glossary-editor.html` + 2 twin jsx aggiornati
- [ ] 2 file nuovi: `chat-fullscreen.html`, `state-matrix.html` (+ jsx twins)
- [ ] `admin-mockups/MOCKUPS_INDEX.md` aggiornato (count: 48 page-mock → 49, 16 component-mock invariato, 10 dev-fixture → 11)
- [ ] `docs/for-developers/frontend/v2-migration-matrix.md` route mapping aggiornato
- [ ] Tutti i mockup: light + dark mode, mobile 375 + desktop 1280, `prefers-reduced-motion` rispettato
- [ ] Audit `2026-05-22-mockup-gaps.md` aggiornato a *"Phase 1 in implementation"* per le sezioni coperte
- [ ] **Aaron actor variations** (sezione 1.1.1): reader mode toggle 📖 e wake-lock 🔆 coerenti cross-route (`translate-viewer`, `chat-fullscreen`, eventualmente `play-session`). Stessa `localStorage` key `reader-mode-enabled` per persistence.
- [ ] **Contrasto AAA-target** sui paragrafi tradotti (≥7:1) — verificare con axe a livello mockup.

## 10. Dependencies / decisioni architetturali incidentali

Le seguenti decisioni emergono dal design mockup ma richiedono follow-up implementazione separato:

1. **GlossaryEntry contexts model** (sezione 1c) — schema migration backend per supportare `contexts: []`. Spec backend separata, non in questo design.
2. **Context similarity service** (sezione 1c) — `IEmbeddingService` riuso per cosine similarity threshold 0.85 — verificare costo token + latency.
3. **`?mode=manual` query param** (sezione 1d) — pattern UI-only, no backend route distinta. Documentato come decisione esplicita.
4. **Slide-over ↔ full-screen viewport routing** (sezione 2) — breakpoint 1024px hard-coded vs configurabile? Per ora hard-coded; reviewing post-implementazione.
5. **Web Wake Lock API** (sezione 1.1.1 + 1b) — supportata: Chrome 84+, Safari 16.4+, Firefox 126+, Edge 84+. Su browser più vecchi (es. Safari < 16.4 su iPhone con iOS < 16.4) fallback graceful: badge 🔆 mostra *"Funzione non supportata — tieni schermo acceso manualmente"* + link a guida `prefers-reduced-motion`/auto-lock settings. Documentare nel mockup come edge case esplicito.
6. **Reader mode `localStorage` key** (sezione 1.1.1 + 1b + 6.4) — `reader-mode-enabled` boolean, scope per-device (no sync server-side v1). Future: sync via `user_preferences` table se Aaron usa multi-device.

## 11. Next steps

1. **User review** di questo spec doc.
2. **Writing-plans skill** → piano di implementazione dettagliato (file-by-file, ordine, agenti coinvolti).
3. **Workflow design produzione mockup**: Claude Design web (Approccio B descritto in audit 2026-05-22 — brief estratti dalle sezioni 1-3 di questo doc).
4. **Phase 2 trigger**: post-validazione demo Iter 1 → re-evaluate boardgamer priority → decidere se sbloccare B11/B17.

## 12. References

- [`docs/for-developers/audits/2026-05-22-mockup-gaps.md`](../../for-developers/audits/2026-05-22-mockup-gaps.md) — audit fonte
- [`admin-mockups/MOCKUPS_INDEX.md`](../../../admin-mockups/MOCKUPS_INDEX.md) — inventario mockup
- [`docs/for-developers/frontend/v2-migration-matrix.md`](../../for-developers/frontend/v2-migration-matrix.md) — route→mockup matrix
- [`docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md`](./2026-05-07-libro-game-nanolith-demo-design.md) — Iter 1 design Aaron persona
- Issue #491 (closure parziale chat full-screen) · #492 (closure false-positive community)
- PR #1056 (`librogame-runthrough-error-states` pattern) · PR #551 (`useThreadMessages` hook)
