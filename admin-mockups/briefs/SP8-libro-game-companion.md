# SP8 Libro Game Companion — Brief Claude Design (Play-Session extension, 1 mockup esteso)

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Questo brief **estende** il mockup esistente `librogame-runthrough-play-session.html` (serie nanolith-runthrough, persona Aaron) con **3 nuovi stati** per completare le 6 functions v1 del **companion model**. NON è un mockup greenfield: i 4 stati esistenti restano intatti.

## Stato programma

| SP | PR | Stato | Audience |
|----|----|-------|----------|
| SP3 public-secondary | merged | ✅ | nuovi visitatori |
| SP4 entity-desktop | merged | ✅ | utenti autenticati |
| SP5 admin-tools | merged | ✅ | admin/power-user |
| SP6 libro-game MVP + nanolith Iter 1 | merged | ✅ | casual gamer (Sara) + dogfood (Aaron) |
| SP7 game-night | merged | ✅ | host/player serate |
| **SP8 libro-game companion** | **(questo brief)** | ⏳ in design | **Aaron libro-game enthusiast** |

**SP8 = estensione companion, NON nuova feature.** La play-session libro-game (`librogame-runthrough-play-session.html`, 49KB, 4 stati: story/encounter/chat/glossary) copre già 3/6 functions v1 del companion. Mancano **diary**, **paragrafi visitati**, **end-campaign**. Questo brief le aggiunge come 3 nuovi stati nello stesso file.

## Persona target & contesto d'uso

**Aaron, libro-game enthusiast** (`badsworm@gmail.com`), già definito in SP6 Cap.2. Gioca campagne libro-game (Tainted Grail, ISS Vanguard) **con amici al tavolo**, smartphone in mano come **companion** — NON game manager.

**Companion model (vincolo architetturale chiave)**:
> L'app NON gestisce il gioco. È un companion che (1) risponde alle domande sulle regole, (2) traduce le pagine su richiesta, (3) salva lo stato della partita. "Con amici" è scenografico — Aaron è l'unico utente loggato, gli amici giocano fisicamente. NO multi-player coordination, NO invite/join libro-game, NO sync stato fra player.

**Setup tavolo**: salotto, luce media, manuale fisico EN aperto, telefono single-device, WiFi instabile è la norma.

### 6 functions companion v1 (gerarchia confermata)

| # | Function | Gerarchia | Stato mockup |
|---|----------|-----------|--------------|
| 1 | Chat regole (agente) | **PRIMARY** | ✅ esiste (`state-03-chat-overlay`) |
| 2 | Translate pagina (foto) | **PRIMARY** | ✅ esiste (photo action in `state-01-story`) |
| 3 | Save/resume stato | **MENU** (auto-background) | ✅ implicito (paragrafo corrente persistito) |
| 4 | **Note/diary utente** | **SECONDARY** | ❌ **questo brief — state-05** |
| 5 | **Lista paragrafi visitati** | **SECONDARY** | ❌ **questo brief — state-06** |
| 8 | **End campaign / mark complete** | **MENU** | ❌ **questo brief — state-07** |

"Altre funzioni verranno aggiunte in futuro" → v1 si congela a queste 6.

## Scope SP8 — esattamente 3 nuovi stati (1 mockup esteso)

| # | Stato (anchor id) | UI pattern | Function v1 |
|---|-------------------|------------|-------------|
| 1 | `state-05-diary-tab` | 5a tab (mobile) / colonna laterale (desktop) | #4 Note/diary |
| 2 | `state-06-paragrafi-drawer` | Drawer slide-up dalla session header | #5 Paragrafi visitati |
| 3 | `state-07-end-campaign` | Kebab menu → dialog 3-vie → post-close screen | #8 End campaign |

**File target**: estendere `admin-mockups/design_files/librogame-runthrough-play-session.{html,jsx}` (il `.jsx` oggi MANCA — crearlo per gli stati interattivi). NON rigenerare i 4 stati esistenti.

