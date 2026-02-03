/**
 * AgentTypeSwitcher Component Tests
 * Issue #3249: [FRONT-013] Agent Type Switcher & Dynamic Typology
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AgentTypeSwitcher } from '../AgentTypeSwitcher';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

// Mock typologies for testing
const mockTypologies: Typology[] = [
  {
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
  },
  {
    id: 'typology-2',
    name: 'Strategy Coach',
    description: 'Strategic advice and tips',
    basePrompt: 'You are a strategy coach...',
    defaultStrategyName: 'VectorOnly',
    defaultStrategyParameters: null,
    status: 'Approved',
    createdBy: 'user-1',
    approvedBy: 'admin-1',
    createdAt: '2026-01-01T00:00:00Z',
    approvedAt: '2026-01-02T00:00:00Z',
    isDeleted: false,
  },
  {
    id: 'typology-3',
    name: 'Setup Assistant',
    description: 'Help with game setup',
    basePrompt: 'You are a setup assistant...',
    defaultStrategyName: 'KeywordOnly',
    defaultStrategyParameters: null,
    status: 'Approved',
    createdBy: 'user-1',
    approvedBy: 'admin-1',
    createdAt: '2026-01-01T00:00:00Z',
    approvedAt: '2026-01-02T00:00:00Z',
    isDeleted: false,
  },
];

describe('AgentTypeSwitcher', () => {
  const mockOnSwitch = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders current typology name', () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
      />
    );

    expect(screen.getByText('Rules Expert')).toBeInTheDocument();
  });

  it('shows loading state when isLoading is true', () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
        isLoading={true}
      />
    );

    expect(screen.getByText('Switching...')).toBeInTheDocument();
  });

  it('displays correct icon for rules typology', () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
      />
    );

    // Rules Expert should have book icon
    expect(screen.getByText('📖')).toBeInTheDocument();
  });

  it('calls onSwitch when selecting different typology', async () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
      />
    );

    // Open dropdown by clicking trigger
    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    // Select different typology
    const strategyOption = await screen.findByText('Strategy Coach');
    fireEvent.click(strategyOption);

    expect(mockOnSwitch).toHaveBeenCalledWith('typology-2');
  });

  it('does not call onSwitch when selecting same typology', async () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
      />
    );

    // Open dropdown
    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    // Select same typology - use getAllByText since text appears in trigger and dropdown
    const rulesOptions = await screen.findAllByText('Rules Expert');
    // Click the one in the dropdown (second one)
    fireEvent.click(rulesOptions[1]);

    expect(mockOnSwitch).not.toHaveBeenCalled();
  });

  it('is disabled when disabled prop is true', () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
        disabled={true}
      />
    );

    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeDisabled();
  });

  it('is disabled when isLoading is true', () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
        isLoading={true}
      />
    );

    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeDisabled();
  });

  it('displays strategy badge in dropdown options', async () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
      />
    );

    // Open dropdown
    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    // Strategy names should appear in badges
    expect(await screen.findByText('HybridSearch')).toBeInTheDocument();
    expect(await screen.findByText('VectorOnly')).toBeInTheDocument();
  });

  it('renders dropdown with all available typologies when opened', async () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
      />
    );

    // Open dropdown
    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    // Wait for dropdown content to appear
    const rulesOptions = await screen.findAllByText('Rules Expert');
    // Should have at least 2: one in trigger, one in dropdown
    expect(rulesOptions.length).toBeGreaterThanOrEqual(2);

    // Other typologies should appear
    expect(await screen.findByText('Strategy Coach')).toBeInTheDocument();
    expect(await screen.findByText('Setup Assistant')).toBeInTheDocument();
  });

  it('displays typology descriptions in dropdown', async () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
      />
    );

    // Open dropdown
    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    // Descriptions should be visible
    expect(await screen.findByText('Get answers to rule questions')).toBeInTheDocument();
    expect(await screen.findByText('Strategic advice and tips')).toBeInTheDocument();
  });

  it('renders all available typologies in dropdown', async () => {
    render(
      <AgentTypeSwitcher
        currentTypology={mockTypologies[0]}
        availableTypologies={mockTypologies}
        onSwitch={mockOnSwitch}
      />
    );

    // Open dropdown
    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    // All typologies should be listed (Rules Expert appears twice - in trigger and dropdown)
    const rulesOptions = await screen.findAllByText('Rules Expert');
    expect(rulesOptions.length).toBeGreaterThanOrEqual(1);
    expect(await screen.findByText('Strategy Coach')).toBeInTheDocument();
    expect(await screen.findByText('Setup Assistant')).toBeInTheDocument();
  });
});
