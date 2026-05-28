/**
 * Issue #1483 — DiscoverSearchBox unit tests.
 *
 * Controlled search input with 300ms debounce on idle + immediate commit on Enter.
 * Supports a disabled-shell variant where the input is read-only and focus emits
 * a telemetry event via onDisabledFocus.
 *
 * Contract:
 *   - data-slot="discover-search-box" on the input element
 *   - value syncs from external prop via useEffect (re-render)
 *   - placeholder text is shown and used as aria-label
 *   - enabled: onCommit is called on Enter key (immediately, no wait)
 *   - enabled: input typing changes draft state
 *   - disabled: input is readOnly, aria-disabled is set
 *   - disabled: title attribute shows disabledTooltip
 *   - disabled: onDisabledFocus is called on focus
 *   - disabled: onCommit is NOT called on Enter
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { DiscoverSearchBox } from '../DiscoverSearchBox';

describe('DiscoverSearchBox', () => {
  it('renders data-slot="discover-search-box" on the input', () => {
    const { container } = render(
      <DiscoverSearchBox value="" onCommit={vi.fn()} placeholder="Cerca giochi..." />
    );
    expect(container.querySelector('[data-slot="discover-search-box"]')).not.toBeNull();
  });

  it('renders with placeholder and uses it as aria-label', () => {
    render(<DiscoverSearchBox value="" onCommit={vi.fn()} placeholder="Cerca giochi..." />);
    const input = screen.getByRole('searchbox', { name: 'Cerca giochi...' });
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute('placeholder', 'Cerca giochi...');
  });

  it('displays the current committed value in the input', () => {
    render(<DiscoverSearchBox value="Catan" onCommit={vi.fn()} placeholder="Cerca..." />);
    expect(screen.getByRole('searchbox')).toHaveValue('Catan');
  });

  it('syncs draft when external value prop changes (rerender)', () => {
    const { rerender } = render(
      <DiscoverSearchBox value="" onCommit={vi.fn()} placeholder="Cerca..." />
    );
    expect(screen.getByRole('searchbox')).toHaveValue('');
    rerender(<DiscoverSearchBox value="Wingspan" onCommit={vi.fn()} placeholder="Cerca..." />);
    expect(screen.getByRole('searchbox')).toHaveValue('Wingspan');
  });

  it('calls onCommit immediately when Enter is pressed (enabled)', () => {
    const onCommit = vi.fn();
    render(<DiscoverSearchBox value="" onCommit={onCommit} placeholder="Cerca..." />);
    const input = screen.getByRole('searchbox');
    fireEvent.change(input, { target: { value: 'Catan' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onCommit).toHaveBeenCalledWith('Catan');
  });

  it('does not call onCommit on Enter when disabled', () => {
    const onCommit = vi.fn();
    render(<DiscoverSearchBox value="" onCommit={onCommit} placeholder="Cerca..." disabled />);
    const input = screen.getByRole('searchbox');
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onCommit).not.toHaveBeenCalled();
  });

  it('is readOnly and aria-disabled when disabled=true', () => {
    render(<DiscoverSearchBox value="" onCommit={vi.fn()} placeholder="Cerca..." disabled />);
    const input = screen.getByRole('searchbox');
    expect(input).toHaveAttribute('readonly');
    expect(input).toHaveAttribute('aria-disabled', 'true');
  });

  it('shows disabledTooltip as title attribute when disabled', () => {
    render(
      <DiscoverSearchBox
        value=""
        onCommit={vi.fn()}
        placeholder="Cerca..."
        disabled
        disabledTooltip="Ricerca non disponibile"
      />
    );
    expect(screen.getByRole('searchbox')).toHaveAttribute('title', 'Ricerca non disponibile');
  });

  it('calls onDisabledFocus when disabled input receives focus', () => {
    const onDisabledFocus = vi.fn();
    render(
      <DiscoverSearchBox
        value=""
        onCommit={vi.fn()}
        placeholder="Cerca..."
        disabled
        onDisabledFocus={onDisabledFocus}
      />
    );
    fireEvent.focus(screen.getByRole('searchbox'));
    expect(onDisabledFocus).toHaveBeenCalledTimes(1);
  });

  it('does not call onDisabledFocus when enabled input receives focus', () => {
    const onDisabledFocus = vi.fn();
    render(
      <DiscoverSearchBox
        value=""
        onCommit={vi.fn()}
        placeholder="Cerca..."
        onDisabledFocus={onDisabledFocus}
      />
    );
    fireEvent.focus(screen.getByRole('searchbox'));
    expect(onDisabledFocus).not.toHaveBeenCalled();
  });

  it('does not have title attribute when enabled (no tooltip)', () => {
    render(<DiscoverSearchBox value="" onCommit={vi.fn()} placeholder="Cerca..." />);
    const input = screen.getByRole('searchbox');
    expect(input).not.toHaveAttribute('title');
  });

  it('typing updates draft value visually (onChange fires)', () => {
    render(<DiscoverSearchBox value="" onCommit={vi.fn()} placeholder="Cerca..." />);
    const input = screen.getByRole('searchbox');
    fireEvent.change(input, { target: { value: 'Risk' } });
    expect(input).toHaveValue('Risk');
  });
});
