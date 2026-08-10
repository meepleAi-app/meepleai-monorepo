# Cover admin: scopribilità dell'editor e ritaglio per contesto

**Issue**: [#3611](https://github.com/meepleAi-app/meepleai-monorepo/issues/3611)
**Data**: 2026-08-07
**Correlati**: #3470 (ha introdotto l'editor), #3590 (giochi senza cover), #3608 (vista cover-gap)

## Problema

Un admin, sulla pagina di un gioco, non trova alcun modo di cambiare l'area di ritaglio o la
sorgente dell'immagine, e vede la maggior parte delle superfici senza una cover adeguata. La
funzione esiste dal merge di #3470, ma `game_cover_assignments` su staging ha zero righe: a mesi
di distanza nessuno l'ha mai usata. Tre difetti distinti si sommano.

### D1 — L'affordance è invisibile su desktop

`AdminCoverEditAffordance.tsx:46` applica `md:opacity-0 md:group-hover:opacity-100`: la matita
esiste solo mentre il mouse è sopra la cover. Nessun bordo, nessun badge, nessun segnale che
dietro quel riquadro ci sia un editor. Su mobile è visibile, su desktop no.

L'intento — non sporcare la superficie pubblica — è legittimo, ma la discrezione dell'hover non
aggiunge protezione: `if (!isEditorOrAbove) return null` nasconde già il controllo ai non-admin.
L'hover nasconde soltanto a chi ha già il permesso.

### D2 — Il punto d'ingresso finisce a vuoto

La vista cover-gap (#3608) è nata per rendere trovabili i giochi da sistemare, ma il suo unico CTA
(`cover-gap/page.tsx:143`) punta a `/shared-games?highlight=<gameId>`. **Il parametro `highlight`
non è gestito da nessuna parte**: nessuna occorrenza né in `app/(public)/shared-games` né in
`components/ui/shared-games`. Si atterra quindi sulla griglia intera, senza evidenziazione, dove
serve comunque scovare la card a occhio e poi passarci sopra il mouse (D1). Il percorso guidato
esiste ma termina in un vicolo cieco.

### D3 — Il crop per contesto non è mai stato implementato

Questo difetto non era noto e ridimensiona la lettura originale dell'issue.

- `AssignCoverCommandHandler` persiste sorgente e punto focale, poi si ferma: nessun render.
- `GameCoverAssignment.SetGeneratedKey()` è invocato **solo dai test**, mai in produzione, quindi
  `GeneratedR2Key` resta null per sempre.
- `IWebpVariantGenerator` espone un overload con punto focale, documentato come *"Used to render
  the per-context cover crop from an admin-set GameCoverAssignment"*, ma le tre chiamate di
  produzione (`SetManualCoverCommandHandler`, `EnrichCatalogCoverCommandHandler`,
  `MaterializePdfCoverCommandHandler`) usano tutte l'overload a quattro argomenti, con crop
  centrato e dimensioni 2:3 fisse.

Di conseguenza `ResolveAssignmentAsync` non trova mai il crop e ricade sulla chiave base della
sorgente, servendo **l'immagine intatta**. Anche compilando le assegnazioni a mano, Hero e Social
riceverebbero il ritratto 2:3: cambierebbe solo *quale* sorgente, non il ritaglio. Il punto focale
che l'admin imposta nella UI oggi non ha alcun effetto visivo.

Il frontend, dal canto suo, usa `object-cover` senza mai `object-position` (nessuna occorrenza in
`components/ui`), quindi il browser ritaglia sempre al centro: sull'hero di una cover derivata da
PDF questo produce una banda di corpo del testo — l'effetto «cover assente» segnalato.

## Decisioni

| Ambito | Decisione |
|--------|-----------|
| Scope | Scopribilità **e** ritaglio per contesto nella stessa issue: senza render, l'admin userebbe uno strumento che non produce effetti |
| Default per i giochi non assegnati | Euristica per sorgente, applicata alla risoluzione |
| Dove avviene il ritaglio | Ibrido: `object-position` per Card/Hero, file generato solo per Social |
| Affordance | Segnale proporzionato al bisogno: contorno sulle cover in placeholder, badge attenuato altrove |
| Valore del default | `focalY ≈ 0,2` per le cover da PDF, centro per le altre sorgenti; costante nel codice |
| Social non assegnati | Fuori scope, tracciati in follow-up |

## Architettura

### Backend — il punto focale entra nella risoluzione

`CoverUrlResolver.ResolvedCover` si estende:

```csharp
internal readonly record struct ResolvedCover(
    string? Url, CoverKind? Kind, double FocalX, double FocalY);
```

Chi risolve una cover ottiene nello stesso giro *dove va guardata*. La regola vive in un solo
punto:

1. vince un'assegnazione admin → il punto focale scelto dall'admin (finalmente usato);
2. vince la catena implicita → `DefaultFocalFor(kind)`, funzione statica pura:
   `Pdf → (0.5, 0.2)`, ogni altro kind → `(0.5, 0.5)`;
3. ramo placeholder → nessuna immagine, valore centrale e irrilevante.

L'euristica è per sorgente perché le due famiglie di immagini hanno composizioni opposte: una
pagina di rulebook porta titolo e illustrazione in alto e testo al centro, mentre una cover
d'artwork (BGG, Wikidata) ha il soggetto al centro e ancorarla in alto lo taglierebbe.

Non c'è alcuna scrittura in `game_cover_assignments`, nessun backfill, nessuna migration: il
default è calcolato a ogni risoluzione. I giochi mai toccati migliorano al deploy e correggere
l'euristica significa cambiare una costante, non rigenerare immagini.

L'invariante di #2123 — esattamente un evento `CoverResolution` per chiamata — resta intatta: i
punti di emissione non cambiano, cambia solo il tipo restituito.

### DTO

Il punto focale è esposto come coppia `CoverFocalX` / `CoverFocalY` (`double`, dominio `[0,1]`)
**sia sul DTO di lista sia su quello di dettaglio**. La Card 2:3 non
ritaglia (sorgente e destinazione condividono la proporzione), ma `MeepleCard` ha sei varianti con
`aspectRatioClass` differenti: `list`, `compact` e `featured` ritagliano. Due `double` sul DTO
costano nulla e rendono posizionabile ogni superficie.

`SocialCoverUrl` non cambia forma: resta un URL, che punterà al crop 1.91:1 quando esiste.

### Frontend — posizionamento

`Cover.tsx` (riga 73) e `hero.tsx` (riga 215) accettano una prop opzionale con il punto focale e la
traducono in `objectPosition: '{x*100}% {y*100}%'` accanto all'`object-cover` esistente. **Quando la
prop è assente non viene emesso alcuno stile**, quindi il rendering resta identico a oggi: è il
vincolo del contratto `MeepleCard`, la stessa disciplina additiva già adottata per
`coverEditSlot`.

### Frontend — affordance

`AdminCoverEditAffordance` guadagna una sola prop, `needsAttention?: boolean`.

- `md:opacity-0 md:group-hover:opacity-100` viene rimosso: è il difetto D1.
- A riposo la matita è attenuata; in hover e in focus è piena.
- Con `needsAttention` la matita è piena da subito e il componente disegna **da sé** il contorno
  tratteggiato, tramite un elemento interno `absolute inset-0 pointer-events-none`.

Il contorno disegnato dall'affordance evita di toccare `GridCard`, `Cover` e l'hero: tutto il
comportamento nuovo resta nel componente già iniettato nello slot.

Il valore di `needsAttention` è calcolato dal call-site con `shouldUsePlaceholder(coverUrl)`, la
stessa funzione che `Cover.tsx:52` usa per decidere il fallback emoji. Nessuna seconda nozione di
«cover mancante» da mantenere allineata.

L'attenuazione a riposo **non** usa `opacity` sull'elemento: abbasserebbe il contrasto di testo e
bordo sotto la soglia AA, e il gate di accessibilità è bloccante. Si usa invece una coppia di
colori che resta sopra 4.5:1 in entrambi i temi, verificata da axe.

### Percorso di ingresso

Il CTA della vista cover-gap passa da `/shared-games?highlight=<id>` a
`/shared-games/{gameId}?cover=edit`: la pagina del gioco, con il dialog già aperto. L'etichetta
diventa «Assegna cover».

L'apertura passa per una prop `defaultOpen` di `AdminCoverEditAffordance`, calcolata dai
`searchParams`. Poiché il componente ritorna `null` per i non-admin, il gating è automatico: non
viene introdotto alcun nuovo percorso di autorizzazione. Alla chiusura il parametro viene rimosso
dall'URL, così un back o un refresh non riaprono il dialog.

### Render Social

Al salvataggio di un'assegnazione per il contesto `Social`, `AssignCoverCommandHandler` genera il
crop 1200×630 con l'overload focal esistente, lo carica su R2 e chiama `SetGeneratedKey` — che
acquisisce così il suo primo chiamante di produzione.

Se il render o l'upload falliscono, `GeneratedR2Key` resta null e il resolver ricade sul
comportamento odierno. Il degrado è grazioso e coerente con il fall-through che il resolver
applica in ogni suo ramo.

## Fuori scope

- **Social per i giochi non assegnati.** Il default euristico corregge Card e Hero via CSS, ma un
  crawler non esegue CSS: per Social serve un file. Il crop nasce quindi solo su assegnazione
  esplicita, e per gli altri l'anteprima social resta quella odierna. Da tracciare in una issue di
  follow-up, insieme alle due strade valutate (generazione in fase di materializzazione più
  backfill, oppure generazione on-demand con cache).
- **Render server-side per Card e Hero.** L'ibrido lascia la porta aperta: il DTO espone il punto
  focale in entrambi gli scenari, quindi il passaggio a file generati resta possibile per singolo
  contesto se la banda diventerà un problema misurato.
- **Bulk-assign.** Il default euristico riduce il bisogno di compilare 160 × 3 assegnazioni a
  mano; se ne resterà necessità, sarà su casi selezionati.

## Casi limite

- **Assegnazione con `GeneratedR2Key` null**: già gestita dal fall-through esistente.
- **Cambio di sorgente o punto focale**: il dominio azzera `GeneratedR2Key`; con il render eseguito
  al salvataggio la chiave viene subito riprodotta, senza stati intermedi da presidiare.
- **Range del punto focale**: `[0,1]` è già validato nel dominio; l'euristica è una costante e non
  introduce input non fidato.
- **Cache**: le voci di `search-games` e `shared-game:{id}` serializzate prima del deploy non
  contengono il punto focale e si deserializzano con il default centrale, mantenendo il
  comportamento attuale fino alla scadenza (15 min – 2 h). `AssignCoverCommandHandler` fa già
  evict su assegnazione, quindi l'unico effetto è un ritardo massimo di due ore per i giochi mai
  toccati. Non si forza l'invalidazione.

## Collaudo

**Dominio e resolver (unit)**

- `DefaultFocalFor` restituisce il valore atteso per ogni `CoverKind`.
- Il punto focale dell'assegnazione prevale sull'euristica.
- Il ramo placeholder non produce un punto focale spurio.
- Resta verificata l'invariante «un solo evento `CoverResolution` per chiamata».

**Frontend (unit)**

- `Cover` senza la prop non emette alcuno stile: è il test che protegge il contratto, accanto a
  `GridCard.coverEditSlot.test.tsx`.
- Con la prop, `objectPosition` assume il valore atteso.
- L'affordance è presente e visibile **a riposo**: test di regressione diretto su D1.
- `needsAttention` aggiunge contorno e matita piena.
- axe su entrambi i temi, sul componente e sul dialog (`cover-editor.axe.test.tsx`).

**Integrazione**

- Assegnazione per il contesto Social → `GeneratedR2Key` popolato.
- Render fallito → chiave null, nessuna eccezione propagata, cover ancora servita.

**Percorso**

- Il CTA di cover-gap produce l'URL con `?cover=edit`.
- Il parametro apre il dialog.
- Un non-admin sullo stesso link non vede né matita né dialog.

## Criteri di accettazione

1. Su desktop, un admin vede l'affordance di modifica senza passare il mouse sulla cover.
2. Le cover in placeholder sono visivamente marcate come da sistemare.
3. Dalla vista cover-gap, un click porta al gioco con l'editor già aperto.
4. Il punto focale impostato da un admin cambia l'inquadratura di Hero (e di Social, che viene
   rigenerato).
5. Senza alcuna assegnazione, l'hero di un gioco con cover derivata da PDF inquadra la parte alta
   della pagina e non una banda di testo.
6. Un gioco senza la prop di punto focale rende esattamente come prima.
7. Il gate di accessibilità AA resta verde.