**Sequence di consegna**: state-05 (diary, più ricco) → state-06 (paragrafi drawer) → state-07 (end-campaign).

## Componenti già stabili — NON ridisegnare

Il mockup li **istanzia**, non li clona:

| Componente | Path codice | Riuso in SP8 |
|------------|-------------|--------------|
| Session header + tab system | `librogame-runthrough-play-session.html` (esistente) | base da estendere: +1 tab Diary, +icon history, +kebab |
| `MeepleCard` | `apps/web/src/components/ui/data-display/meeple-card/` | entry diary cards |
| `EntityChip` / `EntityPip` | `apps/web/src/components/ui/data-display/entity-chip/` | ogni cross-reference (§ paragrafo, glossary) |
| `Drawer` (cascadeNavigationStore) | `apps/web/src/stores/cascadeNavigationStore.ts` | state-06 (bottom-sheet mobile / side-panel desktop) |
| `Dialog` / modal primitive | `03-drawer-variants.html` pattern | confirmation jump-back + end-campaign 3-vie |
| `Tabs` animated underline | wave 1 SP4 | tab system esteso (5a tab Diary) |

**Greenfield emergenti** (da implementare poi in `apps/web/src/components/ui/v2/libro-game/`):
- `DiaryMarkdownEditor` (textarea markdown + toolbar B/I/U/📌/🔗 + live preview)
- `VisitedParagraphsDrawer` + `JumpConfirmDialog`
- `EndCampaignDialog` (3-vie) + `PostCloseSummary`

### `entityHsl` helper inline (palette 9 entity)

```js
const ENTITY_HSL = {
  game:    '25 95% 45%',  player:  '262 83% 58%',  session: '240 60% 55%',
  agent:   '38 92% 50%',  kb:      '174 60% 40%',   chat:    '220 80% 55%',
  event:   '350 89% 60%', toolkit: '142 70% 45%',   tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;
```

## Mapping companion concept → entity color

| Concept SP8 | Entity | Razionale |
|-------------|--------|-----------|
| Sessione di gioco attiva | `session` (🎯 indigo) | la play-session è session-themed (coerente col mockup esistente) |
| Nota diary | `session` base + `player` (👤 viola) accent | le note sono contenuto utente dentro la sessione |
| Paragrafo visitato | `kb` (📄 teal) | i paragrafi sono KB chunks (coerente SP6 mapping) |
| Paragrafo corrente (anchor) | `session` (🎯) | il "dove sono ora" è stato di sessione |
| End: Completata | `toolkit` (🧰 green = success semantico) | conclusione positiva |
| End: Archivia | `session` (🎯) | pausa neutra |
| End: Abbandona | `event` (🎉 rose = danger semantico) | azione distruttiva-soft |
| Export PDF | `kb` (📄) | il riassunto è un documento |

## Vincolo dati (GitGuardian gate)

❌ **VIETATO**: UUID-like, bearer (`sk_...`, `eyJ...`), hex ≥32 char.
✅ **OK**: ID short (`gb-tainted-grail`, `diary-12`, `camp-avalon-1`, `§147`).

### Dati realistici da usare

- **Gamebook**: Tainted Grail: The Fall of Avalon (EN, Awaken Realms, §-numbered)
- **Campagna**: "La grotta dei Goblin", Cap 4, paragrafo corrente §214
- **Player names** (gruppo Aaron): Marco, Giulia, Davide, Luca
- **Glossario realistico**: Niamh, Spada di Avalon (Sword of Avalon), Pietra Spettrale (Wraithstone), Korak (NPC)
- **Paragrafi**: §134, §189, §198, §214 (newest-first nella history)
- **Note diary esempio**: "Goblin liberati senza combattere — bonus reputazione +2", "NPC Korak mi deve un favore, citato a §134", "Tesoro nella grotta non preso, tornare dopo §340"

---

## state-05 — Diary tab (`state-05-diary-tab`)

