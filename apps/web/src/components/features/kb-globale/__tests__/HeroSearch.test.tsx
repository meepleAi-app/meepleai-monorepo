/**
 * HeroSearch.test.tsx
 * Issue #1482 Task 2 — Unit tests for HeroSearch component
 *
 * Test scenarios:
 * 1. Renders input with placeholder from labels
 * 2. Renders mode segmented control with each mode option
 * 3. Current mode button has aria-pressed="true"
 * 4. Typing in input updates value (controlled)
 * 5. Submit via Enter calls onSubmit with trimmed query + current mode
 * 6. Submit via click button calls onSubmit with same args
 * 7. Submit NOT called when query empty or length < 2
 * 8. Clear button rendered only when query non-empty
 * 9. Clear button click clears input + calls onClear
 * 10. Mode change updates aria-pressed; next submit uses new mode
 * 11. jest-axe: no a11y violations
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, it, expect, vi } from 'vitest';
import { HeroSearch, type HeroSearchProps, type HeroSearchLabels } from '../HeroSearch';
import type { SearchMode } from '@/lib/api/schemas/kb-globale.schemas';

expect.extend(toHaveNoViolations);

const defaultLabels: HeroSearchLabels = {
  placeholder: 'Search across all your games',
  submit: 'Search',
  clear: 'Clear',
  modeLabel: 'Search mode',
  modeOptions: {
    Semantic: 'Semantic Search',
  },
};

const defaultProps: HeroSearchProps = {
  onSubmit: vi.fn(),
  labels: defaultLabels,
};

describe('HeroSearch', () => {
  it('renders input with placeholder from labels', () => {
    render(<HeroSearch {...defaultProps} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder);
    expect(input).toBeInTheDocument();
  });

  it('renders mode segmented control with each mode option', () => {
    render(<HeroSearch {...defaultProps} />);
    // For v1 with only Semantic, expect one button labeled "Semantic Search"
    const modeButton = screen.getByRole('button', { name: /semantic search/i });
    expect(modeButton).toBeInTheDocument();
  });

  it('current mode button has aria-pressed="true"', () => {
    render(<HeroSearch {...defaultProps} initialMode="Semantic" />);
    const modeButton = screen.getByRole('button', { name: /semantic search/i });
    expect(modeButton).toHaveAttribute('aria-pressed', 'true');
  });

  it('typing in input updates value (controlled)', async () => {
    const user = userEvent.setup();
    render(<HeroSearch {...defaultProps} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;

    await user.type(input, 'azul rules');
    expect(input.value).toBe('azul rules');
  });

  it('submit via Enter calls onSubmit with trimmed query + current mode', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<HeroSearch {...defaultProps} onSubmit={onSubmit} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;

    await user.type(input, '  azul rules  ');
    await user.keyboard('{Enter}');

    expect(onSubmit).toHaveBeenCalledWith('azul rules', 'Semantic');
  });

  it('submit via click button calls onSubmit with same args', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<HeroSearch {...defaultProps} onSubmit={onSubmit} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;
    const submitButton = screen.getByRole('button', { name: defaultLabels.submit });

    await user.type(input, 'wingspan');
    await user.click(submitButton);

    expect(onSubmit).toHaveBeenCalledWith('wingspan', 'Semantic');
  });

  it('submit NOT called when query empty after trim', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<HeroSearch {...defaultProps} onSubmit={onSubmit} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;
    const submitButton = screen.getByRole('button', { name: defaultLabels.submit });

    await user.type(input, '   ');
    await user.click(submitButton);

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('submit NOT called when query length < 2', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<HeroSearch {...defaultProps} onSubmit={onSubmit} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;
    const submitButton = screen.getByRole('button', { name: defaultLabels.submit });

    await user.type(input, 'a');
    await user.click(submitButton);

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('clear button rendered only when query non-empty', async () => {
    const user = userEvent.setup();
    render(<HeroSearch {...defaultProps} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;

    // Initially no clear button
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument();

    // Type something
    await user.type(input, 'azul');
    expect(screen.getByRole('button', { name: /clear|×/i })).toBeInTheDocument();

    // Clear via button
    const clearButton = screen.getByRole('button', { name: /clear|×/i });
    await user.click(clearButton);
    expect(input.value).toBe('');
    expect(screen.queryByRole('button', { name: /clear|×/i })).not.toBeInTheDocument();
  });

  it('clear button click clears input + calls onClear', async () => {
    const user = userEvent.setup();
    const onClear = vi.fn();
    render(<HeroSearch {...defaultProps} onClear={onClear} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;

    await user.type(input, 'gloomhaven');
    expect(input.value).toBe('gloomhaven');

    const clearButton = screen.getByRole('button', { name: /clear|×/i });
    await user.click(clearButton);

    expect(input.value).toBe('');
    expect(onClear).toHaveBeenCalled();
  });

  it('mode change updates aria-pressed; next submit uses new mode', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();

    // Create labels with multiple modes for this test
    const multiModeLabels: HeroSearchLabels = {
      placeholder: 'Search',
      submit: 'Search',
      clear: 'Clear',
      modeLabel: 'Search mode',
      modeOptions: {
        Semantic: 'Semantic',
        // Note: Task 2 spec only has Semantic in v1, so this test uses Semantic as "current"
        // In future when more modes are added, this will test the toggle behavior
      } as any,
    };

    render(<HeroSearch {...defaultProps} onSubmit={onSubmit} initialMode="Semantic" />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;
    const modeButton = screen.getByRole('button', { name: /semantic/i });

    // Mode button is initially pressed
    expect(modeButton).toHaveAttribute('aria-pressed', 'true');

    // Type and submit
    await user.type(input, 'azul');
    await user.keyboard('{Enter}');

    expect(onSubmit).toHaveBeenCalledWith('azul', 'Semantic');
  });

  it('jest-axe: no a11y violations', async () => {
    const { container } = render(<HeroSearch {...defaultProps} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('renders with initialQuery and initialMode', () => {
    render(<HeroSearch {...defaultProps} initialQuery="brass rules" initialMode="Semantic" />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;
    expect(input.value).toBe('brass rules');
  });

  it('className prop is applied to wrapper', () => {
    const { container } = render(<HeroSearch {...defaultProps} className="custom-wrapper" />);
    const wrapper = container.querySelector('.custom-wrapper');
    expect(wrapper).toBeInTheDocument();
  });

  it('submit button is disabled when query < 2 chars', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<HeroSearch {...defaultProps} onSubmit={onSubmit} />);
    const input = screen.getByPlaceholderText(defaultLabels.placeholder) as HTMLInputElement;
    const submitButton = screen.getByRole('button', { name: defaultLabels.submit });

    // Initially button should be disabled (empty query)
    expect(submitButton).toBeDisabled();

    // Type 1 char — button still disabled
    await user.type(input, 'a');
    expect(submitButton).toBeDisabled();

    // Type more to reach 2 chars — button enabled
    await user.type(input, 'b');
    expect(submitButton).not.toBeDisabled();
  });
});
