/**
 * DocumentTypeSelector Tests - Issue #2764 Sprint 4
 *
 * Tests for DocumentTypeSelector component:
 * - Dropdown select for choosing document type
 * - Color-coded badges in dropdown
 * - Description for each document type
 * - Disabled state support
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 79 lines (100%)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { DocumentTypeSelector, type DocumentType } from '../DocumentTypeSelector';

describe('DocumentTypeSelector - Issue #2764 Sprint 4', () => {
  // ============================================================================
  // TEST 1: Default rendering with initial value
  // ============================================================================
  it('should render with initial value displayed', () => {
    // Arrange & Act
    render(<DocumentTypeSelector value="base" onChange={vi.fn()} />);

    // Assert - Should show the current value via DocumentBadge
    expect(screen.getByText('Base Rules')).toBeInTheDocument();
    expect(screen.getByRole('combobox')).toHaveAttribute('aria-label', 'Select document type');
  });

  // ============================================================================
  // TEST 2: Display expansion type
  // ============================================================================
  it('should display expansion type when selected', () => {
    // Arrange & Act
    render(<DocumentTypeSelector value="expansion" onChange={vi.fn()} />);

    // Assert
    expect(screen.getByText('Expansion')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Display errata type
  // ============================================================================
  it('should display errata type when selected', () => {
    // Arrange & Act
    render(<DocumentTypeSelector value="errata" onChange={vi.fn()} />);

    // Assert
    expect(screen.getByText('Errata')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: Display homerule type
  // ============================================================================
  it('should display homerule type when selected', () => {
    // Arrange & Act
    render(<DocumentTypeSelector value="homerule" onChange={vi.fn()} />);

    // Assert
    expect(screen.getByText('House Rule')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: Open dropdown and show all options
  // ============================================================================
  it('should show all document type options when dropdown is opened', async () => {
    // Arrange
    const user = userEvent.setup();
    render(<DocumentTypeSelector value="base" onChange={vi.fn()} />);

    // Act - Click to open dropdown
    const trigger = screen.getByRole('combobox');
    await user.click(trigger);

    // Assert - All options should be visible
    // Note: DocumentBadge renders labels, so we look for descriptions
    expect(screen.getByText('Core rulebook for the game')).toBeInTheDocument();
    expect(screen.getByText('Additional rules for expansions')).toBeInTheDocument();
    expect(screen.getByText('Official corrections and clarifications')).toBeInTheDocument();
    expect(screen.getByText('Custom variant rules')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 6: Call onChange when option selected
  // ============================================================================
  it('should call onChange when a different option is selected', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnChange = vi.fn();
    render(<DocumentTypeSelector value="base" onChange={mockOnChange} />);

    // Act - Open dropdown and select expansion
    const trigger = screen.getByRole('combobox');
    await user.click(trigger);

    // Find and click the expansion option
    const expansionOption = screen.getByText('Additional rules for expansions');
    await user.click(expansionOption);

    // Assert
    expect(mockOnChange).toHaveBeenCalledWith('expansion');
  });

  // ============================================================================
  // TEST 7: Disabled state
  // ============================================================================
  it('should be disabled when disabled prop is true', () => {
    // Arrange & Act
    render(<DocumentTypeSelector value="base" onChange={vi.fn()} disabled />);

    // Assert
    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeDisabled();
  });

  // ============================================================================
  // TEST 8: Disabled state prevents interaction
  // ============================================================================
  it('should not open dropdown when disabled', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnChange = vi.fn();
    render(<DocumentTypeSelector value="base" onChange={mockOnChange} disabled />);

    // Act - Try to click disabled dropdown
    const trigger = screen.getByRole('combobox');
    await user.click(trigger);

    // Assert - Dropdown should not open (no descriptions visible)
    expect(screen.queryByText('Core rulebook for the game')).not.toBeInTheDocument();
    expect(mockOnChange).not.toHaveBeenCalled();
  });

  // ============================================================================
  // TEST 9: Custom className support
  // ============================================================================
  it('should apply custom className to trigger', () => {
    // Arrange & Act
    render(<DocumentTypeSelector value="base" onChange={vi.fn()} className="custom-width" />);

    // Assert
    const trigger = screen.getByRole('combobox');
    expect(trigger).toHaveClass('custom-width');
  });

  // ============================================================================
  // TEST 10: All document types are selectable
  // ============================================================================
  it('should allow selecting all document types', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnChange = vi.fn();
    const types: DocumentType[] = ['base', 'expansion', 'errata', 'homerule'];
    const descriptions = [
      'Core rulebook for the game',
      'Additional rules for expansions',
      'Official corrections and clarifications',
      'Custom variant rules',
    ];

    // Start with base type
    const { rerender } = render(<DocumentTypeSelector value="base" onChange={mockOnChange} />);

    for (let i = 1; i < types.length; i++) {
      // Act - Open dropdown and select next type
      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      const option = screen.getByText(descriptions[i]);
      await user.click(option);

      // Assert
      expect(mockOnChange).toHaveBeenLastCalledWith(types[i]);

      // Update the value for next iteration
      rerender(<DocumentTypeSelector value={types[i]} onChange={mockOnChange} />);
    }
  });
});
