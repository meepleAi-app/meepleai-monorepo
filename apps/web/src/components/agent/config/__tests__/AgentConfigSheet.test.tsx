/**
 * AgentConfigSheet Component Tests
 * Issue #3238: [FRONT-002] Main agent configuration container
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock child components to isolate testing
vi.mock('../GameSelector', () => ({
  GameSelector: () => <div data-testid="game-selector">Game Selector</div>,
}));

vi.mock('../ModelSelector', () => ({
  ModelSelector: () => <div data-testid="model-selector">Model Selector</div>,
}));

vi.mock('../SlotCards', () => ({
  SlotCards: () => <div data-testid="slot-cards">Slot Cards</div>,
}));

vi.mock('../TemplateCarousel', () => ({
  TemplateCarousel: () => <div data-testid="template-carousel">Template Carousel</div>,
}));

vi.mock('../TokenQuotaDisplay', () => ({
  TokenQuotaDisplay: () => <div data-testid="token-quota">Token Quota</div>,
}));

vi.mock('../../shared/ActionBar', () => ({
  ActionBar: ({ onCancel }: { onCancel: () => void }) => (
    <div data-testid="action-bar">
      <button onClick={onCancel} data-testid="cancel-button">Cancel</button>
    </div>
  ),
}));

// Import after mocks
import { AgentConfigSheet } from '../AgentConfigSheet';

describe('AgentConfigSheet', () => {
  const defaultProps = {
    isOpen: true,
    onClose: vi.fn(),
    gameId: 'game-123',
    gameTitle: 'Test Game Title',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders sheet title "Configure Agent" in config view', () => {
      render(<AgentConfigSheet {...defaultProps} />);
      expect(screen.getByText('Configure Agent')).toBeInTheDocument();
    });

    it('renders game title in header', () => {
      render(<AgentConfigSheet {...defaultProps} />);
      expect(screen.getByText('Test Game Title')).toBeInTheDocument();
    });

    it('renders help button', () => {
      render(<AgentConfigSheet {...defaultProps} />);
      expect(screen.getByRole('button', { name: 'Help' })).toBeInTheDocument();
    });

    it('renders all config components in config view', () => {
      render(<AgentConfigSheet {...defaultProps} />);
      expect(screen.getByTestId('game-selector')).toBeInTheDocument();
      expect(screen.getByTestId('template-carousel')).toBeInTheDocument();
      expect(screen.getByTestId('model-selector')).toBeInTheDocument();
      expect(screen.getByTestId('token-quota')).toBeInTheDocument();
      expect(screen.getByTestId('slot-cards')).toBeInTheDocument();
    });

    it('renders action bar', () => {
      render(<AgentConfigSheet {...defaultProps} />);
      expect(screen.getByTestId('action-bar')).toBeInTheDocument();
    });
  });

  describe('Visibility', () => {
    it('does not render content when closed', () => {
      render(<AgentConfigSheet {...defaultProps} isOpen={false} />);
      expect(screen.queryByText('Configure Agent')).not.toBeInTheDocument();
    });

    it('renders content when open', () => {
      render(<AgentConfigSheet {...defaultProps} isOpen={true} />);
      expect(screen.getByText('Configure Agent')).toBeInTheDocument();
    });
  });

  describe('Close Behavior', () => {
    it('calls onClose when action bar cancel is clicked', () => {
      render(<AgentConfigSheet {...defaultProps} />);
      fireEvent.click(screen.getByTestId('cancel-button'));
      expect(defaultProps.onClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Back Button', () => {
    it('does not show back button in config view', () => {
      render(<AgentConfigSheet {...defaultProps} />);
      expect(screen.queryByRole('button', { name: 'Back' })).not.toBeInTheDocument();
    });
  });

  describe('Different Game Titles', () => {
    it('renders custom game title', () => {
      render(<AgentConfigSheet {...defaultProps} gameTitle="7 Wonders" />);
      expect(screen.getByText('7 Wonders')).toBeInTheDocument();
    });

    it('renders long game title', () => {
      const longTitle = 'Gloomhaven: Jaws of the Lion - Extended Edition';
      render(<AgentConfigSheet {...defaultProps} gameTitle={longTitle} />);
      expect(screen.getByText(longTitle)).toBeInTheDocument();
    });
  });

  describe('Responsive Layout', () => {
    it('has proper sheet structure', () => {
      render(<AgentConfigSheet {...defaultProps} />);
      // Sheet content should have responsive classes
      const content = document.querySelector('.h-\\[90vh\\]');
      expect(content).toBeInTheDocument();
    });
  });
});
