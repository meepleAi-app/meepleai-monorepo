/**
 * Mock Selected Documents Fixtures - Issue #2416
 *
 * Test data for SelectedDocuments component stories and tests.
 * Provides fixtures for various selection scenarios.
 */

import type { SelectedDocument } from '@/components/knowledge-base/SelectedDocuments';

// ========== Helper Functions ==========

function generateSelectedDocument(
  index: number,
  type: 'Rulebook' | 'Errata' | 'Homerule' = 'Rulebook',
  gameName?: string
): SelectedDocument {
  const types: Array<'Rulebook' | 'Errata' | 'Homerule'> = ['Rulebook', 'Errata', 'Homerule'];
  const selectedType = type || types[index % 3];
  const gameNames = gameName
    ? [gameName]
    : [
        'Catan',
        'Ticket to Ride',
        'Pandemic',
        'Azul',
        'Wingspan',
        'Splendor',
        'Codenames',
        'Dominion',
        '7 Wonders',
        'Terraforming Mars',
      ];
  const selectedGame = gameNames[index % gameNames.length];

  const tags = [
    ['strategy', 'resource-management'],
    ['family-friendly', 'set-collection'],
    ['cooperative', 'pandemic'],
    ['abstract', 'pattern-building'],
    ['engine-building', 'birds'],
    ['gem-collecting', 'card-drafting'],
    ['party', 'word-game'],
    ['deck-building', 'medieval'],
    ['civilization', 'card-drafting'],
    ['space', 'engine-building'],
  ];

  const selectedTags = tags[index % tags.length];

  return {
    id: `sel-doc-${String(index).padStart(3, '0')}`,
    title: `${selectedGame} ${selectedType} v${Math.floor(index / 10) + 1}.${index % 10}`,
    documentType: selectedType,
    version: `${Math.floor(index / 10) + 1}.${index % 10}`,
    tags: selectedTags,
    gameName: selectedGame,
  };
}

// ========== Mock Data Sets ==========

/**
 * Empty selection (0 documents)
 */
export const mockSelectedEmpty: SelectedDocument[] = [];

/**
 * Single document selected
 */
export const mockSelectedSingle: SelectedDocument[] = [
  {
    id: 'sel-doc-single',
    title: 'Catan Rulebook v1.0',
    documentType: 'Rulebook',
    version: '1.0',
    tags: ['strategy', 'resource-management', 'trading'],
    gameName: 'Catan',
  },
];

/**
 * Small selection (5 documents)
 */
export const mockSelectedSmall: SelectedDocument[] = [
  {
    id: 'sel-doc-001',
    title: 'Catan Rulebook v1.0',
    documentType: 'Rulebook',
    version: '1.0',
    tags: ['strategy', 'resource-management'],
    gameName: 'Catan',
  },
  {
    id: 'sel-doc-002',
    title: 'Catan Errata v1.1',
    documentType: 'Errata',
    version: '1.1',
    tags: ['errata', 'corrections'],
    gameName: 'Catan',
  },
  {
    id: 'sel-doc-003',
    title: 'Ticket to Ride Rulebook v2.0',
    documentType: 'Rulebook',
    version: '2.0',
    tags: ['family-friendly', 'routes'],
    gameName: 'Ticket to Ride',
  },
  {
    id: 'sel-doc-004',
    title: 'Pandemic Homerule v1.0',
    documentType: 'Homerule',
    version: '1.0',
    tags: ['cooperative', 'custom-rules'],
    gameName: 'Pandemic',
  },
  {
    id: 'sel-doc-005',
    title: 'Azul Rulebook v1.2',
    documentType: 'Rulebook',
    version: '1.2',
    tags: ['abstract', 'pattern-building'],
    gameName: 'Azul',
  },
];

/**
 * Medium selection (15 documents)
 */
export const mockSelectedMedium: SelectedDocument[] = Array.from({ length: 15 }, (_, i) =>
  generateSelectedDocument(i)
);

/**
 * Large selection near limit (45 documents)
 */
export const mockSelectedNearLimit: SelectedDocument[] = Array.from({ length: 45 }, (_, i) =>
  generateSelectedDocument(i)
);

/**
 * Selection at limit (50 documents - max)
 */
export const mockSelectedAtLimit: SelectedDocument[] = Array.from({ length: 50 }, (_, i) =>
  generateSelectedDocument(i)
);

/**
 * Mixed types selection for statistics display
 */
export const mockSelectedMixedTypes: SelectedDocument[] = [
  ...Array.from({ length: 6 }, (_, i) => generateSelectedDocument(i * 3, 'Rulebook')),
  ...Array.from({ length: 3 }, (_, i) => generateSelectedDocument(i * 3 + 1, 'Errata')),
  ...Array.from({ length: 3 }, (_, i) => generateSelectedDocument(i * 3 + 2, 'Homerule')),
];

/**
 * Single game selection (all from same game)
 */
export const mockSelectedSingleGame: SelectedDocument[] = [
  generateSelectedDocument(0, 'Rulebook', 'Catan'),
  generateSelectedDocument(1, 'Rulebook', 'Catan'),
  generateSelectedDocument(2, 'Errata', 'Catan'),
  generateSelectedDocument(3, 'Errata', 'Catan'),
  generateSelectedDocument(4, 'Homerule', 'Catan'),
  generateSelectedDocument(5, 'Homerule', 'Catan'),
];

/**
 * Selection with varied tag counts
 */
export const mockSelectedVariedTags: SelectedDocument[] = [
  {
    id: 'sel-doc-no-tags',
    title: 'Document Without Tags',
    documentType: 'Rulebook',
    version: '1.0',
    tags: [],
    gameName: 'Test Game',
  },
  {
    id: 'sel-doc-one-tag',
    title: 'Document With One Tag',
    documentType: 'Rulebook',
    version: '1.0',
    tags: ['strategy'],
    gameName: 'Test Game',
  },
  {
    id: 'sel-doc-many-tags',
    title: 'Document With Many Tags',
    documentType: 'Rulebook',
    version: '1.0',
    tags: ['strategy', 'cooperative', 'deck-building', 'engine-building', 'resource-management'],
    gameName: 'Test Game',
  },
];