**Function**: #4 Note/diary utente.
**Pattern mobile**: 5a tab nel tab system esistente (Story · Encounter · Chat · Glossary · **Diary**). Scroll-x se 5 tab non entrano in 375px.
**Pattern desktop**: nuova entry "Diary" nella left context panel (380px) + content area destra.

### Content model (decisioni Socratiche)

- **Free-form markdown** (no struttura predefinita)
- **Text-only v1** — NO foto inline (le foto restano nel flow translate; photo-in-diary = v2)
- **Auto-pin paragrafo corrente** — ogni entry ha metadata `§numero + timestamp`
- **Newest-first timeline**
- **Timestamp ibrido** — relativo <24h (`5 min fa`, `ieri 21:34`), assoluto oltre (`12 mag 21:34`)
- **Save model**: **draft locale continuo + commit esplicito**. Ogni keystroke → draft in localStorage (zero perdita su crash/offline). Tap "Salva" → commit + entry in lista.
- **Edit-after-save** consentito

### Layout — lista (default)

- CTA primario top: "+ Nuova nota" (entity=session color)
- Entry card per nota:
  - Header: `📌 §214 · 21:34` (badge paragrafo entity=kb + timestamp mono)
  - Body: testo markdown reso (truncate 3 righe + "espandi")
  - Actions: ✏️ Modifica · 🗑️ Elimina
- Newest-first

### Layout — editor (tap "+ Nuova nota" → full-screen mobile / inline-expand desktop)

- Top bar: `← Annulla` · "Nota §214" · `Salva` (disabled se < 1 char committable)
- Markdown toolbar: B / I / U / 📌 (link paragrafo) / 🔗
- Textarea autofocus (markdown)
- Live preview sotto (throttled 200ms)
- Indicator "Bozza salvata" discreto (conferma draft locale attivo)

### Stati richiesti

- **Default** (3 note newest-first: §214, §198, §134 con testi realistici)
- **Editor pristine** (textarea vuota, Salva disabled, "Bozza" indicator)
- **Editor typed** (~150 char markdown + live preview)
- **Empty** (illustrazione + "Nessuna nota ancora" + CTA "Scrivi la prima")
- **Loading** (skeleton 2 entry)
- **Error save** (banner "Errore salvataggio — riprova" + retry + autosave-local fallback indicator)
- **Offline** (banner "Offline — la nota sarà salvata al ritorno online" + queue indicator)
- **Light + dark**
- **Mobile 375px + desktop 1440px**

### Deviazioni accettate

- Live preview markdown = render statico nel mockup (no parser reale)
- Draft-local indicator = visual placeholder (vero impl usa localStorage/IndexedDB)

---

## state-06 — Paragrafi visitati drawer (`state-06-paragrafi-drawer`)

**Function**: #5 Lista paragrafi visitati.
**Trigger mobile**: tap icon 🕰 history nella session header (a fianco di `§214 · Cap 4`).
**Trigger desktop**: tap breadcrumb-history nella left context panel → drawer slide-in da sinistra.
**Pattern**: Drawer bottom-sheet 75% height (mobile) / side-panel (desktop). Canonico `03-drawer-variants.html`.

### Layout drawer

