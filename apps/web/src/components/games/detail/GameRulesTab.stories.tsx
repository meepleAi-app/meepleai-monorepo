import type { Game, PdfDocumentDto } from '@/lib/api';

import { GameRulesTab } from './GameRulesTab';

import type { Meta, StoryObj } from '@storybook/react';

const mockGame: Game = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Catan Studio',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: new Date().toISOString(),
  imageUrl: null,
  faqCount: null,
  averageRating: null,
};

const createMockDocument = (
  id: string,
  fileName: string,
  status: string,
  pageCount?: number
): PdfDocumentDto => ({
  id,
  gameId: 'game-1',
  fileName,
  fileSizeBytes: Math.random() * 5000000 + 500000,
  uploadedAt: new Date(Date.now() - Math.random() * 30 * 24 * 60 * 60 * 1000).toISOString(),
  uploadedByUserId: 'user-1',
  processingStatus: status,
  processingError: status === 'Failed' ? 'Extraction failed' : null,
  qualityScore: status === 'Completed' ? 0.85 : null,
  textExtractionMethod: status === 'Completed' ? 'unstructured' : null,
  pageCount,
  logUrl: null,
});

const meta = {
  title: 'Games/Detail/GameRulesTab',
  component: GameRulesTab,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof GameRulesTab>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Empty: Story = {
  args: {
    game: mockGame,
    documents: [],
  },
};

export const SingleCompleted: Story = {
  args: {
    game: mockGame,
    documents: [createMockDocument('1', 'catan-rulebook.pdf', 'Completed', 24)],
  },
};

export const MultipleDocuments: Story = {
  args: {
    game: mockGame,
    documents: [
      createMockDocument('1', 'base-rules.pdf', 'Completed', 20),
      createMockDocument('2', 'expansion-rules.pdf', 'Completed', 12),
      createMockDocument('3', 'quick-reference.pdf', 'Completed', 2),
    ],
  },
};

export const WithProcessing: Story = {
  args: {
    game: mockGame,
    documents: [
      createMockDocument('1', 'completed.pdf', 'Completed', 24),
      createMockDocument('2', 'processing.pdf', 'Processing'),
      createMockDocument('3', 'pending.pdf', 'Pending'),
    ],
  },
};

export const WithFailed: Story = {
  args: {
    game: mockGame,
    documents: [
      createMockDocument('1', 'good-rulebook.pdf', 'Completed', 28),
      createMockDocument('2', 'corrupted.pdf', 'Failed'),
      createMockDocument('3', 'invalid-format.pdf', 'Failed'),
    ],
  },
};

export const AllStatuses: Story = {
  args: {
    game: mockGame,
    documents: [
      createMockDocument('1', 'rulebook-v1.pdf', 'Completed', 20),
      createMockDocument('2', 'rulebook-v2.pdf', 'Completed', 24),
      createMockDocument('3', 'expansion.pdf', 'Processing'),
      createMockDocument('4', 'faq.pdf', 'Pending'),
      createMockDocument('5', 'corrupted.pdf', 'Failed'),
    ],
  },
};
