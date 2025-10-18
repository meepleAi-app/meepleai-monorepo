# Magic MCP Server (21st.dev Magic)

Server MCP per generazione AI-powered di componenti UI professionali.

## Panoramica

Magic è un generatore di componenti UI basato su AI che permette di creare interfacce moderne e professionali semplicemente descrivendo cosa vuoi in linguaggio naturale. È come **v0.dev** ma integrato direttamente nel tuo IDE (Cursor, WindSurf, VSCode, Cline).

## Funzionalità

- 🎨 **UI Generation**: Crea componenti UI completi da descrizioni testuali
- 💎 **Professional Quality**: Componenti production-ready con best practices
- 🔷 **TypeScript Support**: Supporto completo TypeScript per type safety
- 🎯 **SVGL Integration**: Accesso a migliaia di brand assets e loghi professionali
- ⚡ **Instant Preview**: Genera e visualizza componenti in tempo reale
- 🎭 **Multi-framework**: Supporto per React, Vue, Svelte e altri framework

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
  -e OPENROUTER_API_KEY=${OPENROUTER_API_KEY} \
  meepleai/mcp-magic:latest
```

## Tools Disponibili

### `magic_generate`
Genera codice o componenti UI basati su descrizione.

**Parametri:**
```json
{
  "description": "Create a modern login form with email and password",
  "language": "typescript",      // Programming language (optional)
  "style": "modern, minimalist"  // Style guidelines (optional)
}
```

**Esempio:**
```
"Crea un componente card pricing con 3 tier (basic, pro, enterprise)"
→ magic_generate con descrizione dettagliata
→ Genera componente React/TypeScript completo con styling
```

### `magic_transform`
Trasforma dati o codice usando pattern matching AI-powered.

**Parametri:**
```json
{
  "data": "existing component code or data",
  "transformation": "convert to dark mode theme",
  "format": "typescript"  // Output format (optional, default: text)
}
```

**Esempio:**
```
"Trasforma questo componente per usare Tailwind invece di CSS modules"
→ magic_transform con codice esistente e descrizione trasformazione
```

### `magic_analyze`
Analizza pattern, struttura e qualità del codice/design.

**Parametri:**
```json
{
  "data": "component code or design spec",
  "analysis_type": "structure"  // "structure", "pattern", "sentiment", "summary"
}
```

**Esempio:**
```
"Analizza questo componente e suggerisci miglioramenti di accessibilità"
→ magic_analyze con analysis_type="structure"
→ Fornisce suggerimenti su ARIA labels, keyboard navigation, etc.
```

### `magic_execute`
Esegue operazioni AI-powered avanzate.

**Parametri:**
```json
{
  "command": "operation to perform",
  "parameters": {
    "key": "value"
  }
}
```

**Esempio:**
```
"Genera varianti colore per questo componente"
→ magic_execute con command="generate_variants"
```

## Esempi d'Uso

### 1. Generazione Componente Completo

```
User: "Crea un dashboard con sidebar, header e area contenuto principale"

Workflow:
1. magic_generate({
     description: "Dashboard layout with collapsible sidebar, header with user menu, main content area",
     language: "typescript",
     style: "modern, clean, responsive"
   })
2. Riceve componente React completo con:
   - TypeScript interfaces
   - Responsive design
   - Accessibility features
   - Clean code structure
```

### 2. Trasformazione Design System

```
User: "Converti questi componenti Material UI in Tailwind CSS"

Workflow:
1. magic_transform({
     data: "<existing MUI component code>",
     transformation: "convert from Material UI to Tailwind CSS, maintain functionality",
     format: "typescript"
   })
2. Riceve componenti trasformati mantenendo logica e accessibilità
```

### 3. Analisi e Miglioramento

```
User: "Analizza questo form e suggerisci miglioramenti UX"

Workflow:
1. magic_analyze({
     data: "<form component>",
     analysis_type: "structure"
   })
2. Riceve report con:
   - Problemi di accessibilità
   - Suggerimenti UX
   - Performance improvements
   - Best practices violations
```

### 4. Generazione con Brand Assets

```
User: "Crea hero section con logo GitHub e CTA buttons"

