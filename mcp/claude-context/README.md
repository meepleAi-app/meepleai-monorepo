# Context7 MCP Server (Upstash Context7)

Server MCP per recupero di documentazione aggiornata delle librerie in tempo reale.

## Panoramica

Context7 fornisce agli LLM accesso a documentazione up-to-date di qualsiasi libreria software, risolvendo il problema dei dati di training obsoleti. Invece di basarsi su conoscenze statiche, Context7 recupera esempi di codice e riferimenti API direttamente dalle fonti ufficiali.

## Funzionalità

- 📚 **Documentazione in tempo reale**: Recupera docs aggiornate per qualsiasi libreria
- 🎯 **Version-specific**: Supporta versioni specifiche (es. React 18.2.0)
- 🔍 **Smart search**: Ricerca intelligente basata su topic
- 💡 **Code examples**: Fornisce esempi di codice pratici
- 🚀 **Zero latency**: Caching intelligente per performance ottimali

## Avvio

```bash
docker run -i --rm \
  --read-only \
  --tmpfs /tmp:rw,size=64m \
  --cap-drop ALL \
  --security-opt no-new-privileges \
  --pids-limit 128 \
  --memory 512m \
  --user $(id -u):$(id -g) \
  meepleai/mcp-context7:latest
```

## Tools Disponibili

### `resolve-library-id`
Risolve un nome di libreria a un ID compatibile con Context7.

**Parametri:**
```json
{
  "libraryName": "react"  // Nome della libreria da cercare
}
```

**Esempio:**
```
"Trova l'ID per la libreria Next.js"
→ resolve-library-id con libraryName="next.js"
→ Ritorna: "/vercel/next.js"
```

**Ritorna:**
- Lista di librerie corrispondenti con ID Context7
- Informazioni su trust score, numero di docs, code snippets
- Descrizione della libreria

### `get-library-docs`
Recupera documentazione aggiornata per una libreria specifica.

**Parametri:**
```json
{
  "context7CompatibleLibraryID": "/vercel/next.js",  // ID ottenuto da resolve-library-id
  "topic": "routing",                                 // Topic specifico (optional)
  "tokens": 5000                                      // Max tokens da recuperare (optional, default: 5000)
}
```

**Esempio:**
```
"Mostrami la documentazione di Next.js 14 sui React Server Components"
→ 1. resolve-library-id("next.js")
→ 2. get-library-docs("/vercel/next.js/v14.3.0", topic="server components")
```

**Ritorna:**
- Documentazione testuale aggiornata
- Esempi di codice rilevanti
- Best practices dalla documentazione ufficiale

## Esempi d'Uso

### 1. Ricerca Libreria + Documentazione

```
User: "Come funziona useEffect in React 18?"

Workflow:
1. resolve-library-id("react")
   → ID: "/facebook/react"
2. get-library-docs("/facebook/react/v18.2.0", topic="useEffect")
   → Docs aggiornate su useEffect con esempi
```

### 2. Versione Specifica

```
User: "Mostrami la sintassi di fetch in Next.js 14"

Workflow:
1. resolve-library-id("next.js")
2. Seleziona versione specifica: /vercel/next.js/v14.3.0
3. get-library-docs(..., topic="data fetching")
```

### 3. Multiple Topics

```
User: "Implementa autenticazione in Supabase"

Workflow:
1. resolve-library-id("supabase")
2. get-library-docs("/supabase/supabase", topic="authentication", tokens=8000)
   → Recupera docs estese su auth con esempi
```

## Casi d'Uso Tipici

### Development Workflow

- **Durante coding**: "Qual è la sintassi per [...] in libreria X?"
- **Debugging**: "Come risolvere errore Y in libreria Z versione N?"
- **Best practices**: "Qual è il pattern consigliato per [...] in framework X?"
- **Migration**: "Differenze tra versione X e Y di libreria Z?"

### Learning & Exploration

- **Nuove librerie**: "Come iniziare con libreria X?"
- **Advanced features**: "Feature avanzate di framework Y?"
- **Comparazioni**: "Differenze tra libreria A e B per use case X?"

## Vantaggi vs Conoscenza Statica LLM

| Aspetto | LLM Standard | Context7 |
|---------|-------------|----------|
| **Aggiornamento** | Training cut-off (es. Gen 2024) | Real-time docs |
| **Versioni** | Spesso miste/vecchie | Version-specific |
| **Accuratezza** | Può essere obsoleta | Always up-to-date |
| **Breaking changes** | Non rilevati | Documentati |
| **New features** | Non conosciute | Disponibili subito |

## Configurazione

Context7 non richiede API key per uso base. Per uso enterprise o rate limit più alti, consultare [Upstash Context7 docs](https://upstash.com/docs/context7).

## Performance

- **Latency**: ~200-500ms per query (con caching: <50ms)
- **Cache TTL**: 24 ore (docs stabili), 1 ora (docs in sviluppo)
- **Max tokens**: Configurabile, default 5000 (~ 1-2 pagine di docs)

## Troubleshooting

### Libreria non trovata

```bash
# Prova varianti del nome
resolve-library-id("react")
resolve-library-id("reactjs")
resolve-library-id("facebook/react")
```

### Docs troppo generiche

```bash
# Usa topic più specifico
get-library-docs("/vercel/next.js", topic="app router")  # ✅ Specifico
# vs
get-library-docs("/vercel/next.js", topic="routing")     # ❌ Generico
```

### Rate limiting

Context7 ha limiti di rate ragionevoli. Se raggiunti:
- Usa caching più aggressivo
- Riduci `tokens` parameter
- Considera upgrade a piano enterprise

## Links Utili

- **Upstash Context7 Blog**: https://upstash.com/blog/context7-mcp
- **GitHub Repository**: https://github.com/upstash/context7
- **NPM Package**: @upstash/context7-mcp
- **Official Docs**: https://upstash.com/docs/context7

## Integrazione con Altri MCP

### Con Memory Bank

```
"Ricorda questo pattern per uso futuro"
1. get-library-docs(...) → recupera best practice
2. memory_store → salva per riferimenti futuri
```

### Con GitHub

```
"Implementa feature X usando libreria Y e committa"
1. get-library-docs → recupera sintassi/esempi
2. Implementa codice
3. github_create_pr → crea PR con docs reference
```

### Con Sequential Thinking

```
"Pianifica migrazione da libreria A a B"
1. sequential_start → inizia ragionamento
2. get-library-docs(A) → docs vecchia versione
3. get-library-docs(B) → docs nuova libreria
4. sequential_conclude → piano di migrazione
```

## Note di Sicurezza

- Context7 recupera solo documentazione pubblica
- Non espone codice proprietario o privato
- Rispetta robots.txt e politiche di scraping
- Implementa rate limiting per evitare abuse

## Contribuire

Per segnalare librerie mancanti o problemi:
- GitHub: https://github.com/upstash/context7/issues
- Community: Upstash Discord

## Licenza

Vedi documentazione ufficiale Upstash Context7.
