import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { ChatPanelHeader } from '../ChatPanelHeader';

describe('ChatPanelHeader', () => {
  it('renders the title and subtitle', () => {
    render(<ChatPanelHeader subtitle="KB aggiornata 2 giorni fa" onClose={() => {}} />);
    expect(screen.getByText(/Chat con l'agente/i)).toBeInTheDocument();
    expect(screen.getByText(/KB aggiornata/i)).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', async () => {
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(<ChatPanelHeader subtitle="x" onClose={onClose} />);
    await user.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalled();
  });
});
