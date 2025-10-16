/**
 * Unit tests for Toast component
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Toast, ToastContainer } from '../Toast';

describe('Toast', () => {
  const mockDismiss = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  it('should render success toast', () => {
    render(
      <Toast
        id="toast-1"
        type="success"
        title="Success"
        message="Operation completed"
        onDismiss={mockDismiss}
      />
    );

    expect(screen.getByText('Success')).toBeInTheDocument();
    expect(screen.getByText('Operation completed')).toBeInTheDocument();
  });

  it('should render error toast', () => {
    render(
      <Toast
        id="toast-2"
        type="error"
        title="Error"
        message="Something went wrong"
        onDismiss={mockDismiss}
      />
    );

    expect(screen.getByText('Error')).toBeInTheDocument();
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('should render warning toast', () => {
    render(
      <Toast
        id="toast-3"
        type="warning"
        title="Warning"
        onDismiss={mockDismiss}
      />
    );

    expect(screen.getByText('Warning')).toBeInTheDocument();
  });

  it('should render info toast', () => {
    render(
      <Toast
        id="toast-4"
        type="info"
        title="Information"
        onDismiss={mockDismiss}
      />
    );

    expect(screen.getByText('Information')).toBeInTheDocument();
  });

  it('should render without message', () => {
    render(
      <Toast
        id="toast-5"
        type="success"
        title="Title only"
        onDismiss={mockDismiss}
      />
    );

    expect(screen.getByText('Title only')).toBeInTheDocument();
    expect(screen.queryByText('Operation completed')).not.toBeInTheDocument();
  });

  it('should dismiss when close button is clicked', async () => {
    const user = userEvent.setup({ delay: null });

    render(
      <Toast
        id="toast-6"
        type="success"
        title="Dismissible"
        onDismiss={mockDismiss}
      />
    );

    const dismissButton = screen.getByRole('button', { name: /dismiss notification/i });
    await user.click(dismissButton);

    act(() => {
      jest.advanceTimersByTime(300); // Animation duration
    });

    await waitFor(() => {
      expect(mockDismiss).toHaveBeenCalledWith('toast-6');
    });
  });

  it('should auto-dismiss after duration', () => {
    render(
      <Toast
        id="toast-7"
        type="success"
        title="Auto-dismiss"
        duration={2000}
        onDismiss={mockDismiss}
      />
    );

    expect(mockDismiss).not.toHaveBeenCalled();

    act(() => {
      jest.advanceTimersByTime(2000);
    });

    act(() => {
      jest.advanceTimersByTime(300); // Animation duration
    });

    expect(mockDismiss).toHaveBeenCalledWith('toast-7');
  });

  it('should not auto-dismiss if duration is 0', () => {
    render(
      <Toast
        id="toast-8"
        type="success"
        title="No auto-dismiss"
        duration={0}
        onDismiss={mockDismiss}
      />
    );

    act(() => {
      jest.advanceTimersByTime(10000);
    });

    expect(mockDismiss).not.toHaveBeenCalled();
  });

  it('should have proper ARIA attributes', () => {
    render(
      <Toast
        id="toast-9"
        type="error"
        title="Accessible toast"
        onDismiss={mockDismiss}
      />
    );

    const toast = screen.getByRole('alert');
    expect(toast).toHaveAttribute('aria-live', 'assertive');
    expect(toast).toHaveAttribute('aria-atomic', 'true');
  });
});

describe('ToastContainer', () => {
  const mockDismiss = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render multiple toasts', () => {
    const toasts = [
      { id: 'toast-1', type: 'success' as const, title: 'Success 1', onDismiss: mockDismiss },
      { id: 'toast-2', type: 'error' as const, title: 'Error 1', onDismiss: mockDismiss },
      { id: 'toast-3', type: 'warning' as const, title: 'Warning 1', onDismiss: mockDismiss }
    ];

    render(<ToastContainer toasts={toasts} onDismiss={mockDismiss} />);

    expect(screen.getByText('Success 1')).toBeInTheDocument();
    expect(screen.getByText('Error 1')).toBeInTheDocument();
    expect(screen.getByText('Warning 1')).toBeInTheDocument();
  });

  it('should render empty container with no toasts', () => {
    const { container } = render(<ToastContainer toasts={[]} onDismiss={mockDismiss} />);

    expect(container.querySelector('[aria-label="Notifications"]')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('should position at top-right by default', () => {
    const { container } = render(<ToastContainer toasts={[]} onDismiss={mockDismiss} />);

    const containerEl = container.querySelector('[aria-label="Notifications"]');
    expect(containerEl).toHaveClass('top-4', 'right-4');
  });

  it('should position at top-left', () => {
    const { container } = render(
      <ToastContainer toasts={[]} onDismiss={mockDismiss} position="top-left" />
    );

    const containerEl = container.querySelector('[aria-label="Notifications"]');
    expect(containerEl).toHaveClass('top-4', 'left-4');
  });

  it('should position at bottom-right', () => {
    const { container } = render(
      <ToastContainer toasts={[]} onDismiss={mockDismiss} position="bottom-right" />
    );

    const containerEl = container.querySelector('[aria-label="Notifications"]');
    expect(containerEl).toHaveClass('bottom-4', 'right-4');
  });

  it('should position at bottom-left', () => {
    const { container } = render(
      <ToastContainer toasts={[]} onDismiss={mockDismiss} position="bottom-left" />
    );

    const containerEl = container.querySelector('[aria-label="Notifications"]');
    expect(containerEl).toHaveClass('bottom-4', 'left-4');
  });

  it('should position at top-center', () => {
    const { container } = render(
      <ToastContainer toasts={[]} onDismiss={mockDismiss} position="top-center" />
    );

    const containerEl = container.querySelector('[aria-label="Notifications"]');
    expect(containerEl).toHaveClass('top-4', 'left-1/2');
  });

  it('should position at bottom-center', () => {
    const { container } = render(
      <ToastContainer toasts={[]} onDismiss={mockDismiss} position="bottom-center" />
    );

    const containerEl = container.querySelector('[aria-label="Notifications"]');
    expect(containerEl).toHaveClass('bottom-4', 'left-1/2');
  });
});
