# KB_TOOLPAGE_DESIGN_PROMPTS.md — Mock per le 6 KB tool-page mancanti

> Generati 2026-05-28 per F3-FU-3 (issue #1652). Workflow analogo a `SURPLUS_DESIGN_PROMPTS.md`: l'utente genera i mock su claude.ai web, poi l'agente li integra nel codice.

## Contesto

Survey 2026-05-28: delle 7 KB tool-page del prodotto, solo 1 ha un mockup SP5 (`sp5-admin-rag-backup.html` ≈ snapshots). Le altre 6 (vectors, queue, pipeline-monitor, embedding, feedback, settings) non hanno mockup. F3-FU-3 ne richiede il re-skin, quindi servono i 6 mock prima dell'implementazione.

## Decisione header (incorporata nei mock)

Layout `/admin/knowledge-base/layout.tsx` possiede la banda di sezione condivisa: header `<h1>Knowledge Base</h1>` + crumbs (`247 docs · 18.487 chunks · 2.4TB`) + actions (search ⌘K, + Upload PDF) + sub-nav `.admin-tabs` (8 tab, riusata da `sp5-admin-kb-subnav.html`). Le pagine NON ripetono un h1 "Knowledge Base"; l'identità della pagina = il tab attivo della sub-nav. Se serve un titolo di contenuto, `.admin-panel-head` (h3), non h1.

## Workflow su claude.ai

1. **claude.ai** → nuova conversazione, **Artifacts attivi**.
2. **Messaggio 1**: incolla il Brief (§1) **+** allega i **5 file** (§2). Claude conferma, attende.
3. **Messaggi 2-7**: invia un prompt-schermata alla volta (§3-§8). Ogni risposta = 1 HTML.
4. Salva ogni artifact come `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-<id>.html` e passalo all'agente (oppure incolla l'HTML). L'agente penserà a sostituire `<style>` inline con `<link>`, voce nav, SCREENS, ecc.

## 1. Brief (Messaggio 1)

```
Sei un designer frontend. Produci MOCKUP HTML statici e standalone per le tool-page
"Knowledge Base" della Admin Console di MeepleAI, nello stile SP5 dei 3 CSS allegati
(tokens.css, components.css, admin-base.css). Ti allego anche sp5-admin-kb.html (pattern
chunk-table/KPI/doc-card + stili custom) e sp5-admin-kb-subnav.html (la sub-nav KB).

REGOLE FERME:
- Vanilla HTML, un file per schermata. NIENTE React/framework/build.
- Dark theme default, density alta, desktop-first 1440px. SOLO le CSS variables e le classi
  del design system (admin-shell, admin-top, admin-tabs, admin-table, admin-kpi, admin-panel,
  status-chip, btn-admin, admin-form-row, admin-search, alert-banner, ecc.).
- VIETATO hex hardcoded: usa var(--*) e le utility entità .e-game/.e-kb/.e-event/...
- Dati realistici e plausibili (nomi giochi, numeri, timestamp). Niente Lorem ipsum.
- Per l'anteprima: inlinea i 3 CSS + gli stili custom di sp5-admin-kb.html in <style>.

STRUTTURA OBBLIGATORIA di OGNI tool-page (riempi solo la parte "CONTENUTO"):
<!doctype html><html lang="it" data-theme="dark"><head>…<style>/* CSS inline */</style></head>
<body class="admin-page">
  <div class="admin-mobile-fallback"><div class="em">🖥️</div><h2>Console solo desktop</h2><p>≥880px.</p></div>
  <div class="admin-shell">
    <div data-admin-nav-mount></div>
    <main>
      <!-- BANDA DI SEZIONE (uguale per tutte le tool-page) -->
      <header class="admin-top">
        <div><h1>Knowledge Base</h1><div class="crumbs">Admin · KB · 247 docs · 18.487 chunks · 2.4TB</div></div>
        <div class="spacer"></div>
        <div class="admin-top-actions">
          <div class="admin-search"><span>🔍</span><input placeholder="Search docs, chunks, games…"/><kbd>⌘K</kbd></div>
          <a href="#" class="btn-admin primary">+ Upload PDF</a>
        </div>
      </header>
      <div class="admin-body">
        <!-- SUB-NAV: copia la .admin-tabs di sp5-admin-kb-subnav.html (8 tab),
             con SOLO il tab di QUESTA pagina in stato .active -->
        <div class="admin-tabs" role="tablist" aria-label="Knowledge Base sezioni">… 8 tab …</div>
        <!-- CONTENUTO SPECIFICO DELLA PAGINA (NIENTE h1 "Knowledge Base" ripetuto:
             il tab attivo è l'identità della pagina; parti dal contenuto funzionale) -->
      </div>
    </main>
  </div>
  <script src="admin-nav.js"></script><script>renderAdminNav('kb');</script>
</body></html>

PRINCIPIO HEADER: la banda "Knowledge Base" + crumbs + sub-nav è condivisa (vive nel layout).
Le pagine NON ripetono un h1 di sezione; se serve un titolo di contenuto usa .admin-panel-head
(h3), non un h1 grande. Conferma di aver capito e attendi il primo prompt-schermata.
```

