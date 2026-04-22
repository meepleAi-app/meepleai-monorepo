# SP3 — Brief Claude Design: Public Secondary Pages

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Tutti i token, convenzioni, DoD si applicano a questo brief.

## Contesto

MeepleAI ha una sezione pubblica (non-authenticated) in `apps/web/src/app/(public)/`. La parte marketing principale (landing, pricing, about, contact, terms/privacy) è già coperta da `public.jsx`. **Mancano le pagine secondarie** — legali secondari, onboarding all'app tramite invito, FAQ, educazione prodotto, vetrina community.

Questo sub-project sblocca: (1) completamento pubblico per SEO + compliance legale; (2) flussi di invito che portano nuovi utenti dentro l'app; (3) credibilità prodotto via FAQ e how-it-works.

## Fonti di riferimento

- `public.jsx` — pattern nav + footer + hero esistenti (da **estendere**, non riscrivere)
- `notifications.jsx` — pattern list + detail (utile per FAQ accordion)
- `tokens.css` — tutti i token
- `02-desktop-patterns.html` — layout desktop

## Audience

- **Visitatori non loggati** (search engine, link condivisi, inviti)
- **Utenti loggati che cliccano un link legale** dal footer
- Niente stato di "power user" qui

## Schermate da produrre (9)

### 1. FAQ — `sp3-faq.{html,jsx}`

**Route target**: `/faq`
**Scope**: Lista di domande raggruppate per categoria, accordion expandable, search bar in-page, deep-link tramite anchor (`#question-slug`).

**Sezioni**:
- Hero compatto (titolo + subtitle + search)
- 4 categorie: Generale, Agenti AI, Library & Giochi, Account & Billing
- Ogni FAQ item: domanda (click → toggle), risposta con markdown minimo (paragrafi, link, liste), CTA "Hai ancora bisogno di aiuto? Contatta" in fondo
- Sidebar desktop: indice categoria + "Articoli correlati" (usa pattern chips)

**Stati**: default con 12+ FAQ, empty search ("Nessuna FAQ trovata"), expanded single item.

**Componenti v2 da progettare** (nuovi): `FAQAccordion`, `FAQSearchBar`, `FAQCategoryNav`.

---

### 2. How it works — `sp3-how-it-works.{html,jsx}`

**Route target**: `/how-it-works`
**Scope**: Pagina educativa che spiega il prodotto in 4-5 step visivi. Meno vendita, più istruzione — chi arriva qui vuole capire **come funziona**.

**Sezioni**:
- Hero con emoji board-game grande, titolo "Come funziona MeepleAI"
- Step 1: Costruisci la tua library (screenshot mockup search/add gioco)
- Step 2: Gli agenti leggono le regole (visualizza PDF → agent → chat)
- Step 3: Gioca con l'aiuto dell'AI (session live con timer)
- Step 4: Condividi toolkit con la community (pubblica bundle)
- CTA finale: "Inizia gratis" → `/register`

**Stili**: ogni step è un card grande con illustrazione + numero + titolo + 2-3 bullet. Alterna left/right su desktop, stacked su mobile.

**Stati**: default (no varianti dinamiche).

**Componenti v2 da progettare**: `HowItWorksStep` (nuovo), riusa `HeroGradient`.

---

### 3. Legal pages template — `sp3-legal.{html,jsx}`

**Route target**: `/terms`, `/privacy`, `/cookies`
**Scope**: Template unico riutilizzabile per le tre pagine legali. Contenuto è lungo testo, ma deve essere leggibile su mobile e con TOC navigabile desktop.

**Struttura**:
- Hero minimale: titolo documento + "Ultimo aggiornamento: <data>" + versione
- **Desktop**: sidebar sticky sinistra con TOC auto-generato (H2 del documento), smooth scroll
- **Mobile**: TOC collassabile in un `<details>` in cima
- Body: tipografia ottimizzata per reading (larghezza max 68ch, `--f-body`, line-height `--lh-relax`)
- Footer: "Hai domande? Contatta il DPO: privacy@meepleai.app" + link a contact form

