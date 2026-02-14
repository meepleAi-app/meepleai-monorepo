/**
 * ChatThreadHeader Tests - Issue #4364
 *
 * Tests for the chat thread header:
 * 1. Renders title and metadata
 * 2. Title editing flow
 * 3. Action buttons
 *
 * Pattern: Vitest + React Testing Library
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { ChatThreadHeader } from '../ChatThreadHeader';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

describe('ChatThreadHeader', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders title and metadata', () => {
    render(
      <ChatThreadHeader
        title="Test Chat"
        gameName="Catan"
        agentName="Tutor"
      />
    );

    expect(screen.getByText('Test Chat')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Tutor')).toBeInTheDocument();
  });

  it('renders back button linking to /chat/new', () => {
    render(<ChatThreadHeader title="Test" />);
    const backBtn = screen.getByTestId('header-back-btn');
    expect(backBtn).toHaveAttribute('href', '/chat/new');
  });

  it('enables title editing on click', async () => {
    const user = userEvent.setup();
    const onTitleChange = vi.fn();

    render(
      <ChatThreadHeader
        title="Original Title"
        onTitleChange={onTitleChange}
      />
    );

    await user.click(screen.getByTestId('title-display'));
    expect(screen.getByTestId('title-edit-input')).toBeInTheDocument();
    expect(screen.getByTestId('title-edit-input')).toHaveValue('Original Title');
  });

  it('saves title on Enter', async () => {
    const user = userEvent.setup();
    const onTitleChange = vi.fn();

    render(
      <ChatThreadHeader
        title="Original"
        onTitleChange={onTitleChange}
      />
    );

    await user.click(screen.getByTestId('title-display'));
    const input = screen.getByTestId('title-edit-input');
    await user.clear(input);
    await user.type(input, 'New Title{Enter}');

    expect(onTitleChange).toHaveBeenCalledWith('New Title');
  });

  it('cancels edit on Escape', async () => {
    const user = userEvent.setup();
    const onTitleChange = vi.fn();

    render(
      <ChatThreadHeader
        title="Original"
        onTitleChange={onTitleChange}
      />
    );

    await user.click(screen.getByTestId('title-display'));
    await user.keyboard('{Escape}');

    expect(onTitleChange).not.toHaveBeenCalled();
    expect(screen.getByText('Original')).toBeInTheDocument();
  });

  it('renders action buttons when handlers provided', () => {
    render(
      <ChatThreadHeader
        title="Test"
        onHistoryToggle={vi.fn()}
        onExport={vi.fn()}
        onShare={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    expect(screen.getByTestId('header-history-btn')).toBeInTheDocument();
    expect(screen.getByTestId('header-export-btn')).toBeInTheDocument();
    expect(screen.getByTestId('header-share-btn')).toBeInTheDocument();
    expect(screen.getByTestId('header-delete-btn')).toBeInTheDocument();
  });

  it('hides action buttons when handlers not provided', () => {
    render(<ChatThreadHeader title="Test" />);

    expect(screen.queryByTestId('header-history-btn')).not.toBeInTheDocument();
    expect(screen.queryByTestId('header-export-btn')).not.toBeInTheDocument();
  });

  it('calls action handlers on click', async () => {
    const user = userEvent.setup();
    const onHistoryToggle = vi.fn();
    const onDelete = vi.fn();

    render(
      <ChatThreadHeader
        title="Test"
        onHistoryToggle={onHistoryToggle}
        onDelete={onDelete}
      />
    );

    await user.click(screen.getByTestId('header-history-btn'));
    expect(onHistoryToggle).toHaveBeenCalledOnce();

    await user.click(screen.getByTestId('header-delete-btn'));
    expect(onDelete).toHaveBeenCalledOnce();
  });
});
