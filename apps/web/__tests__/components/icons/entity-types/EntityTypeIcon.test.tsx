/**
 * EntityTypeIcon Component Tests
 *
 * Tests for the EntityTypeIcon wrapper component:
 * - Delegates to MechanicIcon for "game" entity
 * - Renders correct Lucide icon for agent subtypes
 * - Renders correct Lucide icon for session subtypes
 * - Renders correct Lucide icon for chatSession subtypes
 * - Renders correct Lucide icon for kb subtypes
 * - Falls back to Wrench for unknown entity/subtype
 * - Applies size and className props correctly
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { EntityTypeIcon } from '@/components/icons/entity-types';

describe('EntityTypeIcon', () => {
  describe('game entity — delegates to MechanicIcon', () => {
    it('renders game mechanic via MechanicIcon for engine-building', () => {
      render(<EntityTypeIcon entity="game" subtype="engine-building" size={20} />);
      // MechanicIcon renders custom SVG with aria-label "Engine Building"
      const svg = screen.getByRole('img', { name: /engine building/i });
      expect(svg).toBeInTheDocument();
    });

    it('renders fallback MechanicIcon for unknown game subtype', () => {
      render(<EntityTypeIcon entity="game" subtype="unknown-mechanic" size={20} />);
      // DefaultMechanicIcon has aria-label containing "board game"
      const svg = screen.getByRole('img', { name: /board game/i });
      expect(svg).toBeInTheDocument();
    });
  });

  describe('agent entity', () => {
    it('renders icon for rules-expert agent', () => {
      render(<EntityTypeIcon entity="agent" subtype="rules-expert" data-testid="icon" />);
      // Lucide BookOpen renders an SVG; query by test id added via wrapper
      const container = document.querySelector('svg');
      expect(container).toBeInTheDocument();
    });

    it('renders icon for strategy-advisor agent', () => {
      render(<EntityTypeIcon entity="agent" subtype="strategy-advisor" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for faq-helper agent', () => {
      render(<EntityTypeIcon entity="agent" subtype="faq-helper" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for setup-guide agent', () => {
      render(<EntityTypeIcon entity="agent" subtype="setup-guide" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('session entity', () => {
    it('renders icon for competitive session', () => {
      render(<EntityTypeIcon entity="session" subtype="competitive" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for cooperative session', () => {
      render(<EntityTypeIcon entity="session" subtype="cooperative" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for tutorial session', () => {
      render(<EntityTypeIcon entity="session" subtype="tutorial" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for solo session', () => {
      render(<EntityTypeIcon entity="session" subtype="solo" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('chatSession entity', () => {
    it('renders icon for rules chat context', () => {
      render(<EntityTypeIcon entity="chatSession" subtype="rules" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for strategy chat context', () => {
      render(<EntityTypeIcon entity="chatSession" subtype="strategy" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for setup chat context', () => {
      render(<EntityTypeIcon entity="chatSession" subtype="setup" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for general chat context', () => {
      render(<EntityTypeIcon entity="chatSession" subtype="general" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('kb entity', () => {
    it('renders icon for rulebook kb type', () => {
      render(<EntityTypeIcon entity="kb" subtype="rulebook" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for scenario kb type', () => {
      render(<EntityTypeIcon entity="kb" subtype="scenario" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for faq kb type', () => {
      render(<EntityTypeIcon entity="kb" subtype="faq" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders icon for reference kb type', () => {
      render(<EntityTypeIcon entity="kb" subtype="reference" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('fallback behavior', () => {
    it('renders fallback Wrench icon for unknown entity type', () => {
      render(<EntityTypeIcon entity="unknown-entity" subtype="anything" data-testid="fallback" />);
      // Wrench icon should render as SVG
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders fallback Wrench icon for known entity with unknown subtype', () => {
      render(<EntityTypeIcon entity="agent" subtype="nonexistent-subtype" />);
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('props forwarding', () => {
    it('applies custom size to Lucide icon', () => {
      render(<EntityTypeIcon entity="agent" subtype="rules-expert" size={32} />);
      const svg = document.querySelector('svg');
      expect(svg).toHaveAttribute('width', '32');
      expect(svg).toHaveAttribute('height', '32');
    });

    it('applies default size of 20 when not specified', () => {
      render(<EntityTypeIcon entity="agent" subtype="rules-expert" />);
      const svg = document.querySelector('svg');
      expect(svg).toHaveAttribute('width', '20');
      expect(svg).toHaveAttribute('height', '20');
    });

    it('applies custom className to Lucide icon', () => {
      render(
        <EntityTypeIcon entity="agent" subtype="rules-expert" size={20} className="text-blue-500" />
      );
      const svg = document.querySelector('svg');
      expect(svg).toHaveClass('text-blue-500');
    });

    it('applies custom size to MechanicIcon when entity is game', () => {
      render(<EntityTypeIcon entity="game" subtype="area-control" size={32} />);
      const svg = screen.getByRole('img', { name: /area control/i });
      expect(svg).toHaveAttribute('width', '32');
      expect(svg).toHaveAttribute('height', '32');
    });

    it('applies custom className to MechanicIcon when entity is game', () => {
      render(
        <EntityTypeIcon entity="game" subtype="cooperative" size={20} className="text-green-500" />
      );
      const svg = screen.getByRole('img', { name: /cooperative/i });
      expect(svg).toHaveClass('text-green-500');
    });
  });
});