**Varianti**: stessa struttura, contenuto diverso. Produci UN mockup con placeholder "Terms of Service" e indica che `privacy` + `cookies` useranno lo stesso template.

**Stati**: default. Nessun empty/error (content statico).

**Componenti v2 da progettare**: `LegalPageShell`, `LegalTOC` (sticky + mobile collapsible).

---

### 4. Accept invite — `sp3-accept-invite.{html,jsx}`

**Route target**: `/accept-invite?token=...`
**Scope**: Landing di un link di invito ricevuto via email. Utente vede da chi arriva l'invito, a quale scope (gruppo/game-night/toolkit), e decide se accettare.

**Sezioni**:
- Card centrata piccola (max-width 480px)
- Avatar + nome dell'inviter (entity=player)
- Titolo: "<Inviter> ti invita a <scope>"
- Context del scope: se game-night → dettagli serata (data, game, luogo); se gruppo → nome gruppo + membri count
- Due CTA: "Accetta e registrati" (primary, full-width mobile) + "Rifiuta" (ghost)
- Se utente già loggato con account diverso: banner "Sei loggato come X, cambia account per accettare"

**Stati**:
- **default**: invito valido
- **expired**: token scaduto → messaggio + CTA "Chiedi nuovo invito"
- **used**: già accettato → CTA "Vai all'app"
- **invalid**: token malformato → messaggio generico + link a /

**Componenti v2 da progettare**: `InviteCard` (riusa AuthCard shell + entity pip inviter).

---

### 5. Join / waitlist — `sp3-join.{html,jsx}`

**Route target**: `/join`
**Scope**: Pagina di registrazione alpha/waitlist. L'app ha alpha mode, quindi molti visitatori cadono qui per richiedere accesso.

**Sezioni**:
- Hero: "MeepleAI è in alpha privata" + copy breve
- Form waitlist: email, nome (opzionale), "Quale gioco vorresti un agente per?" (select con top 10 + "Altro"), checkbox newsletter
- Submit CTA: "Entra in waitlist"
- Stato post-submit: SuccessCard "Ti contatteremo appena ci sarà posto" con countdown stimato
- Sezione "Cosa ti aspetta": 3 mini-card con features alpha (Library smart · Agenti AI · Session live)

**Stati**:
- **default** (form)
- **submitting** (button loading)
- **success** (SuccessCard)
- **error** (inline error field-specific o banner)
- **already-on-list** (email già registrata → "Sei già in lista, posizione #N")

**Componenti v2 da progettare**: riusa `AuthCard`, `InputField`, `SuccessCard` (da SP1). Nuovo: `GamePreferenceSelect`.

---

### 6. Shared games (public) — `sp3-shared-games.{html,jsx}`

**Route target**: `/shared-games`
**Scope**: Vetrina pubblica della community SharedGameCatalog. Non-loggati vedono catalogo giochi con toolkit/agent pubblicati. CTA per iscrizione se vogliono installare.

