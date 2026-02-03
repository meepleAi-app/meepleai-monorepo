/**
 * GlobalSearch Tests
 * Issue #3289 - Phase 3: GlobalSearch Component
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LayoutProvider } from '../../LayoutProvider';
import { SearchInput } from '../SearchInput';
import { SearchTrigger } from '../SearchTrigger';
import { RecentSearches } from '../RecentSearches';

// Mock dependencies
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    deviceType: 'desktop',
    isMobile: false,
    isTablet: false,
    isDesktop: true,
    viewportWidth: 1024,
  }),
}));

// Create query client for tests
const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

// Helper wrapper
function TestWrapper({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  return (
    <QueryClientProvider client={queryClient}>
      <LayoutProvider>{children}</LayoutProvider>
    </QueryClientProvider>
  );
}

describe('SearchTrigger', () => {
  it('should render search button', () => {
    const onClick = vi.fn();
    render(<SearchTrigger onClick={onClick} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button', { name: /apri ricerca/i });
    expect(button).toBeInTheDocument();
  });

  it('should call onClick when clicked', () => {
    const onClick = vi.fn();
    render(<SearchTrigger onClick={onClick} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button');
    fireEvent.click(button);

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('should show keyboard shortcut hint', () => {
    render(<SearchTrigger onClick={() => {}} showHint={true} />, {
      wrapper: TestWrapper,
    });

    expect(screen.getByText('K')).toBeInTheDocument();
  });

  it('should hide keyboard shortcut hint when showHint is false', () => {
    render(<SearchTrigger onClick={() => {}} showHint={false} />, {
      wrapper: TestWrapper,
    });

    expect(screen.queryByText('K')).not.toBeInTheDocument();
  });
});

describe('SearchInput', () => {
  it('should render input field', () => {
    render(<SearchInput value="" onChange={() => {}} />, {
      wrapper: TestWrapper,
    });

    const input = screen.getByRole('searchbox');
    expect(input).toBeInTheDocument();
  });

  it('should display placeholder text', () => {
    render(
      <SearchInput
        value=""
        onChange={() => {}}
        placeholder="Search games..."
      />,
      { wrapper: TestWrapper }
    );

    const input = screen.getByPlaceholderText('Search games...');
    expect(input).toBeInTheDocument();
  });

  it('should call onChange when typing', () => {
    const onChange = vi.fn();
    render(<SearchInput value="" onChange={onChange} />, {
      wrapper: TestWrapper,
    });

    const input = screen.getByRole('searchbox');
    fireEvent.change(input, { target: { value: 'test' } });

    expect(onChange).toHaveBeenCalledWith('test');
  });

  it('should show clear button when has value', () => {
    render(<SearchInput value="test" onChange={() => {}} />, {
      wrapper: TestWrapper,
    });

    const clearButton = screen.getByRole('button', { name: /cancella/i });
    expect(clearButton).toBeInTheDocument();
  });

  it('should hide clear button when empty', () => {
    render(<SearchInput value="" onChange={() => {}} />, {
      wrapper: TestWrapper,
    });

    expect(screen.queryByRole('button', { name: /cancella/i })).not.toBeInTheDocument();
  });

  it('should call onClear when clear button clicked', () => {
    const onClear = vi.fn();
    const onChange = vi.fn();
    render(<SearchInput value="test" onChange={onChange} onClear={onClear} />, {
      wrapper: TestWrapper,
    });

    const clearButton = screen.getByRole('button', { name: /cancella/i });
    fireEvent.click(clearButton);

    expect(onChange).toHaveBeenCalledWith('');
    expect(onClear).toHaveBeenCalled();
  });

  it('should call onSubmit on Enter key', () => {
    const onSubmit = vi.fn();
    render(<SearchInput value="test" onChange={() => {}} onSubmit={onSubmit} />, {
      wrapper: TestWrapper,
    });

    const input = screen.getByRole('searchbox');
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onSubmit).toHaveBeenCalled();
  });

  it('should call onEscape on Escape key', () => {
    const onEscape = vi.fn();
    render(<SearchInput value="test" onChange={() => {}} onEscape={onEscape} />, {
      wrapper: TestWrapper,
    });

    const input = screen.getByRole('searchbox');
    fireEvent.keyDown(input, { key: 'Escape' });

    expect(onEscape).toHaveBeenCalled();
  });
});

describe('RecentSearches', () => {
  const mockSearches = ['Gloomhaven', 'Wingspan', 'Catan'];

  it('should render recent searches', () => {
    render(
      <RecentSearches
        searches={mockSearches}
        onSelect={() => {}}
        onRemove={() => {}}
        onClearAll={() => {}}
      />,
      { wrapper: TestWrapper }
    );

    expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('should not render when no searches', () => {
    const { container } = render(
      <RecentSearches
        searches={[]}
        onSelect={() => {}}
        onRemove={() => {}}
        onClearAll={() => {}}
      />,
      { wrapper: TestWrapper }
    );

    expect(container.firstChild).toBeNull();
  });

  it('should call onSelect when search is clicked', () => {
    const onSelect = vi.fn();
    render(
      <RecentSearches
        searches={mockSearches}
        onSelect={onSelect}
        onRemove={() => {}}
        onClearAll={() => {}}
      />,
      { wrapper: TestWrapper }
    );

    fireEvent.click(screen.getByText('Gloomhaven'));

    expect(onSelect).toHaveBeenCalledWith('Gloomhaven');
  });

  it('should call onClearAll when clear all is clicked', () => {
    const onClearAll = vi.fn();
    render(
      <RecentSearches
        searches={mockSearches}
        onSelect={() => {}}
        onRemove={() => {}}
        onClearAll={onClearAll}
      />,
      { wrapper: TestWrapper }
    );

    fireEvent.click(screen.getByText(/cancella tutto/i));

    expect(onClearAll).toHaveBeenCalled();
  });

  it('should show clear all button', () => {
    render(
      <RecentSearches
        searches={mockSearches}
        onSelect={() => {}}
        onRemove={() => {}}
        onClearAll={() => {}}
      />,
      { wrapper: TestWrapper }
    );

    expect(screen.getByText(/cancella tutto/i)).toBeInTheDocument();
  });
});

describe('GlobalSearch accessibility', () => {
  it('should have proper aria labels on search input', () => {
    render(
      <SearchInput
        value=""
        onChange={() => {}}
        placeholder="Search here"
      />,
      { wrapper: TestWrapper }
    );

    const input = screen.getByRole('searchbox');
    expect(input).toHaveAttribute('aria-label', 'Search here');
  });

  it('should have proper aria labels on trigger', () => {
    render(<SearchTrigger onClick={() => {}} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Apri ricerca');
  });

  it('should set aria-expanded correctly', () => {
    render(<SearchTrigger onClick={() => {}} isExpanded={true} />, {
      wrapper: TestWrapper,
    });

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });
});
