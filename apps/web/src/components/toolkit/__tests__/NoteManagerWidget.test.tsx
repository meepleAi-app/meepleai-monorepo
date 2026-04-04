/**
 * Unit tests for NoteManagerWidget.
 * Issue #5156 — Epic B13.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import React from 'react';

vi.mock('@/lib/domain-hooks/useWidgetSync', () => ({
  useWidgetSync: () => ({ broadcastState: vi.fn(), isConnected: false }),
}));

import { NoteManagerWidget } from '../NoteManagerWidget';

describe('NoteManagerWidget', () => {
  it('renders private notes tab by default', () => {
    render(<NoteManagerWidget isEnabled={true} />);
    expect(screen.getByLabelText('Private notes')).toBeInTheDocument();
  });

  it('has Save Notes button', () => {
    render(<NoteManagerWidget isEnabled={true} />);
    expect(screen.getByRole('button', { name: /save notes/i })).toBeInTheDocument();
  });

  it('saves notes and calls onStateChange', async () => {
    const onStateChange = vi.fn();
    render(<NoteManagerWidget isEnabled={true} onStateChange={onStateChange} />);

    const textarea = screen.getByLabelText('Private notes');
    fireEvent.change(textarea, { target: { value: 'My secret strategy' } });
    fireEvent.click(screen.getByRole('button', { name: /save notes/i }));

    expect(onStateChange).toHaveBeenCalledTimes(1);
    expect(onStateChange).toHaveBeenCalledWith(expect.stringContaining('My secret strategy'));
  });

  it('shows Saved feedback temporarily', async () => {
    vi.useFakeTimers();
    render(<NoteManagerWidget isEnabled={true} />);

    fireEvent.click(screen.getByRole('button', { name: /save notes/i }));
    expect(screen.getByRole('button', { name: /saved/i })).toBeInTheDocument();

    await act(async () => {
      vi.advanceTimersByTime(2000);
    });
    expect(screen.getByRole('button', { name: /save notes/i })).toBeInTheDocument();
    vi.useRealTimers();
  });

  it('switches to public notes tab', () => {
    render(<NoteManagerWidget isEnabled={true} />);
    fireEvent.click(screen.getByRole('tab', { name: /public/i }));
    expect(screen.getByLabelText('Public notes')).toBeInTheDocument();
  });

  it('saves both public and private notes in state', () => {
    const onStateChange = vi.fn();
    render(<NoteManagerWidget isEnabled={true} onStateChange={onStateChange} />);

    // Private note
    fireEvent.change(screen.getByLabelText('Private notes'), {
      target: { value: 'Private text' },
    });

    // Switch to public tab
    fireEvent.click(screen.getByRole('tab', { name: /public/i }));
    fireEvent.change(screen.getByLabelText('Public notes'), {
      target: { value: 'Public text' },
    });

    fireEvent.click(screen.getByRole('button', { name: /save notes/i }));
    const saved = JSON.parse(onStateChange.mock.calls[0][0]);
    expect(saved.privateNote).toBe('Private text');
    expect(saved.publicNote).toBe('Public text');
  });
});
