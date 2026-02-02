# RAG Strategy Visual Builder - Specification

**Created**: 2026-02-02
**Epic**: #3434 (Tier-Strategy-Model Architecture) → Future Epic: Visual Builder
**Status**: Brainstorming → Specification

---

## 🎯 Overview

Visual pipeline builder for creating custom RAG strategies by dragging and connecting building blocks.

**User Preferences** (from brainstorming):
- ✅ Modal popup for block explanations
- ✅ Hybrid approach: Static examples + live test option
- ✅ Drag & Drop Canvas (like Figma/n8n)
- ✅ All block types: Layers, Agents, Models, Conditionals, Parallel, Transforms

---

## 📋 Feature 1: Block Click → Explanation Modal

### UX Flow
```
User clicks block → Modal opens → Shows:
├─ What it does (plain language)
├─ When to use it (use cases)
├─ How it works (technical details)
├─ Code implementation (file:line references)
├─ Parameters configuration
├─ Metrics (tokens, latency, cost, accuracy impact)
└─ [Button: Close] [Button: Configure]
```

### Modal Structure

#### Section 1: Header
```tsx
<ModalHeader>
  <BlockIcon size={48} color={block.color} />
  <Title>{block.name}</Title>
  <Badge type={block.type} />
  <ComplexityIndicator level={block.complexity} />
</ModalHeader>
```

#### Section 2: Description Tab (Default)
```tsx
<Tab name="Description">
  <PlainLanguage>
    What: Cross-encoder reranking re-scores retrieved documents...
    Why: Solves "lost in middle" problem, boosts precision +20-40%
    When: Use after initial retrieval when precision matters
  </PlainLanguage>

  <UseCases>
    • High-precision requirements (legal, medical)
    • Large candidate sets (TopK > 20)
    • Critical decision support
  </UseCases>

  <Metrics>
    Tokens: ~0 (inference only)
    Latency: +100-500ms
    Accuracy Impact: +20-40% precision
    Cost: ~$0.001 per call
  </Metrics>
</Tab>
```

#### Section 3: Technical Tab
```tsx
<Tab name="Technical">
  <HowItWorks>
    1. Takes query + top K docs
    2. Runs cross-attention (query, doc) pairs
    3. Scores each pair with relevance (0-1)
    4. Re-sorts docs by new scores
    5. Returns top N (N < K)
  </HowItWorks>

  <CodeReferences>
    Backend: apps/api/.../RerankerService.cs:45-120
    Models: mxbai-rerank, ColBERTv2, Cohere Rerank
    Config: appsettings.json → Reranking section
  </CodeReferences>

  <Parameters expandable>
    • Model: [mxbai-rerank, ColBERTv2, Cohere] (dropdown)
    • TopN: 3-10 (slider, default: 5)
    • InputTopK: 20-50 (slider, default: 30)
  </Parameters>
</Tab>
```

#### Section 4: Examples Tab
```tsx
<Tab name="Examples">
  <BeforeAfter>
    Input (TopK=20):
    1. [0.85] Relevant doc A
    2. [0.84] Spam doc B
    ...
    11. [0.72] VERY relevant doc C (lost in middle!)

    After Reranking (TopN=5):
    1. [0.98] VERY relevant doc C ← PROMOTED!
    2. [0.92] Relevant doc A
    3. [0.88] Another relevant doc
    4. [0.81] Moderately relevant
    5. [0.76] Borderline relevant
    (Spam doc B removed)
  </BeforeAfter>

  <ImpactVisualization>
    [Mini chart showing precision improvement]
  </ImpactVisualization>
</Tab>
```

#### Section 5: Research Tab (Optional)
```tsx
<Tab name="Research">
  <AcademicSources>
    • "Improving Retrieval with Cross-Encoders" (2024)
    • "Lost in Middle" paper (Stanford, 2024)
    • Cohere Rerank API docs
  </AcademicSources>

  <ProductionUsage>
    Companies using: Databricks, LangChain, LlamaIndex
    Success rate: 90%+ in production RAG systems
  </ProductionUsage>
</Tab>
```

### Modal Actions
- **Close**: ESC key or click outside
- **Configure**: Opens parameter editor
- **Add to Canvas**: Drag from modal to canvas
- **View Code**: Link to backend implementation

---

## 📋 Feature 2: Example Input/Output with Live Test

### UX Flow: Static Examples (Default)

