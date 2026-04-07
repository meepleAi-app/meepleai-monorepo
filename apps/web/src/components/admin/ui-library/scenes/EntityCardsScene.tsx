'use client';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

interface MockCard {
  entity: MeepleEntityType;
  title: string;
  subtitle: string;
  rating?: number;
  ratingMax?: 5 | 10;
}

const MOCK_CARDS: MockCard[] = [
  {
    entity: 'game',
    title: 'Wingspan',
    subtitle: 'Stonemaier Games',
    rating: 8.1,
    ratingMax: 10,
  },
  {
    entity: 'player',
    title: 'Marco Rossi',
    subtitle: 'Active 2 days ago',
  },
  {
    entity: 'kb',
    title: 'Rulebook Collection',
    subtitle: '14 documents',
  },
  {
    entity: 'agent',
    title: 'Wingspan Assistant',
    subtitle: 'RAG + Rules QA',
  },
];

export default function EntityCardsScene() {
  return (
    <div className="space-y-6">
      <div>
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Entity Cards — Grid Variant
        </h3>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          MeepleCard in four entity types: game, player, kb (knowledge base), and agent.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {MOCK_CARDS.map(card => (
          <MeepleCard
            key={card.title}
            entity={card.entity}
            variant="grid"
            title={card.title}
            subtitle={card.subtitle}
            rating={card.rating}
            ratingMax={card.ratingMax}
          />
        ))}
      </div>

      <div>
        <h3 className="mb-3 font-quicksand text-base font-semibold text-foreground">
          List Variant
        </h3>
        <div className="space-y-2">
          {MOCK_CARDS.slice(0, 3).map(card => (
            <MeepleCard
              key={`list-${card.title}`}
              entity={card.entity}
              variant="list"
              title={card.title}
              subtitle={card.subtitle}
            />
          ))}
        </div>
      </div>

      <div>
        <h3 className="mb-3 font-quicksand text-base font-semibold text-foreground">
          Compact Variant
        </h3>
        <div className="space-y-1.5">
          {MOCK_CARDS.map(card => (
            <MeepleCard
              key={`compact-${card.title}`}
              entity={card.entity}
              variant="compact"
              title={card.title}
              subtitle={card.subtitle}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