- Drag handle top (mobile) + "Paragrafi visitati" + close X
- Search box: "Cerca per numero o testo…" — **scope: solo §numero/chapter v1** (NON full-text: l'app ha il testo completo SOLO per paragrafi tradotti → full-text sarebbe asimmetrico, rimandato v2)
- Top sticky: paragrafo **corrente** highlighted (`§214 (ora) · Cap 4`, entity=session)
- Lista **newest-first** (most-recently-visited):
  - `§198 · 22 min fa · Cap 4` + snippet "Entri nel corridoio…" + CTA `→ [vai qui]`
  - `§189 · 1h fa · Cap 4`
  - `§134 · 2h fa · Cap 3`

### Jump-back confirmation dialog (tap `[vai qui]`)

- Title "Tornare al §189?"
- Body "Perderai la posizione corrente §214. Il §214 resterà comunque visitato nella history."
- CTA: `[Annulla]` · `[Sì, torna]`
- Side effects post-confirm: save-state → §189; §214 resta in history (no cancellazione); drawer chiude; story tab → §189

### Stati richiesti

- **Default** (4 paragrafi newest-first + corrente highlighted)
- **Confirmation dialog** (jump-back §189)
- **Empty** (solo §1 visitato — single entry + tip "Continua a leggere per popolare la history")
- **Loading** (skeleton 3 entry)
- **Error** (banner "Impossibile caricare la history" + retry)
- **Offline** (history cached read-only; `[vai qui]` disabled con tooltip "Disponibile online")
- **Light + dark**
- **Mobile + desktop**

### Deviazioni accettate

- Search = filtro client-side su numero/chapter (no full-text backend nel mockup)

---

## state-07 — End campaign (`state-07-end-campaign`)

**Function**: #8 End campaign / mark complete.
**Trigger mobile**: tap kebab `⋮` in alto-dx session header.
**Trigger desktop**: CTA "Chiudi campagna" nel footer della left context panel.

### Kebab menu

```
Impostazioni sessione
─────────────────────
📦 Esporta dati
🏁 Chiudi campagna   ← target
⚙️ Preferenze
```

### Dialog 3-vie soft (tap "🏁 Chiudi campagna")

3 opzioni card-selezionabili:
- **✅ Completata** (entity=toolkit/success): "Ho finito la storia." → side effects: lock paragrafi (read-only history) + **server-side PDF generation** (async endpoint) + resume-picker → "Completate"
- **📥 Archivia** (entity=session): "Chiudo per ora, posso riaprire." → side effects: resume-picker → "Archiviate" con CTA "Riapri" (no lock, no PDF)
- **❌ Abbandona** (entity=event/danger): "Chiudo definitivamente. Posso riaprire con un **singolo confirm**." → side effects: lock paragrafi + resume-picker → "Abbandonate"

> **Nota Socratica**: il reopen di Abbandona usa **single confirm** (NON doppio): riaprire è non-distruttivo, friction proporzionata al rischio nullo.

### Post-close summary screen (dopo conferma)

Solo per **Completata** mostra CTA PDF. Riassunto:
- `✅ Campagna chiusa` · "La grotta dei Goblin"
- Status: Completata · Durata: 3h 24m · Paragrafi visitati: 47 · Note diary: 12 · Foto translate: 8 · Chat regola: 15
- CTA primario (solo Completata): `📄 Scarica PDF riassunto` (entity=kb) — trigger server-side generation
- CTA `🏠 Torna alla libreria` → `/library`

### Stati richiesti

- **Kebab menu aperto**
- **Dialog 3-vie** (3 card selezionabili)
- **Loading** (state transition BE — spinner overlay "Chiudendo…")
- **Error** (banner "Impossibile chiudere — riprova" + retry + "Forza chiusura locale" opzione)
- **Offline** (conferma marcata "pending sync" con badge)
- **Post-close Completata** (summary + CTA PDF)
- **Post-close Archivia** (summary senza PDF, con "Riapri")
- **Light + dark**
- **Mobile + desktop**

### Deviazioni accettate

- PDF generation = CTA placeholder (vero impl chiama endpoint BE async — vedi nota implementazione)
- Confetti/celebration su Completata = opzionale, rispetta `prefers-reduced-motion`

---

## IA aggiornata (post-extension)

**Mobile tab system**: `Story · Encounter · Chat · Glossary · Diary` (5 tab, scroll-x se serve) + session header con `🕰 history` + `⋮ kebab`.

**Desktop split-view**: left context panel (380px) lista 5 tab + secondary actions (History drawer, End campaign) · right content flex.

---

## Definition of Done (per gli stati SP8)

### Token & visual
- [ ] Solo CSS variables da `tokens.css` (zero hex hardcoded)
- [ ] `entityHsl(entity, alpha?)` inline per i 9 entity color
- [ ] Light + dark entrambi (`<html data-theme="dark">`)
- [ ] Mobile 375px + desktop 1440px entrambi

### Componenti
- [ ] EntityChip/Pip per ogni reference (§ paragrafo=kb, glossary, ecc.)
- [ ] Drawer (state-06) usa pattern canonico bottom-sheet/side-panel
- [ ] Tab system esteso NON ridisegna i 4 stati esistenti
- [ ] Dialog 3-vie (state-07) usa `role="alertdialog"`

### Stati
- [ ] Default + Empty + Loading (skeleton) + Error per state-05 e state-06
- [ ] Offline state per tutti e 3 (companion = WiFi instabile)
- [ ] state-07: kebab + dialog + loading + error + 2 post-close variants

### A11y
- [ ] Markdown editor: `role="textbox"`, `aria-multiline`, `aria-label`
- [ ] Drawer: `role="dialog"`, `aria-modal`, focus-trap, ESC-to-close
- [ ] Dialog 3-vie: `role="alertdialog"`, focus su prima azione, ESC=Annulla
- [ ] Kebab: `role="menu"`, arrow-key nav
- [ ] `prefers-reduced-motion`: disabilita slide/bounce drawer + confetti

### Dati
- [ ] Testo UI in italiano
- [ ] Tainted Grail + "La grotta dei Goblin" + glossario realistico (Niamh, Korak)
- [ ] Player names italiani · note diary realistiche
- [ ] NO UUID-like, NO bearer-pattern

---

## File di riferimento da allegare in chat Claude Design

Quando apri la sessione Claude Design (no repo access), allega:

**Obbligatori (preambolo)**:
1. `admin-mockups/briefs/_common.md` — preambolo design system
2. `admin-mockups/briefs/SP8-libro-game-companion.md` — questo brief
3. `admin-mockups/design_files/tokens.css` — design tokens
4. `admin-mockups/design_files/components.css` — classi base
5. `admin-mockups/design_files/data.js` — dati finti (**NON rigenerarlo** — read-only, è shared)

**Base da estendere (CRITICO)**:
6. `admin-mockups/design_files/librogame-runthrough-play-session.html` — il mockup esistente (4 stati) da estendere con i 3 nuovi. NON rigenerare i 4 esistenti.

**Reference visivi 1:1 prod**:
7. `admin-mockups/design_files/librogame-runthrough-translate-viewer.html` — pattern reading-mode + Aaron persona
8. `admin-mockups/design_files/03-drawer-variants.html` — drawer canonico (state-06)
9. `admin-mockups/design_files/sp4-session-summary.jsx` — pattern summary/diary timeline (state-07 post-close)

## Risposta attesa nel thread Claude Design

1. Conferma scope SP8 (3 nuovi stati, estensione NON greenfield, companion model Aaron, persona single-user)
2. **Una risposta = uno stato** (state-05 → state-06 → state-07)
3. File completo HTML + JSX (crea il `.jsx` mancante)
4. Path salvataggio: `admin-mockups/design_files/librogame-runthrough-play-session.{html,jsx}` (esteso)
5. Note finali: deviazioni flaggate + nuovi componenti v2 emersi + stati Socratici coperti

## Note finali per Claude Design

**Tono UI**: warm, casual, paritario (gruppo italiano informale "tu/voi"). Aaron è enthusiast, non power-user tecnico.

**Microcopy hint**:
- Diary empty: "Nessuna nota ancora — scrivi quello che vuoi ricordare"
- Jump-back confirm: "Perderai la posizione corrente" (non "navigation state reset")
- End completata: "Hai finito la storia!" (celebrativo, non "campaign status: completed")
- Offline diary: "La nota sarà salvata appena torni online" (rassicurante, no "sync queue pending")

**Vincolo non-functional**: lettura distante 1.5m al tavolo → body text ≥ 18pt nelle aree di lettura (coerente play-session esistente).
