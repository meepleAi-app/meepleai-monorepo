/**
 * Tests for AITab component
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AITab } from '../tabs/AITab';
import type { AITabData } from '../types';

// ============================================================================
// Test Data
// ============================================================================

const baseAIData: AITabData = {
  messages: [],
  quickActions: [
    { label: 'Explain rules', prompt: 'Explain the rules of this game' },
    { label: 'Suggest strategy', prompt: 'What strategy should I use?' },
  ],
  isLoading: false,
};

const dataWithMessages: AITabData = {
  ...baseAIData,
  agentName: 'MeepleAI Assistant',
  sessionContext: 'Catan - Friday Night Session',
  messages: [
    {
      id: 'msg-1',
      role: 'user',
      content: 'What are the rules for trading?',
      timestamp: '2026-02-19T10:00:00Z',
    },
    {
      id: 'msg-2',
      role: 'assistant',
      content: 'You can trade resources with other players on your turn.',
      timestamp: '2026-02-19T10:00:05Z',
      sources: [
        { title: 'Catan Rules', snippet: 'Trading is allowed during...' },
      ],
    },
  ],
  quickActions: [],
};

// ============================================================================
// Tests
// ============================================================================

describe('AITab', () => {
  // --- Empty states ---

  it('renders empty state when no data', () => {
    render(<AITab />);

    expect(screen.getByText('AI Assistant')).toBeInTheDocument();
    expect(screen.getByText('Connect a RAG agent to get started')).toBeInTheDocument();
  });

  it('renders welcome state when data but no messages', () => {
    render(<AITab data={baseAIData} />);

    expect(screen.getByText('Ask anything about this game session')).toBeInTheDocument();
  });

  // --- Quick actions ---

  it('renders quick actions when no messages', () => {
    render(<AITab data={baseAIData} />);

    expect(screen.getByText('Explain rules')).toBeInTheDocument();
    expect(screen.getByText('Suggest strategy')).toBeInTheDocument();
  });

  it('hides quick actions when messages exist', () => {
    render(<AITab data={dataWithMessages} />);

    expect(screen.queryByTestId('ai-quick-actions')).not.toBeInTheDocument();
  });

  it('sends quick action prompt when clicked', () => {
    const onSendMessage = vi.fn();
    render(<AITab data={baseAIData} onSendMessage={onSendMessage} />);

    fireEvent.click(screen.getAllByTestId('ai-quick-action')[0]);
    expect(onSendMessage).toHaveBeenCalledWith('Explain the rules of this game');
  });

  // --- Message rendering ---

  it('renders user and assistant messages', () => {
    render(<AITab data={dataWithMessages} />);

    expect(screen.getByText('What are the rules for trading?')).toBeInTheDocument();
    expect(screen.getByText('You can trade resources with other players on your turn.')).toBeInTheDocument();
  });

  it('renders message test ids', () => {
    render(<AITab data={dataWithMessages} />);

    expect(screen.getByTestId('ai-message-msg-1')).toBeInTheDocument();
    expect(screen.getByTestId('ai-message-msg-2')).toBeInTheDocument();
  });

  it('renders source citations on assistant messages', () => {
    render(<AITab data={dataWithMessages} />);

    expect(screen.getByText('Catan Rules')).toBeInTheDocument();
    expect(screen.getByText('Trading is allowed during...')).toBeInTheDocument();
  });

  // --- Agent context header ---

  it('renders agent name and session context', () => {
    render(<AITab data={dataWithMessages} />);

    expect(screen.getByText('MeepleAI Assistant')).toBeInTheDocument();
    expect(screen.getByText('Catan - Friday Night Session')).toBeInTheDocument();
  });

  it('does not render agent header when no agent name', () => {
    render(<AITab data={baseAIData} />);

    expect(screen.queryByText('MeepleAI Assistant')).not.toBeInTheDocument();
  });

  // --- Input and sending ---

  it('renders input textarea and send button', () => {
    render(<AITab data={baseAIData} />);

    expect(screen.getByTestId('ai-input')).toBeInTheDocument();
    expect(screen.getByTestId('ai-send-button')).toBeInTheDocument();
  });

  it('sends message on send button click', () => {
    const onSendMessage = vi.fn();
    render(<AITab data={baseAIData} onSendMessage={onSendMessage} />);

    const input = screen.getByTestId('ai-input');
    fireEvent.change(input, { target: { value: 'Hello AI' } });
    fireEvent.click(screen.getByTestId('ai-send-button'));

    expect(onSendMessage).toHaveBeenCalledWith('Hello AI');
  });

  it('sends message on Enter key', () => {
    const onSendMessage = vi.fn();
    render(<AITab data={baseAIData} onSendMessage={onSendMessage} />);

    const input = screen.getByTestId('ai-input');
    fireEvent.change(input, { target: { value: 'Test message' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onSendMessage).toHaveBeenCalledWith('Test message');
  });

  it('does not send on Shift+Enter', () => {
    const onSendMessage = vi.fn();
    render(<AITab data={baseAIData} onSendMessage={onSendMessage} />);

    const input = screen.getByTestId('ai-input');
    fireEvent.change(input, { target: { value: 'Test' } });
    fireEvent.keyDown(input, { key: 'Enter', shiftKey: true });

    expect(onSendMessage).not.toHaveBeenCalled();
  });

  it('clears input after sending', () => {
    const onSendMessage = vi.fn();
    render(<AITab data={baseAIData} onSendMessage={onSendMessage} />);

    const input = screen.getByTestId('ai-input') as HTMLTextAreaElement;
    fireEvent.change(input, { target: { value: 'Hello' } });
    fireEvent.click(screen.getByTestId('ai-send-button'));

    expect(input.value).toBe('');
  });

  it('does not send empty or whitespace-only messages', () => {
    const onSendMessage = vi.fn();
    render(<AITab data={baseAIData} onSendMessage={onSendMessage} />);

    const input = screen.getByTestId('ai-input');
    fireEvent.change(input, { target: { value: '   ' } });
    fireEvent.click(screen.getByTestId('ai-send-button'));

    expect(onSendMessage).not.toHaveBeenCalled();
  });

  // --- Loading state ---

  it('renders loading indicator when isLoading', () => {
    const loadingData: AITabData = { ...dataWithMessages, isLoading: true };
    render(<AITab data={loadingData} />);

    expect(screen.getByTestId('ai-loading')).toBeInTheDocument();
    expect(screen.getByText('Thinking...')).toBeInTheDocument();
  });

  it('disables send button when loading', () => {
    const loadingData: AITabData = { ...baseAIData, isLoading: true };
    render(<AITab data={loadingData} />);

    const sendBtn = screen.getByTestId('ai-send-button');
    expect(sendBtn).toBeDisabled();
  });
});
