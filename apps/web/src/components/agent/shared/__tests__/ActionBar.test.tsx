/**
 * ActionBar Component Tests
 * Issue #3241: [FRONT-005] Contextual Action Bar
 * Issue #3251: [FRONT-015] PDF button integration
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock stores
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: vi.fn(() => ({
    selectedGameId: null,
    selectedTypologyId: null,
    selectedModelId: null,
  })),
}));

// Import after mocks
import { ActionBar } from '../ActionBar';
import { useAgentStore } from '@/stores/agentStore';

describe('ActionBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Config State', () => {
    it('renders Cancel and Launch Agent buttons', () => {
      render(<ActionBar state="config" />);

      expect(screen.getByText('Cancel')).toBeInTheDocument();
      expect(screen.getByText(/Launch Agent/)).toBeInTheDocument();
    });

    it('disables Launch button when config is incomplete', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedGameId: null,
        selectedTypologyId: null,
        selectedModelId: null,
      } as ReturnType<typeof useAgentStore>);

      render(<ActionBar state="config" />);

      expect(screen.getByText(/Launch Agent/).closest('button')).toBeDisabled();
    });

    it('enables Launch button when config is complete', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedGameId: 'game-123',
        selectedTypologyId: 'typology-123',
        selectedModelId: 'model-123',
      } as ReturnType<typeof useAgentStore>);

      render(<ActionBar state="config" />);

      expect(screen.getByText(/Launch Agent/).closest('button')).not.toBeDisabled();
    });

    it('disables Launch button when disabled prop is true', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedGameId: 'game-123',
        selectedTypologyId: 'typology-123',
        selectedModelId: 'model-123',
      } as ReturnType<typeof useAgentStore>);

      render(<ActionBar state="config" disabled={true} />);

      expect(screen.getByText(/Launch Agent/).closest('button')).toBeDisabled();
    });

    it('calls onLaunch when Launch button clicked', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedGameId: 'game-123',
        selectedTypologyId: 'typology-123',
        selectedModelId: 'model-123',
      } as ReturnType<typeof useAgentStore>);

      const mockOnLaunch = vi.fn();
      render(<ActionBar state="config" onLaunch={mockOnLaunch} />);

      fireEvent.click(screen.getByText(/Launch Agent/));

      expect(mockOnLaunch).toHaveBeenCalledTimes(1);
    });

    it('calls onCancel when Cancel button clicked', () => {
      const mockOnCancel = vi.fn();
      render(<ActionBar state="config" onCancel={mockOnCancel} />);

      fireEvent.click(screen.getByText('Cancel'));

      expect(mockOnCancel).toHaveBeenCalledTimes(1);
    });
  });

  describe('Chat State', () => {
    it('renders Settings, Export, and Minimize buttons', () => {
      render(<ActionBar state="chat" />);

      // Check for the buttons by their icons (using aria or button structure)
      expect(screen.getAllByRole('button').length).toBeGreaterThanOrEqual(3);
    });

    it('does not render PDF button when hasPdf is false', () => {
      render(<ActionBar state="chat" hasPdf={false} />);

      // Should have 3 buttons: settings, export, minimize
      expect(screen.getAllByRole('button')).toHaveLength(3);
    });

    it('renders PDF button when hasPdf is true', () => {
      render(<ActionBar state="chat" hasPdf={true} />);

      // Should have 4 buttons: pdf, settings, export, minimize
      expect(screen.getAllByRole('button')).toHaveLength(4);
    });

    it('calls onSettings when Settings button clicked', () => {
      const mockOnSettings = vi.fn();
      render(<ActionBar state="chat" onSettings={mockOnSettings} />);

      // Settings is the first button
      fireEvent.click(screen.getAllByRole('button')[0]);

      expect(mockOnSettings).toHaveBeenCalledTimes(1);
    });

    it('calls onExport when Export button clicked', () => {
      const mockOnExport = vi.fn();
      render(<ActionBar state="chat" onExport={mockOnExport} />);

      // Export is the second button
      fireEvent.click(screen.getAllByRole('button')[1]);

      expect(mockOnExport).toHaveBeenCalledTimes(1);
    });

    it('calls onMinimize when Minimize button clicked', () => {
      const mockOnMinimize = vi.fn();
      render(<ActionBar state="chat" onMinimize={mockOnMinimize} />);

      // Minimize is the third button
      fireEvent.click(screen.getAllByRole('button')[2]);

      expect(mockOnMinimize).toHaveBeenCalledTimes(1);
    });

    it('calls onOpenPdf when PDF button clicked', () => {
      const mockOnOpenPdf = vi.fn();
      render(<ActionBar state="chat" hasPdf={true} onOpenPdf={mockOnOpenPdf} />);

      // PDF is the first button when hasPdf is true
      fireEvent.click(screen.getAllByRole('button')[0]);

      expect(mockOnOpenPdf).toHaveBeenCalledTimes(1);
    });
  });

  describe('Slots State', () => {
    it('renders View Usage and Back buttons', () => {
      render(<ActionBar state="slots" />);

      expect(screen.getByText('View Usage')).toBeInTheDocument();
      expect(screen.getByText('Back')).toBeInTheDocument();
    });

    it('calls onViewUsage when View Usage button clicked', () => {
      const mockOnViewUsage = vi.fn();
      render(<ActionBar state="slots" onViewUsage={mockOnViewUsage} />);

      fireEvent.click(screen.getByText('View Usage'));

      expect(mockOnViewUsage).toHaveBeenCalledTimes(1);
    });

    it('calls onCancel when Back button clicked', () => {
      const mockOnCancel = vi.fn();
      render(<ActionBar state="slots" onCancel={mockOnCancel} />);

      fireEvent.click(screen.getByText('Back'));

      expect(mockOnCancel).toHaveBeenCalledTimes(1);
    });
  });

  describe('Edge Cases', () => {
    it('returns null for unknown state', () => {
      // @ts-expect-error Testing invalid state
      const { container } = render(<ActionBar state="unknown" />);

      expect(container.firstChild).toBeNull();
    });

    it('handles missing callback gracefully', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedGameId: 'game-123',
        selectedTypologyId: 'typology-123',
        selectedModelId: 'model-123',
      } as ReturnType<typeof useAgentStore>);

      render(<ActionBar state="config" />);

      // Should not throw when clicking without callback
      expect(() => {
        fireEvent.click(screen.getByText(/Launch Agent/));
      }).not.toThrow();
    });
  });
});
