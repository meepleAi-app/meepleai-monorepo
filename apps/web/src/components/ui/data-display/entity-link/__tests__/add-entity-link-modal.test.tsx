/**
 * AddEntityLinkModal — C6
 * Tests for Issue #5162 / #5165
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { AddEntityLinkModal } from '../add-entity-link-modal';

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

const DEFAULT_PROPS = {
  open: true,
  onOpenChange: vi.fn(),
  sourceEntityType: 'Game' as const,
  sourceEntityId: 'game-source-id',
};

describe('AddEntityLinkModal', () => {
  beforeEach(() => {
    mockFetch.mockReset();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders "Add Connection" title when open', () => {
    render(<AddEntityLinkModal {...DEFAULT_PROPS} />);
    expect(screen.getByText('Add Connection')).toBeInTheDocument();
  });

  it('renders all link type chips', () => {
    render(<AddEntityLinkModal {...DEFAULT_PROPS} />);
    // Should have link type selection chips
    expect(screen.getByText(/expansion of/i)).toBeInTheDocument();
    expect(screen.getByText(/related/i)).toBeInTheDocument();
  });

  it('shows target type options after selecting a link type', () => {
    render(<AddEntityLinkModal {...DEFAULT_PROPS} />);
    const expansionChip = screen
      .getAllByRole('button')
      .find(b => b.textContent?.includes('expansion of'));
    expect(expansionChip).toBeDefined();
    fireEvent.click(expansionChip!);
    // ExpansionOf targets only 'Game'
    expect(screen.getByRole('button', { name: /^game$/i })).toBeInTheDocument();
  });

  it('shows ID input after selecting both link type and target type', () => {
    render(<AddEntityLinkModal {...DEFAULT_PROPS} />);
    const expansionChip = screen
      .getAllByRole('button')
      .find(b => b.textContent?.includes('expansion of'));
    fireEvent.click(expansionChip!);
    const gameButton = screen.getByRole('button', { name: /^game$/i });
    fireEvent.click(gameButton);
    expect(screen.getByPlaceholderText(/paste entity uuid/i)).toBeInTheDocument();
  });

  it('submit button is disabled when form is incomplete', () => {
    render(<AddEntityLinkModal {...DEFAULT_PROPS} />);
    const submitBtn = screen.getByRole('button', { name: /add connection/i });
    expect(submitBtn).toBeDisabled();
  });

  it('closes when Cancel is clicked', () => {
    const onOpenChange = vi.fn();
    render(<AddEntityLinkModal {...DEFAULT_PROPS} onOpenChange={onOpenChange} />);
    fireEvent.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('calls POST /api/v1/library/entity-links on submit', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ id: 'new-link-id' }),
    });

    const onLinkCreated = vi.fn();
    render(<AddEntityLinkModal {...DEFAULT_PROPS} onLinkCreated={onLinkCreated} />);

    // Select link type
    const expansionChip = screen
      .getAllByRole('button')
      .find(b => b.textContent?.includes('expansion of'));
    fireEvent.click(expansionChip!);

    // Select target entity type
    fireEvent.click(screen.getByRole('button', { name: /^game$/i }));

    // Enter target entity ID
    fireEvent.change(screen.getByPlaceholderText(/paste entity uuid/i), {
      target: { value: 'target-game-uuid' },
    });

    // Submit
    const submitBtn = screen.getByRole('button', { name: /add connection/i });
    expect(submitBtn).not.toBeDisabled();
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(mockFetch).toHaveBeenCalledWith(
        '/api/v1/library/entity-links',
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
        })
      );
    });

    await waitFor(() => {
      expect(onLinkCreated).toHaveBeenCalledOnce();
    });
  });

  it('shows error message on submit failure', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      text: () => Promise.resolve('Conflict: link already exists'),
    });

    render(<AddEntityLinkModal {...DEFAULT_PROPS} />);

    const expansionChip = screen
      .getAllByRole('button')
      .find(b => b.textContent?.includes('expansion of'));
    fireEvent.click(expansionChip!);
    fireEvent.click(screen.getByRole('button', { name: /^game$/i }));
    fireEvent.change(screen.getByPlaceholderText(/paste entity uuid/i), {
      target: { value: 'target-game-uuid' },
    });
    fireEvent.click(screen.getByRole('button', { name: /add connection/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  it('does not render when open=false', () => {
    render(<AddEntityLinkModal {...DEFAULT_PROPS} open={false} />);
    // Dialog content should not be visible
    expect(screen.queryByText('Add Connection')).toBeNull();
  });
});
