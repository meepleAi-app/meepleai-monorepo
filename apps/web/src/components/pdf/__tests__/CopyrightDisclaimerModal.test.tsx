import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';

import { CopyrightDisclaimerModal } from '../CopyrightDisclaimerModal';

describe('CopyrightDisclaimerModal', () => {
  it('renders disclaimer text when open', () => {
    render(<CopyrightDisclaimerModal open onAccept={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByText(/possiedo una copia/i)).toBeInTheDocument();
    expect(screen.getByText(/riferimento personale/i)).toBeInTheDocument();
    expect(screen.getByText(/non verrà redistribuito/i)).toBeInTheDocument();
  });

  it('calls onAccept when confirm button clicked', async () => {
    const onAccept = vi.fn();
    const user = userEvent.setup();
    render(<CopyrightDisclaimerModal open onAccept={onAccept} onCancel={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: /confermo e carico/i }));
    expect(onAccept).toHaveBeenCalledOnce();
  });

  it('calls onCancel when cancel button clicked', async () => {
    const onCancel = vi.fn();
    const user = userEvent.setup();
    render(<CopyrightDisclaimerModal open onAccept={vi.fn()} onCancel={onCancel} />);
    await user.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('does not render when closed', () => {
    render(<CopyrightDisclaimerModal open={false} onAccept={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.queryByText(/possiedo una copia/i)).not.toBeInTheDocument();
  });
});
