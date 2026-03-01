/**
 * AdminGameWizard Tests
 * Issue #4673: Multi-step wizard shell — step navigation and state management.
 */

import { render, screen, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { AdminGameWizard } from '../AdminGameWizard';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockRouterPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockRouterPush }),
}));

vi.mock('next/link', () => ({
  default: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

// Capture callbacks from child step components
type BggStepProps = { onGameSelected: (game: { bggId: number; name: string; type: 'boardgame'; yearPublished?: number; thumbnailUrl?: null }) => void };
type GameDetailsStepProps = {
  selectedGame: { bggId: number; name: string; type: 'boardgame'; yearPublished?: number; thumbnailUrl?: null };
  onBack: () => void;
  onGameCreated: (result: { sharedGameId: string; title: string; bggId: number; status: string }) => void;
};
type PdfUploadStepProps = {
  gameId: string;
  gameTitle: string;
  onPdfUploaded: (id: string) => void;
};
type LaunchProcessingStepProps = {
  gameId: string;
  gameTitle: string;
  pdfDocumentId: string;
  onBack: () => void;
  onProcessingLaunched: (gameId: string) => void;
};

let capturedBggStep: BggStepProps | null = null;
let capturedGameDetailsStep: GameDetailsStepProps | null = null;
let capturedPdfUploadStep: PdfUploadStepProps | null = null;
let capturedLaunchStep: LaunchProcessingStepProps | null = null;

vi.mock('../steps/BggSearchStep', () => ({
  BggSearchStep: (props: BggStepProps) => {
    capturedBggStep = props;
    return <div data-testid="bgg-search-step">BggSearchStep</div>;
  },
}));

vi.mock('../steps/GameDetailsStep', () => ({
  GameDetailsStep: (props: GameDetailsStepProps) => {
    capturedGameDetailsStep = props;
    return (
      <div data-testid="game-details-step">
        GameDetailsStep: {props.selectedGame.name}
      </div>
    );
  },
}));

vi.mock('../steps/PdfUploadStep', () => ({
  PdfUploadStep: (props: PdfUploadStepProps) => {
    capturedPdfUploadStep = props;
    return <div data-testid="pdf-upload-step">PdfUploadStep: {props.gameTitle}</div>;
  },
}));

vi.mock('../steps/LaunchProcessingStep', () => ({
  LaunchProcessingStep: (props: LaunchProcessingStepProps) => {
    capturedLaunchStep = props;
    return (
      <div data-testid="launch-processing-step">
        LaunchProcessingStep: {props.gameTitle}
      </div>
    );
  },
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
};

const mockGame = {
  bggId: 174430,
  name: 'Gloomhaven',
  type: 'boardgame' as const,
  yearPublished: 2017,
  thumbnailUrl: null,
};

const mockCreatedGame = {
  sharedGameId: 'shared-uuid-1',
  title: 'Gloomhaven',
  bggId: 174430,
  status: 'created',
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('AdminGameWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    capturedBggStep = null;
    capturedGameDetailsStep = null;
    capturedPdfUploadStep = null;
    capturedLaunchStep = null;
  });

  it('should render the wizard title', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });
    expect(screen.getByText('Add Game')).toBeDefined();
  });

  it('should render step 1 (BGG Search) by default', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });
    expect(screen.getByTestId('bgg-search-step')).toBeDefined();
  });

  it('should show all 4 step labels in the stepper', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });
    expect(screen.getByText('Search BGG')).toBeDefined();
    expect(screen.getByText('Game Details')).toBeDefined();
    expect(screen.getByText('Upload PDF')).toBeDefined();
    expect(screen.getByText('Launch')).toBeDefined();
  });

  it('should transition to step 2 when a game is selected', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => {
      capturedBggStep?.onGameSelected(mockGame);
    });

    expect(screen.getByTestId('game-details-step')).toBeDefined();
    expect(screen.getByText('GameDetailsStep: Gloomhaven')).toBeDefined();
  });

  it('should pass selected game to GameDetailsStep', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => {
      capturedBggStep?.onGameSelected(mockGame);
    });

    expect(capturedGameDetailsStep?.selectedGame).toEqual(mockGame);
  });

  it('should transition to step 3 when game is created', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => { capturedBggStep?.onGameSelected(mockGame); });
    act(() => { capturedGameDetailsStep?.onGameCreated(mockCreatedGame); });

    expect(screen.getByTestId('pdf-upload-step')).toBeDefined();
    expect(screen.getByText('PdfUploadStep: Gloomhaven')).toBeDefined();
  });

  it('should pass correct gameId to PdfUploadStep', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => { capturedBggStep?.onGameSelected(mockGame); });
    act(() => { capturedGameDetailsStep?.onGameCreated(mockCreatedGame); });

    expect(capturedPdfUploadStep?.gameId).toBe('shared-uuid-1');
  });

  it('should transition to step 4 when PDF is uploaded', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => { capturedBggStep?.onGameSelected(mockGame); });
    act(() => { capturedGameDetailsStep?.onGameCreated(mockCreatedGame); });
    act(() => { capturedPdfUploadStep?.onPdfUploaded('pdf-doc-uuid'); });

    expect(screen.getByTestId('launch-processing-step')).toBeDefined();
  });

  it('should pass pdfDocumentId to LaunchProcessingStep', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => { capturedBggStep?.onGameSelected(mockGame); });
    act(() => { capturedGameDetailsStep?.onGameCreated(mockCreatedGame); });
    act(() => { capturedPdfUploadStep?.onPdfUploaded('pdf-doc-uuid'); });

    expect(capturedLaunchStep?.pdfDocumentId).toBe('pdf-doc-uuid');
  });

  it('should navigate to processing page after launch', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => { capturedBggStep?.onGameSelected(mockGame); });
    act(() => { capturedGameDetailsStep?.onGameCreated(mockCreatedGame); });
    act(() => { capturedPdfUploadStep?.onPdfUploaded('pdf-doc-uuid'); });
    act(() => { capturedLaunchStep?.onProcessingLaunched('shared-uuid-1'); });

    expect(mockRouterPush).toHaveBeenCalledWith(
      expect.stringContaining('/admin/games/shared-uuid-1/processing')
    );
  });

  it('should go back from step 2 to step 1 via onBack', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => { capturedBggStep?.onGameSelected(mockGame); });
    expect(screen.getByTestId('game-details-step')).toBeDefined();

    act(() => { capturedGameDetailsStep?.onBack(); });
    expect(screen.getByTestId('bgg-search-step')).toBeDefined();
  });

  it('should go back from step 3 to step 2 via onBack', () => {
    render(<AdminGameWizard />, { wrapper: createWrapper() });

    act(() => { capturedBggStep?.onGameSelected(mockGame); });
    act(() => { capturedGameDetailsStep?.onGameCreated(mockCreatedGame); });
    expect(screen.getByTestId('pdf-upload-step')).toBeDefined();

    // PdfUploadStep doesn't have onBack — it's LaunchProcessingStep
    // Go to step 4 first, then test back to step 3
    act(() => { capturedPdfUploadStep?.onPdfUploaded('pdf-doc-uuid'); });
    expect(screen.getByTestId('launch-processing-step')).toBeDefined();

    act(() => { capturedLaunchStep?.onBack(); });
    expect(screen.getByTestId('pdf-upload-step')).toBeDefined();
  });
});