```
Strategy card footer:
[Button: "📝 View Examples"]
  ↓
Modal/Sidebar opens with 3-5 curated examples:

Example 1: Simple FAQ
  Input: "Quanti giocatori supporta?"
  Output: "Il gioco supporta 2-4 giocatori."
  Metrics: 1850 tokens, <150ms, $0 (cached)

Example 2: Rules Lookup
  Input: "Posso muovere in diagonale?"
  Output: "Sì, secondo la sezione 3.2 pagina 12..."
  Metrics: 2100 tokens, 180ms, $0 (Llama 3.3)

Example 3: Edge Case
  Input: "Cosa succede se pareggio al tie-breaker?"
  Output: "Secondo regole pagina 45, si applica..."
  Metrics: 2400 tokens, 250ms, $0.0001
```

### UX Flow: Live Test (Admin Only)

```
Strategy card footer:
[Button: "🧪 Test Live"] (only visible to Admin tier)
  ↓
Opens test interface:

┌─ Live Strategy Test ────────────────────┐
│ Strategy: FAST                           │
│                                          │
│ Input Query:                             │
│ ┌────────────────────────────────────┐  │
│ │ Quanti giocatori?                  │  │
│ └────────────────────────────────────┘  │
│                                          │
│ Game Context:                            │
│ [Dropdown: Catan, Ticket to Ride, ...]  │
│                                          │
│ [⚡ Run Test]  [📊 Compare Strategies]  │
│                                          │
│ ─── Results (live streaming) ───────    │
│                                          │
│ ⏳ L1: Routing... 45ms ✓                │
│ ⏳ L2: Cache check... MISS ✗            │
│ ⏳ L3: Retrieval... 3 chunks, 120ms ✓   │
│ ⏳ L5: Generation... 180ms ✓            │
│                                          │
│ 📤 Output:                               │
│ "Il gioco supporta 2-4 giocatori..."    │
│                                          │
│ 📊 Metrics:                              │
│ • Tokens: 2,045 (vs estimated 2,060)    │
│ • Latency: 345ms (vs target <200ms) ⚠️  │
│ • Cost: $0.0001                          │
│ • Confidence: 0.82                       │
│ • Citations: 1 source (rulebook p.3)    │
└──────────────────────────────────────────┘
```

### Implementation

**Backend API Endpoint**:
```csharp
POST /api/v1/admin/rag/test-strategy
{
  "strategyId": "FAST",
  "query": "Quanti giocatori?",
  "gameId": "uuid",
  "userId": "admin-uuid"
}

Response (SSE stream):
{
  "event": "layer_start",
  "layer": "L1-Routing",
  "timestamp": "2026-02-02T10:30:00Z"
}
{
  "event": "layer_complete",
  "layer": "L1-Routing",
  "tokensUsed": 320,
  "latencyMs": 45
}
...
{
  "event": "complete",
  "answer": "Il gioco supporta 2-4 giocatori...",
  "totalTokens": 2045,
  "totalLatency": 345,
  "cost": 0.0001,
  "citations": [...]
}
```

**Frontend Component**:
```tsx
// apps/web/src/components/admin/StrategyLiveTester.tsx
export function StrategyLiveTester({ strategy }: Props) {
  const [isRunning, setIsRunning] = useState(false);
  const [results, setResults] = useState<TestResult[]>([]);

  const runTest = async (query: string, gameId: string) => {
    setIsRunning(true);

    const stream = useStrategyTestStream({
      strategyId: strategy.id,
      query,
      gameId
    });

    stream.on('layer_start', (data) => {
      // Update UI with layer status
    });

    stream.on('layer_complete', (data) => {
      // Show metrics for completed layer
    });

    stream.on('complete', (data) => {
      setResults([...results, data]);
      setIsRunning(false);
    });
  };

  return (
    <Modal>
      <QueryInput onChange={setQuery} />
      <GameSelector onChange={setGameId} />
      <Button onClick={() => runTest(query, gameId)} loading={isRunning}>
        ⚡ Run Test
      </Button>
      <LiveResults results={results} />
    </Modal>
  );
}
```

---

## 📋 Feature 3: Visual Strategy Builder (Drag & Drop Canvas)

### Architecture: React Flow + Custom Blocks

**Tech Stack**:
- **ReactFlow**: Canvas and node management
- **Zustand**: Builder state management
- **React DnD**: Drag and drop interactions
- **Monaco Editor**: JSON/YAML preview (optional)

### Canvas Layout

