/**
 * SessionChatWidget Component Tests
 * Issue #5587 — Live Game Session UI
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { SessionChatWidget, type ChatMessage } from '../SessionChatWidget';

describe('SessionChatWidget', () => {
  const messages: ChatMessage[] = [
    { id: '1', role: 'user', content: 'Come funziona il commercio?', timestamp: new Date() },
    {
      id: '2',
      role: 'assistant',
      content: 'Il commercio permette di scambiare risorse...',
      timestamp: new Date(),
    },
  ];

  const defaultProps = {
    messages: [],
    onSend: vi.fn(),
  };

  it('should render the chat toggle button', () => {
    render(<SessionChatWidget {...defaultProps} />);
    expect(screen.getByTestId('chat-toggle')).toBeInTheDocument();
    expect(screen.getByText('Chat regole')).toBeInTheDocument();
  });

  it('should start collapsed by default', () => {
    render(<SessionChatWidget {...defaultProps} />);
    expect(screen.queryByTestId('chat-messages')).not.toBeInTheDocument();
    expect(screen.queryByTestId('chat-input')).not.toBeInTheDocument();
  });

  it('should expand when toggle is clicked', () => {
    render(<SessionChatWidget {...defaultProps} />);
    fireEvent.click(screen.getByTestId('chat-toggle'));
    expect(screen.getByTestId('chat-messages')).toBeInTheDocument();
    expect(screen.getByTestId('chat-input')).toBeInTheDocument();
  });

  it('should collapse when toggle is clicked again', () => {
    render(<SessionChatWidget {...defaultProps} />);
    // Expand
    fireEvent.click(screen.getByTestId('chat-toggle'));
    expect(screen.getByTestId('chat-messages')).toBeInTheDocument();
    // Collapse
    fireEvent.click(screen.getByTestId('chat-toggle'));
    expect(screen.queryByTestId('chat-messages')).not.toBeInTheDocument();
  });

  it('should render defaultExpanded=true', () => {
    render(<SessionChatWidget {...defaultProps} defaultExpanded={true} />);
    expect(screen.getByTestId('chat-messages')).toBeInTheDocument();
  });

  it('should display messages when expanded', () => {
    render(<SessionChatWidget {...defaultProps} messages={messages} defaultExpanded={true} />);
    expect(screen.getByText('Come funziona il commercio?')).toBeInTheDocument();
    expect(screen.getByText('Il commercio permette di scambiare risorse...')).toBeInTheDocument();
  });

  it('should show empty state when no messages', () => {
    render(<SessionChatWidget {...defaultProps} defaultExpanded={true} />);
    expect(screen.getByText('Chiedi una regola per iniziare...')).toBeInTheDocument();
  });

  it('should call onSend when form is submitted', () => {
    const onSend = vi.fn();
    render(<SessionChatWidget {...defaultProps} onSend={onSend} defaultExpanded={true} />);

    const input = screen.getByTestId('chat-input');
    fireEvent.change(input, { target: { value: 'Quante risorse?' } });
    fireEvent.click(screen.getByTestId('chat-send'));

    expect(onSend).toHaveBeenCalledWith('Quante risorse?');
  });

  it('should not send empty messages', () => {
    const onSend = vi.fn();
    render(<SessionChatWidget {...defaultProps} onSend={onSend} defaultExpanded={true} />);

    fireEvent.click(screen.getByTestId('chat-send'));
    expect(onSend).not.toHaveBeenCalled();
  });

  it('should clear input after sending', () => {
    const onSend = vi.fn();
    render(<SessionChatWidget {...defaultProps} onSend={onSend} defaultExpanded={true} />);

    const input = screen.getByTestId('chat-input') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'Test message' } });
    fireEvent.click(screen.getByTestId('chat-send'));

    expect(input.value).toBe('');
  });

  it('should show streaming indicator', () => {
    render(<SessionChatWidget {...defaultProps} isStreaming={true} defaultExpanded={true} />);
    expect(screen.getByText('Sta rispondendo...')).toBeInTheDocument();
  });

  it('should disable send button when streaming', () => {
    render(<SessionChatWidget {...defaultProps} isStreaming={true} defaultExpanded={true} />);
    expect(screen.getByTestId('chat-send')).toBeDisabled();
  });

  it('should show last assistant message preview when collapsed', () => {
    render(<SessionChatWidget {...defaultProps} messages={messages} />);
    // Collapsed — preview of last assistant message should appear
    expect(screen.getByText(/Il commercio permette/)).toBeInTheDocument();
  });

  it('should have data-testid on container', () => {
    render(<SessionChatWidget {...defaultProps} />);
    expect(screen.getByTestId('session-chat-widget')).toBeInTheDocument();
  });
});
