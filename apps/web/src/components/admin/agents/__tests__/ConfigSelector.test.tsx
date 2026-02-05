/**
 * ConfigSelector Component Tests
 * Issue #3380
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ConfigSelector } from '../ConfigSelector';

const mockStrategies = [
  { value: 'FAST', label: 'Fast', description: 'Quick lookups', color: 'green' },
  { value: 'BALANCED', label: 'Balanced', description: 'Standard', color: 'blue' },
  { value: 'PRECISE', label: 'Precise', description: 'Complex rules', color: 'purple' },
] as const;

const mockModels = [
  { value: 'gpt-4o-mini', label: 'GPT-4o Mini', tier: 'free' },
  { value: 'claude-3-haiku', label: 'Claude 3 Haiku', tier: 'normal' },
  { value: 'claude-3-sonnet', label: 'Claude 3 Sonnet', tier: 'premium' },
] as const;

const mockConfig = {
  id: 'test-config-1',
  strategy: 'BALANCED',
  model: 'gpt-4o-mini',
};

describe('ConfigSelector', () => {
  it('renders config label correctly', () => {
    const onUpdate = vi.fn();
    render(
      <ConfigSelector
        config={mockConfig}
        index={0}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    expect(screen.getByText('A')).toBeInTheDocument();
    expect(screen.getByText('Config A')).toBeInTheDocument();
  });

  it('renders strategy selector with current value', () => {
    const onUpdate = vi.fn();
    render(
      <ConfigSelector
        config={mockConfig}
        index={1}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    expect(screen.getByText('Balanced')).toBeInTheDocument();
  });

  it('renders model selector with tier badge', () => {
    const onUpdate = vi.fn();
    render(
      <ConfigSelector
        config={mockConfig}
        index={0}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    expect(screen.getByText('GPT-4o Mini')).toBeInTheDocument();
    expect(screen.getByText('free')).toBeInTheDocument();
  });

  it('shows remove button when onRemove is provided', () => {
    const onUpdate = vi.fn();
    const onRemove = vi.fn();

    render(
      <ConfigSelector
        config={mockConfig}
        index={0}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
        onRemove={onRemove}
      />
    );

    const removeButton = screen.getByRole('button');
    expect(removeButton).toBeInTheDocument();
  });

  it('does not show remove button when onRemove is not provided', () => {
    const onUpdate = vi.fn();

    render(
      <ConfigSelector
        config={mockConfig}
        index={0}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    // Should have no buttons when onRemove is not provided
    // (Select components use combobox role, not button)
    const buttons = screen.queryAllByRole('button');
    expect(buttons.length).toBe(0);
  });

  it('calls onRemove when remove button is clicked', () => {
    const onUpdate = vi.fn();
    const onRemove = vi.fn();

    render(
      <ConfigSelector
        config={mockConfig}
        index={0}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
        onRemove={onRemove}
      />
    );

    // Find the remove button (with trash icon)
    const buttons = screen.getAllByRole('button');
    const removeButton = buttons.find(btn =>
      btn.className.includes('ghost') || btn.querySelector('svg')
    );

    if (removeButton) {
      fireEvent.click(removeButton);
      expect(onRemove).toHaveBeenCalledWith('test-config-1');
    }
  });

  it('renders correct config labels for different indices', () => {
    const onUpdate = vi.fn();

    const { rerender } = render(
      <ConfigSelector
        config={mockConfig}
        index={0}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    expect(screen.getByText('A')).toBeInTheDocument();

    rerender(
      <ConfigSelector
        config={mockConfig}
        index={1}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    expect(screen.getByText('B')).toBeInTheDocument();

    rerender(
      <ConfigSelector
        config={mockConfig}
        index={2}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    expect(screen.getByText('C')).toBeInTheDocument();
  });

  it('shows strategy description', () => {
    const onUpdate = vi.fn();

    render(
      <ConfigSelector
        config={mockConfig}
        index={0}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    expect(screen.getByText('Standard')).toBeInTheDocument();
  });

  it('applies correct color classes based on strategy', () => {
    const onUpdate = vi.fn();

    const { container } = render(
      <ConfigSelector
        config={{ ...mockConfig, strategy: 'FAST' }}
        index={0}
        strategies={mockStrategies}
        models={mockModels}
        onUpdate={onUpdate}
      />
    );

    const card = container.querySelector('.border-green-500\\/50');
    expect(card).toBeInTheDocument();
  });
});
