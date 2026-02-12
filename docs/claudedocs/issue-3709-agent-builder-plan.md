# Issue #3709 - Agent Builder UI Implementation Plan

**Epic**: #3687 (AI Platform) | **SP**: 5 | **Estimate**: 3-4 giorni
**Dependencies**: #3708 ✅ DONE (AgentDefinition Type, Strategy, Stats)
**Generated**: 2026-02-12

---

## Scope

Interfaccia completa per creazione e configurazione Agent Definitions nell'AI Lab.

**Features**:
- Form multi-step per creazione agent
- Monaco editor per system prompt
- Tools selection (multi-select from available tools)
- Strategy configuration (dropdown predefined + custom parameters)
- Agent type selection (RAG, Citation, Confidence, RulesInterpreter, Conversation, Custom)
- Preview panel real-time
- Save/Edit/Delete operations

---

## Component Structure

### Main Component

**File**: `apps/web/src/app/(protected)/admin/ai-lab/agents/builder/page.tsx`

```tsx
export default function AgentBuilderPage() {
  // State
  const [step, setStep] = useState(1);  // 1-4 multi-step
  const [agent, setAgent] = useState<AgentForm>(defaultAgent);

  // API
  const { mutate: createAgent } = useCreateAgent();

  return (
    <div className="container">
      <AgentBuilderHeader />
      <AgentBuilderSteps currentStep={step} />

      {step === 1 && <BasicInfoStep agent={agent} onChange={setAgent} />}
      {step === 2 && <PromptEditorStep agent={agent} onChange={setAgent} />}
      {step === 3 && <ToolsStrategyStep agent={agent} onChange={setAgent} />}
      {step === 4 && <ReviewStep agent={agent} onSubmit={handleSubmit} />}

      <AgentPreviewPanel agent={agent} />
    </div>
  );
}
```

### Sub-Components

**BasicInfoStep** (~80 lines):
- Name input (required, max 100)
- Description textarea (max 1000)
- Type selector (dropdown: RAG, Citation, etc.)
- Model selector (GPT-4, Claude-3.5, DeepSeek)
- Temperature slider (0-2)
- Max tokens slider (100-32000)

**PromptEditorStep** (~120 lines):
- Monaco editor for system prompt
- Role tabs (System, User, Assistant)
- Add/Remove prompt templates
- Syntax highlighting
- Character count
- Preview pane

**ToolsStrategyStep** (~100 lines):
- Available tools multi-select checklist
- Tool configuration (per tool settings)
- Strategy dropdown (HybridSearch, VectorOnly, etc.)
- Strategy parameters (dynamic based on selection)

**ReviewStep** (~60 lines):
- Summary of all configuration
- Test button (call API with sample query)
- Save/Cancel buttons

**AgentPreviewPanel** (~80 lines):
- Live preview of agent config
- JSON view (collapsible)
- Validation errors display

---

## API Integration

**Endpoint**: `POST /api/v1/admin/agent-definitions`

**Request**:
```json
{
  "name": "Chess Tutor",
  "description": "Expert chess teacher",
  "type": "RAG",
  "model": "gpt-4",
  "maxTokens": 2048,
  "temperature": 0.7,
  "strategyName": "HybridSearch",
  "strategyParameters": { "topK": 10, "minScore": 0.55 },
  "prompts": [
    { "role": "system", "content": "You are a chess expert..." }
  ],
  "tools": [
    { "name": "web_search", "settings": {} }
  ]
}
```

**Hooks**:
- `useCreateAgent()` - Create mutation
- `useUpdateAgent()` - Update mutation
- `useAgentDefinitions()` - List query
- `useDeleteAgent()` - Delete mutation

**Zod Schema**:
```tsx
const agentFormSchema = z.object({
  name: z.string().min(1).max(100),
  description: z.string().max(1000),
  type: z.enum(['RAG', 'Citation', 'Confidence', 'RulesInterpreter', 'Conversation', 'Custom']),
  model: z.string(),
  maxTokens: z.number().min(100).max(32000),
  temperature: z.number().min(0).max(2),
  strategyName: z.string().optional(),
  strategyParameters: z.record(z.any()).optional(),
  prompts: z.array(promptSchema).max(20),
  tools: z.array(toolSchema).max(50)
});
```

---

## Files to Create

```
apps/web/src/
├── app/(protected)/admin/ai-lab/agents/
│   ├── builder/
│   │   └── page.tsx (AgentBuilderPage, ~300 lines)
│   └── [id]/edit/
│       └── page.tsx (AgentEditorPage, reuse builder)
│
├── components/admin/agent-builder/
│   ├── AgentBuilderHeader.tsx (40 lines)
│   ├── AgentBuilderSteps.tsx (60 lines)
│   ├── BasicInfoStep.tsx (120 lines)
│   ├── PromptEditorStep.tsx (150 lines)
│   ├── ToolsStrategyStep.tsx (130 lines)
│   ├── ReviewStep.tsx (80 lines)
│   ├── AgentPreviewPanel.tsx (100 lines)
│   └── index.ts (exports)
│
├── hooks/admin/
│   ├── use-agent-builder.ts (80 lines)
│   └── use-agent-definitions.ts (100 lines)
│
├── lib/api/
│   └── admin-agent-client.ts (150 lines)
│
├── lib/schemas/
│   └── agent-definition-schema.ts (80 lines)
│
└── __tests__/components/admin/
    └── agent-builder.test.tsx (200 lines, 15 tests)
```

**Total**: ~15 files, ~1600 lines

---

## Implementation Phases

### Phase 1: Basic Form (Day 1)
- BasicInfoStep component
- Agent type + model + config inputs
- Zod validation
- useAgentBuilder hook

### Phase 2: Monaco Editor (Day 2)
- PromptEditorStep with Monaco
- Multi-prompt management
- Role tabs

### Phase 3: Tools & Strategy (Day 2-3)
- ToolsStrategyStep
- Dynamic strategy parameters
- Tool configuration

### Phase 4: Integration (Day 3-4)
- API client methods
- React Query mutations
- Review & Preview
- Tests

---

## Monaco Editor Integration

**Package**: `@monaco-editor/react`

```tsx
import Editor from '@monaco-editor/react';

<Editor
  height="400px"
  language="markdown"
  theme="vs-dark"
  value={prompt.content}
  onChange={(value) => updatePrompt(index, value)}
  options={{
    minimap: { enabled: false },
    fontSize: 14,
    lineNumbers: 'on',
    wordWrap: 'on'
  }}
/>
```

---

## Testing Strategy

**Unit Tests** (15 scenarios):
- Form validation (required fields, max lengths)
- Type selection updates available tools
- Strategy parameter validation
- Monaco editor content updates
- Multi-prompt add/remove
- Tool selection/deselection
- Save button enabled/disabled
- API error handling

**Integration Tests**:
- Create agent flow (all steps)
- Edit existing agent
- Delete agent with confirmation

**Coverage**: >85% (Vitest)

---

**Next Session**: Use this plan with `/implementa 3709`

*Plan saved for future implementation*
