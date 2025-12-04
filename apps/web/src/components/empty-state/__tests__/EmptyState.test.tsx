/**
 * Tests for EmptyState component
 * Reusable empty state UI with variants and actions
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { Upload, Folder } from 'lucide-react';
import { EmptyState } from '../EmptyState';

// Mock useReducedMotion hook
const mockUseReducedMotion = vi.fn();
vi.mock('@/lib/animations', () => ({
  ...vi.importActual('@/lib/animations'),
  useReducedMotion: () => mockUseReducedMotion(),
}));

describe('EmptyState', () => {
  beforeEach(() => {
    mockUseReducedMotion.mockReturnValue(false);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('Basic rendering', () => {
    it('should render with title only', () => {
      render(<EmptyState title="No items" />);
      expect(screen.getByText('No items')).toBeInTheDocument();
    });

    it('should render with title and description', () => {
      render(<EmptyState title="No items" description="Add your first item to get started" />);
      expect(screen.getByText('No items')).toBeInTheDocument();
      expect(screen.getByText('Add your first item to get started')).toBeInTheDocument();
    });

    it('should render with correct test ID', () => {
      render(<EmptyState title="Test" testId="custom-empty-state" />);
      expect(screen.getByTestId('custom-empty-state')).toBeInTheDocument();
    });

    it('should render with default test ID', () => {
      render(<EmptyState title="Test" />);
      expect(screen.getByTestId('empty-state')).toBeInTheDocument();
    });
  });

  describe('Variants', () => {
    it('should render default variant', () => {
      const { container } = render(<EmptyState title="No data" />);
      expect(container.querySelector('svg')).toBeInTheDocument();
    });

    it('should render noData variant', () => {
      const { container } = render(<EmptyState title="No data" variant="noData" />);
      expect(container.querySelector('svg')).toBeInTheDocument();
    });

    it('should render noResults variant', () => {
      const { container } = render(<EmptyState title="No results" variant="noResults" />);
      expect(container.querySelector('svg')).toBeInTheDocument();
    });

    it('should render noAccess variant', () => {
      const { container } = render(<EmptyState title="No access" variant="noAccess" />);
      expect(container.querySelector('svg')).toBeInTheDocument();
    });

    it('should render error variant', () => {
      const { container } = render(<EmptyState title="Error" variant="error" />);
      expect(container.querySelector('svg')).toBeInTheDocument();
    });
  });

  describe('Custom icon', () => {
    it('should render with custom icon', () => {
      const { container } = render(<EmptyState title="Upload files" icon={Upload} />);
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('should override variant icon with custom icon', () => {
      const { container } = render(<EmptyState title="Custom" variant="noResults" icon={Folder} />);
      // Should render the custom Folder icon, not the Search icon
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('Action button', () => {
    it('should render action button when provided', () => {
      const handleClick = vi.fn();
      render(<EmptyState title="No items" action={{ label: 'Add item', onClick: handleClick }} />);
      expect(screen.getByRole('button', { name: 'Add item' })).toBeInTheDocument();
    });

    it('should call onClick when action button is clicked', () => {
      const handleClick = vi.fn();
      render(<EmptyState title="No items" action={{ label: 'Add item', onClick: handleClick }} />);
      fireEvent.click(screen.getByRole('button', { name: 'Add item' }));
      expect(handleClick).toHaveBeenCalledTimes(1);
    });

    it('should render primary variant button by default', () => {
      render(<EmptyState title="No items" action={{ label: 'Add item', onClick: () => {} }} />);
      const button = screen.getByRole('button', { name: 'Add item' });
      expect(button).toHaveClass('bg-blue-600');
    });

    it('should render secondary variant button when specified', () => {
      render(
        <EmptyState
          title="No items"
          action={{ label: 'Add item', onClick: () => {}, variant: 'secondary' }}
        />
      );
      const button = screen.getByRole('button', { name: 'Add item' });
      expect(button).toHaveClass('bg-slate-100');
    });

    it('should not render action button when not provided', () => {
      render(<EmptyState title="No items" />);
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });
  });

  describe('Custom className', () => {
    it('should apply custom className', () => {
      render(<EmptyState title="Test" className="custom-class" />);
      const emptyState = screen.getByTestId('empty-state');
      expect(emptyState).toHaveClass('custom-class');
    });

    it('should preserve base classes with custom className', () => {
      render(<EmptyState title="Test" className="bg-red-500" />);
      const emptyState = screen.getByTestId('empty-state');
      expect(emptyState).toHaveClass('bg-red-500');
      expect(emptyState).toHaveClass('flex');
      expect(emptyState).toHaveClass('flex-col');
    });
  });

  describe('Accessibility', () => {
    it('should have role="status"', () => {
      render(<EmptyState title="No data" />);
      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('should have aria-live="polite"', () => {
      render(<EmptyState title="No data" />);
      const emptyState = screen.getByRole('status');
      expect(emptyState).toHaveAttribute('aria-live', 'polite');
    });

    it('should have icon with aria-hidden', () => {
      const { container } = render(<EmptyState title="No data" />);
      const iconWrapper = container.querySelector('[aria-hidden="true"]');
      expect(iconWrapper).toBeInTheDocument();
    });

    it('should include screen reader text with title', () => {
      render(<EmptyState title="No items found" />);
      // Title appears both in h3 and sr-only span
      const elements = screen.getAllByText(/No items found/);
      expect(elements.length).toBeGreaterThanOrEqual(1);
      // Verify the sr-only span exists
      const srOnlyElement = elements.find(el => el.classList.contains('sr-only'));
      expect(srOnlyElement).toBeInTheDocument();
    });

    it('should include screen reader text with description', () => {
      render(<EmptyState title="No items" description="Try adding some items" />);
      const srText = screen.getByText(/No items.*Try adding some items/s);
      expect(srText).toHaveClass('sr-only');
    });

    it('should include screen reader text with action', () => {
      render(<EmptyState title="No items" action={{ label: 'Add item', onClick: () => {} }} />);
      const srText = screen.getByText(/Action available: Add item/);
      expect(srText).toHaveClass('sr-only');
    });
  });

  describe('Reduced motion', () => {
    it('should have transition classes when reduced motion is false', () => {
      mockUseReducedMotion.mockReturnValue(false);
      const { container } = render(<EmptyState title="Test" />);
      const iconWrapper = container.querySelector('[aria-hidden="true"]');
      expect(iconWrapper).toHaveClass('transition-transform');
    });

    it('should not have transition classes when reduced motion is true', () => {
      mockUseReducedMotion.mockReturnValue(true);
      const { container } = render(<EmptyState title="Test" />);
      const iconWrapper = container.querySelector('[aria-hidden="true"]');
      expect(iconWrapper).not.toHaveClass('transition-transform');
    });

    it('should not have button transition when reduced motion is true', () => {
      mockUseReducedMotion.mockReturnValue(true);
      render(<EmptyState title="Test" action={{ label: 'Click', onClick: () => {} }} />);
      const button = screen.getByRole('button');
      expect(button).not.toHaveClass('transition-colors');
    });
  });

  describe('Snapshot tests', () => {
    it('should match snapshot for default variant', () => {
      const { container } = render(
        <EmptyState title="No items" description="Add your first item" />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for noResults variant with action', () => {
      const { container } = render(
        <EmptyState
          title="No results found"
          description="Try adjusting your search"
          variant="noResults"
          action={{ label: 'Clear filters', onClick: () => {} }}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for noAccess variant', () => {
      const { container } = render(
        <EmptyState
          title="Access denied"
          description="You do not have permission"
          variant="noAccess"
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for error variant', () => {
      const { container } = render(
        <EmptyState
          title="Something went wrong"
          description="Please try again later"
          variant="error"
          action={{ label: 'Retry', onClick: () => {}, variant: 'primary' }}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot with custom icon', () => {
      const { container } = render(
        <EmptyState title="Upload files" description="Drag and drop files here" icon={Upload} />
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });
});
