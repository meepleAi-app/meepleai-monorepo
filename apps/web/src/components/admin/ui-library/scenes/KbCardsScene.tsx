'use client';

import { VectorCollectionCard } from '@/components/admin/knowledge-base/vector-collection-card';

// UploadZone requires game search hook (useApiClient) and queue-api imports.
// ProcessingQueue requires similar API context.
// We render VectorCollectionCard directly and show placeholders for the rest.

function PlaceholderCard({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-dashed border-border/50 bg-muted/20 p-5">
      <p className="font-quicksand text-sm font-semibold text-foreground">{title}</p>
      <p className="font-nunito text-xs text-muted-foreground">{description}</p>
      <span className="inline-flex w-fit items-center rounded-full border border-border/50 px-2.5 py-0.5 font-nunito text-[10px] text-muted-foreground">
        requires provider context
      </span>
    </div>
  );
}

const MOCK_COLLECTIONS = [
  {
    name: 'game_rules_en',
    vectorCount: 12_480,
    dimensions: 1536,
    storage: '48.2 MB',
    health: 98,
  },
  {
    name: 'game_rules_it',
    vectorCount: 8_320,
    dimensions: 1536,
    storage: '31.6 MB',
    health: 95,
  },
  {
    name: 'faq_chunks',
    vectorCount: 2_104,
    dimensions: 1536,
    storage: '8.1 MB',
    health: 72,
  },
];

export default function KbCardsScene() {
  return (
    <div className="space-y-8">
      {/* Vector collection cards */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Vector Collection Cards
        </h3>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {MOCK_COLLECTIONS.map(col => (
            <VectorCollectionCard
              key={col.name}
              name={col.name}
              vectorCount={col.vectorCount}
              dimensions={col.dimensions}
              storage={col.storage}
              health={col.health}
              onReindex={() => undefined}
              onDelete={() => undefined}
            />
          ))}
        </div>
      </div>

      {/* Placeholder for context-dependent components */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Additional Components
        </h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <PlaceholderCard
            title="UploadZone"
            description="Drag-and-drop PDF upload zone with game search, progress tracking, and indexing queue."
          />
          <PlaceholderCard
            title="ProcessingQueue"
            description="Real-time queue showing pending, active, and completed PDF processing jobs."
          />
        </div>
      </div>
    </div>
  );
}
