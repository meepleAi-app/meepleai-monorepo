import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { AcknowledgeSelectedModal } from '../AcknowledgeSelectedModal';

describe('AcknowledgeSelectedModal', () => {
  it('renders count and submits with note', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    const onCancel = vi.fn();

    render(
      <AcknowledgeSelectedModal open selectedCount={3} onConfirm={onConfirm} onCancel={onCancel} />
    );

    expect(screen.getByText(/Acknowledge 3 dead-letter row/i)).toBeInTheDocument();
    const textarea = screen.getByLabelText(/note/i) as HTMLTextAreaElement;
    fireEvent.change(textarea, { target: { value: 'commons deleted file' } });
    fireEvent.click(screen.getByRole('button', { name: /^Confirm$/i }));

    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith('commons deleted file'));
  });

  it('cancel returns control without note submission', () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    render(
      <AcknowledgeSelectedModal open selectedCount={1} onConfirm={onConfirm} onCancel={onCancel} />
    );

    fireEvent.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalled();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('enforces 500-char note limit on textarea maxLength', () => {
    render(
      <AcknowledgeSelectedModal open selectedCount={1} onConfirm={vi.fn()} onCancel={vi.fn()} />
    );

    const textarea = screen.getByLabelText(/note/i) as HTMLTextAreaElement;
    expect(textarea.maxLength).toBe(500);
  });

  it('singular grammar for selectedCount=1', () => {
    render(
      <AcknowledgeSelectedModal open selectedCount={1} onConfirm={vi.fn()} onCancel={vi.fn()} />
    );

    expect(screen.getByText(/Acknowledge 1 dead-letter row\?/i)).toBeInTheDocument();
  });

  it('passes null instead of empty string when textarea is empty', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(
      <AcknowledgeSelectedModal open selectedCount={1} onConfirm={onConfirm} onCancel={vi.fn()} />
    );

    fireEvent.click(screen.getByRole('button', { name: /^Confirm$/i }));

    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith(null));
  });

  it('does not render when open is false', () => {
    render(
      <AcknowledgeSelectedModal
        open={false}
        selectedCount={1}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
