/**
 * Add Game Wizard Component Tests
 * Issue #3477: Integration tests for wizard UI and step navigation
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { screen, fireEvent } from '@testing-library/react';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { AddGameWizard } from '@/components/collection/wizard/AddGameWizard';
import { useAddGameWizardStore } from '@/stores/addGameWizardStore';
import { WIZARD_TEST_IDS } from '@/lib/test-ids';
import { t, getTextMatcher } from '@/test-utils/test-i18n';

// Mock toast
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
  usePathname: vi.fn(() => '/library'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t }),
}));

describe('AddGameWizard', () => {
  beforeEach(() => {
    useAddGameWizardStore.getState().reset();
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders wizard title and subtitle', () => {
      renderWithQuery(<AddGameWizard />);

      expect(screen.getByTestId(WIZARD_TEST_IDS.title)).toHaveTextContent(t('collection.addGameTitle'));
      expect(screen.getByTestId(WIZARD_TEST_IDS.subtitle)).toHaveTextContent(
        t('collection.addGameSubtitle')
      );
    });

    it('renders back to collection link', () => {
      renderWithQuery(<AddGameWizard />);

      const backLink = screen.getByRole('link', { name: getTextMatcher('collection.backToCollection') });
      expect(backLink).toBeInTheDocument();
      expect(backLink).toHaveAttribute('href', '/library');
    });

    it('renders step indicator (3 steps when shared game selected)', () => {
      renderWithQuery(<AddGameWizard />);

      // Initially, no game selected → Step 2 hidden
      expect(screen.getByText('1. Search/Select')).toBeInTheDocument();
      expect(screen.queryByText('2. Game Details')).not.toBeInTheDocument();
      expect(screen.getByText('3. Upload PDF')).toBeInTheDocument();
      expect(screen.getByText('4. Review')).toBeInTheDocument();
    });

    it('renders all 4 steps when custom game selected', () => {
      const { selectCustomGame } = useAddGameWizardStore.getState();

      selectCustomGame();
      renderWithQuery(<AddGameWizard />);

      // All 4 steps should be visible
      expect(screen.getByText('1. Search/Select')).toBeInTheDocument();
      expect(screen.getByText('2. Game Details')).toBeInTheDocument();
      expect(screen.getByText('3. Upload PDF')).toBeInTheDocument();
      expect(screen.getByText('4. Review')).toBeInTheDocument();
    });

    it('starts on search/select step', () => {
      renderWithQuery(<AddGameWizard />);

      expect(screen.getByText('Search or Create Game')).toBeInTheDocument();
    });
  });

  describe('Step Navigation', () => {
    it('shows only 3 steps when shared game selected (hides Step 2)', () => {
      const { selectSharedGame } = useAddGameWizardStore.getState();
      const mockGame = { id: '1', title: 'Test Game', createdAt: '2024-01-01' };

      selectSharedGame(mockGame);
      renderWithQuery(<AddGameWizard />);

      // Step 2 should not be visible in step indicator
      const stepLabels = screen.queryByText('2. Game Details');
      expect(stepLabels).not.toBeInTheDocument();
    });

    it('advances to Game Details when custom game selected', () => {
      const { selectCustomGame, goNext } = useAddGameWizardStore.getState();

      renderWithQuery(<AddGameWizard />);

      selectCustomGame();
      goNext();

      // Step 2 (Game Details) should now be visible
      expect(useAddGameWizardStore.getState().step).toBe(2);
    });
  });

  describe('Summary Card', () => {
    it('shows summary when game selected', () => {
      const mockGame = { id: '1', title: 'Catan', createdAt: '2024-01-01' };
      const { selectSharedGame } = useAddGameWizardStore.getState();

      selectSharedGame(mockGame);
      renderWithQuery(<AddGameWizard />);

      expect(screen.getByText('Summary')).toBeInTheDocument();
      // Game title appears in both game list and summary, use getAllByText
      const catanElements = screen.getAllByText('Catan');
      expect(catanElements.length).toBeGreaterThan(0);
    });

    it('shows PDF in summary when uploaded', () => {
      const mockGame = { id: '1', title: 'Catan', createdAt: '2024-01-01' };
      const { selectSharedGame, setUploadedPdf } = useAddGameWizardStore.getState();

      selectSharedGame(mockGame);
      setUploadedPdf('pdf-123', 'rulebook.pdf');
      renderWithQuery(<AddGameWizard />);

      expect(screen.getByText('Summary')).toBeInTheDocument();
      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
    });

    it('shows custom badge for custom games', () => {
      const { selectCustomGame, setCustomGameData } = useAddGameWizardStore.getState();

      selectCustomGame();
      setCustomGameData({ name: 'My Unique Custom Game 2024' }); // Unique name to avoid conflicts
      renderWithQuery(<AddGameWizard />);

      expect(screen.getByText('My Unique Custom Game 2024')).toBeInTheDocument();
      expect(screen.getByText('Custom')).toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('displays error message when present', () => {
      const mockGame = { id: '1', title: 'Catan', createdAt: '2024-01-01' };

      // Set error state before render
      useAddGameWizardStore.setState({
        selectedGame: mockGame,
        isCustomGame: false,
        error: 'Network error occurred',
      });

      renderWithQuery(<AddGameWizard />);

      expect(screen.getByText('Network error occurred')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper step descriptions for screen readers (shared game path)', () => {
      renderWithQuery(<AddGameWizard />);

      // When no custom game, Step 2 hidden
      expect(screen.getByTestId(WIZARD_TEST_IDS.stepDescription(1))).toHaveTextContent('Find or create game');
      expect(screen.queryByTestId(WIZARD_TEST_IDS.stepDescription(2))).not.toBeInTheDocument();
      expect(screen.getByTestId(WIZARD_TEST_IDS.stepDescription(3))).toHaveTextContent('Optional rulebook');
      expect(screen.getByTestId(WIZARD_TEST_IDS.stepDescription(4))).toHaveTextContent('Confirm and submit');
    });

    it('has all step descriptions when custom game selected', () => {
      const { selectCustomGame } = useAddGameWizardStore.getState();

      selectCustomGame();
      renderWithQuery(<AddGameWizard />);

      // All 4 steps visible when custom game
      expect(screen.getByTestId(WIZARD_TEST_IDS.stepDescription(1))).toHaveTextContent('Find or create game');
      expect(screen.getByTestId(WIZARD_TEST_IDS.stepDescription(2))).toHaveTextContent('Custom game info');
      expect(screen.getByTestId(WIZARD_TEST_IDS.stepDescription(3))).toHaveTextContent('Optional rulebook');
      expect(screen.getByTestId(WIZARD_TEST_IDS.stepDescription(4))).toHaveTextContent('Confirm and submit');
    });
  });
});