## 2. File da allegare (5)

```
admin-mockups\design_handoff_admin\admin\tokens.css
admin-mockups\design_handoff_admin\admin\components.css
admin-mockups\design_handoff_admin\admin\admin-base.css
admin-mockups\design_handoff_admin\admin\sp5-admin-kb.html
admin-mockups\design_handoff_admin\admin\sp5-admin-kb-subnav.html
```

## 3. Prompt — Vector Collections

```
Crea il mockup "Knowledge Base · Vector Collections" (tab "Vector Collections" .active nella sub-nav).
Contesto: admin ispeziona la salute pgvector e fa ricerca semantica sui chunk. Ruolo: admin.
CONTENUTO della .admin-body (dopo la sub-nav), in ordine:
1. KPI strip (4× .admin-kpi): Total Vectors (.e-kb), Games Indexed (.e-game), Dimensions (768),
   Avg Health % (.e-toolkit) — ognuna con valore + trend + sparkline.
2. .admin-panel "Ricerca semantica": riga con .admin-search ampia (placeholder "Cerca nei chunk…")
   + select "Tutti i giochi" + select limit [5/10/20/50] + btn-admin.primary "Cerca".
3. Risultati: lista di 4-5 item espandibili (mono): documentId (8 char) · pageNumber · chunkIndex
   · snippet di testo · score. Stile come la chunk-table-row di sp5-admin-kb.html.
4. .admin-panel "Vettori per gioco": grid di card (1 per gioco) con nome gioco (.e-game),
   conteggio vettori, dimensione. ~6 giochi realistici (Wingspan, Brass, Tainted Grail…).
Realtime: no (refresh manuale, btn-admin "⟳ Refresh" in alto a dx della prima panel).
Endpoint (solo realismo): getVectorStats(), searchVectors(query, limit, gameId?).
Rispetta SCHELETRO + sub-nav. Inlinea i CSS.
```

## 4. Prompt — Processing Queue

```
Crea il mockup "Knowledge Base · Processing Queue" (tab "Processing Queue" .active, badge .count "3").
Contesto: admin monitora la coda di elaborazione PDF in tempo reale, con azioni bulk e drill-down job. Ruolo: admin.
CONTENUTO della .admin-body, in ordine:
1. alert-banner.warning "Coda" (1 alert proattivo, es. "2 job falliti nelle ultime 24h").
2. Control bar (.admin-panel compatta): toggle Pausa/Riprendi coda (.admin-toggle), n° worker,
   indicatore backpressure (status-chip).
3. Capacity indicator + Stats bar: 4 .admin-kpi inline → Attivi (.e-agent, status .running),
   In coda, Completati (.e-toolkit), Falliti (.e-event).
4. Bulk actions bar (.bulk-bar, mostrata con "2 selezionati") + filtri (.admin-search + filter-chip stato).
5. Layout 2 colonne grid 40%/60%: sx = .admin-table lista job (col: PDF mono | Gioco | Stato
   status-chip queued/running/completed/failed | Progresso | Avviato mono | ⋮); 1 riga .selected .running.
   dx = .admin-panel "Dettaglio job" (hero job + step timeline + log mono live).
6. In fondo: MetricsDashboard mini (throughput, tempo medio).
Realtime: SÌ — aspetto "live", dot status-chip.running "● streaming" in testa alla lista + al dettaglio.
Endpoint: getQueueList(), getJobDetail() + SSE coda/job.
Rispetta SCHELETRO + sub-nav. Inlinea i CSS.
```

## 5. Prompt — RAG Pipeline (monitor)