```
┌─────────────────────────────────────────────────────────────┐
│  Strategy Builder - Admin                        [Save] [Test] │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─ Block Palette ──┐  ┌─ Canvas ─────────────────────┐   │
│  │                   │  │                               │   │
│  │ 📦 RETRIEVAL     │  │   [Input Query]               │   │
│  │  🔍 Vector        │  │         ↓                     │   │
│  │  📝 Keyword       │  │   [Hybrid Search]             │   │
│  │  ⚖️ Hybrid        │  │         ↓                     │   │
│  │  🔄 Multi-Hop     │  │   [Reranking]                 │   │
│  │                   │  │         ↓                     │   │
│  │ ⚡ OPTIMIZATION   │  │   [CRAG Eval] ─No→ [WebSearch]│   │
│  │  ✏️ Rewrite       │  │         ↓ Yes                 │   │
│  │  🔀 Decompose     │  │   [Generation]                │   │
│  │                   │  │         ↓                     │   │
│  │ 🎯 RANKING        │  │   [Output]                    │   │
│  │  📊 Reranker      │  │                               │   │
│  │  🏷️ Filter        │  │   Tokens: ~3200               │   │
│  │                   │  │   Est. Cost: $0.015           │   │
│  │ ✅ VALIDATION     │  │   Est. Latency: 1.2s          │   │
│  │  ⚖️ CRAG          │  │                               │   │
│  │  🔍 Citation      │  │                               │   │
│  │                   │  └───────────────────────────────┘   │
│  │ 🤖 AGENTS         │                                       │
│  │  🧠 General       │  ┌─ Configuration Panel ───────┐    │
│  │  ⚖️ Rules         │  │                              │    │
│  │                   │  │ Block: Hybrid Search         │    │
│  │ 🎨 MODELS         │  │                              │    │
│  │  🦙 Llama 3.3     │  │ Alpha: 0.5 [━━━●━━]         │    │
│  │  🤖 DeepSeek      │  │ VectorTopK: 10 [━━●━━━]     │    │
│  │  ✨ Sonnet        │  │ KeywordTopK: 10 [━━●━━━]    │    │
│  │                   │  │ FusionMethod:                 │    │
│  │ 🔀 CONTROL        │  │  ○ Weighted  ● RRF           │    │
│  │  🔀 If/Else       │  │                              │    │
│  │  🔄 Loop          │  │ [Apply] [Reset to Default]   │    │
│  │  ⚡ Parallel      │  └──────────────────────────────┘    │
│  └───────────────────┘                                      │
└─────────────────────────────────────────────────────────────┘
```

### Component Hierarchy

```tsx
<StrategyBuilderPage>
  <BuilderHeader />
  <BuilderContainer>
    <BlockPalette>
      <PaletteSection category="retrieval" />
      <PaletteSection category="optimization" />
      <PaletteSection category="ranking" />
      <PaletteSection category="validation" />
      <PaletteSection category="agents" />
      <PaletteSection category="models" />
      <PaletteSection category="control" />
    </BlockPalette>

    <CanvasArea>
      <ReactFlow
        nodes={strategyNodes}
        edges={strategyEdges}
        onDrop={handleBlockDrop}
        onConnect={handleBlockConnect}
        onNodeClick={handleNodeClick}
      />
      <CanvasToolbar />
      <MetricsPreview />
    </CanvasArea>

    <ConfigPanel>
      <BlockConfiguration block={selectedBlock} />
      <BlockExplanation />
    </ConfigPanel>
  </BuilderContainer>

  <BlockExplanationModal
    block={selectedBlock}
    onClose={closeModal}
    onConfigure={openConfigPanel}
  />

  <ExampleIOModal
    strategy={currentStrategy}
    onRunLiveTest={triggerLiveTest}
  />

  <LiveTestModal
    isRunning={testRunning}
    results={testResults}
    onCancel={cancelTest}
  />
</StrategyBuilderPage>
```

---

## 📋 Feature 2: Example Input/Output System

### Static Examples Data Structure

```typescript
interface StrategyExample {
  id: string;
  strategyId: RagStrategy;
  title: string;
  category: 'simple_faq' | 'rules_lookup' | 'strategy' | 'edge_case';

  input: {
    query: string;
    gameContext?: string;
    userTier: UserTier;
  };

  output: {
    answer: string;
    sources: Citation[];
    confidence: number;
  };

  metrics: {
    tokensUsed: number;
    latencyMs: number;
    cost: number;
    cacheHit: boolean;
  };

  explanation: string; // Why this example demonstrates the strategy
}

// Example data
const FAST_EXAMPLES: StrategyExample[] = [
  {
    id: 'fast-1',
    strategyId: 'FAST',
    title: 'Simple FAQ - Player Count',
    category: 'simple_faq',
    input: {
      query: 'Quanti giocatori supporta?',
      gameContext: 'Catan',
      userTier: 'User'
    },
    output: {
      answer: 'Il gioco supporta 2-4 giocatori (o 5-6 con espansione).',
      sources: [{ page: 3, text: 'Numero giocatori: 2-4' }],
      confidence: 0.92
    },
    metrics: {
      tokensUsed: 1850,
      latencyMs: 145,
      cost: 0,
      cacheHit: false
    },
    explanation: 'FAST is optimal for simple lookups: single-hop retrieval, free model, sub-200ms response.'
  },
  // ... 4 more examples
];
```

