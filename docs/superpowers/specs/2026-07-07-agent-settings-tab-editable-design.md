# Settings tab editabile via `AgentConfigFields` condiviso

**Issue**: #2732 (follow-up di #2727 / PR #2731)
**Data**: 2026-07-07
**Tipo**: feature (frontend)

## Contesto e problema

Nel fix #2727 il tab **Settings** di `/agents/[id]` è stato cablato al salvataggio della config AI per-gioco (`useUpdateAgentConfig` → `PUT /api/v1/library/games/{gameId}/agent-config`). Tuttavia `AgentSettingsForm` è un componente **display-only**: il sotto-componente `ConfigDisplay` renderizza `strategy` e `parameters` come testo/JSON read-only, senza input editabili. Lo stato `editable` mostra gli stessi dati read-only più i bottoni Salva/Annulla, ma **l'utente non può modificare nulla**.

Il form editabile completo esiste già in `AgentConfigModal` (library) — i 6 campi della config AI per-gioco, con la loro UI e le option dal contratto `agent-config.schemas`.

## Obiettivo

Rendere il tab Settings un **editor reale** della config AI per-gioco, riusando un componente di campi condiviso col modal — senza duplicare la UI.

## Contratto config (invariato, da #2727)

`AgentConfigDto` (UserLibrary, per-gioco):

```ts
{
  llmModel: AIModel;            // enum: llama-3.3-70b-free | google-gemini-pro | deepseek-chat | llama-3.3-70b
  temperature: number;         // 0.0–2.0
  maxTokens: number;           // 512–8192 (UI); BE accetta 100–32000
  personality: AgentPersonality;   // Amichevole | Professionale | Umoristico | Conciso | Dettagliato
  detailLevel: DetailLevel;        // Breve | Normale | Dettagliato | Esaustivo
  personalNotes: string | null;    // max 1000
}
```

## Design

### 1. Nuovo componente `AgentConfigFields` (condiviso, controllato)

`apps/web/src/components/agent/config/AgentConfigFields.tsx` — presentational puro, controllato. Estrae *verbatim* i 6 campi oggi inline in `AgentConfigModal.tsx`:

- **Modello**: `Select` su `MODEL_OPTIONS`
- **Temperatura**: `Slider` 0–2 step 0.1
- **Max Tokens**: `Input` number 512–8192
- **Personalità**: `RadioGroup` su `PERSONALITY_OPTIONS`
- **Livello dettaglio**: `RadioGroup` su `DETAIL_LEVEL_OPTIONS`
- **Note personali**: `Textarea` maxLength 1000 + contatore

Contratto:

```ts
export interface AgentConfigFieldsValue {
  llmModel: AIModel;
  temperature: number;
  maxTokens: number;
  personality: AgentPersonality;
  detailLevel: DetailLevel;
  personalNotes: string; // stringa vuota anziché null nel form; normalizzata a null in uscita dai consumer
}

export interface AgentConfigFieldsProps {
  value: AgentConfigFieldsValue;
  onChange: (patch: Partial<AgentConfigFieldsValue>) => void;
  disabled?: boolean; // read-only / archived
}
```

Nessun hook, nessun fetch. Usa `MODEL_OPTIONS`/`PERSONALITY_OPTIONS`/`DETAIL_LEVEL_OPTIONS`/tipi da `agent-config.schemas`. Ogni input disabilitato quando `disabled`.

### 2. Refactor `AgentConfigModal`

