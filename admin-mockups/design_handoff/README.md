# Handoff Claude Code — MeepleAI Mock Integration

## Cosa contiene questo pacchetto

Una guida operativa per integrare i ~75 mockup HTML/JSX in `design/` nel tuo codebase reale (React + backend MeepleAI), usando Claude Code in VSCode.

I file in `design/` sono **prototipi di riferimento, non codice di produzione**. Replicano look-and-feel finale ma usano React/Babel inline + dati fake. Il task è ricreare quei design nel codebase esistente, riusando i componenti già in produzione (`ConnectionBar`, `MeepleCard`, `Drawer`, ecc.) e cablando il backend reale.

## File in questo pacchetto

| File | A cosa serve |
|---|---|
| `README.md` | Questo file — overview + workflow |
| `WIRING_GUIDE.md` | Come tradurre un mock JSX in componente reale + cablaggio store/API |
| `BACKEND_PROMPTS.md` | Prompt pronti da incollare in Claude Code per ogni feature backend |
| `SCREENS.md` | Inventario di tutti i mockup, organizzati per fase + endpoint backend attesi |
| `DESIGN_TOKENS.md` | Token CSS in uso — copiare 1:1 |

## Workflow consigliato (3 step)

### 1. Apri il progetto in VSCode + lancia Claude Code

```bash
cd /percorso/al/tuo/progetto-meepleai
code .
# In un terminale integrato:
claude
```

### 2. Fai vedere a Claude Code il pacchetto + i mock

Nel primo messaggio a Claude Code, incolla:

```
Ciao. Devo integrare nel codebase un set di mockup React (~75 schermate)
che trovi nella cartella `design/` (o equivalente).

Leggi prima questi documenti, in ordine:
- design_handoff/README.md
- design_handoff/WIRING_GUIDE.md
- design_handoff/SCREENS.md
- design_handoff/DESIGN_TOKENS.md

Poi dimmi:
1. Quali componenti del mio codebase esistono già e possono essere riutilizzati
2. Quali endpoint backend mancano per supportare i mock
3. Quale schermata propone come pilota per iniziare (la più isolata, bassa complessità)

NON modificare codice ancora — voglio prima un piano scritto.
```

### 3. Procedi una schermata alla volta

Quando Claude Code ha il quadro, scegli una schermata e usa il prompt corrispondente da `BACKEND_PROMPTS.md`. Esempio:

```
Implementiamo SP4 Game Detail (design/sp4-game-detail.jsx → /games/[id]).
Usa il prompt 4.1 da BACKEND_PROMPTS.md.
```

## Regole d'oro per Claude Code

Quando lavora sui mock, ricordagli sempre:

1. **NON copia/incolla il JSX inline** dei mock. Quei file sono showcase con dati hardcoded, multipli stati side-by-side e React via Babel CDN. Estrae i componenti significativi e li ricrea con il tuo stack (Next.js / Vite / ecc.) usando i componenti già in produzione (`ConnectionBar`, `MeepleCard`, `Drawer`, `EntityChip`, `Tabs`...).

2. **Usa SOLO i token CSS** di `design/tokens.css` — niente hex hardcoded, niente colori grigi diretti. Vedi `DESIGN_TOKENS.md`.

3. **Mantieni le 9 entity colors** (game/player/session/agent/kb/chat/event/toolkit/tool) — non aggiungerne mai una decima.

4. **Cabla il backend reale** sostituendo i dati fake (`data.js`) con chiamate API. Per ogni mock, identifica:
   - GET endpoints per leggere
   - POST/PATCH/DELETE per scrivere
   - WebSocket / SSE se serve realtime (chat agente, session live)

5. **Stati richiesti per ogni schermata**: Default + Empty + Loading (skeleton, no spinner) + Error. Sono nel mock, vanno preservati.

6. **Dati realistici fake durante sviluppo OK**, ma:
   - NO UUID-like, NO bearer-token in commit (GitGuardian gate)
   - Usa ID short tipo `gn-saturday-3`, `p-marco`, `kb-wingspan-rules`

7. **Mobile + Desktop entrambi**: ogni feature deve funzionare su 375px e 1440px.

## Cosa fare con i mock

Tre opzioni in ordine di consiglio:

| Opzione | Quando usare |
|---|---|
| **A. Tieni `design/` come reference fuori da `src/`** (consigliato) | Mock come fonte di verità visiva. Claude Code legge da lì ma costruisce in `src/`. |
| **B. Importa solo i CSS** (`tokens.css`, `components.css`) | Se nel codebase mancano i token, copiali direttamente in `src/styles/`. |
| **C. Recupera SOLO i componenti nuovi** che emergono dai mock | Es. `HouseRuleDrawer`, `ConfidenceBadge`, `SuggestedQueriesRow` — li ricrei e li metti nel design system reale. |

## Setup iniziale che puoi delegare a Claude Code

Apri Claude Code e incolla:

```
Setup iniziale:

1. Crea cartella src/design-tokens/ e copia design/tokens.css → src/design-tokens/tokens.css
   (sostituendo eventuali token esistenti se in conflitto).

2. Importa tokens.css in src/main.tsx (o entry point equivalente).

3. Crea src/lib/entity-color.ts che esporta:
   - entityColor(type, alpha?) → restituisce hsl(...) usando i CSS vars
   - 9 entity types tipizzati

4. Verifica che il design system base (Quicksand, Nunito, JetBrains Mono)
   sia caricato. Se no, aggiungilo a index.html o equivalente.

5. Genera un manifest di tutti i mock in design/ con:
   - Nome file
   - Route attesa
   - Sprint di riferimento
   - Componenti React custom usati
   - Dati che si aspettano (entity, count, shape)
   
   Salvalo in design_handoff/MANIFEST.json
```

Questo prepara il terreno. Da lì in poi, ogni feature parte da un prompt di `BACKEND_PROMPTS.md`.

## Domande frequenti

**Posso buttare via `design/` dopo aver finito?**  
Sì, ma conservalo per gli usability test futuri. Il demo `demo.html` resta utile come reference visiva.

**E se il mio backend ha endpoint diversi?**  
Adatta i prompt in `BACKEND_PROMPTS.md` ai tuoi nomi reali. Le forme dei dati restano valide.

**Claude Code può fare tutto in un colpo?**  
Sconsigliato. Schermata per schermata mantiene il contesto pulito e il code review possibile.

---

Vedi `WIRING_GUIDE.md` per il dettaglio operativo, `BACKEND_PROMPTS.md` per i prompt pronti, `SCREENS.md` per l'inventario completo.