### Live Test Implementation

**Components**:
```tsx
// Hook for SSE streaming
function useStrategyLiveTest() {
  const [status, setStatus] = useState<'idle' | 'running' | 'complete' | 'error'>('idle');
  const [layerProgress, setLayerProgress] = useState<LayerProgress[]>([]);
  const [finalResult, setFinalResult] = useState<TestResult | null>(null);

  const runTest = async (params: TestParams) => {
    const eventSource = new EventSource(`/api/v1/admin/rag/test-strategy?strategyId=${params.strategyId}&query=${encodeURIComponent(params.query)}`);

    eventSource.addEventListener('layer_start', (e) => {
      const data = JSON.parse(e.data);
      setLayerProgress(prev => [...prev, { layer: data.layer, status: 'running' }]);
    });

    eventSource.addEventListener('layer_complete', (e) => {
      const data = JSON.parse(e.data);
      setLayerProgress(prev => prev.map(l =>
        l.layer === data.layer
          ? { ...l, status: 'complete', tokens: data.tokensUsed, latency: data.latencyMs }
          : l
      ));
    });

    eventSource.addEventListener('complete', (e) => {
      const data = JSON.parse(e.data);
      setFinalResult(data);
      setStatus('complete');
      eventSource.close();
    });

    eventSource.addEventListener('error', (e) => {
      setStatus('error');
      eventSource.close();
    });
  };

  return { status, layerProgress, finalResult, runTest };
}

// Live Test Modal Component
function LiveTestModal({ strategy, onClose }: Props) {
  const { runTest, layerProgress, finalResult } = useStrategyLiveTest();
  const [query, setQuery] = useState('');
  const [gameId, setGameId] = useState('');

  return (
    <Modal size="lg">
      <ModalHeader>
        <Icon name={strategy.icon} />
        <Title>Live Test: {strategy.name}</Title>
        <Badge>Admin Only</Badge>
      </ModalHeader>

      <ModalBody>
        {/* Input Section */}
        <Section title="Test Configuration">
          <Input
            label="Query"
            value={query}
            onChange={setQuery}
            placeholder="Quanti giocatori supporta?"
          />
          <Select
            label="Game Context"
            value={gameId}
            options={availableGames}
          />
        </Section>

        {/* Live Progress */}
        <Section title="Execution Progress">
          {layerProgress.map(layer => (
            <LayerProgressRow
              key={layer.layer}
              name={layer.layer}
              status={layer.status}
              tokens={layer.tokens}
              latency={layer.latency}
            />
          ))}
        </Section>

        {/* Final Result */}
        {finalResult && (
          <Section title="Result">
            <AnswerDisplay answer={finalResult.answer} />
            <SourcesDisplay sources={finalResult.sources} />
            <MetricsComparison
              actual={finalResult.metrics}
              estimated={strategy.metrics}
            />
          </Section>
        )}
      </ModalBody>

      <ModalFooter>
        <Button onClick={() => runTest({ query, gameId, strategyId: strategy.id })}>
          ⚡ Run Test
        </Button>
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      </ModalFooter>
    </Modal>
  );
}
```

---

## 📋 Feature 3: Visual Strategy Builder - Drag & Drop Canvas

### Canvas Engine: ReactFlow

**Why ReactFlow**:
- Production-ready graph/flow editor
- Built-in drag-drop, pan, zoom
- Custom node types
- Edge routing algorithms
- State management
- Export/Import JSON

### Block Types Definition

