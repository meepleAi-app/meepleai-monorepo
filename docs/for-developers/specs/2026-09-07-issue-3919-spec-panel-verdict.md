# #3919 — spec-panel verdict: la scala glass a 3 livelli

**Issue**: [#3919](https://github.com/meepleAi-app/meepleai-monorepo/issues/3919) — `feat(tokens): scala glass a 3 livelli + primitiva GlassSurface` (UX-02a)
**Epic**: [#3916](https://github.com/meepleAi-app/meepleai-monorepo/issues/3916) · piano [`2026-09-07-web-ux-glass-redesign-plan.md`](../audits/2026-09-07-web-ux-glass-redesign-plan.md)
**Data**: 2026-09-07
**Metodo**: `/sc:spec-panel` — modalità critique (Wiegers · Adzic · Cockburn · Fowler · Newman · Nygard · Crispin · Hightower)
**Misure**: `apps/web` @ `bd244d1a0`; il diff verso `main-dev` (`b486c656d`) non tocca alcun file glass o di token
**Stato**: verdetto emesso, riformulazione proposta, **tre alpha decisi** (§6: 0,20 / 0,85 / 0,95). Nessun codice modificato.
**Bloccate da questa issue**: [#3920](https://github.com/meepleAi-app/meepleai-monorepo/issues/3920) (regola ESLint) · [#3921](https://github.com/meepleAi-app/meepleai-monorepo/issues/3921) · [#3922](https://github.com/meepleAi-app/meepleai-monorepo/issues/3922) · [#3923](https://github.com/meepleAi-app/meepleai-monorepo/issues/3923) (le 3 ondate di codemod)

---

## 1. Verdetto in una riga

La scala è espressa come **tre intervalli disgiunti**, e i due buchi che lascia — 0,25–0,65 e
0,85–0,95 — contengono **289 delle 673 superfici** che la scala deve regolare (43%). Per quelle
superfici la issue non dice dove atterrano: dice «il livello va scelto dal contenuto», che è un
criterio di giudizio umano, non un mapping. Le tre ondate di codemod ereditano quella decisione
senza saperlo.

Il secondo difetto è indipendente e più insidioso: l'AC di contrasto misura la condizione **facile**
(«sul fondo di pagina») mentre il parco reale è fatto di superfici sopra **immagini di copertina**.
Misurato sul fondo peggiore, il minimo dell'intervallo `functional` — α 0,65, tema scuro — dà
**4,38:1**, sotto AA. La scala ammette configurazioni che violano l'AC della issue stessa, e il test
proposto non se ne accorgerebbe.

## 2. Il terreno, ricontato

I due numeri della issue reggono: **526** `backdrop-blur*` su **308** file; **672** occorrenze di
`bg-card/α` su **14** alpha (la issue dice ~671 su 13). Due rettifiche minori, prodotte dallo script
di §6 quando è stato messo a contare da sé: il 673° “uso” è un regex in un test
(`LibraryHybridGrid.test.tsx:212`), non una superficie; e gli alpha distinti sono 14, perché la issue
e il piano omettono `/25`, che ne ha uno solo. Ciò che il conteggio non mostra è come si distribuiscono
rispetto alla scala proposta:

| Livello | Intervallo | Alpha presenti | Occorrenze |
|---|---|---|---|
| `decorative` | 0,15–0,25 | /15 · /20 · /25 | **37** |
| `functional` | 0,65–0,85 | /70 (276) · /80 (54) · /85 (6) | **336** |
| `content` | ≥ 0,95 | /95 | **10** |
| — **nessuno** | buco 0,25–0,65 | /30 (7) · /40 (43) · /50 (61) · /60 (58) | **169** |
| — **nessuno** | buco 0,85–0,95 | /90 | **74** |
| — **nessuno** | sotto 0,15 | /5 (19) · /10 (27) | **46** |

**289 su 673 non hanno destinazione.** Fra queste, il caso più visibile: `/90` con 74 usi si trova
esattamente fra il tetto di `functional` e il pavimento di `content` — cioè fra «pannello» e
«long-form», dove sta la maggior parte delle card dense.

E le vie per dichiarare una superficie translucida oggi non sono una: sono **cinque**.

| # | Via | Usi | Dove è definita |
|---|---|---|---|
| 1 | `.glass-card` / `.glass-nav` / `.glass-modal` | 3 file | `design-tokens.css:705-735` |
| 2 | `<GlassCard>` | 4 consumatori | `ui/surfaces/GlassCard.tsx` |
| 3 | `.glass` | 3 usi | `globals.css:291` — `bg-background/80 backdrop-blur-lg` |
| 4 | `var(--glass-bg)` a mano | 8 file | `design-tokens-canonical.css:206,238` |
| 5 | `bg-card/α` + `backdrop-blur-*` ad-hoc | 673 | ovunque |

Il piano conta «3 file che usano le utility glass-* centralizzate» ed è esatto per la via 1. Le vie
2, 3 e 4 esistono comunque, e la issue ne nomina una sola.

## 3. Findings del panel

| # | Sev | Esperto | Finding |
|---|---|---|---|
| F-1 | 🔴 | Wiegers | La scala dà **intervalli**, non valori. #3920 assume tre alpha discreti. Finché il numero non è fissato le due issue non possono essere entrambe soddisfatte |
| F-2 | 🔴 | Wiegers/Cockburn | I due buchi della scala contengono 289 superfici su 673 (§2). Il token canonico `--glass-bg` light (0,88) cade nel secondo: **il chrome globale non è conforme alla scala che questa issue introduce** |
| F-3 | 🔴 | Crispin/Wiegers | L'AC di contrasto misura il fondo sbagliato. Sul fondo peggiore, `functional` α 0,65 in dark = **4,38:1**, sotto AA. La soglia vera è ≈ 0,66 |
| F-4 | 🟡 | Fowler | «Token di alpha» non è componibile da solo, e in Tailwind v4 non genera utility fuori da `@theme inline`. Alpha numerico e colore completo sono due implementazioni diverse; la issue non sceglie |
| F-5 | 🟡 | Fowler/Newman | Il codemod cambierà il **colore**, non solo l'alpha: `bg-card/α` risolve oggi su un grigio neutro, `GlassSurface` comporrà dal marrone canonico. ~670 superfici cambiano tinta in dark, non dichiarato |
| F-6 | 🟡 | Nygard | «Nessun consumatore modificato» e «solo i 3 livelli» sono **incompatibili**: il default dark di `.glass-card` è α **0,50**, che non appartiene a nessun livello |
| F-7 | 🟡 | Crispin | La story a 8 stati contraddice l'AC che dichiara la superficie non interattiva. E «un quarto valore non compila» non è un test Vitest |
| F-8 | 🟢 | Cockburn | «~105 usi sotto soglia» regge per coincidenza: sono 133 occorrenze, di cui 31 skeleton senza testo. Il criterio che le seleziona (l'alpha) non distingue una superficie che porta testo da un placeholder |
| F-9 | 🟢 | Hightower | Debito adiacente che la issue attraversa senza nominarlo: un token auto-referenziale, un consumo con la sintassi sbagliata, una quarta utility |

---

### F-1 — la scala non ha numeri, e la issue a valle presume che li abbia

#3919 definisce `decorative` come «0,15–0,25». #3920 chiede che la regola ESLint «segnali `bg-card/45`
(alpha fuori scala) e **non** segnali gli alpha dei 3 livelli». Un intervallo continuo non produce un
insieme di alpha da confrontare: `/18` è dentro l'intervallo e fuori da qualsiasi token.

La scelta non è cosmetica — **decide quanto lavoro fanno le tre ondate**:

- `functional = 0,70` → i 276 usi di `/70` restano invariati, cambiano gli altri 60
- `functional = 0,80` → i 54 usi di `/80` restano, cambiano 282 superfici, `/70` incluso

Duecentoventidue superfici di differenza a seconda di un numero che la issue non scrive. Va scritto
qui, non scoperto nella PR di #3921.

### F-2 — il chrome globale non passa la propria scala

`design-tokens-canonical.css:206` definisce `--glass-bg: rgba(255,255,255,0.88)` per light e `:238`
`rgba(30,22,14,0.85)` per dark. Il commento di terreno sulla issue lo legge come «entrambi già nella
fascia functional». Metà è vero: 0,85 è il tetto esatto di `functional`; **0,88 non appartiene a
nessun livello**.

Quel token non è teorico. Lo consumano `AppTopBar.tsx:48`, `MobileTopBar.tsx:44`,
`MobileBottomBar.tsx:56`, `MobileStatusBar.tsx:23`, `MobileScorebar.tsx:23`, `MiniNavSlot.tsx:41`,
`ChatMobile.tsx` (5 punti), `category-tabs.tsx:64`. Cioè la barra superiore, la barra inferiore e la
chat mobile: la superficie glass che ogni utente vede su ogni pagina è, alla lettera della scala,
fuori scala.

Delle due l'una: o `content` scende a 0,88, o il chrome cambia opacità in light. È una decisione di
prodotto, e va presa in questa issue perché #3921 (ondata 1, chrome globale) è la prima a incassarla.

### F-3 — l'AC di contrasto misura il caso che non rompe

L'AC dice: «Il livello `content` produce una superficie con contrasto testo ≥ 4,5:1 **sul fondo di
pagina**». Ma le superfici translucide di questo repo non stanno sul fondo di pagina: stanno sopra le
**cover dei giochi**. `EntityBadge` è `bg-card/85 + backdrop-blur-md` sopra una copertina arbitraria;
`MenuPlaceholder` è `bg-white/85` sopra la stessa; `GameDetailHero.tsx:137` è `bg-card/20` sopra
l'immagine dell'hero.

Il fondo di pagina è il caso **facile** — passa sempre, in entrambi i temi, a ogni alpha della scala.
Il caso che rompe è il fondo arbitrario, e `backdrop-filter: blur()` non aiuta: il blur riduce la
varianza della luminanza, non la sua media. Il caso peggiore resta dov'è.

Composizione `α·S + (1−α)·B`, testo `--text`, luminanza WCAG su sRGB linearizzato:

| Tema | Superficie | Fondo peggiore | α 0,65 | α 0,70 | α 0,80 |
|---|---|---|---|---|---|
| dark | `--bg-card` `#1e1710` | bianco | **4,38:1** ❌ | 5,22:1 ✅ | 7,49:1 ✅ |
| dark | `bg-card` reale `#2d2d2d` | bianco | **3,59:1** ❌ | **4,19:1** ❌ | 7,49:1 ✅ |
| light | `#ffffff` | nero | 6,59:1 ✅ | — | 10,0:1 ✅ |

Tre conseguenze, tutte azionabili:

1. **Il pavimento di `functional` è appena sotto la soglia.** La soglia calcolata è α ≈ 0,66; il
   range parte da 0,65. Non è un margine: è un difetto di un punto percentuale, e chi implementa
   sceglierà legittimamente il minimo del range perché è il più «glass».
2. **L'alpha sicuro dipende dal tema.** In light 0,65 dà 6,59:1 con margine largo; in dark fallisce.
   Un intervallo identico nei due temi non può essere corretto in entrambi — perché il testo chiaro
   su superficie scura si avvicina al fondo bianco, mentre il testo scuro su superficie chiara resta
   distante dal fondo nero. Servono due pavimenti, o un unico pavimento tarato sul tema peggiore.
3. **Alpha e colore di superficie non sono separabili** (→ F-5). Con la superficie che `bg-card`
   risolve *oggi* in dark, nemmeno 0,70 basta.

*Assunzione dichiarata*: bianco e nero puri sono il limite, non il caso medio; le cover reali stanno
in mezzo. Ma un vincolo di accessibilità si scrive sul caso peggiore raggiungibile, e una cover chiara
è raggiungibile.

### F-4 — un alpha da solo non produce una superficie

`--glass-alpha-functional: 0.8` è un numero. Per diventare un `background` serve
`color-mix(in srgb, var(--bg-card) 80%, transparent)` o `rgb(from … / …)`, e il numero va convertito
in percentuale. In alternativa i token sono colori completi (`--glass-bg-functional: rgba(…)`) e
allora «token che nomina l'alpha» è impreciso.

Non è pedanteria: le due strade hanno costi diversi a valle. Con l'alpha numerico, `GlassSurface`
può comporre sopra **qualsiasi** superficie (`--bg-card`, `--bg-muted`, entity color) — che è ciò
che serve per assorbire le 673 varianti. Con il colore completo servono N token per N superfici.

Vale anche la trappola nominata nel commento di terreno: in Tailwind v4 le utility nascono solo da
variabili dichiarate in `@theme inline` (`globals.css:491`). È #3161, e #3917 ha appena aggiunto il
test di regressione per le varianti entity. Se `GlassSurface` applica classi Tailwind, i token vanno
lì; se applica `style`, no. La issue non dice quale delle due.

### F-5 — il codemod cambia la tinta, non solo l'opacità

Ci sono due sistemi di token per la stessa idea, e i 673 usi pescano dal secondo:

| | canonical (`design-tokens-canonical.css`) | Tailwind (`globals.css`) |
|---|---|---|
| superficie card, light | `--bg-card #ffffff` | `--card 0 0% 100%` → `#ffffff` |
| superficie card, **dark** | `--bg-card #1e1710` (marrone caldo) | `--card 0 0% 18%` → **`#2d2d2d`** (grigio neutro) |
| fondo, dark | `--bg #14100a` | `--background 0 0% 10%` → `#1a1a1a` |

`bg-card/70` risolve su `--card`. `GlassSurface`, composta dai token canonici, risolverà su
`--bg-card`. In light coincidono; **in dark no**. Sostituire l'uno con l'altro su ~670 superfici è un
cambio di tinta di massa che nessuna delle quattro issue dichiara, e che il criterio di successo del
piano («13 valori di alpha → 3») non misura.

Stesso innesto, stessa causa: il fallback mobile di `.glass-card` (`design-tokens.css:744`) usa
`hsl(var(--background))`, cioè `#1a1a1a`, non il fondo canonico.

### F-6 — «byte-identico» e «solo tre livelli» non stanno insieme

Il commento di terreno propone che i 4 consumatori di `GlassCard` restino invariati «se l'aggiunta è
additiva e il default è byte-identico all'attuale». Il default attuale è:

```
design-tokens.css:706   .glass-card { background: var(--glass-bg-light) }        → rgba(255,255,255,0.70)   functional ✅
design-tokens.css:729   .dark .glass-card { background: var(--glass-bg-dark) }   → rgba(0,0,0,0.50)         nessun livello ❌
```

In light il default è già `functional`. In **dark** è α 0,50 — nel buco fra `decorative` e
`functional`. Se il default resta 0,50 la primitiva viola la propria scala; se sale a `functional` i
quattro consumatori cambiano aspetto in dark. L'AC «Nessun consumatore esistente è modificato in
questa PR» va riscritta come: *nessun consumatore cambia in light; in dark il cambiamento è atteso,
misurato e mostrato negli snapshot*.

Nota di merito, indipendente dalla scala: `rgba(0,0,0,0.50)` è nero puro sopra un tema il cui fondo è
`#14100a`. Non è un caso che sia l'unico valore fuori scala — è un residuo di quando il tema scuro era
la palette gaming, prima che il canonico diventasse autorevole.

### F-7 — la matrice di test non prova ciò che il componente deve garantire

I `Tests` chiedono «story a 8 stati (default, hover, active, focus, disabled, loading, light, dark)
per i 3 livelli». L'Accessibility, tre righe sopra, dichiara la superficie **non interattiva**
(«Keyboard — n/a»). `hover`, `active`, `focus`, `disabled`, `loading` non esistono per questo
componente: sono 24 celle di cui 15 vuote per costruzione.

La matrice che conta è un'altra — è quella che rende visibile F-3:

```
3 livelli × 2 temi × 3 fondi (pagina · cover chiara · cover scura) = 18 celle
```

E l'AC «il livello ignoto non compila» non è verificabile da Vitest: è un `@ts-expect-error` in un
file che `pnpm typecheck` compila. Se il quarto valore *compilasse*, sarebbe il `@ts-expect-error`
inutilizzato a far fallire il gate. Va scritto così, o non è un criterio.

### F-8 — il numero giusto per il motivo sbagliato

La fascia `/5`–`/40` conta oggi **133** occorrenze, non ~105. Di queste **31 sono skeleton**
(`animate-pulse`: 29 su `/40`, 2 su `/10`) — superfici che per definizione non portano testo.
Restano ~102, che è il numero della issue.

La coincidenza non salva il criterio. L'alpha da solo non distingue
`AgentsTab.tsx:56` (`bg-card/40 animate-pulse`, un placeholder) da `CitationSheet.tsx:33`
(`bg-card/5` su un `<blockquote>` con testo citato) — e sono due casi opposti: il primo non ha
bisogno di alcun livello, il secondo è il difetto che l'Epic vuole eliminare.

Il metrico corretto è già scritto nel piano — «usi di alpha < 0,5 su nodi con testo → 0» (§L) — ma
nessuno lo sa calcolare da un grep, perché richiede di sapere se il nodo ha figli di testo. Delle due
l'una: o la regola di #3920 ispeziona i figli JSX, o il criterio va riformulato su qualcosa di
decidibile staticamente.

Un caso reale, oggi in codice, che vale come test di accettazione per l'intera scala:

```tsx
// GameDetailHero.tsx:136-137
{/* eslint-disable-next-line local/no-hardcoded-color-utility -- text-white justified: bg-card/20 declared on same className */}
<span className="… bg-card/20 … text-white backdrop-blur-md">
```

`text-white` sopra α **0,20**, sopra la cover del gioco. Su una copertina chiara il testo sparisce.
La motivazione scritta nel `disable` — «giustificato perché c'è un bg colorato sulla stessa
className» — è esattamente il ragionamento che la scala di questa issue dichiara sbagliato
(`decorative`: «mai testo sopra»). Due occorrenze, stesso file, stessa motivazione.

### F-9 — debito adiacente, da nominare e non necessariamente da riparare qui

```
design-tokens.css:174    --glass-bg: var(--glass-bg);          ← auto-riferimento: ciclo,
design-tokens.css:175    --glass-border: var(--glass-border);     invalid at computed-value time
```

Inerte, perché `design-tokens-canonical.css` non è dentro un `@layer` e batte `@layer tokens`
qualunque sia la specificità. Ma sono due righe che *sembrano* definire un token e non definiscono
nulla — e per la stessa ragione `.light { --glass-bg: rgba(160,120,60,0.04) }` (`:603`) non si applica
mai.

```
category-tabs.tsx:64     bg-[hsl(var(--glass-bg))]
```

`--glass-bg` è `rgba(255,255,255,0.88)`, non una tripletta HSL: `hsl(rgba(…))` è invalido e la barra
delle categorie **non ha sfondo**. Nessun gate se ne accorge — è la dimostrazione più economica del
perché #3920 serve, e andrebbe citata lì come caso di test.

Infine `.glass` (`globals.css:291`) è la quarta utility, e compone `bg-background/80` — cioè una
quinta superficie di partenza, diversa dalle altre quattro.

---

## 4. Riformulazione proposta

**Titolo** — «estendere `GlassCard` con la scala a 3 livelli», non «creare `GlassSurface`»
(rettifica già registrata nel piano, §O). Se si preferisce il nome nuovo, la PR migra i 4 consumatori
nella stessa PR: due primitive glass coesistenti sono la frammentazione che l'Epic riduce.

**Scala, con numeri** — derivazione completa in §6:

| Livello | α | Blur | Vincolo che lo fissa |
|---|---|---|---|
| `decorative` | **0,20** | sm | nessun contrasto (non porta testo): decide il riuso (29 usi) e il fallimento rumoroso |
| `functional` | **0,85** | md | `--text-sec` in dark sul fondo peggiore (soglia 0,781) — e è il valore già de-facto del sistema |
| `content` | **0,95** | none/sm | `--text-sec` in **AAA** sul fondo peggiore (soglia 0,914 light / 0,907 dark) |

**AC riscritti** (i tre che cambiano):

- ~~«contrasto ≥ 4,5:1 sul fondo di pagina»~~ →
  **«per ogni livello che ammette testo, contrasto ≥ 4,5:1 sul fondo peggiore — bianco per il tema
  scuro, nero per il chiaro — verificato in entrambi i temi»**
- ~~«nessun consumatore esistente è modificato»~~ →
  **«in light nessun consumatore cambia resa; in dark il cambiamento è atteso e catturato negli
  snapshot dei 4 consumatori»**
- ~~«il livello ignoto non compila» (unit)~~ →
  **«`@ts-expect-error` su un quarto valore, verificato da `pnpm typecheck`»**

**AC da aggiungere:**

- **I due buchi hanno una destinazione dichiarata**: una tabella `alpha attuale → livello` che copre
  tutti e 13 gli alpha, così le tre ondate non ridecidono 289 volte.
- **Il token canonico `--glass-bg` (0,88) è ricondotto alla scala**, o la scala lo accoglie. Il chrome
  globale è il primo consumatore di #3921.
- **La superficie di partenza è `--bg-card` canonico, non `--card`.** Non è una preferenza: con
  `--card` (grigio `#2d2d2d` in dark) la soglia AA per `--text-sec` sale a **0,859**, sopra il valore
  scelto — gli 8 punti fra le due superfici sono più del margine di sicurezza (§6.3, F-5).
- **`GlassSurface` compone via `color-mix()` sopra una superficie parametrica**, così un solo token di
  alpha serve tutte le superfici (F-4) — e i token vivono in `@theme inline` se e solo se servono
  utility Tailwind.

**Tests** — sostituire la matrice a 8 stati con: 3 livelli × 2 temi × 3 fondi = 18 celle, più uno
snapshot per ciascuno dei 4 consumatori attuali di `GlassCard`.

**Definition of Done** — invariata nei comandi, più: *la tabella di mapping dei 13 alpha è nel corpo
della issue prima che #3921 apra*.

## 5. Cosa resta valido senza modifiche

- La diagnosi. «Il blur non sostituisce il contrasto» è corretto, ed è la frase che regge tutto.
- La scelta di **non** applicare la scala ai consumatori in questa PR: separare la definizione
  dall'adozione è giusto, ed è ciò che rende le tre ondate revisionabili.
- Il rischio dichiarato («se la scala è troppo rigida i consumatori la aggireranno») e la sua
  mitigazione via #3920. Il rischio è reale — 309 `eslint-disable` della regola colori esistente lo
  documentano.
- L'ordine delle dipendenze: definire → vincolare → migrare, con la regola introdotta come `warn`
  e promossa a `error` alla fine.
- Il vincolo responsive: il degrado del blur sotto `md` **esiste già** (`design-tokens.css:740-749`,
  fallback opaco sotto 768px) e va ereditato, non riprogettato.

---

## 6. Derivazione dei tre alpha (2026-09-07)

Il §4 nella sua prima stesura proponeva 0,20 / 0,80 / 0,97 tarando su `--text`. Risolti i vincoli su
tutti i token di testo, il valore che comanda è un altro e la terna cambia. Script rieseguibili:
composizione in sRGB come la fa il browser, luminanza WCAG 2.1 su sRGB linearizzato, ricerca binaria
della soglia. Lo strumento vive nel repo: `apps/web/scripts/glass-alpha-audit.mjs`
(`pnpm audit:glass-alpha`, `--json` per l'output machine-readable), con 23 test in
`scripts/__tests__/`. Non ha i token hardcoded — li legge da `design-tokens-canonical.css`, così
quando un token cambia lo script cambia risposta invece di mentire; se non li trova esce con 2 senza
emettere verdetto. Esce con 1 se un livello che ammette testo scende sotto 4,5:1, quindi può diventare
un gate il giorno in cui i token della scala esistono.

### 6.1 Il vincolo binding è `--text-sec`, non `--text`

Alpha minimo perché il testo resti leggibile **sul fondo peggiore** (bianco sotto il tema scuro, nero
sotto il chiaro), superficie `--bg-card` canonico:

| tema · testo | AA 4,5:1 | AAA 7:1 |
|---|---|---|
| light · `--text` | 0,531 | 0,671 |
| light · `--text-sec` | 0,739 | 0,914 |
| dark · `--text` | 0,658 | 0,781 |
| **dark · `--text-sec`** | **0,781** | 0,907 |
| light/dark · `--text-muted` | **mai** | **mai** |

`--text-sec` è ciò che portano sottotitoli e metadati delle card — cioè proprio le superfici che
stanno sopra le copertine. Tararsi su `--text` lascia scoperto il caso più comune.

### 6.2 `functional` = 0,85

0,80 supera la soglia di due punti (4,81:1 contro 4,5). 0,85 dà **5,76:1 in dark** e **6,01:1 in
light**: un margine che sopravvive a un ritocco futuro dei token di testo.

L'argomento che chiude è però un altro: **0,85 è già il valore che il sistema ha scelto da sé** dove
il problema si presenta. `--glass-bg` dark canonical è `rgba(30,22,14,0.85)`; `--glass-bg-light-strong`
è 0,85; `EntityBadge` usa `bg-card/85`; `MenuPlaceholder` usa `bg-white/85`. Sono i componenti che
stanno sopra le cover: chi li ha scritti ha trovato empiricamente la soglia che qui esce dal calcolo.
Ed è il tetto già dichiarato dalla issue, quindi il range non cambia.

**Effetto sul chrome globale (F-2)**: 0,88 → 0,85 vale **Δ 0,5/255 sul fondo di pagina**, dove il
chrome effettivamente sta; in dark `--glass-bg` è già 0,85 e non cambia nulla. Il dilemma «o `content`
scende a 0,88, o il chrome cambia opacità» si scioglie senza che si veda.

### 6.3 La superficie di partenza sposta la soglia di 8 punti

| superficie · dark · `--text-sec` | soglia AA |
|---|---|
| `--bg-card` canonico `#1e1710` | 0,781 |
| `bg-card` → `--card` Tailwind `#2d2d2d` | **0,859** |

Con `--card`, **0,85 non basterebbe**. La raccomandazione di F-5 smette di essere una preferenza
estetica e diventa una precondizione di accessibilità.

### 6.4 `content` = 0,95 · `decorative` = 0,20

**`content` = 0,95** è il minimo che porta `--text-sec` in **AAA** sul fondo peggiore (7,61:1 light ·
7,98:1 dark). Centra l'obiettivo dell'Epic «AAA dove possibile su chat, upload, rules», coincide col
limite già scritto nella issue e lascia conformi i 10 usi di `/95`. 0,97 aggiungerebbe margine senza
cambiare la classe di conformità.

**`decorative` = 0,20** non ha vincolo di contrasto — non porta testo. Decidono il riuso (29 usi, il
più diffuso della fascia) e il fatto che l'errore sia **rumoroso**: chi ci mette testo ottiene 1,22:1
in dark, un fallimento visibile a occhio nudo.

⚠️ Da scrivere nella doc del livello: fra 0,15 e 0,25 la superficie è **invisibile sul fondo di
pagina** (1,01–1,03:1 in entrambi i temi). `decorative` esiste solo sopra un'immagine o un gradiente,
oppure accompagnato da un bordo.

### 6.5 Mappatura dei 13 alpha e costo

**45 usi restano invariati, 627 migrano** (502 salgono di opacità, 125 scendono). La mappa è una
**decisione**, non un calcolo: derivarla dalla distanza fra i livelli sbaglierebbe due volte — `/50`
finirebbe in `decorative` (50 < 52,5) e `/90` in `content`, mentre il campionamento qui sotto dice
`functional` per entrambi. La regola vera è che una superficie che porta testo non scende sotto
`functional`, e l'alpha da solo non sa se il nodo porta testo.

| alpha | usi | → livello | Δ |
|---|---|---|---|
| /5 · /10 · /15 | 53 | `decorative` | +15 · +10 · +5 |
| /20 | 29 | `decorative` | — |
| /25 · /30 · /40 | 51 | `decorative` | −5 · −10 · −20 |
| /50 · /60 | 119 | `functional` | +35 · +25 |
| /70 | 276 | `functional` | +15 |
| /80 | 54 | `functional` | +5 |
| /85 | 6 | `functional` | — |
| /90 | 74 | `functional` | −5 |
| /95 | 10 | `content` | — |

Il numero che rende accettabile la migrazione dei 276 usi di `/70`:

| contesto | Δ max canale |
|---|---|
| sopra il fondo di pagina (dark) | **1,5/255** — invisibile |
| sopra il fondo di pagina (light) | **2,5/255** — invisibile |
| sopra una cover di luminanza opposta | 36–38/255 — visibile |

Il costo percettivo si concentra **solo** sui casi in cui il valore vecchio rompeva l'accessibilità.

Campionamento a validazione della mappa: `/50` sono pannelli con bordo e testo
(`toolkit/stats/client.tsx:88`, `queue-item.tsx:108`, `config-tab.tsx:80`) → `functional`; `/90` sono
**campi di input** (`contact/page.tsx`, 4 occorrenze) → `functional`, non `content`; `/60` è misto,
5 su 57 sono skeleton e vanno a `decorative`.

### 6.6 Due conseguenze per il codemod

1. **`--text-muted` è fuori portata a qualunque alpha**, anche su superficie completamente opaca
   (3,43:1 su bianco puro in light). Non è un difetto introdotto dalla scala e la scala non può
   ripararlo: va usato solo per testo grande (≥24px, o ≥18,66px bold) o per elementi non testuali.
   Va scritto nella doc, altrimenti qualcuno tenterà di risolverlo alzando l'alpha.
2. **Le 27 varianti `dark:bg-card/α` vanno rimosse, e dodici vanno nella direzione sbagliata**:
   `dark:bg-card/5` (×4), `/10` (×4), `/15` (×3) *abbassano* l'opacità nel tema dove la soglia è più
   alta (0,781 dark contro 0,739 light). L'intuizione diffusa — «in dark serve meno opacità» — è
   invertita rispetto alla matematica.

### 6.7 Limiti dichiarati

Bianco e nero puri sono il caso limite raggiungibile, non il caso medio: le cover reali stanno in
mezzo. E il calcolo assume che `backdrop-filter: blur()` non sposti la **media** della luminanza del
fondo — vero per la media, non per un singolo glifo sopra un dettaglio ad alto contrasto.
