/**
 * AgentConfigSheet Component Tests
 * Issue #3238: [FRONT-002] Main agent configuration container
 * Issue #3375: Agent Session Launch API Integration
 * Issue #3376: Added Strategy, ModelTier, CostPreview
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock Next.js router (required by useAgentSessionLaunch)
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

// Mock useAgentSessionLaunch hook (Issue #3375)
vi.mock('@/hooks/useAgentSessionLaunch', () => ({
  useAgentSessionLaunch: () => ({
    launch: vi.fn(),
    isLaunching: false,
  }),
}));

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

// Mock Issue #3376 components
vi.mock('../StrategySelector', () => ({
  StrategySelector: () => <div data-testid="strategy-selector">Strategy Selector</div>,
}));

vi.mock('../ModelTierSelector', () => ({
  ModelTierSelector: () => <div data-testid="model-tier-selector">Model Tier Selector</div>,
}));

vi.mock('../CostPreview', () => ({
  CostPreview: () => <div data-testid="cost-preview">Cost Preview</div>,
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
      expect(screen.getByTestId('token-quota')).toBeInTheDocument();
      expect(screen.getByTestId('slot-cards')).toBeInTheDocument();
      // Issue #3376 components
      expect(screen.getByTestId('strategy-selector')).toBeInTheDocument();
      expect(screen.getByTestId('model-tier-selector')).toBeInTheDocument();
      expect(screen.getByTestId('cost-preview')).toBeInTheDocument();
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
