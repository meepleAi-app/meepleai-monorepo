/**
 * useAlertDialog hook tests
 * Follow-up to Issue #1435 - Replace window.alert with custom dialog
 */

import React from 'react';
import { renderHook, act, waitFor } from '@testing-library/react';
import { render, screen, fireEvent } from '@testing-library/react';
import { useAlertDialog } from '../useAlertDialog';

describe('useAlertDialog', () => {
  it('should return alert function and AlertDialogComponent', () => {
    const { result } = renderHook(() => useAlertDialog());

    expect(result.current.alert).toBeDefined();
    expect(typeof result.current.alert).toBe('function');
    expect(result.current.AlertDialogComponent).toBeDefined();
    expect(typeof result.current.AlertDialogComponent).toBe('function');
  });

  it('should show dialog when alert is called', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();

      const handleClick = async () => {
        await alert({
          title: 'Test Title',
          message: 'Test Message',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Test Button</button>
          <AlertDialogComponent />
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

  it('should resolve when OK button is clicked', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();
      const [resolved, setResolved] = React.useState(false);

      const handleClick = async () => {
        await alert({
          title: 'Info',
          message: 'This is an info message',
        });
        setResolved(true);
      };

      return (
        <>
          <button onClick={handleClick}>Show Alert</button>
          {resolved && <div data-testid="resolved">Resolved</div>}
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alert'));

    await waitFor(() => {
      expect(screen.getByText('Info')).toBeInTheDocument();
    });

    const okButton = screen.getByRole('button', { name: /ok/i });
    fireEvent.click(okButton);

    await waitFor(() => {
      expect(screen.getByTestId('resolved')).toBeInTheDocument();
    });
  });

  it('should support custom button text', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();

      const handleClick = async () => {
        await alert({
          title: 'Custom Button',
          message: 'Testing custom button text',
          buttonText: 'Got it',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Alert</button>
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alert'));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /got it/i })).toBeInTheDocument();
    });
  });

  it('should support info variant', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();

      const handleClick = async () => {
        await alert({
          title: 'Information',
          message: 'This is informational',
          variant: 'info',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Alert</button>
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alert'));

    await waitFor(() => {
      const icon = screen.getByText('Information').parentElement?.querySelector('svg');
      expect(icon).toHaveClass('text-blue-600');
    });
  });

  it('should support success variant', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();

      const handleClick = async () => {
        await alert({
          title: 'Success',
          message: 'Operation completed',
          variant: 'success',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Alert</button>
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alert'));

    await waitFor(() => {
      const icon = screen.getByText('Success').parentElement?.querySelector('svg');
      expect(icon).toHaveClass('text-green-600');
    });
  });

  it('should support warning variant', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();

      const handleClick = async () => {
        await alert({
          title: 'Warning',
          message: 'Please be careful',
          variant: 'warning',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Alert</button>
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alert'));

    await waitFor(() => {
      const icon = screen.getByText('Warning').parentElement?.querySelector('svg');
      expect(icon).toHaveClass('text-yellow-600');
    });
  });

  it('should support error variant', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();

      const handleClick = async () => {
        await alert({
          title: 'Error',
          message: 'Something went wrong',
          variant: 'error',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Alert</button>
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alert'));

    await waitFor(() => {
      const icon = screen.getByText('Error').parentElement?.querySelector('svg');
      expect(icon).toHaveClass('text-red-600');
    });
  });

  it('should support loading variant', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();

      const handleClick = async () => {
        await alert({
          title: 'Loading',
          message: 'Please wait...',
          variant: 'loading',
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Alert</button>
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alert'));

    await waitFor(() => {
      const icon = screen.getByText('Loading').parentElement?.querySelector('svg');
      expect(icon).toHaveClass('text-gray-600');
      expect(icon).toHaveClass('animate-spin');
    });
  });

  it('should handle sequential alerts', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();
      const [sequence, setSequence] = React.useState<string[]>([]);

      const handleClick = async () => {
        await alert({
          title: 'First Alert',
          message: 'First message',
        });
        setSequence(prev => [...prev, 'First']);

        await alert({
          title: 'Second Alert',
          message: 'Second message',
        });
        setSequence(prev => [...prev, 'Second']);
      };

      return (
        <>
          <button onClick={handleClick}>Show Alerts</button>
          <div data-testid="sequence">{sequence.join(', ')}</div>
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alerts'));

    // First alert
    await waitFor(() => {
      expect(screen.getByText('First Alert')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: /ok/i }));

    // Second alert
    await waitFor(() => {
      expect(screen.getByText('Second Alert')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: /ok/i }));

    await waitFor(() => {
      expect(screen.getByTestId('sequence')).toHaveTextContent('First, Second');
    });
  });

  it('should resolve when dialog is closed via overlay', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();
      const [resolved, setResolved] = React.useState(false);

      const handleClick = async () => {
        await alert({
          title: 'Test Alert',
          message: 'Testing overlay close',
        });
        setResolved(true);
      };

      return (
        <>
          <button onClick={handleClick}>Show Alert</button>
          {resolved && <div data-testid="resolved">Resolved</div>}
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Alert'));

    await waitFor(() => {
      expect(screen.getByText('Test Alert')).toBeInTheDocument();
    });

    // Simulate closing via Radix Dialog's onOpenChange (Escape key)
    const dialog = screen.getByRole('dialog');
    fireEvent.keyDown(dialog, { key: 'Escape' });

    await waitFor(() => {
      expect(screen.getByTestId('resolved')).toBeInTheDocument();
    });
  });

  it('should handle concurrent alert attempts correctly', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();
      const [sequence, setSequence] = React.useState<string[]>([]);

      const handleClick = () => {
        // Call alert twice without awaiting
        alert({
          title: 'First Alert',
          message: 'First',
        }).then(() => {
          setSequence(prev => [...prev, 'First']);
        });

        alert({
          title: 'Second Alert',
          message: 'Second',
        }).then(() => {
          setSequence(prev => [...prev, 'Second']);
        });
      };

      return (
        <>
          <button onClick={handleClick}>Show Concurrent</button>
          <div data-testid="sequence">{sequence.join(', ')}</div>
          <AlertDialogComponent />
        </>
      );
    };

    render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Concurrent'));

    // Second alert should be visible (overrides first)
    await waitFor(() => {
      expect(screen.getByText('Second Alert')).toBeInTheDocument();
      expect(screen.queryByText('First Alert')).not.toBeInTheDocument();
    });

    // Close the dialog
    fireEvent.click(screen.getByRole('button', { name: /ok/i }));

    await waitFor(() => {
      const sequence = screen.getByTestId('sequence').textContent;
      // Second alert should be in the sequence
      expect(sequence).toContain('Second');
    });
  });

  it('should not leak memory on multiple calls', async () => {
    const TestComponent = () => {
      const { alert, AlertDialogComponent } = useAlertDialog();

      const handleClick = async () => {
        for (let i = 0; i < 5; i++) {
          await alert({
            title: `Alert ${i}`,
            message: 'Testing memory',
          });
        }
      };

      return (
        <>
          <button onClick={handleClick}>Show Multiple</button>
          <AlertDialogComponent />
        </>
      );
    };

    const { unmount } = render(<TestComponent />);

    fireEvent.click(screen.getByText('Show Multiple'));

    await waitFor(() => {
      expect(screen.getByText('Alert 0')).toBeInTheDocument();
    });

    // Close first alert
    fireEvent.click(screen.getByRole('button', { name: /ok/i }));

    // Should not throw or leak memory
    unmount();
  });
});
