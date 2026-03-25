import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { LibraryEmptyState } from '@/components/library/LibraryEmptyState';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('LibraryEmptyState (immersive)', () => {
  const defaultProps = {
    onExploreCatalog: vi.fn(),
    onImportBgg: vi.fn(),
    onCreateCustom: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders animated illustration area as aria-hidden', () => {
    const { container } = render(<LibraryEmptyState {...defaultProps} />);
    const illustration = container.querySelector('[aria-hidden="true"]');
    expect(illustration).toBeInTheDocument();
  });

  it('renders gradient title', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    expect(screen.getByText(/la tua collezione ti aspetta/i)).toBeInTheDocument();
  });

  it('renders 3 quick-start action cards', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    expect(screen.getByText(/esplora il catalogo/i)).toBeInTheDocument();
    expect(screen.getByText(/importa da bgg/i)).toBeInTheDocument();
    expect(screen.getByText(/crea gioco custom/i)).toBeInTheDocument();
  });

  it('calls onExploreCatalog when clicking explore card', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    fireEvent.click(screen.getByText(/esplora il catalogo/i));
    expect(defaultProps.onExploreCatalog).toHaveBeenCalledOnce();
  });

  it('calls onImportBgg when clicking import card', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    fireEvent.click(screen.getByText(/importa da bgg/i));
    expect(defaultProps.onImportBgg).toHaveBeenCalledOnce();
  });

  it('calls onCreateCustom when clicking create card', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    fireEvent.click(screen.getByText(/crea gioco custom/i));
    expect(defaultProps.onCreateCustom).toHaveBeenCalledOnce();
  });

  it('renders trending games section', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    expect(screen.getByText(/popolari questa settimana/i)).toBeInTheDocument();
  });

  it('renders without optional callbacks (backward compat)', () => {
    render(<LibraryEmptyState />);
    expect(screen.getByText(/la tua collezione ti aspetta/i)).toBeInTheDocument();
  });

  it('preserves data-testid for existing test compatibility', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
  });
});
