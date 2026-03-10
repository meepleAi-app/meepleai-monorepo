/**
 * SetupWizard Tests — Issue #5583
 *
 * Test Coverage:
 * - Wizard step navigation (next/back)
 * - Player add/remove/reorder
 * - Start Game button triggers callback
 * - Games without analysis show warning
 * - Loading states
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { RulebookAnalysisDto, EntityLinkDto } from '@/lib/api';

// ========== Mocks ==========

const mockAnalysis: RulebookAnalysisDto = {
  id: 'analysis-1',
  sharedGameId: 'game-1',
  pdfDocumentId: 'pdf-1',
  gameTitle: 'Catan',
  summary: 'A game about settling an island by collecting resources.',
  keyMechanics: ['Dice Rolling', 'Trading', 'Area Control'],
  victoryConditions: {
    primary: 'First player to reach 10 victory points',
    alternatives: ['Longest Road', 'Largest Army'],
    isPointBased: true,
    targetPoints: 10,
  },
  resources: [
    { name: 'Brick', type: 'Resource', usage: 'Building roads and settlements', isLimited: false },
    { name: 'Ore', type: 'Resource', usage: 'Building cities', isLimited: false },
  ],
  gamePhases: [
    {
      name: 'Setup',
      description: 'Place the board and initial settlements',
      order: 0,
      isOptional: false,
    },
    { name: 'Main Phase', description: 'Roll dice, trade, build', order: 1, isOptional: false },
  ],
  commonQuestions: ['Can I trade on my first turn?'],
  confidenceScore: 0.85,
  version: 1,
  isActive: true,
  source: 'pdf-1',
  analyzedAt: '2025-01-01T00:00:00Z',
  createdBy: 'user-1',
  keyConcepts: [],
  generatedFaqs: [
    {
      question: 'How many roads can I build per turn?',
      answer: 'As many as you have resources for.',
      sourceSection: 'Building',
      confidence: 0.9,
      tags: ['building'],
    },
  ],
  gameStateSchemaJson: null,
  completionStatus: 'Complete',
  missingSections: null,
};

const mockExpansions: EntityLinkDto[] = [
  {
    id: 'link-1',
    sourceEntityType: 'Game',
    sourceEntityId: 'game-1',
    targetEntityType: 'Game',
    targetEntityId: 'expansion-1',
    linkType: 'ExpansionOf',
    isBidirectional: false,
    scope: 'Shared',
    ownerUserId: 'user-1',
    metadata: 'Seafarers',
    isAdminApproved: true,
    isBggImported: true,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    isOwner: false,
  },
];

// Mock the hooks
let mockAnalysisReturn = { data: mockAnalysis, isLoading: false };
let mockExpansionsReturn = { data: mockExpansions, isLoading: false };

vi.mock('@/hooks/queries', () => ({
  useGameAnalysis: () => mockAnalysisReturn,
  useGameExpansions: () => mockExpansionsReturn,
}));

// Import after mocking
import { SetupWizard } from '../SetupWizard';

// ========== Helpers ==========

function createQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
}

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createQueryClient();
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

// ========== Tests ==========

describe('SetupWizard', () => {
  const defaultProps = {
    gameId: 'game-1',
    gameTitle: 'Catan',
    open: true,
    onOpenChange: vi.fn(),
    onStartGame: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockAnalysisReturn = { data: mockAnalysis, isLoading: false };
    mockExpansionsReturn = { data: mockExpansions, isLoading: false };
  });

  // ========== Step Navigation ==========

  it('should render Step 1 (Players) by default', () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    // The stepper and the step heading both contain "Giocatori" — verify via the heading
    expect(screen.getByRole('heading', { name: /giocatori/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Nome giocatore')).toBeInTheDocument();
  });

  it('should disable Next when no players added', () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    const nextBtn = screen.getByRole('button', { name: /avanti/i });
    expect(nextBtn).toBeDisabled();
  });

  it('should enable Next after adding a player', () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));

    const nextBtn = screen.getByRole('button', { name: /avanti/i });
    expect(nextBtn).not.toBeDisabled();
  });

  it('should navigate to Step 2 after clicking Next', async () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    // Add player
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));

    // Go next
    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));

    await waitFor(() => {
      expect(screen.getByText('Preparazione Fisica')).toBeInTheDocument();
    });
  });

  it('should navigate back from Step 2 to Step 1', async () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    // Add player and go to step 2
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));
    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));

    await waitFor(() => {
      expect(screen.getByText('Preparazione Fisica')).toBeInTheDocument();
    });

    // Go back
    fireEvent.click(screen.getByRole('button', { name: /indietro/i }));

    await waitFor(() => {
      expect(screen.getByPlaceholderText('Nome giocatore')).toBeInTheDocument();
    });
  });

  it('should show Annulla on Step 1 instead of Indietro', () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });

  // ========== Player Management ==========

  it('should add a player with name', () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));

    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('should add player on Enter key', () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Bob' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('should remove a player', () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    // Add then remove
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));

    expect(screen.getByText('Alice')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /rimuovi alice/i }));

    expect(screen.queryByText('Alice')).not.toBeInTheDocument();
  });

  it('should assign Host role to first player', () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));

    expect(screen.getByText('Host')).toBeInTheDocument();
  });

  // ========== Start Game ==========

  it('should show Avvia Partita on the last step', async () => {
    renderWithProviders(<SetupWizard {...defaultProps} />);

    // Add player and navigate to step 3
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));
    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));

    await waitFor(() => {
      expect(screen.getByText('Preparazione Fisica')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /avvia partita/i })).toBeInTheDocument();
    });
  });

  it('should call onStartGame with players and expansions when Start Game is clicked', async () => {
    const onStartGame = vi.fn();
    renderWithProviders(<SetupWizard {...defaultProps} onStartGame={onStartGame} />);

    // Add player
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));

    // Navigate to step 3
    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));
    await waitFor(() => {
      expect(screen.getByText('Preparazione Fisica')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /avvia partita/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /avvia partita/i }));

    expect(onStartGame).toHaveBeenCalledTimes(1);
    expect(onStartGame).toHaveBeenCalledWith(
      expect.objectContaining({
        players: expect.arrayContaining([expect.objectContaining({ name: 'Alice' })]),
        activeExpansionIds: [],
      })
    );
  });

  // ========== No Analysis ==========

  it('should show warning on Step 2 when no analysis is available', async () => {
    mockAnalysisReturn = { data: null as unknown as RulebookAnalysisDto, isLoading: false };

    renderWithProviders(<SetupWizard {...defaultProps} />);

    // Add player and go to step 2
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));
    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));

    await waitFor(() => {
      expect(screen.getByText('Nessuna analisi disponibile')).toBeInTheDocument();
    });
  });

  it('should show warning on Step 3 when no analysis is available', async () => {
    mockAnalysisReturn = { data: null as unknown as RulebookAnalysisDto, isLoading: false };

    renderWithProviders(<SetupWizard {...defaultProps} />);

    // Navigate all the way to step 3
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));
    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));

    await waitFor(() => {
      expect(screen.getByText('Nessuna analisi disponibile')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));

    await waitFor(() => {
      expect(screen.getByText(/non ha un regolamento analizzato/)).toBeInTheDocument();
    });
  });

  // ========== Loading ==========

  it('should show loading spinner when data is loading on Step 2', async () => {
    mockAnalysisReturn = { data: undefined as unknown as RulebookAnalysisDto, isLoading: true };
    mockExpansionsReturn = { data: undefined as unknown as EntityLinkDto[], isLoading: true };

    renderWithProviders(<SetupWizard {...defaultProps} />);

    // Add player and go to step 2
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));
    fireEvent.click(screen.getByRole('button', { name: /avanti/i }));

    await waitFor(() => {
      expect(screen.getByText('Caricamento dati del gioco...')).toBeInTheDocument();
    });
  });

  // ========== Sheet ==========

  it('should call onOpenChange when Annulla is clicked', () => {
    const onOpenChange = vi.fn();
    renderWithProviders(<SetupWizard {...defaultProps} onOpenChange={onOpenChange} />);

    fireEvent.click(screen.getByRole('button', { name: /annulla/i }));

    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
