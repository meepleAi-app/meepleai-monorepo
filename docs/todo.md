Architettura KB (importante da capire prima)

La Knowledge Base non è un'entità esplicita nel sistema. È la collezione implicita di VectorDocument (PDF indicizzati)  
 associati a un gioco. Quindi:

- Non esiste un bottone "Crea KB"
- La KB si crea automaticamente quando indicizzi dei PDF su un gioco
- Gli agenti interrogano la KB via RAG quando rispondono

---

Sequenza UI per Admin

1. CREA SHARED GAME /admin/shared-games/new
   └─ Aggiungi titolo, players, duration, BGG match

2. CARICA PDF + CREA KB /admin/games/import/wizard
   ├─ Step 1: Upload PDF
   ├─ Step 2: Metadata extraction (automatica)
   ├─ Step 3: Match BGG
   └─ Step 4: Confirm → indicizzazione avvia in background

   oppure

   /admin/pdfs → upload manuale + reindex
   /admin/knowledge-base/queue → monitora la coda di indicizzazione

3. VERIFICA KB PRONTA /admin/knowledge-base
   └─ Storage health: PostgreSQL docs/chunks + Qdrant vectors

4. CREA AGENT DEFINITION /admin/agent-definitions/create
   ├─ Nome, descrizione
   ├─ Modello (GPT-4, Claude, etc.) + temperatura/token
   ├─ Prompt templates
   └─ Tool configuration

5. DEBUG CHAT /admin/agents/test
   ⚠️ MOCKED - vedi gap sotto

---

Gap da Colmare (Issues da Implementare)

🔴 Gap Critico: Agent Test Console completamente mockato

File: apps/web/src/app/(authenticated)/admin/agents/test/client.tsx

Il codice commenta l'API reale e usa dati finti:
// Simulate API call - replace with actual API when available
// const response = await api.agents.test(selectedTypologyId, { query, ... });
await new Promise(resolve => setTimeout(resolve, 1500 + Math.random() \* 1000));
// → risposta hardcoded, metriche false

Issues correlate:

- #3378 — Agent Testing Console (feature issue principale)
- #3379 — Save test result to history (non implementato, solo toast.success() vuoto)

Cosa manca nel backend: endpoint POST /api/v1/agents/{typologyId}/test

---

🟡 Bug: PDF Retry usa endpoint sbagliato

File: apps/web/src/app/(authenticated)/admin/pdfs/client.tsx (riga ~157)

// SBAGLIATO:
await fetch(`/api/v1/documents/${pdfId}/retry`, { method: 'POST' });

// CORRETTO (endpoint esiste):
await apiClient.admin.reindexPdf(pdfId); // → /api/v1/admin/pdfs/{pdfId}/reindex

---

🟢 Funziona già tutto il resto

┌───────────────────────┬─────────────────────┬──────────────────────────────────┐
│ Flow │ Status │ Route │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ Crea Shared Game │ ✅ Implementato │ /admin/shared-games/new │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ Import da BGG │ ✅ Implementato │ /admin/shared-games/add-from-bgg │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ Upload PDF (wizard) │ ✅ Implementato │ /admin/games/import/wizard │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ Monitor PDF indexing │ ✅ Implementato │ /admin/knowledge-base/queue │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ KB health/stats │ ✅ Implementato │ /admin/knowledge-base │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ Crea Agent Definition │ ✅ Implementato │ /admin/agent-definitions/create │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ Approval typologies │ ✅ Implementato │ /admin/agent-typologies │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ Debug chat (UI shell) │ ⚠️ Mocked │ /admin/agents/test │
├───────────────────────┼─────────────────────┼──────────────────────────────────┤
│ Save test history │ ❌ Non implementato │ (#3379) │
└───────────────────────┴─────────────────────┴──────────────────────────────────┘

---

Issues da Implementare (priorità)

┌──────────┬─────────────┬────────────────────────────────────────────────────────┐
│ Priority │ Issue │ Descrizione │
├──────────┼─────────────┼────────────────────────────────────────────────────────┤
│ 🔴 Alta │ Nuova issue │ Backend endpoint POST /api/v1/agents/{typologyId}/test │
├──────────┼─────────────┼────────────────────────────────────────────────────────┤
│ 🔴 Alta │ #3378 │ Collegare frontend test console all'API reale │
├──────────┼─────────────┼────────────────────────────────────────────────────────┤
│ 🟡 Media │ #3379 │ Persistere test history nel DB │
├──────────┼─────────────┼────────────────────────────────────────────────────────┤
│ 🟢 Bassa │ Bug fix │ Fix PDF retry endpoint (1 riga) │
└──────────┴─────────────┴────────────────────────────────────────────────────────┘

note: la KB e' l'insieme della conoscenza legata a un gioco.  
 Al momento sono solo pdf, ma in futuro verranno integrati altri formati.  
 Quando si carica un pdf, questo entra in una coda. Quando termina il processo, diventa disponibile  
 come KB del gioco e dell'agente come RAG
