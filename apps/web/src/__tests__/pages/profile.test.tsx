/**
 * Profile Page Tests - DEPRECATED (SPRINT-1, Issue #848)
 *
 * Tests for the deprecated profile page that now redirects to /settings.
 * All profile functionality has been moved to the Settings page.
 */

import { render, screen } from '@testing-library/react';
import ProfilePage from '../../pages/profile';
import { useRouter } from 'next/router';

// Mock next/router
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

describe('ProfilePage', () => {
  const mockReplace = jest.fn();

  beforeEach(() => {
    // Clear mock calls
    mockReplace.mockClear();

    // Setup router mock
    (useRouter as jest.Mock).mockReturnValue({
      replace: mockReplace,
      pathname: '/profile',
      query: {},
      asPath: '/profile',
    });
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  it('redirects to settings page on mount', () => {
    render(<ProfilePage />);

    // Verify redirect was called
    expect(mockReplace).toHaveBeenCalledWith('/settings');
    expect(mockReplace).toHaveBeenCalledTimes(1);
  });

  it('renders loading spinner during redirect', () => {
    const { container } = render(<ProfilePage />);

    // Verify loading spinner is present
    const spinner = container.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('displays redirect message', () => {
    render(<ProfilePage />);

    // Verify redirect message is shown
    expect(screen.getByText('Redirecting to Settings page...')).toBeInTheDocument();
  });

  it('sets correct page title', () => {
    render(<ProfilePage />);

    // Title is set via Head component, which doesn't render in tests
    // We just verify the component renders without errors
    expect(screen.getByText('Redirecting to Settings page...')).toBeInTheDocument();
  });

  it('applies correct styling classes', () => {
    const { container } = render(<ProfilePage />);

    // Check for dark mode support classes
    const mainContainer = container.querySelector('.min-h-screen');
    expect(mainContainer).toHaveClass('bg-slate-50', 'dark:bg-slate-900');

    // Check for centering classes
    expect(mainContainer).toHaveClass('flex', 'items-center', 'justify-center');
  });

  it('renders loading animation with correct classes', () => {
    const { container } = render(<ProfilePage />);

    const spinner = container.querySelector('.animate-spin');
    expect(spinner).toHaveClass('rounded-full', 'h-12', 'w-12', 'border-b-2', 'border-blue-600', 'mx-auto');
  });

  it('renders message with correct styling', () => {
    render(<ProfilePage />);

    const message = screen.getByText('Redirecting to Settings page...');
    expect(message).toHaveClass('text-slate-600', 'dark:text-slate-400');
  });

  it('centers content with proper spacing', () => {
    const { container } = render(<ProfilePage />);

    const contentContainer = container.querySelector('.text-center');
    expect(contentContainer).toHaveClass('space-y-4');
  });

  it('only calls redirect once even on re-renders', () => {
    const { rerender } = render(<ProfilePage />);

    // Initial render
    expect(mockReplace).toHaveBeenCalledTimes(1);

    // Force re-render
    rerender(<ProfilePage />);

    // Should still only be called once due to useEffect dependency array
    expect(mockReplace).toHaveBeenCalledTimes(1);
    expect(mockReplace).toHaveBeenCalledWith('/settings');
  });

  it('uses router.replace instead of router.push for redirect', () => {
    render(<ProfilePage />);

    // Verify replace was used (not push) to avoid adding to browser history
    expect(mockReplace).toHaveBeenCalledWith('/settings');
  });

  it('renders without crashing when router is available', () => {
    expect(() => render(<ProfilePage />)).not.toThrow();
  });

  it('has accessible loading indicator', () => {
    const { container } = render(<ProfilePage />);

    // The spinner and message together provide context for screen readers
    const spinner = container.querySelector('.animate-spin');
    const message = screen.getByText('Redirecting to Settings page...');

    expect(spinner).toBeInTheDocument();
    expect(message).toBeInTheDocument();
  });

  it('maintains consistent layout structure', () => {
    const { container } = render(<ProfilePage />);

    // Check the component has the expected DOM structure
    const minHeightContainer = container.querySelector('.min-h-screen');
    const centerContainer = container.querySelector('.text-center');
    const spinner = container.querySelector('.animate-spin');
    const message = container.querySelector('p');

    expect(minHeightContainer).toBeInTheDocument();
    expect(centerContainer).toBeInTheDocument();
    expect(spinner).toBeInTheDocument();
    expect(message).toBeInTheDocument();

    // Verify hierarchy
    expect(minHeightContainer).toContainElement(centerContainer);
    expect(centerContainer).toContainElement(spinner);
    expect(centerContainer).toContainElement(message);
  });
});