```typescript
// Core block interface
interface RagBlock {
  id: string; // unique instance ID
  type: RagBlockType;
  name: string;
  category: BlockCategory;
  icon: string;
  color: string;

  // Configuration
  parameters: Record<string, any>;
  defaultParams: Record<string, any>;

  // Connections
  inputs: ConnectionPort[];
  outputs: ConnectionPort[];
  canConnectTo: string[]; // valid next block types

  // Metrics
  estimatedTokens: number;
  estimatedLatencyMs: number;
  estimatedCost: number;
  accuracyImpact: number; // +/- percentage

  // Constraints
  requiredTier: UserTier;
  maxInstances: number; // max in single strategy
  requiredBlocks: string[]; // dependencies

  // Documentation
  description: string;
  useCases: string[];
  codeReferences: CodeReference[];
}

type RagBlockType =
  // Retrieval
  | 'vector_search'
  | 'keyword_search'
  | 'hybrid_search'
  | 'multi_hop'
  | 'graph_rag'
  | 'web_search'
  // Optimization
  | 'query_rewrite'
  | 'query_decompose'
  | 'query_expand'
  | 'hyde'
  | 'rag_fusion'
  // Ranking
  | 'reranker'
  | 'metadata_filter'
  | 'deduplication'
  | 'doc_repacking'
  // Validation
  | 'crag_evaluator'
  | 'citation_check'
  | 'hallucination_detect'
  | 'confidence_score'
  | 'self_rag'
  // Agents
  | 'agent_general'
  | 'agent_rules'
  | 'agent_citation'
  | 'agent_strategy'
  // Models
  | 'model_llama'
  | 'model_deepseek'
  | 'model_haiku'
  | 'model_sonnet'
  | 'model_opus'
  | 'model_gpt4o'
  // Control Flow
  | 'conditional_branch'
  | 'parallel_split'
  | 'parallel_merge'
  | 'loop'
  | 'early_stop';

type BlockCategory = 'retrieval' | 'optimization' | 'ranking' | 'validation' | 'agent' | 'model' | 'control';

interface ConnectionPort {
  id: string;
  type: 'input' | 'output';
  dataType: 'query' | 'documents' | 'answer' | 'score' | 'metadata';
  required: boolean;
}
```

### Drag & Drop Workflow

#### Step 1: Drag Block from Palette
```tsx
<PaletteBlock
  block={hybridSearchBlock}
  draggable
  onDragStart={(e) => {
    e.dataTransfer.setData('blockType', 'hybrid_search');
  }}
>
  <BlockIcon icon="⚖️" />
  <BlockName>Hybrid Search</BlockName>
  <BlockBadge>Retrieval</BlockBadge>
</PaletteBlock>
```

#### Step 2: Drop on Canvas
```tsx
<ReactFlow
  onDrop={(e) => {
    const blockType = e.dataTransfer.getData('blockType');
    const position = getDropPosition(e);

    addBlock({
      id: generateId(),
      type: blockType,
      position,
      data: { ...getDefaultParams(blockType) }
    });
  }}
  onDragOver={(e) => e.preventDefault()}
/>
```

#### Step 3: Connect Blocks
```tsx
<ReactFlow
  onConnect={(connection) => {
    // Validate connection
    if (!canConnect(connection.source, connection.target)) {
      showError('Invalid connection: Reranker cannot connect to Agent');
      return;
    }

    addEdge({
      id: generateEdgeId(),
      source: connection.source,
      target: connection.target,
      type: 'default', // or 'conditional'
      animated: true
    });
  }}
/>
```

#### Step 4: Configure Block
```tsx
// Click on block → shows config panel
<ConfigPanel block={selectedBlock}>
  <ParamEditor
    param="alpha"
    type="slider"
    min={0}
    max={1}
    step={0.1}
    value={block.parameters.alpha}
    onChange={(val) => updateBlockParam(block.id, 'alpha', val)}
  />

  <ParamEditor
    param="vectorTopK"
    type="number"
    min={1}
    max={50}
    value={block.parameters.vectorTopK}
  />

  <ParamEditor
    param="fusionMethod"
    type="select"
    options={['weighted', 'RRF']}
    value={block.parameters.fusionMethod}
  />
</ConfigPanel>
```

### Custom Node Types

```tsx
// apps/web/src/components/admin/builder/nodes/

// BaseNode.tsx - All nodes extend this
function RagBlockNode({ data, id, selected }: NodeProps) {
  const blockDef = getBlockDefinition(data.type);

  return (
    <div
      className={cn('rag-node', `category-${blockDef.category}`, selected && 'selected')}
      style={{ borderColor: blockDef.color }}
    >
      <Handle type="target" position="top" />

      <div className="node-header">
        <span className="node-icon">{blockDef.icon}</span>
        <span className="node-name">{blockDef.name}</span>
      </div>

      <div className="node-body">
        {/* Key parameters preview */}
        {Object.entries(data.parameters).slice(0, 2).map(([key, val]) => (
          <div key={key} className="param-preview">
            <span className="param-key">{key}:</span>
            <span className="param-val">{val}</span>
          </div>
        ))}
      </div>

      <div className="node-footer">
        <MetricBadge icon="⚡" value={`${blockDef.estimatedTokens}tok`} />
        <MetricBadge icon="⏱️" value={`${blockDef.estimatedLatencyMs}ms`} />
      </div>

      <Handle type="source" position="bottom" />
    </div>
  );
}

// ConditionalNode.tsx - Special node for If/Else
function ConditionalBlockNode({ data }: NodeProps) {
  return (
    <div className="rag-node conditional">
      <Handle type="target" position="top" />

      <div className="node-icon">🔀</div>
      <div className="condition-text">{data.condition}</div>

      <Handle type="source" position="bottom" id="true" style={{ left: '30%' }}>
        <span className="handle-label">✓ True</span>
      </Handle>
      <Handle type="source" position="bottom" id="false" style={{ left: '70%' }}>
        <span className="handle-label">✗ False</span>
      </Handle>
    </div>
  );
}

// ParallelNode.tsx - Split/Merge nodes
function ParallelSplitNode({ data }: NodeProps) {
  return (
    <div className="rag-node parallel-split">
      <Handle type="target" position="top" />
      <div className="node-icon">⚡</div>
      <div>Parallel Split</div>
      <div className="outputs">
        {data.branches.map((branch, i) => (
          <Handle
            key={i}
            type="source"
            position="bottom"
            id={`branch-${i}`}
            style={{ left: `${(i + 1) * 100 / (data.branches.length + 1)}%` }}
          />
        ))}
      </div>
    </div>
  );
}
```

