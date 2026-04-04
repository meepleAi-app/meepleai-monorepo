'use client';

import { VectorGameCard } from '@/components/admin/knowledge-base/vector-game-card';

// UploadZone requires game search hook (useApiClient) and queue-api imports.
// ProcessingQueue requires similar API context.
// We render VectorGameCard directly and show placeholders for the rest.

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

const MOCK_GAMES = [
  {
    gameId: 'game-001',
    gameName: 'Catan',
    vectorCount: 12_480,
    completedCount: 12_480,
    failedCount: 0,
    healthPercent: 98,
  },
  {
    gameId: 'game-002',
    gameName: 'Pandemic',
    vectorCount: 8_320,
    completedCount: 8_100,
    failedCount: 220,
    healthPercent: 75,
  },
  {
    gameId: 'game-003',
    gameName: 'Ticket to Ride',
    vectorCount: 2_104,
    completedCount: 1_200,
    failedCount: 904,
    healthPercent: 57,
  },
];

export default function KbCardsScene() {
  return (
    <div className="space-y-8">
      {/* Vector game cards */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Vector Game Cards
        </h3>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {MOCK_GAMES.map(game => (
            <VectorGameCard key={game.gameId} game={game} />
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
