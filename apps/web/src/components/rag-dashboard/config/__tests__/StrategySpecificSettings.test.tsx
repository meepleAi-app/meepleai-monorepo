/**
 * @fileoverview Tests for StrategySpecificSettings component
 * @description Tests strategy-specific parameter controls
 */

import React from 'react';

import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { StrategySpecificSettings } from '../StrategySpecificSettings';
import type {
  StrategySpecificSettings as StrategySpecificSettingsType,
  RetrievalStrategyType,
} from '../types';

describe('StrategySpecificSettings', () => {
  const defaultSettings: StrategySpecificSettingsType = {
    hybridAlpha: 0.5,
    contextWindow: 5,
    maxHops: 3,
  };

  const mockOnChange = vi.fn();

  beforeEach(() => {
    mockOnChange.mockClear();
  });

  describe('when activeStrategy is Hybrid', () => {
    it('renders Hybrid Alpha control', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Hybrid"
        />
      );

      expect(screen.getByText('Hybrid Alpha')).toBeInTheDocument();
    });

    it('displays current Hybrid Alpha value', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Hybrid"
        />
      );

      expect(screen.getByText('0.50')).toBeInTheDocument();
    });

    it('shows helper text for Hybrid Alpha', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Hybrid"
        />
      );

      expect(screen.getByText('Keyword (BM25)')).toBeInTheDocument();
      expect(screen.getByText('Vector (Semantic)')).toBeInTheDocument();
    });

    it('does not render other controls', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Hybrid"
        />
      );

      expect(screen.queryByText('Context Window')).not.toBeInTheDocument();
      expect(screen.queryByText('Max Hops')).not.toBeInTheDocument();
    });
  });

  describe('when activeStrategy is Contextual', () => {
    it('renders Context Window control', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Contextual"
        />
      );

      expect(screen.getByText('Context Window')).toBeInTheDocument();
    });

    it('displays current Context Window value', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Contextual"
        />
      );

      expect(screen.getByDisplayValue('5')).toBeInTheDocument();
    });

    it('shows helper text for Context Window', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Contextual"
        />
      );

      expect(screen.getByText(/Include last 5 messages/)).toBeInTheDocument();
    });

    it('calls onChange when Context Window changes', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Contextual"
        />
      );

      const input = screen.getByDisplayValue('5');
      fireEvent.change(input, { target: { value: '10' } });

      const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
      expect(lastCall.contextWindow).toBe(10);
    });

    it('clamps Context Window to valid range (1-20)', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Contextual"
        />
      );

      const input = screen.getByDisplayValue('5');
      fireEvent.change(input, { target: { value: '25' } });

      const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
      expect(lastCall.contextWindow).toBe(20);
    });
  });

  describe('when activeStrategy is Agentic', () => {
    it('renders Max Hops control', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Agentic"
        />
      );

      expect(screen.getByText('Max Hops')).toBeInTheDocument();
    });

    it('displays current Max Hops value', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Agentic"
        />
      );

      expect(screen.getByDisplayValue('3')).toBeInTheDocument();
    });

    it('shows helper text for Max Hops', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Agentic"
        />
      );

      expect(screen.getByText(/Maximum 3 retrieval iterations/)).toBeInTheDocument();
    });

    it('calls onChange when Max Hops changes', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Agentic"
        />
      );

      const input = screen.getByDisplayValue('3');
      fireEvent.change(input, { target: { value: '5' } });

      const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
      expect(lastCall.maxHops).toBe(5);
    });

    it('clamps Max Hops to valid range (1-10)', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Agentic"
        />
      );

      const input = screen.getByDisplayValue('3');
      fireEvent.change(input, { target: { value: '15' } });

      const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
      expect(lastCall.maxHops).toBe(10);
    });
  });

  describe('when activeStrategy is MultiQuery', () => {
    it('renders Max Hops control', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="MultiQuery"
        />
      );

      expect(screen.getByText('Max Hops')).toBeInTheDocument();
    });
  });

  describe('when activeStrategy is Semantic', () => {
    it('shows no strategy-specific settings message', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Semantic"
        />
      );

      expect(screen.getByText(/No strategy-specific settings/)).toBeInTheDocument();
      expect(screen.getByText('Semantic')).toBeInTheDocument();
    });
  });

  describe('when activeStrategy is Keyword', () => {
    it('shows no strategy-specific settings message', () => {
      render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy="Keyword"
        />
      );

      expect(screen.getByText(/No strategy-specific settings/)).toBeInTheDocument();
      expect(screen.getByText('Keyword')).toBeInTheDocument();
    });
  });

  it('applies custom className', () => {
    const { container } = render(
      <StrategySpecificSettings
        settings={defaultSettings}
        onChange={mockOnChange}
        activeStrategy="Hybrid"
        className="custom-class"
      />
    );

    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('shows tooltips for controls', () => {
    render(
      <StrategySpecificSettings
        settings={defaultSettings}
        onChange={mockOnChange}
        activeStrategy="Hybrid"
      />
    );

    const label = screen.getByText('Hybrid Alpha').closest('label');
    expect(label).toHaveClass('cursor-help');
  });

  it('preserves other settings when one changes', async () => {
    const user = userEvent.setup();
    render(
      <StrategySpecificSettings
        settings={defaultSettings}
        onChange={mockOnChange}
        activeStrategy="Contextual"
      />
    );

    const input = screen.getByDisplayValue('5');
    await user.clear(input);
    await user.type(input, '8');
    fireEvent.blur(input);

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.hybridAlpha).toBe(0.5);
    expect(lastCall.maxHops).toBe(3);
  });

  it('renders icons for controls', () => {
    render(
      <StrategySpecificSettings
        settings={defaultSettings}
        onChange={mockOnChange}
        activeStrategy="Hybrid"
      />
    );

    const svgs = document.querySelectorAll('svg');
    expect(svgs.length).toBeGreaterThanOrEqual(1);
  });

  it('handles all strategy types correctly', () => {
    const strategies: RetrievalStrategyType[] = [
      'Hybrid',
      'Semantic',
      'Keyword',
      'Contextual',
      'MultiQuery',
      'Agentic',
    ];

    strategies.forEach((strategy) => {
      const { unmount } = render(
        <StrategySpecificSettings
          settings={defaultSettings}
          onChange={mockOnChange}
          activeStrategy={strategy}
        />
      );

      // Should render without error for each strategy
      expect(document.querySelector('div')).toBeInTheDocument();
      unmount();
    });
  });
});