### Validation Engine

```typescript
interface ValidationResult {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
  metrics: {
    totalTokens: number;
    totalLatency: number;
    totalCost: number;
    estimatedAccuracy: number;
  };
}

function validateStrategy(strategy: CustomStrategy): ValidationResult {
  const errors: ValidationError[] = [];
  const warnings: ValidationWarning[] = [];

  // Rule 1: Must have input and output
  if (!hasBlock(strategy, 'input')) {
    errors.push({ type: 'missing_block', message: 'Strategy must start with Input block' });
  }
  if (!hasBlock(strategy, 'output')) {
    errors.push({ type: 'missing_block', message: 'Strategy must end with Output block' });
  }

  // Rule 2: Model required before generation
  const genBlock = findBlock(strategy, 'generation');
  const modelBlock = findBlockBefore(strategy, genBlock, 'model');
  if (genBlock && !modelBlock) {
    errors.push({ type: 'missing_dependency', message: 'Generation requires Model selection before it' });
  }

  // Rule 3: Token budget
  const totalTokens = calculateTotalTokens(strategy);
  if (totalTokens > 30000) {
    errors.push({ type: 'token_limit', message: `Total tokens (${totalTokens}) exceeds limit (30,000)` });
  }
  if (totalTokens > 20000) {
    warnings.push({ type: 'token_warning', message: `High token usage (${totalTokens}). Consider optimization.` });
  }

  // Rule 4: Latency SLA
  const totalLatency = calculateTotalLatency(strategy);
  if (totalLatency > 30000) {
    errors.push({ type: 'latency_limit', message: `Estimated latency (${totalLatency}ms) exceeds 30s UX limit` });
  }

  // Rule 5: Cost cap
  const totalCost = calculateTotalCost(strategy);
  if (totalCost > 0.50) {
    warnings.push({ type: 'cost_warning', message: `High cost per query ($${totalCost}). Ensure ROI justifies.` });
  }

  // Rule 6: Orphan blocks
  const orphans = findOrphanBlocks(strategy);
  if (orphans.length > 0) {
    errors.push({ type: 'disconnected_blocks', message: `${orphans.length} blocks are not connected to the flow` });
  }

  // Rule 7: Circular dependencies
  if (hasCircularDependency(strategy)) {
    errors.push({ type: 'circular_dependency', message: 'Strategy contains infinite loop (no exit condition)' });
  }

  return {
    isValid: errors.length === 0,
    errors,
    warnings,
    metrics: {
      totalTokens,
      totalLatency,
      totalCost,
      estimatedAccuracy: calculateAccuracy(strategy)
    }
  };
}
```

### Block Connection Rules

```typescript
const CONNECTION_RULES: Record<RagBlockType, string[]> = {
  // Input can connect to anything that starts a flow
  'input': ['query_rewrite', 'query_decompose', 'vector_search', 'hybrid_search', 'agent_*'],

  // Retrieval blocks connect to ranking or agents
  'vector_search': ['reranker', 'metadata_filter', 'agent_*', 'model_*'],
  'hybrid_search': ['reranker', 'crag_evaluator', 'agent_*', 'model_*'],
  'multi_hop': ['reranker', 'crag_evaluator', 'model_*'],

  // Ranking blocks connect to validation or generation
  'reranker': ['crag_evaluator', 'deduplication', 'model_*', 'generation'],

  // Validation blocks can trigger corrections or proceed
  'crag_evaluator': ['web_search', 'generation', 'conditional_branch'],
  'conditional_branch': ['*'], // can connect to anything

  // Agents connect to models
  'agent_*': ['model_*'],

  // Models connect to generation
  'model_*': ['generation', 'multi_agent_*'],

  // Generation connects to validation or output
  'generation': ['citation_check', 'hallucination_detect', 'confidence_score', 'output'],

  // Final validation connects to output
  'citation_check': ['output', 'conditional_branch'],
  'hallucination_detect': ['output', 'conditional_branch'],

  // Output is terminal
  'output': []
};
```

