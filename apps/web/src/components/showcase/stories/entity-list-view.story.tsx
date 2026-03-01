/**
 * EntityListView Story
 */

import { EntityListView } from '@/components/ui/data-display/entity-list-view';

import type { ShowcaseStory } from '../types';

interface SampleGame {
  id: string;
  title: string;
  publisher: string;
  rating: number;
  imageUrl: string;
}

const SAMPLE_GAMES: SampleGame[] = [
  { id: '1', title: 'Catan', publisher: 'Mayfair Games', rating: 7.2, imageUrl: '' },
  { id: '2', title: 'Pandemic', publisher: 'Z-Man Games', rating: 7.6, imageUrl: '' },
  { id: '3', title: 'Ticket to Ride', publisher: 'Days of Wonder', rating: 7.4, imageUrl: '' },
  { id: '4', title: 'Carcassonne', publisher: 'Z-Man Games', rating: 7.5, imageUrl: '' },
  { id: '5', title: 'Splendor', publisher: 'Space Cowboys', rating: 7.4, imageUrl: '' },
  { id: '6', title: 'Codenames', publisher: 'Czech Games', rating: 7.8, imageUrl: '' },
];

type EntityListViewShowcaseProps = {
  defaultViewMode: string;
  loading: boolean;
  searchable: boolean;
  gridColumns: number;
};

export const entityListViewStory: ShowcaseStory<EntityListViewShowcaseProps> = {
  id: 'entity-list-view',
  title: 'EntityListView',
  category: 'Data Display',
  description: 'Generic multi-view list with grid, list, carousel, and table modes.',

  component: function EntityListViewStory({ defaultViewMode, loading, searchable, gridColumns }: EntityListViewShowcaseProps) {
    return (
      <div className="w-[800px]">
        <EntityListView
          items={loading ? [] : SAMPLE_GAMES}
          entity="game"
          persistenceKey="showcase-entity-list"
          renderItem={(game) => ({
            id: game.id,
            title: game.title,
            subtitle: game.publisher,
            rating: game.rating,
            ratingMax: 10,
          })}
          defaultViewMode={defaultViewMode as 'grid' | 'list' | 'carousel'}
          loading={loading}
          searchable={searchable}
          searchFields={['title', 'publisher']}
          title="Sample Games"
          gridColumns={{ sm: 2, md: Number(gridColumns) as 2 | 3 | 4, lg: Math.min(Number(gridColumns) + 1, 4) as 3 | 4 }}
        />
      </div>
    );
  },

  defaultProps: {
    defaultViewMode: 'grid',
    loading: false,
    searchable: true,
    gridColumns: 3,
  },

  controls: {
    defaultViewMode: {
      type: 'select',
      label: 'defaultViewMode',
      options: ['grid', 'list', 'carousel'],
      default: 'grid',
    },
    gridColumns: {
      type: 'select',
      label: 'gridColumns',
      options: ['2', '3', '4'],
      default: '3',
    },
    searchable: { type: 'boolean', label: 'searchable', default: true },
    loading: { type: 'boolean', label: 'loading', default: false },
  },

  presets: {
    gamesGrid: { label: 'Games Grid', props: { defaultViewMode: 'grid', searchable: true } },
    playersList: { label: 'List View', props: { defaultViewMode: 'list' } },
    empty: { label: 'Empty State', props: { loading: false } },
    loading: { label: 'Loading', props: { loading: true } },
  },
};
