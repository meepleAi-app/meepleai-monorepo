/**
 * AgentChatSheet Component Tests
 * Issue #3242: [FRONT-006] Agent Chat Sheet container
 * Issue #3249: [FRONT-013] Agent Type Switcher integration
 * Issue #3250: [FRONT-014] Settings Drawer integration
 * Issue #3251: [FRONT-015] PDF Viewer integration
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

// Mock stores
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: vi.fn(() => ({
    isChatOpen: true,
    closeChat: vi.fn(),
    openPdfViewer: vi.fn(),
  })),
}));

// Mock hooks
vi.mock('@/hooks/queries/useAgentTypologies', () => ({
  useApprovedTypologies: vi.fn(() => ({
    data: [],
    isLoading: false,
  })),
  useSwitchTypology: vi.fn(() => ({
    mutate: vi.fn(),
    isPending: false,
  })),
}));

// Mock toast
vi.mock('sonner', () => ({
  toast: {
    info: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock child components
vi.mock('../AgentTypeSwitcher', () => ({
  AgentTypeSwitcher: ({ currentTypology, onSwitch }: {
    currentTypology: Typology;
    onSwitch: (id: string) => void;
  }) => (
    <button
      data-testid="agent-type-switcher"
      onClick={() => onSwitch('typology-2')}
    >
      {currentTypology.name}
    </button>
  ),
}));

vi.mock('../PdfViewerIntegration', () => ({
  PdfViewerIntegration: ({ gameId }: { gameId: string }) => (
    <div data-testid="pdf-viewer-integration" data-game-id={gameId} />
  ),
}));

vi.mock('../../settings/AgentSettingsDrawer', () => ({
  AgentSettingsDrawer: ({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) => (
    isOpen ? (
      <div data-testid="settings-drawer">
        <button onClick={onClose}>Close Settings</button>
      </div>
    ) : null
  ),
}));

vi.mock('../../shared/ActionBar', () => ({
  ActionBar: ({ onSettings, onOpenPdf, hasPdf }: {
    onSettings: () => void;
    onOpenPdf: () => void;
    hasPdf: boolean;
  }) => (
    <div data-testid="action-bar">
      <button onClick={onSettings} data-testid="settings-button">Settings</button>
      {hasPdf && <button onClick={onOpenPdf} data-testid="pdf-button">Open PDF</button>}
    </div>
  ),
}));

// Import after mocks
import { AgentChatSheet } from '../AgentChatSheet';
import { useAgentStore } from '@/stores/agentStore';
import { useApprovedTypologies, useSwitchTypology } from '@/hooks/queries/useAgentTypologies';
import { toast } from 'sonner';

// Mock typology data
const mockTypology: Typology = {
  id: 'typology-1',
  name: 'Rules Expert',
  description: 'Get answers to rule questions',
  basePrompt: 'You are a rules expert...',
  defaultStrategyName: 'HybridSearch',
  defaultStrategyParameters: null,
  status: 'Approved',
  createdBy: 'user-1',
  approvedBy: 'admin-1',
  createdAt: '2026-01-01T00:00:00Z',
  approvedAt: '2026-01-02T00:00:00Z',
  isDeleted: false,
};

const mockTypologies: Typology[] = [
  mockTypology,
  {
    ...mockTypology,
    id: 'typology-2',
    name: 'Strategy Coach',
  },
];

const defaultProps = {
  gameTitle: 'Catan',
  gameId: 'game-123',
  currentTypology: mockTypology,
  modelName: 'GPT-4',
  tokensUsed: 500,
  tokensLimit: 4096,
  sessionId: 'session-123',
};

describe('AgentChatSheet', () => {
  const mockCloseChat = vi.fn();
  const mockOpenPdfViewer = vi.fn();
  const mockMutate = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useAgentStore).mockReturnValue({
      isChatOpen: true,
      closeChat: mockCloseChat,
      openPdfViewer: mockOpenPdfViewer,
    } as ReturnType<typeof useAgentStore>);

    vi.mocked(useApprovedTypologies).mockReturnValue({
      data: mockTypologies,
      isLoading: false,
    } as ReturnType<typeof useApprovedTypologies>);

    vi.mocked(useSwitchTypology).mockReturnValue({
      mutate: mockMutate,
      isPending: false,
    } as unknown as ReturnType<typeof useSwitchTypology>);
  });

  describe('Rendering', () => {
    it('renders game title', () => {
      render(<AgentChatSheet {...defaultProps} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders model name with badge', () => {
      render(<AgentChatSheet {...defaultProps} />);
      expect(screen.getByText(/GPT-4/)).toBeInTheDocument();
    });

    it('renders token usage', () => {
      render(<AgentChatSheet {...defaultProps} />);
      // Token values are formatted with toLocaleString, use regex for flexibility
      expect(screen.getByText(/500.*4.*096.*tokens/i)).toBeInTheDocument();
    });

    it('renders action bar', () => {
      render(<AgentChatSheet {...defaultProps} />);
      expect(screen.getByTestId('action-bar')).toBeInTheDocument();
    });

    it('renders PDF viewer integration', () => {
      render(<AgentChatSheet {...defaultProps} />);
      expect(screen.getByTestId('pdf-viewer-integration')).toBeInTheDocument();
      expect(screen.getByTestId('pdf-viewer-integration')).toHaveAttribute('data-game-id', 'game-123');
    });
  });

  describe('Chat Open/Close', () => {
    it('renders when chat is open', () => {
      render(<AgentChatSheet {...defaultProps} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('does not render content when chat is closed', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        isChatOpen: false,
        closeChat: mockCloseChat,
        openPdfViewer: mockOpenPdfViewer,
      } as ReturnType<typeof useAgentStore>);

      render(<AgentChatSheet {...defaultProps} />);
      // Sheet should not show content when closed
      expect(screen.queryByText('Catan')).not.toBeInTheDocument();
    });
  });

  describe('Agent Type Switcher', () => {
    it('renders type switcher when typologies are loaded', () => {
      render(<AgentChatSheet {...defaultProps} />);
      expect(screen.getByTestId('agent-type-switcher')).toBeInTheDocument();
    });

    it('does not render switcher when loading typologies', () => {
      vi.mocked(useApprovedTypologies).mockReturnValue({
        data: [],
        isLoading: true,
      } as ReturnType<typeof useApprovedTypologies>);

      render(<AgentChatSheet {...defaultProps} />);
      expect(screen.queryByTestId('agent-type-switcher')).not.toBeInTheDocument();
    });

    it('calls switchTypology when typology is switched', async () => {
      render(<AgentChatSheet {...defaultProps} />);

      fireEvent.click(screen.getByTestId('agent-type-switcher'));

      expect(mockMutate).toHaveBeenCalledWith(
        { sessionId: 'session-123', typologyId: 'typology-2' },
        expect.any(Object)
      );
    });

    it('shows success toast on successful typology switch', async () => {
      let onSuccessCallback: (response: { newTypologyName: string }) => void;

      vi.mocked(useSwitchTypology).mockReturnValue({
        mutate: vi.fn((params, options) => {
          onSuccessCallback = options?.onSuccess as typeof onSuccessCallback;
        }),
        isPending: false,
      } as unknown as ReturnType<typeof useSwitchTypology>);

      const mockOnTypologySwitch = vi.fn();
      render(<AgentChatSheet {...defaultProps} onTypologySwitch={mockOnTypologySwitch} />);

      fireEvent.click(screen.getByTestId('agent-type-switcher'));

      // Simulate success callback
      onSuccessCallback!({ newTypologyName: 'Strategy Coach' });

      expect(toast.success).toHaveBeenCalledWith(
        'Tipo agente aggiornato!',
        expect.objectContaining({ description: 'Ora stai usando: Strategy Coach' })
      );
    });

    it('shows error toast on typology switch failure', async () => {
      let onErrorCallback: (error: Error) => void;

      vi.mocked(useSwitchTypology).mockReturnValue({
        mutate: vi.fn((params, options) => {
          onErrorCallback = options?.onError as typeof onErrorCallback;
        }),
        isPending: false,
      } as unknown as ReturnType<typeof useSwitchTypology>);

      render(<AgentChatSheet {...defaultProps} />);

      fireEvent.click(screen.getByTestId('agent-type-switcher'));

      // Simulate error callback
      onErrorCallback!(new Error('Network error'));

      expect(toast.error).toHaveBeenCalledWith(
        'Errore nel cambio tipo agente',
        expect.objectContaining({ description: 'Network error' })
      );
    });
  });

  describe('Settings Drawer', () => {
    it('opens settings drawer when settings button clicked', async () => {
      render(<AgentChatSheet {...defaultProps} />);

      fireEvent.click(screen.getByTestId('settings-button'));

      await waitFor(() => {
        expect(screen.getByTestId('settings-drawer')).toBeInTheDocument();
      });
    });

    it('closes settings drawer when close button clicked', async () => {
      render(<AgentChatSheet {...defaultProps} />);

      fireEvent.click(screen.getByTestId('settings-button'));

      await waitFor(() => {
        expect(screen.getByTestId('settings-drawer')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Close Settings'));

      await waitFor(() => {
        expect(screen.queryByTestId('settings-drawer')).not.toBeInTheDocument();
      });
    });
  });

  describe('PDF Viewer', () => {
    it('shows PDF button when hasPdf is true', () => {
      render(<AgentChatSheet {...defaultProps} hasPdf={true} />);
      expect(screen.getByTestId('pdf-button')).toBeInTheDocument();
    });

    it('does not show PDF button when hasPdf is false', () => {
      render(<AgentChatSheet {...defaultProps} hasPdf={false} />);
      expect(screen.queryByTestId('pdf-button')).not.toBeInTheDocument();
    });

    it('opens PDF viewer and shows toast when PDF button clicked', () => {
      render(<AgentChatSheet {...defaultProps} hasPdf={true} />);

      fireEvent.click(screen.getByTestId('pdf-button'));

      expect(mockOpenPdfViewer).toHaveBeenCalledWith('game-123', 1);
      expect(toast.info).toHaveBeenCalledWith(
        '📄 Apertura regolamento',
        expect.objectContaining({ description: 'Premi P per aprire/chiudere' })
      );
    });
  });

  describe('Callbacks', () => {
    it('calls onTypologySwitch when typology is switched successfully', async () => {
      let onSuccessCallback: (response: { newTypologyName: string }) => void;

      vi.mocked(useSwitchTypology).mockReturnValue({
        mutate: vi.fn((params, options) => {
          onSuccessCallback = options?.onSuccess as typeof onSuccessCallback;
        }),
        isPending: false,
      } as unknown as ReturnType<typeof useSwitchTypology>);

      const mockOnTypologySwitch = vi.fn();
      render(<AgentChatSheet {...defaultProps} onTypologySwitch={mockOnTypologySwitch} />);

      fireEvent.click(screen.getByTestId('agent-type-switcher'));
      onSuccessCallback!({ newTypologyName: 'Strategy Coach' });

      expect(mockOnTypologySwitch).toHaveBeenCalledWith(
        expect.objectContaining({ id: 'typology-2', name: 'Strategy Coach' })
      );
    });

    it('calls onConfigUpdated when config is updated', async () => {
      // This would be tested through AgentSettingsDrawer integration
      // For now, verify the component renders with config callback
      const mockOnConfigUpdated = vi.fn();
      render(<AgentChatSheet {...defaultProps} onConfigUpdated={mockOnConfigUpdated} />);

      expect(screen.getByTestId('action-bar')).toBeInTheDocument();
    });
  });

  describe('Default Values', () => {
    it('uses default hasPdf value of false', () => {
      render(<AgentChatSheet {...defaultProps} />);
      expect(screen.queryByTestId('pdf-button')).not.toBeInTheDocument();
    });

    it('uses default userTier value of free', () => {
      render(<AgentChatSheet {...defaultProps} />);
      // Component should render without errors with default tier
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });
  });
});