### Strategy Serialization

```typescript
interface CustomStrategy {
  id: string;
  name: string;
  description: string;
  createdBy: string; // admin user ID
  createdAt: string;

  blocks: StrategyBlock[];
  edges: StrategyEdge[];

  metadata: {
    estimatedTokens: number;
    estimatedCost: number;
    estimatedLatency: number;
    targetAccuracy: number;
  };

  validation: ValidationResult;
  isPublished: boolean; // available to users
  allowedTiers: UserTier[]; // who can use this strategy
}

interface StrategyBlock {
  instanceId: string; // unique in this strategy
  blockType: RagBlockType;
  position: { x: number; y: number };
  parameters: Record<string, any>;
}

interface StrategyEdge {
  id: string;
  source: string; // block instance ID
  target: string;
  sourceHandle?: string; // for conditional (true/false)
  targetHandle?: string;
  condition?: string; // for conditional edges
}

// Example serialization
const customStrategy: CustomStrategy = {
  id: 'custom-balanced-plus',
  name: 'Balanced Plus (Custom)',
  description: 'BALANCED with added reranking and citation check',
  createdBy: 'admin-user-123',
  createdAt: '2026-02-02T10:30:00Z',

  blocks: [
    { instanceId: 'input-1', blockType: 'input', position: { x: 400, y: 50 }, parameters: {} },
    { instanceId: 'agent-1', blockType: 'agent_rules', position: { x: 400, y: 150 }, parameters: {} },
    { instanceId: 'model-1', blockType: 'model_deepseek', position: { x: 400, y: 250 }, parameters: {} },
    { instanceId: 'search-1', blockType: 'hybrid_search', position: { x: 400, y: 350 }, parameters: { alpha: 0.5, topK: 10 } },
    { instanceId: 'rerank-1', blockType: 'reranker', position: { x: 400, y: 450 }, parameters: { model: 'mxbai-rerank', topN: 5 } },
    { instanceId: 'crag-1', blockType: 'crag_evaluator', position: { x: 400, y: 550 }, parameters: { threshold: 0.8 } },
    { instanceId: 'gen-1', blockType: 'generation', position: { x: 400, y: 650 }, parameters: {} },
    { instanceId: 'cite-1', blockType: 'citation_check', position: { x: 400, y: 750 }, parameters: {} },
    { instanceId: 'output-1', blockType: 'output', position: { x: 400, y: 850 }, parameters: {} }
  ],

  edges: [
    { id: 'e1', source: 'input-1', target: 'agent-1' },
    { id: 'e2', source: 'agent-1', target: 'model-1' },
    { id: 'e3', source: 'model-1', target: 'search-1' },
    { id: 'e4', source: 'search-1', target: 'rerank-1' },
    { id: 'e5', source: 'rerank-1', target: 'crag-1' },
    { id: 'e6', source: 'crag-1', target: 'gen-1' },
    { id: 'e7', source: 'gen-1', target: 'cite-1' },
    { id: 'e8', source: 'cite-1', target: 'output-1' }
  ],

  metadata: {
    estimatedTokens: 3800,
    estimatedCost: 0.018,
    estimatedLatency: 1800,
    targetAccuracy: 90
  },

  validation: { isValid: true, errors: [], warnings: [] },
  isPublished: false,
  allowedTiers: ['Editor', 'Admin']
};
```

### Save & Export

```typescript
// Save to database
async function saveCustomStrategy(strategy: CustomStrategy) {
  const response = await fetch('/api/v1/admin/rag/custom-strategies', {
    method: 'POST',
    body: JSON.stringify(strategy)
  });

  return response.json();
}

// Export as JSON
function exportStrategy(strategy: CustomStrategy) {
  const json = JSON.stringify(strategy, null, 2);
  downloadFile(`${strategy.name}.strategy.json`, json);
}

// Import from JSON
function importStrategy(file: File) {
  const reader = new FileReader();
  reader.onload = (e) => {
    const strategy = JSON.parse(e.target?.result as string);
    const validation = validateStrategy(strategy);

    if (!validation.isValid) {
      showErrors(validation.errors);
      return;
    }

    loadStrategyIntoCanvas(strategy);
  };
  reader.readAsText(file);
}
```

---