**Sezioni**:
- Header con filtri: search + chip filters (con toolkit, con agent, top rated, nuovi)
- Grid di card MeepleCard entity=game variante `grid` — mostra cover gradient, titolo, rating community, badge "N toolkit · M agent"
- Click card → detail pubblico (vedi #7)
- Paginazione / infinite scroll
- Sidebar desktop: "Top contributors" (lista player con avatar + stats)
- Empty state con CTA a contattare / registrarsi

**Stati**:
- **default**: grid con 24+ giochi
- **loading**: skeleton grid 3×3
- **empty-search**: "Nessun gioco trovato"
- **filtered-empty**: con CTA reset filtri

**Componenti v2 da progettare**: riusa `MeepleCard` entity=game; nuovo: `SharedGamesFilters`, `ContributorsSidebar`.

---

### 7. Shared game detail (public) — `sp3-shared-game-detail.{html,jsx}`

**Route target**: `/shared-games/[slug]`
**Scope**: Dettaglio pubblico di un gioco community, con toolkit/agent/KB pubblicati disponibili per install (dopo login).

**Sezioni**:
- Hero: cover gradient + titolo + meta (designer, anno, giocatori, durata, complexity)
- Connection bar: pip toolkit (N), agent (N), kb (N), player (top contributors)
- Tab "Toolkit pubblicati" — lista toolkit con autore, install count, rating
- Tab "Agenti" — lista agent con game expertise + modello
- Tab "KB documents" — lista PDF/regole indicizzate
- CTA floating mobile / sticky desktop: "Accedi per installare"

**Stati**:
- **default**: gioco con 3+ toolkit
- **no-content**: gioco senza toolkit — CTA "Sii il primo a pubblicare"
- **loading**: skeleton hero + tab

**Componenti v2 da progettare**: `GamePublicHero`, `ToolkitPublicListItem`, riusa connection-bar pattern di SP4.

---

### 8. Library public (community showcase) — `sp3-library-public.{html,jsx}`

**Route target**: `/library` (public variant, **non-loggato**)
**Scope**: Preview della library community aggregata — "cosa si gioca su MeepleAI". Diverso da `/library` authenticated (che è la library personale).

**Sezioni**:
- Hero: "La library della community" + count totale giochi
- Stats row: "X giocatori attivi · Y partite giocate · Z ore di chat con agenti"
- Featured row: top 10 più giocati (carousel MeepleCard hero)
- Trending row: top crescita ultima settimana
- Categories row: per genere (strategia, famiglia, party, ecc.)
- CTA final: "Unisciti alla community"

**Stati**:
- **default**: tutte le sezioni popolate
- **loading**: skeleton sections
- Nessun empty (dati community sempre presenti)

**Componenti v2 da progettare**: `CommunityStatsRow`, `FeaturedGamesCarousel`, riusa `MeepleCard`.

---

### 9. Contact enhanced — `sp3-contact.{html,jsx}` (facoltativo, solo se `public.jsx` contact è minimale)

**Route target**: `/contact`
**Scope**: Se la versione in `public.jsx` è solo form base, estendi con routing per tipo richiesta (supporto · DPO · press · partnership) e widget di stato sistema.

**Sezioni**:
- Heading + subtitle
- Segmented control tipo richiesta
- Form contestuale per tipo (campi diversi per DPO vs press)
- Sidebar destra desktop: response time atteso per tipo + alternative (Discord, email diretta per press)
- Status widget "Tutti i sistemi operativi" con link a status page

**Stati**: default · submitting · success · error.

**Componenti v2 da progettare**: `ContactRouter`, riusa `InputField`, `SuccessCard`.

**Skip questo se `public.jsx` già ha contact form adeguato.**

---

## Priorità produzione

In quest'ordine (alto impatto → medio):

1. Accept invite (#4) + Join (#5) — sbloccano onboarding nuovo utente
2. FAQ (#1) + How it works (#2) — riducono churn pre-registrazione
3. Legal template (#3) — compliance blocker
4. Shared games index + detail (#6, #7) — feature visibility
5. Library public (#8) — nice-to-have community marketing
6. Contact enhanced (#9) — solo se gap rispetto a `public.jsx`

## Consegna

Quando finisci una schermata, produci un messaggio di handoff con:
- Nome file creato
- Route target
- Componenti v2 nuovi introdotti (lista nomi)
- Screenshot mentali delle varianti light/dark/mobile/desktop
- Eventuali dubbi per il reviewer

Ping finale quando tutto SP3 è completo: elenca tutti i file `sp3-*` prodotti e i nuovi componenti v2 da implementare (andranno in `apps/web/src/components/ui/v2/`).
