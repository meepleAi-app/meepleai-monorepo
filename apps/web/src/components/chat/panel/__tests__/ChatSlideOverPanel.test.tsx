import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, beforeEach } from 'vitest';

import { useChatPanelStore } from '@/lib/stores/chat-panel-store';

import { ChatSlideOverPanel } from '../ChatSlideOverPanel';

describe('ChatSlideOverPanel', () => {
  beforeEach(() => {
    useChatPanelStore.getState().close();
    useChatPanelStore.getState().clearGameContext();
  });

  it('renders nothing when panel is closed', () => {
    const { container } = render(<ChatSlideOverPanel />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders panel when open', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);
    expect(screen.getByText(/Chat con l'agente/i)).toBeInTheDocument();
  });

  it('renders game context in switcher when set', () => {
    useChatPanelStore.getState().open({
      id: 'azul',
      name: 'Azul',
      year: 2017,
      pdfCount: 3,
      kbStatus: 'ready',
    });
    render(<ChatSlideOverPanel />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('closes when the ✕ header button is clicked', async () => {
    useChatPanelStore.getState().open();
    const user = userEvent.setup();
    render(<ChatSlideOverPanel />);
    await user.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(useChatPanelStore.getState().isOpen).toBe(false);
  });

  it('closes on Esc keypress', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true });
    window.dispatchEvent(event);
    expect(useChatPanelStore.getState().isOpen).toBe(false);
  });
});
