'use client';

// SandboxChat, PipelineTraceTree, and RetrievedChunkCard all require
// SandboxProviders (PipelineContext, SandboxSessionContext, SourceContext).
// We show realistic descriptive placeholder cards representing their functionality.

interface DebugSceneCard {
  title: string;
  description: string;
  features: string[];
}

const SCENE_CARDS: DebugSceneCard[] = [
  {
    title: 'SandboxChat',
    description:
      'Interactive chat interface connected to the RAG pipeline sandbox. Streams responses with citations.',
    features: [
      'SSE streaming with abort support',
      'Inline source citation links',
      'Message history with pipeline trace per message',
      'Auto-scroll with "jump to bottom" button',
    ],
  },
  {
    title: 'PipelineTraceTree',
    description:
      'Hierarchical view of RAG pipeline execution steps with latency breakdown per node.',
    features: [
      'Collapsible tree of pipeline stages',
      'Color-coded latency indicators',
      'Click-to-expand step details',
      'Integration with PipelineDiagram node selection',
    ],
  },
  {
    title: 'RetrievedChunkCard',
    description:
      'Displays a single retrieved document chunk with source, score, and highlighted excerpt.',
    features: [
      'Similarity score badge (0–1)',
      'Source document name and page number',
      'Highlighted matching text excerpt',
      'Expand/collapse full chunk text',
    ],
  },
];

export default function SandboxDebugScene() {
  return (
    <div className="space-y-6">
      <div>
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Sandbox & Debug Components
        </h3>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          These components operate inside SandboxProviders which manages shared pipeline state.
          Rendering them in isolation requires their full provider tree.
        </p>
      </div>

      <div className="grid gap-4">
        {SCENE_CARDS.map(card => (
          <div key={card.title} className="rounded-xl border border-border/60 bg-background p-5">
            <div className="mb-3 flex items-start justify-between gap-4">
              <div>
                <h4 className="font-quicksand text-sm font-semibold text-foreground">
                  {card.title}
                </h4>
                <p className="mt-1 font-nunito text-xs text-muted-foreground">{card.description}</p>
              </div>
              <span className="shrink-0 rounded-full border border-border/50 px-2.5 py-0.5 font-nunito text-[10px] text-muted-foreground">
                requires SandboxProviders
              </span>
            </div>

            <ul className="space-y-1">
              {card.features.map(feature => (
                <li key={feature} className="flex items-start gap-2">
                  <span className="mt-0.5 text-amber-500">→</span>
                  <span className="font-nunito text-xs text-muted-foreground">{feature}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </div>
  );
}
