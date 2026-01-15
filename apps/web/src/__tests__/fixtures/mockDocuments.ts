/**
 * Mock Documents Fixtures - Issue #2415
 *
 * Test data for DocumentPicker component stories and tests.
 * Provides fixtures for empty, single, small, medium, and large datasets.
 */

import type { DocumentMetadata } from '@/components/knowledge-base/DocumentPicker';

// ========== Helper Functions ==========

function generateDocument(
  index: number,
  type: 'Rulebook' | 'Errata' | 'Homerule' = 'Rulebook',
  gameName?: string
): DocumentMetadata {
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
    id: `doc-${String(index).padStart(3, '0')}`,
    title: `${selectedGame} ${selectedType} v${Math.floor(index / 10) + 1}.${index % 10}`,
    documentType: selectedType,
    version: `${Math.floor(index / 10) + 1}.${index % 10}`,
    pageCount: 10 + ((index * 3) % 50),
    fileSize: `${(2 + (index % 10) * 0.5).toFixed(1)} MB`,
    uploadedAt: new Date(2024, 0, 1 + index).toISOString(),
    uploadedBy: index % 3 === 0 ? 'Admin User' : index % 3 === 1 ? 'Editor User' : 'John Doe',
    tags: selectedTags,
    gameName: selectedGame,
    isActive: index % 5 !== 4, // 80% active, 20% inactive
  };
}

// ========== Mock Data Sets ==========

/**
 * Empty dataset (0 documents)
 */
export const mockDocumentsEmpty: DocumentMetadata[] = [];

/**
 * Single document (1 document)
 */
export const mockDocumentsSingle: DocumentMetadata[] = [
  {
    id: 'doc-single',
    title: 'Catan Rulebook v1.0',
    documentType: 'Rulebook',
    version: '1.0',
    pageCount: 24,
    fileSize: '3.2 MB',
    uploadedAt: '2024-01-15T10:30:00Z',
    uploadedBy: 'Admin User',
    tags: ['strategy', 'resource-management', 'trading'],
    gameName: 'Catan',
    isActive: true,
  },
];

/**
 * Small dataset (5 documents)
 */
export const mockDocumentsSmall: DocumentMetadata[] = [
  {
    id: 'doc-001',
    title: 'Catan Rulebook v1.0',
    documentType: 'Rulebook',
    version: '1.0',
    pageCount: 24,
    fileSize: '3.2 MB',
    uploadedAt: '2024-01-15T10:30:00Z',
    uploadedBy: 'Admin User',
    tags: ['strategy', 'resource-management'],
    gameName: 'Catan',
    isActive: true,
  },
  {
    id: 'doc-002',
    title: 'Catan Errata v1.1',
    documentType: 'Errata',
    version: '1.1',
    pageCount: 4,
    fileSize: '0.8 MB',
    uploadedAt: '2024-02-01T14:20:00Z',
    uploadedBy: 'Editor User',
    tags: ['errata', 'corrections'],
    gameName: 'Catan',
    isActive: true,
  },
  {
    id: 'doc-003',
    title: 'Ticket to Ride Rulebook v2.0',
    documentType: 'Rulebook',
    version: '2.0',
    pageCount: 18,
    fileSize: '2.5 MB',
    uploadedAt: '2024-01-20T09:15:00Z',
    uploadedBy: 'John Doe',
    tags: ['family-friendly', 'routes'],
    gameName: 'Ticket to Ride',
    isActive: true,
  },
  {
    id: 'doc-004',
    title: 'Pandemic Homerule v1.0',
    documentType: 'Homerule',
    version: '1.0',
    pageCount: 8,
    fileSize: '1.2 MB',
    uploadedAt: '2024-03-05T16:45:00Z',
    uploadedBy: 'Admin User',
    tags: ['cooperative', 'custom-rules'],
    gameName: 'Pandemic',
    isActive: false,
  },
  {
    id: 'doc-005',
    title: 'Azul Rulebook v1.2',
    documentType: 'Rulebook',
    version: '1.2',
    pageCount: 12,
    fileSize: '1.8 MB',
    uploadedAt: '2024-02-10T11:00:00Z',
    uploadedBy: 'Editor User',
    tags: ['abstract', 'pattern-building'],
    gameName: 'Azul',
    isActive: true,
  },
];