```
Crea il mockup "Knowledge Base · RAG Pipeline" (tab "RAG Pipeline" .active).
Contesto: admin visualizza il flusso pipeline Ingest→Extract→Chunk→Embed→Index con salute e metriche. Ruolo: admin.
NB: è il MONITOR della pipeline RAG, NON il workflow-builder.
CONTENUTO della .admin-body, in ordine:
1. Stage flow: 5 box orizzontali collegati da frecce → (Ingest · Extract · Chunk · Embed · Index),
   ognuno con status-chip (healthy/warning/error) + micro-metrica. Box cliccabili (espandibili).
2. Health summary: 3 card "Healthy / Warning / Error" con conteggi (es. "3/5 Healthy") + 4 distribution
   stat-card (.admin-kpi): Documenti, Chunks, Vettori, Storage.
3. .admin-panel "Metriche Elaborazione": step-card per fase con barre P50/P95/P99; + .admin-table di
   confronto col: Fase | Media | P50 | P95 | P99 | Campioni. Evidenzia in .e-event la fase bottleneck.
Realtime: SÌ — refetch 30s (nota "auto-refresh 30s" + btn ⟳ Refresh).
Endpoint: getPipelineHealth(), getProcessingMetrics().
Rispetta SCHELETRO + sub-nav. Inlinea i CSS.
```

## 6. Prompt — Embedding Service

```
Crea il mockup "Knowledge Base · Embedding" (tab "Embedding" .active).
Contesto: admin monitora il servizio di embedding multilingue (specifiche modello + throughput). Ruolo: admin.
CONTENUTO della .admin-body, in ordine:
1. .admin-panel "Stato servizio" con status-chip (Healthy/Unavailable) in testa: righe key-value mono →
   Modello (es. multilingual-e5-base), Device (CPU/GPU), Dimensioni (768), Lingue (EN, IT, DE, FR, ES…),
   Max input chars, Max batch size, nota "auto-refresh 30s".
2. "Throughput": 6× .admin-kpi → Total Requests, Total Failures (.e-event), Failure Rate %,
   Avg Duration (ms), Total Duration (s), Characters Processed (.e-kb). Con sparkline dove sensato.
Realtime: SÌ — refetch 30s + btn ⟳ Refresh.
Endpoint: getEmbeddingInfo(), getEmbeddingMetrics().
Rispetta SCHELETRO + sub-nav. Inlinea i CSS.
```

## 7. Prompt — Feedback KB

```
Crea il mockup "Knowledge Base · Feedback" (tab "Feedback" .active, badge .count "12").
Contesto: admin rivede i feedback 👍/👎 degli utenti sulle risposte KB, filtra per esito, pagina. Ruolo: admin.
CONTENUTO della .admin-body, in ordine:
1. .admin-form-row "Game ID": .admin-input mono con placeholder UUID + hint validazione.
2. Toolbar: select esito [Tutti | Utili | Non utili] (filter-chip) + conteggio totale a dx (mono "12 risultati").
3. Lista feedback: 5-6 item (.admin-panel righe) → badge esito (status-chip.healthy "Utile" /
   .danger "Non utile") + messageId (8 char mono) + data + commento opzionale (testo).
4. Paginazione in fondo: "Pag 1" + btn prev/next (btn-admin.sm).
Realtime: no.
Endpoint: getAdminKbFeedback(gameId, {outcome?, page, pageSize}).
Rispetta SCHELETRO + sub-nav. Inlinea i CSS. Etichette in italiano.
```

## 8. Prompt — Settings

```
Crea il mockup "Knowledge Base · Settings" (tab "Settings" .active).
Contesto: admin vede la config KB (read-only) e lancia operazioni di manutenzione. Ruolo: admin.
CONTENUTO della .admin-body, in ordine:
1. alert-banner.info "Configurazione read-only" + btn ⟳ Refresh.
2. Grid 2-3 colonne di 6 .admin-panel (titolo .admin-panel-head h3 + righe key-value mono):
   - Embedding Model (Provider, Model, Dimensions)
   - Vector Database (Type pgvector, Host, Port)
   - Text Chunking (Default Chunk Size, Overlap, Min/Max, Token Limit, Chars/Token)
   - Cache Configuration (Redis Host/Port, HybridCache Expiration, L1/L2 TTL, Multi-Tier)
   - Reranker Service (Provider, Model, stato)
   - File Storage (Provider, bucket/path)
3. "Danger Zone" (.admin-panel con bordo .e-event): 2 azioni distruttive btn-admin.danger →
   "Rebuild Vector Index" e "Clear KB Cache", ognuna con pattern typed-confirm (mostra solo il pattern UI).
Realtime: no.
Endpoint: getKBSettings(), rebuildIndex(), clearKBCache().
Rispetta SCHELETRO + sub-nav. Inlinea i CSS.
```

## Output finale

Salva i 6 mock come (in ordine di sub-nav):

- `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-vectors.html`
- `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-queue.html`
- `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-pipeline.html`
- `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-embedding.html`
- `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-feedback.html`
- `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-settings.html`

Per **snapshots** non serve un nuovo mock: si segue `sp5-admin-rag-backup.html` esistente.
