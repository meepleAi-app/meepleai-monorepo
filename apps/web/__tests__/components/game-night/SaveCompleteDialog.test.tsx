/**
 * SaveCompleteDialog Component Tests
 *
 * Issue #122 — Enhanced Save/Resume: notes, photo upload, AI summary
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { SaveCompleteDialog } from '@/components/game-night/SaveCompleteDialog';

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      saveComplete: vi.fn().mockResolvedValue({ recap: 'test', snapshotIndex: 1, photoCount: 0 }),
    },
  },
}));

describe('SaveCompleteDialog', () => {
  const props = {
    open: true,
    onOpenChange: vi.fn(),
    sessionId: 'session-1',
    onSaveComplete: vi.fn(),
  };

  it('renders notes textarea', () => {
    render(<SaveCompleteDialog {...props} />);
    expect(screen.getByPlaceholderText(/note/i)).toBeInTheDocument();
  });

  it('renders photo upload button', () => {
    render(<SaveCompleteDialog {...props} />);
    expect(screen.getByText(/foto/i)).toBeInTheDocument();
  });

  it('renders AI summary button', () => {
    render(<SaveCompleteDialog {...props} />);
    expect(screen.getByRole('button', { name: /riepilogo/i })).toBeInTheDocument();
  });

  it('renders save button', () => {
    render(<SaveCompleteDialog {...props} />);
    expect(screen.getByTestId('save-complete-confirm')).toBeInTheDocument();
  });

  it('allows typing in notes textarea', () => {
    render(<SaveCompleteDialog {...props} />);
    const textarea = screen.getByPlaceholderText(/note/i);
    fireEvent.change(textarea, { target: { value: 'Ottima partita!' } });
    expect(textarea).toHaveValue('Ottima partita!');
  });

  it('generates AI summary on button click', async () => {
    render(<SaveCompleteDialog {...props} />);
    const aiButton = screen.getByRole('button', { name: /riepilogo/i });
    fireEvent.click(aiButton);
    // The placeholder summary should appear
    expect(await screen.findByTestId('ai-summary')).toBeInTheDocument();
  });
});
