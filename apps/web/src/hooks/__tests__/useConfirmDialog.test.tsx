/**
 * useConfirmDialog hook tests
 * Issue #1435 - Replace window.confirm with custom dialog
 */

import React from 'react';
import { renderHook, act, waitFor } from '@testing-library/react';
import { render, screen, fireEvent } from '@testing-library/react';
import { useConfirmDialog } from '../useConfirmDialog';

describe('useConfirmDialog', () => {
  it('should return confirm function and ConfirmDialogComponent', () => {
    const { result } = renderHook(() => useConfirmDialog());

    expect(result.current.confirm).toBeDefined();
    expect(typeof result.current.confirm).toBe('function');
    expect(result.current.ConfirmDialogComponent).toBeDefined();
    expect(typeof result.current.ConfirmDialogComponent).toBe('function');
  });

  it('should show dialog when confirm is called', async () => {
    const TestComponent = () => {
      const { confirm, ConfirmDialogComponent } = useConfirmDialog();

      const handleClick = async () => {
        await confirm({
          title: 'Test Title',
          message: 'Test Message',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Test Button</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    const button = screen.getByText('Test Button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(screen.getByText('Test Title')).toBeInTheDocument();
      expect(screen.getByText('Test Message')).toBeInTheDocument();
    });
  });

  it('should resolve to true when confirm button is clicked', async () => {
    const TestComponent = () => {
      const { confirm, ConfirmDialogComponent } = useConfirmDialog();
      const [result, setResult] = React.useState<boolean | null>(null);

      const handleClick = async () => {
        const confirmed = await confirm({
          title: 'Confirm Action',
          message: 'Are you sure?',
        });
        setResult(confirmed);
      };

      return (
        <>
          <button onClick={handleClick}>Show Dialog</button>
          {result !== null && <div data-testid="result">{result ? 'Confirmed' : 'Cancelled'}</div>}
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Dialog'));

    await waitFor(() => {
      expect(screen.getByText('Confirm Action')).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByTestId('result')).toHaveTextContent('Confirmed');
    });
  });

  it('should resolve to false when cancel button is clicked', async () => {
    const TestComponent = () => {
      const { confirm, ConfirmDialogComponent } = useConfirmDialog();
      const [result, setResult] = React.useState<boolean | null>(null);

      const handleClick = async () => {
        const confirmed = await confirm({
          title: 'Confirm Action',
          message: 'Are you sure?',
        });
        setResult(confirmed);
      };

      return (
        <>
          <button onClick={handleClick}>Show Dialog</button>
          {result !== null && <div data-testid="result">{result ? 'Confirmed' : 'Cancelled'}</div>}
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Dialog'));

    await waitFor(() => {
      expect(screen.getByText('Confirm Action')).toBeInTheDocument();
    });

    const cancelButton = screen.getByRole('button', { name: /cancel/i });
    fireEvent.click(cancelButton);

    await waitFor(() => {
      expect(screen.getByTestId('result')).toHaveTextContent('Cancelled');
    });
  });

  it('should support custom button text', async () => {
    const TestComponent = () => {
      const { confirm, ConfirmDialogComponent } = useConfirmDialog();

      const handleClick = async () => {
        await confirm({
          title: 'Delete Item',
          message: 'This will permanently delete the item.',
          confirmText: 'Delete',
          cancelText: 'Keep',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Dialog</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Dialog'));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /keep/i })).toBeInTheDocument();
    });
  });

  it('should support destructive variant', async () => {
    const TestComponent = () => {
      const { confirm, ConfirmDialogComponent } = useConfirmDialog();

      const handleClick = async () => {
        await confirm({
          title: 'Delete Account',
          message: 'This action cannot be undone.',
          variant: 'destructive',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Dialog</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Dialog'));

    await waitFor(() => {
      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      expect(confirmButton).toHaveClass('bg-destructive');
    });
  });

  it('should handle multiple sequential confirms', async () => {
    const TestComponent = () => {
      const { confirm, ConfirmDialogComponent } = useConfirmDialog();
      const [results, setResults] = React.useState<boolean[]>([]);

      const handleClick = async () => {
        const result1 = await confirm({
          title: 'First Confirmation',
          message: 'Confirm first action?',
        });
        setResults(prev => [...prev, result1]);

        const result2 = await confirm({
          title: 'Second Confirmation',
          message: 'Confirm second action?',
        });
        setResults(prev => [...prev, result2]);
      };

      return (
        <>
          <button onClick={handleClick}>Show Dialogs</button>
          <div data-testid="results">{results.join(',')}</div>
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Dialogs'));

    // First dialog
    await waitFor(() => {
      expect(screen.getByText('First Confirmation')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: /confirm/i }));

    // Second dialog
    await waitFor(() => {
      expect(screen.getByText('Second Confirmation')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => {
      expect(screen.getByTestId('results')).toHaveTextContent('true,false');
    });
  });

  it('should resolve to false when dialog is closed via overlay', async () => {
    const TestComponent = () => {
      const { confirm, ConfirmDialogComponent } = useConfirmDialog();
      const [result, setResult] = React.useState<boolean | null>(null);

      const handleClick = async () => {
        const confirmed = await confirm({
          title: 'Confirm Action',
          message: 'Are you sure?',
        });
        setResult(confirmed);
      };

      return (
        <>
          <button onClick={handleClick}>Show Dialog</button>
          {result !== null && <div data-testid="result">{result ? 'Confirmed' : 'Cancelled'}</div>}
          <ConfirmDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Dialog'));

    await waitFor(() => {
      expect(screen.getByText('Confirm Action')).toBeInTheDocument();
    });

    // Simulate closing via Radix Dialog's onOpenChange
    const dialog = screen.getByRole('dialog');
    fireEvent.keyDown(dialog, { key: 'Escape' });

    await waitFor(() => {
      expect(screen.getByTestId('result')).toHaveTextContent('Cancelled');
    });
  });

  it('should not leak memory on multiple calls', async () => {
    const TestComponent = () => {
      const { confirm, ConfirmDialogComponent } = useConfirmDialog();

      const handleClick = async () => {
        for (let i = 0; i < 10; i++) {
          await confirm({
            title: `Confirmation ${i}`,
            message: 'Confirm?',
          });
        }
      };

      return (
        <>
          <button onClick={handleClick}>Show Dialogs</button>
          <ConfirmDialogComponent />
        </>
      );
    };

    const { unmount } = render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Dialogs'));

    await waitFor(() => {
      expect(screen.getByText('Confirmation 0')).toBeInTheDocument();
    });

    // Confirm first dialog
    fireEvent.click(screen.getByRole('button', { name: /confirm/i }));

    // Should not throw or leak memory
    unmount();
  });
});