Sostituisce i campi inline (righe ~250–376) con `<AgentConfigFields value={{...}} onChange={patch => applica setter}} />`. Lo stato locale (`llmModel`, `temperature`, …), `handleSave`, `handleResetToDefault` restano invariati. **Comportamento identico** — i test esistenti (inclusi quelli #2727) sono il guardrail.

### 3. `AgentSettingsForm` editabile

Il componente del tab evolve da display-only a editor:

- `SettingsState` porta il **value tipizzato**:
  ```ts
  type SettingsState =
    | { kind: 'loading' }
    | { kind: 'error'; retry: () => void }
    | { kind: 'editable'; value: AgentConfigFieldsValue }
    | { kind: 'read-only'; value: AgentConfigFieldsValue; readOnlyReason: 'archived' | 'standalone' };
  ```
- Il form possiede lo **stato di edit locale** (init da `value`; `useEffect` che risincronizza lo stato quando cambia il `value` in ingresso, es. dopo un refetch), monta `AgentConfigFields`.
- `editable`: campi attivi + nota per-gioco + Salva/Annulla. **Salva** → `onSave(localValue)`; **Annulla** → reset a `value`.
- `read-only`: `AgentConfigFields disabled` + banner (`role="status"`) con testo dipendente da `readOnlyReason` (archiviato vs standalone).
- **Nota per-gioco** (stato editable): banner informativo "Queste impostazioni valgono per tutti gli agenti di questo gioco." (label i18n).

Props: `onSave: (value: AgentConfigFieldsValue) => void` (era `(config: AgentConfig) => void`).

### 4. Wiring in `AgentDetailView`

- `mapSettingsState(query, variant, agentHasGameId, onRetry)`:
  - loading/error invariati.
  - Costruisce `value` da `configQueryGated.data` con fallback ai default (`DEFAULT_AGENT_CONFIG` + `personalNotes: ''`).
  - `archived` → `{ kind: 'read-only', value, readOnlyReason: 'archived' }`.
  - **standalone** (agente senza `gameId`) → `{ kind: 'read-only', value, readOnlyReason: 'standalone' }`.
  - altrimenti → `{ kind: 'editable', value }`.
- `onSave(value)` → guardia `isPending`; `gameId` presente per costruzione (editable solo con gameId); `updateConfig.mutate({ gameId, request: { ...value, personalNotes: value.personalNotes || null } }, { onSuccess: toast.success, onError: toast.error })`.

Questo **estende** il wiring #2727: si salva il **value editato** dal form (non più la config caricata). La guardia default-create di #2727 non serve più (il form parte sempre dai default quando non c'è config → salva quei valori).

## Nuove label i18n (`apps/web/src/locales/it.json` + `en.json`)

- `pages.agentDetail.settings.perGameNote`
- `pages.agentDetail.settings.readOnlyStandalone`

(La chiave esistente `readOnlyBanner` resta per il caso archived.) Il tab passa le nuove label a `AgentSettingsForm` via il dizionario `settingsLabels` già costruito in `AgentDetailView`.

## Edge case

- **Standalone (no gameId)**: read-only con nota dedicata; salvataggio non possibile (config per-gioco).
- **Config assente**: il form parte dai default; Salva crea la config con quei valori.
- **Archived**: read-only (comportamento attuale preservato).

## Testing (TDD)

1. `AgentConfigFields.test.tsx`: render dei 6 input; `onChange` emette il patch corretto per ogni campo; `disabled` disabilita tutti gli input.
2. `AgentConfigModal.test.tsx`: i test esistenti restano verdi (refactor non-regressivo); asserzione che il payload di Salva resti BE-aligned.
3. `AgentSettingsForm.test.tsx`: stato editable mostra input; modificando un campo e premendo Salva, `onSave` riceve il value **modificato**; read-only disabilita gli input e mostra il banner corretto per reason.
4. `AgentDetailView.test.tsx`: (a) in editable, modifica campo + Salva → `updateConfig.mutate` col value modificato + `personalNotes` normalizzato; (b) agente standalone → tab read-only reason=standalone; (c) archived → read-only reason=archived.

## Fuori scope (YAGNI)

- Nessun tocco alla config **per-agente** (`AgentDefinition`: strategy/prompts/tools) o al PATCH `/agents/{id}/configuration`.
- Nessun refactor non correlato.
- Editabilità dei campi restano i 6 della config per-gioco (nessun campo nuovo).

## File impattati

| File | Azione |
|---|---|
| `components/agent/config/AgentConfigFields.tsx` | **nuovo** |
| `components/library/AgentConfigModal.tsx` | refactor → usa `AgentConfigFields` |
| `components/features/agent-detail/AgentSettingsForm.tsx` | editabile + `AgentConfigFields` |
| `app/(authenticated)/agents/[id]/_components/AgentDetailView.tsx` | `mapSettingsState` + `onSave` value editato |
| `messages/it.json`, `messages/en.json` | 2 label nuove |
| relativi `__tests__/` | test TDD |
