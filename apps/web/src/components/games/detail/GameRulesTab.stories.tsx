import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

import { GameRulesTab } from './GameRulesTab';

import type { Meta, StoryObj } from '@storybook/react';

const createMockDocument = (
  id: string,
  fileName: string,
  processingState: string,
  pageCount?: number
): PdfDocumentDto => ({
  id,
  gameId: 'game-1',
  fileName,
  filePath: `/uploads/${fileName}`,
  fileSizeBytes: Math.random() * 5000000 + 500000,
  uploadedAt: new Date(Date.now() - Math.random() * 30 * 24 * 60 * 60 * 1000).toISOString(),
  processingStatus: processingState === 'Ready' ? 'Completed' : processingState,
  processedAt: processingState === 'Ready' ? new Date().toISOString() : null,
  pageCount: pageCount ?? null,
  documentType: 'base',
  isPublic: false,
  processingState,
  progressPercentage: processingState === 'Ready' ? 100 : 0,
  retryCount: 0,
  maxRetries: 3,
  canRetry: processingState === 'Failed',
  errorCategory: processingState === 'Failed' ? 'ExtractionError' : null,
  processingError: processingState === 'Failed' ? 'Extraction failed' : null,
  documentCategory: 'Rulebook',
  baseDocumentId: null,
  isActiveForRag: true,
  hasAcceptedDisclaimer: false,
  versionLabel: null,
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
    gameId: 'game-1',
    documents: [],
  },
};

export const SingleReady: Story = {
  args: {
    gameId: 'game-1',
    documents: [createMockDocument('1', 'catan-rulebook.pdf', 'Ready', 24)],
  },
};

export const MultipleDocuments: Story = {
  args: {
    gameId: 'game-1',
    documents: [
      createMockDocument('1', 'base-rules.pdf', 'Ready', 20),
      createMockDocument('2', 'expansion-rules.pdf', 'Ready', 12),
      createMockDocument('3', 'quick-reference.pdf', 'Ready', 2),
    ],
  },
};

export const WithProcessing: Story = {
  args: {
    gameId: 'game-1',
    documents: [
      createMockDocument('1', 'completed.pdf', 'Ready', 24),
      createMockDocument('2', 'extracting.pdf', 'Extracting'),
      createMockDocument('3', 'pending.pdf', 'Pending'),
    ],
  },
};

export const WithFailed: Story = {
  args: {
    gameId: 'game-1',
    documents: [
      createMockDocument('1', 'good-rulebook.pdf', 'Ready', 28),
      createMockDocument('2', 'corrupted.pdf', 'Failed'),
      createMockDocument('3', 'invalid-format.pdf', 'Failed'),
    ],
  },
};

export const AllStatuses: Story = {
  args: {
    gameId: 'game-1',
    documents: [
      createMockDocument('1', 'rulebook-v1.pdf', 'Ready', 20),
      createMockDocument('2', 'rulebook-v2.pdf', 'Ready', 24),
      createMockDocument('3', 'expansion.pdf', 'Extracting'),
      createMockDocument('4', 'embedding.pdf', 'Embedding'),
      createMockDocument('5', 'faq.pdf', 'Pending'),
      createMockDocument('6', 'corrupted.pdf', 'Failed'),
    ],
  },
};

export const Loading: Story = {
  args: {
    gameId: 'game-1',
    documents: [],
    isLoading: true,
  },
};
