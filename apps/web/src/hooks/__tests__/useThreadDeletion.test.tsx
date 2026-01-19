/**
 * useThreadDeletion hook tests
 * Issue #2258 - Thread deletion with confirmation dialog
 */

import React from 'react';
import { renderHook, act, waitFor } from '@testing-library/react';
import { render, screen, fireEvent } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { useThreadDeletion } from '../useThreadDeletion';
import { getDialogHeading, queryDialogHeading, getByRoleInDialog } from '@/test-utils/locale-queries';
import { useChatStore } from '@/store/chat/store';
import * as toastUtils from '@/lib/toastUtils';

// Mock dependencies
vi.mock('@/store/chat/store');
vi.mock('@/lib/toastUtils');

describe('useThreadDeletion', () => {
  const mockDeleteChat = vi.fn();
  const mockShowSuccessToast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    // Mock Zustand store
    vi.mocked(useChatStore).mockReturnValue(mockDeleteChat);

    // Mock toast utils
    vi.mocked(toastUtils.showSuccessToast).mockImplementation(mockShowSuccessToast);
  });

  it('should return handleThreadDelete function and ConfirmDialogComponent', () => {
    const { result } = renderHook(() => useThreadDeletion());

    expect(result.current.handleThreadDelete).toBeDefined();
    expect(typeof result.current.handleThreadDelete).toBe('function');
    expect(result.current.ConfirmDialogComponent).toBeDefined();
    expect(typeof result.current.ConfirmDialogComponent).toBe('function');
  });

  it('should show confirmation dialog when handleThreadDelete is called', async () => {
    const TestComponent = () => {
      const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();

      const handleClick = () => {
        void handleThreadDelete('test-thread-id');
      };

      return (
        <>
          <button onClick={handleClick}>Delete Thread</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    const button = screen.getByText('Delete Thread');
    fireEvent.click(button);

    await waitFor(() => {
      expect(getDialogHeading(/elimina thread/i)).toBeInTheDocument();
      expect(
        screen.getByText(
          /Questa azione non può essere annullata. Tutti i messaggi in questo thread verranno eliminati permanentemente./i
        )
      ).toBeInTheDocument();
    });
  });

  it('should call deleteChat and show success toast when confirmed', async () => {
    const TestComponent = () => {
      const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();

      const handleClick = () => {
        void handleThreadDelete('test-thread-id');
      };

      return (
        <>
          <button onClick={handleClick}>Delete Thread</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    // Click delete button
    fireEvent.click(screen.getByText('Delete Thread'));

    // Wait for dialog to appear
    await waitFor(() => {
      expect(getDialogHeading(/elimina thread/i)).toBeInTheDocument();
    });

    // Click confirm button
    const confirmButton = getByRoleInDialog('button', { name: /elimina/i });
    fireEvent.click(confirmButton);

    // Verify deleteChat was called
    await waitFor(() => {
      expect(mockDeleteChat).toHaveBeenCalledWith('test-thread-id');
      expect(mockShowSuccessToast).toHaveBeenCalledWith('Thread eliminato con successo');
    });
  });

  it('should NOT call deleteChat when cancelled', async () => {
    const TestComponent = () => {
      const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();

      const handleClick = () => {
        void handleThreadDelete('test-thread-id');
      };

      return (
        <>
          <button onClick={handleClick}>Delete Thread</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    // Click delete button
    fireEvent.click(screen.getByText('Delete Thread'));

    // Wait for dialog to appear
    await waitFor(() => {
      expect(getDialogHeading(/elimina thread/i)).toBeInTheDocument();
    });

    // Click cancel button
    const cancelButton = getByRoleInDialog('button', { name: /annulla/i });
    fireEvent.click(cancelButton);

    // Verify deleteChat was NOT called
    await waitFor(() => {
      expect(mockDeleteChat).not.toHaveBeenCalled();
      expect(mockShowSuccessToast).not.toHaveBeenCalled();
    });
  });

  it('should NOT proceed if chatId is empty', async () => {
    const TestComponent = () => {
      const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();

      const handleClick = () => {
        void handleThreadDelete('');
      };

      return (
        <>
          <button onClick={handleClick}>Delete Thread</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    // Click delete button
    fireEvent.click(screen.getByText('Delete Thread'));

    // Dialog should NOT appear
    await waitFor(
      () => {
        expect(queryDialogHeading(/elimina thread/i)).not.toBeInTheDocument();
      },
      { timeout: 1000 }
    );

    // deleteChat should NOT be called
    expect(mockDeleteChat).not.toHaveBeenCalled();
    expect(mockShowSuccessToast).not.toHaveBeenCalled();
  });

  it('should use destructive variant for dialog', async () => {
    const TestComponent = () => {
      const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();

      const handleClick = () => {
        void handleThreadDelete('test-thread-id');
      };

      return (
        <>
          <button onClick={handleClick}>Delete Thread</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    // Click delete button
    fireEvent.click(screen.getByText('Delete Thread'));

    // Wait for dialog to appear
    await waitFor(() => {
      expect(getDialogHeading(/elimina thread/i)).toBeInTheDocument();
    });

    // Check that confirm button has destructive styling (red)
    const confirmButton = getByRoleInDialog('button', { name: /elimina/i });
    expect(confirmButton).toBeInTheDocument();
    // The destructive variant should apply specific styling classes
    // This is a basic check - actual styling verification would need visual regression tests
  });

  it('should have correct button labels', async () => {
    const TestComponent = () => {
      const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();

      const handleClick = () => {
        void handleThreadDelete('test-thread-id');
      };

      return (
        <>
          <button onClick={handleClick}>Delete Thread</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    // Click delete button
    fireEvent.click(screen.getByText('Delete Thread'));

    // Wait for dialog to appear
    await waitFor(() => {
      expect(getDialogHeading(/elimina thread/i)).toBeInTheDocument();
    });

    // Check button labels
    expect(getByRoleInDialog('button', { name: /elimina/i })).toBeInTheDocument();
    expect(getByRoleInDialog('button', { name: /annulla/i })).toBeInTheDocument();
  });
});