/**
 * Medium dataset (20 documents - 1 page at default pageSize)
 */
export const mockDocumentsMedium: DocumentMetadata[] = Array.from({ length: 20 }, (_, i) =>
  generateDocument(i)
);

/**
 * Large dataset (105 documents - multi-page)
 */
export const mockDocumentsLarge: DocumentMetadata[] = Array.from({ length: 105 }, (_, i) =>
  generateDocument(i)
);

/**
 * Mixed types dataset for filtering tests
 */
export const mockDocumentsMixedTypes: DocumentMetadata[] = [
  ...Array.from({ length: 10 }, (_, i) => generateDocument(i * 3, 'Rulebook')),
  ...Array.from({ length: 5 }, (_, i) => generateDocument(i * 3 + 1, 'Errata')),
  ...Array.from({ length: 5 }, (_, i) => generateDocument(i * 3 + 2, 'Homerule')),
];

/**
 * Single game dataset (all documents for one game)
 */
export const mockDocumentsSingleGame: DocumentMetadata[] = [
  generateDocument(0, 'Rulebook', 'Catan'),
  generateDocument(1, 'Rulebook', 'Catan'),
  generateDocument(2, 'Errata', 'Catan'),
  generateDocument(3, 'Errata', 'Catan'),
  generateDocument(4, 'Homerule', 'Catan'),
  generateDocument(5, 'Homerule', 'Catan'),
];

/**
 * Documents with varied tag counts (for tag display tests)
 */
export const mockDocumentsVariedTags: DocumentMetadata[] = [
  {
    id: 'doc-no-tags',
    title: 'Document Without Tags',
    documentType: 'Rulebook',
    version: '1.0',
    pageCount: 20,
    fileSize: '2.5 MB',
    uploadedAt: '2024-01-01T10:00:00Z',
    uploadedBy: 'Admin User',
    tags: [],
    gameName: 'Test Game',
    isActive: true,
  },
  {
    id: 'doc-one-tag',
    title: 'Document With One Tag',
    documentType: 'Rulebook',
    version: '1.0',
    pageCount: 20,
    fileSize: '2.5 MB',
    uploadedAt: '2024-01-02T10:00:00Z',
    uploadedBy: 'Admin User',
    tags: ['strategy'],
    gameName: 'Test Game',
    isActive: true,
  },
  {
    id: 'doc-many-tags',
    title: 'Document With Many Tags',
    documentType: 'Rulebook',
    version: '1.0',
    pageCount: 20,
    fileSize: '2.5 MB',
    uploadedAt: '2024-01-03T10:00:00Z',
    uploadedBy: 'Admin User',
    tags: ['strategy', 'cooperative', 'deck-building', 'engine-building', 'resource-management'],
    gameName: 'Test Game',
    isActive: true,
  },
];

/**
 * Get documents by type (for testing type filtering)
 */
export function getDocumentsByType(
  documents: DocumentMetadata[],
  type: 'Rulebook' | 'Errata' | 'Homerule'
): DocumentMetadata[] {
  return documents.filter(doc => doc.documentType === type);
}

/**
 * Get active/inactive documents (for testing status filtering)
 */
export function getDocumentsByStatus(
  documents: DocumentMetadata[],
  isActive: boolean
): DocumentMetadata[] {
  return documents.filter(doc => doc.isActive === isActive);
}

/**
 * Search documents (for testing search functionality)
 */
export function searchDocuments(documents: DocumentMetadata[], query: string): DocumentMetadata[] {
  const lowerQuery = query.toLowerCase();
  return documents.filter(
    doc =>
      doc.title.toLowerCase().includes(lowerQuery) ||
      doc.gameName?.toLowerCase().includes(lowerQuery) ||
      doc.tags.some(tag => tag.toLowerCase().includes(lowerQuery))
  );
}