Workflow:
1. magic_generate con descrizione che include "GitHub logo"
2. Magic usa SVGL integration per includere logo ufficiale GitHub
3. Genera hero section professionale con assets corretti
```

## Casi d'Uso Tipici

### Rapid Prototyping
- "Crea landing page per SaaS product"
- "Genera componenti dashboard analytics"
- "Design sistema di cards responsive"

### Refactoring & Migration
- "Converti da CSS-in-JS a Tailwind"
- "Migra da Class components a Hooks"
- "Trasforma in TypeScript"

### Design System Creation
- "Genera button component library con tutte le varianti"
- "Crea sistema di spacing consistente"
- "Design token system generation"

### Accessibility Enhancement
- "Aggiungi ARIA labels a questo componente"
- "Rendi questo form keyboard-navigable"
- "Migliora contrast ratio per WCAG AAA"

## Integrazione IDE

Magic funziona nativamente con:

- **Cursor**: AI-first code editor
- **WindSurf**: Codeium's IDE
- **VSCode**: Con estensioni MCP
- **Cline**: AI coding assistant

### Installazione Cursor/WindSurf

```bash
npx @21st-dev/magic@latest
```

Segui il wizard di configurazione per integrare Magic nel tuo IDE.

## API Key & Configuration

Magic richiede API key da [21st.dev](https://21st.dev/magic):

1. Registrati su https://21st.dev/magic/console
2. Genera API key
3. Configura in environment variables:

```bash
MAGIC_API_KEY=your_api_key_here
```

## Vantaggi vs Coding Manuale

| Aspetto | Coding Manuale | Magic |
|---------|----------------|-------|
| **Tempo** | Ore per componente complesso | Secondi/minuti |
| **Consistency** | Varia per sviluppatore | Sempre best practices |
| **Accessibility** | Spesso dimenticata | Built-in di default |
| **TypeScript** | Richiede setup manuale | Auto-generated types |
| **Responsive** | CSS mediaquery manuali | Mobile-first automatico |
| **Brand Assets** | Ricerca e download | SVGL integration |

## Best Practices

### 1. Descrizioni Dettagliate

```
❌ "Crea un form"
✅ "Crea form di registrazione con email, password, conferma password, checkbox privacy policy, submit button. Stile moderno con validazione real-time."
```

### 2. Specificare Tecnologie

```json
{
  "description": "Login form",
  "language": "typescript",
  "style": "using Tailwind CSS, React Hook Form, Zod validation"
}
```

### 3. Iterazione Graduale

```
1. magic_generate → genera versione base
2. Testa e identifica miglioramenti
3. magic_transform → applica modifiche
4. magic_analyze → verifica qualità
```

### 4. Componenti Riutilizzabili

```
"Crea Button component riutilizzabile con varianti: primary, secondary, danger, ghost. Supporta size: sm, md, lg. TypeScript props interface."
```

## Performance

- **Generation Time**: 2-10 secondi per componente standard
- **Quality**: Production-ready code seguendo best practices
- **Token Usage**: Ottimizzato per minimizzare costi API
- **Caching**: Componenti simili usano cache per velocità

## Troubleshooting

### Generazione troppo generica

```
# Aggiungi più dettagli e vincoli
magic_generate({
  description: "... [dettagli specifici] ...",
  style: "must use: Tailwind, shadcn/ui, Lucide icons"
})
```

### Stile inconsistente

```
# Specifica design system
"Follow Material Design 3 guidelines"
"Use only primary color #3B82F6, font: Inter"
```

### TypeScript errors

```
# Richiedi types espliciti
"Generate with strict TypeScript, explicit return types, no any"
```

## Integrazione con Altri MCP

### Con Context7

```
"Genera componente usando le ultime API di React 18"
1. get-library-docs("/facebook/react/v18.2.0", topic="hooks")
2. magic_generate con specifiche basate su docs aggiornate
```

### Con GitHub

```
"Crea component library e versiona su GitHub"
1. magic_generate → genera componenti
2. github_create_pr → crea PR con nuovi componenti
3. magic_analyze → code review automatizzata
```

### Con Sequential Thinking

```
"Pianifica e implementa design system completo"
1. sequential_start → pianifica architettura design system
2. Per ogni componente:
   - magic_generate → genera implementazione
   - magic_analyze → verifica qualità
3. sequential_conclude → design system completo
```

### Con Memory Bank

```
"Salva questo pattern per riutilizzo futuro"
1. magic_generate → crea componente
2. memory_store → salva pattern e decisioni di design
3. Future: memory_recall → riusa pattern salvati
```

## Limiti e Considerazioni

- **Componenti Custom Complex**: Per logica business molto specifica, potrebbe richiedere refinement manuale
- **Performance Optimization**: Generazione standard, ottimizzazioni avanzate potrebbero richiedere tuning
- **Framework-specific**: Alcuni pattern avanzati framework-specific potrebbero non essere supportati
- **Design Creativity**: Ottimo per UI standard, design altamente custom potrebbero richiedere iterazioni

## Links Utili

- **21st.dev Magic**: https://21st.dev/magic
- **Console & API Keys**: https://21st.dev/magic/console
- **GitHub Repository**: https://github.com/21st-dev/magic-mcp
- **Documentation**: https://21st.dev/docs
- **Examples Gallery**: https://21st.dev/magic/examples

## Costi

Magic usa API di 21st.dev:
- **Free Tier**: Limitato a X generazioni/mese
- **Pro**: Generazioni illimitate + features avanzate
- **Enterprise**: Custom pricing per team

Consulta [21st.dev pricing](https://21st.dev/pricing) per dettagli.

## Sicurezza

- Generated code è sempre safe-by-default
- No injection vulnerabilities nel codice generato
- Accessibility best practices built-in
- Type safety con TypeScript

## Contribuire

Per segnalare bug o suggerire features:
- GitHub Issues: https://github.com/21st-dev/magic-mcp/issues
- Community: Discord / Forum 21st.dev

## Licenza

Vedi [21st.dev Terms of Service](https://21st.dev/terms).