## 🎯 Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
**Epic Issue**: TBD (depends on #3434 completion)

**Tasks**:
1. Create block type definitions (TypeScript interfaces)
2. Implement 23 block metadata records
3. Setup ReactFlow canvas infrastructure
4. Create basic palette component
5. Implement drag-drop mechanics

**Deliverables**:
- Block type system
- Basic canvas with palette
- Can drag blocks to canvas

### Phase 2: Core Blocks (Week 3-4)

**Tasks**:
1. Implement Tier 1 blocks (7 essential)
   - Vector, Keyword, Hybrid search
   - Reranking, CRAG, Confidence, Citation
2. Create custom node components for each block
3. Implement connection validation logic
4. Add parameter configuration panel

**Deliverables**:
- 7 working blocks
- Connection validation
- Parameter editing

### Phase 3: Explanations & Examples (Week 5)

**Tasks**:
1. Create explanation modal component
2. Write explanation content for each block (23 blocks)
3. Create static example dataset (5 examples × 6 strategies)
4. Implement example viewer modal
5. Add code reference links

**Deliverables**:
- Click block → explanation modal
- View examples button → examples modal
- 30 curated examples total

### Phase 4: Live Testing (Week 6)

**Tasks**:
1. Create backend API endpoint for strategy testing
2. Implement SSE streaming for live progress
3. Create live test modal component
4. Add metrics comparison (actual vs estimated)
5. Add test history persistence

**Deliverables**:
- Admin can test custom strategies live
- Real-time progress tracking
- Metrics validation

### Phase 5: Advanced Features (Week 7-8)

**Tasks**:
1. Implement Tier 2 blocks (6 advanced)
2. Add conditional branching nodes
3. Add parallel split/merge nodes
4. Implement strategy validation engine
5. Add save/load/export functionality

**Deliverables**:
- Full block palette (23 blocks)
- Control flow support
- Save custom strategies to DB

### Phase 6: Polish & Production (Week 9-10)

**Tasks**:
1. Add strategy templates (Quick Start)
2. Implement undo/redo
3. Add keyboard shortcuts
4. Create admin documentation
5. E2E testing with Playwright

**Deliverables**:
- Production-ready builder
- User documentation
- Test coverage 85%+

---

## ✅ Definition of Done

### For Epic Completion

- [ ] All 23 blocks defined and documented
- [ ] Drag & drop canvas functional
- [ ] Block explanation modals working
- [ ] Static examples for all 6 strategies (30 total)
- [ ] Live test capability for Admin
- [ ] Custom strategies can be saved/loaded
- [ ] Validation engine prevents invalid configurations
- [ ] Strategy export/import (JSON)
- [ ] Admin documentation complete
- [ ] E2E tests pass

### Quality Gates

- **Performance**: Canvas smooth at 60fps with 50+ blocks
- **UX**: <3 clicks to create simple strategy
- **Validation**: 100% catch invalid configurations before save
- **Testing**: 85%+ coverage (Vitest + Playwright)
- **Accessibility**: Keyboard navigation, screen reader support

---

## 🔗 Dependencies

**Prerequisite**:
- Epic #3434: Tier-Strategy-Model architecture ✅ (docs complete)
- Epic #3413: Plugin-Based RAG architecture ⏸️ (blocked)

**Blocks**:
- Epic #3434 (strategy selection refactor)
- Epic #3413 (plugin architecture enables dynamic blocks)

**Recommendation**: Start visual builder after #3434 backend complete, parallel to #3413

---

## 📦 Technical Stack

### Frontend
- **ReactFlow**: Canvas and graph engine
- **React DnD**: Drag and drop
- **Zustand**: State management
- **Monaco Editor**: JSON/YAML editing (optional)
- **Framer Motion**: Animations
- **Tailwind + shadcn/ui**: UI components

### Backend
- **Strategy Definition**: JSON schema in DB (PostgreSQL JSONB)
- **Execution Engine**: Dynamic pipeline builder from JSON
- **Validation**: C# validation service
- **Testing API**: SSE endpoint for live execution

### Storage
```sql
CREATE TABLE CustomRagStrategies (
  Id UUID PRIMARY KEY,
  Name VARCHAR(100) NOT NULL,
  Description TEXT,
  CreatedBy UUID REFERENCES Users(Id),
  CreatedAt TIMESTAMP DEFAULT NOW(),
  UpdatedAt TIMESTAMP DEFAULT NOW(),

  -- Strategy definition (JSONB for flexibility)
  Definition JSONB NOT NULL,

  -- Metadata
  EstimatedTokens INT,
  EstimatedCost DECIMAL(10,4),
  EstimatedLatency INT,
  TargetAccuracy INT,

  -- Access control
  IsPublished BOOLEAN DEFAULT FALSE,
  AllowedTiers TEXT[], -- ['Editor', 'Admin']

  -- Validation
  ValidationErrors JSONB,
  LastValidated TIMESTAMP
);
```

---

**Status**: 🎨 Specification Complete
**Next**: Design visual mockups and create Epic issue
