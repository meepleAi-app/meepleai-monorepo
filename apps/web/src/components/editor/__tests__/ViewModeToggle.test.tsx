import { render, screen, fireEvent } from '@testing-library/react';
import ViewModeToggle from '../ViewModeToggle';

describe('ViewModeToggle', () => {
  const mockOnModeChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders both mode buttons', () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    expect(screen.getByText(/📝 Editor Visuale/)).toBeInTheDocument();
    expect(screen.getByText(/\{ \} Codice JSON/)).toBeInTheDocument();
  });

  it('highlights rich mode when active', () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    // Check Tailwind classes instead of inline styles
    expect(richButton).toHaveClass('bg-white', 'text-blue-600', 'font-bold', 'shadow-sm');
  });

  it('highlights json mode when active', () => {
    render(<ViewModeToggle mode="json" onModeChange={mockOnModeChange} />);

    const jsonButton = screen.getByText(/\{ \} Codice JSON/);
    // Check Tailwind classes instead of inline styles
    expect(jsonButton).toHaveClass('bg-white', 'text-blue-600', 'font-bold', 'shadow-sm');
  });

  it('does not highlight inactive rich mode', () => {
    render(<ViewModeToggle mode="json" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    // Check Tailwind classes instead of inline styles
    expect(richButton).toHaveClass('bg-transparent', 'text-gray-600', 'font-normal');
  });

  it('does not highlight inactive json mode', () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const jsonButton = screen.getByText(/\{ \} Codice JSON/);
    // Check Tailwind classes instead of inline styles
    expect(jsonButton).toHaveClass('bg-transparent', 'text-gray-600', 'font-normal');
  });

  it("calls onModeChange with 'rich' when rich button is clicked", () => {
    render(<ViewModeToggle mode="json" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    fireEvent.click(richButton);

    expect(mockOnModeChange).toHaveBeenCalledWith('rich');
    expect(mockOnModeChange).toHaveBeenCalledTimes(1);
  });

  it("calls onModeChange with 'json' when json button is clicked", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const jsonButton = screen.getByText(/\{ \} Codice JSON/);
    fireEvent.click(jsonButton);

    expect(mockOnModeChange).toHaveBeenCalledWith('json');
    expect(mockOnModeChange).toHaveBeenCalledTimes(1);
  });

  it("allows clicking the same mode button (doesn't prevent redundant clicks)", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    fireEvent.click(richButton);

    expect(mockOnModeChange).toHaveBeenCalledWith('rich');
  });

  it('displays tooltip titles for accessibility', () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    const jsonButton = screen.getByText(/\{ \} Codice JSON/);

    expect(richButton).toHaveAttribute('title', 'Editor visuale con formattazione');
    expect(jsonButton).toHaveAttribute('title', 'Visualizza e modifica JSON direttamente');
  });

  it('has proper container styling', () => {
    const { container } = render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const wrapper = container.firstChild as HTMLElement;
    // Check Tailwind classes instead of inline styles
    expect(wrapper).toHaveClass('inline-flex', 'bg-gray-100', 'rounded', 'p-0.5');
  });

  it('applies box shadow to active button', () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    // Check Tailwind class shadow-sm instead of inline style
    expect(richButton).toHaveClass('shadow-sm');
  });

  it('does not apply box shadow to inactive button', () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const jsonButton = screen.getByText(/\{ \} Codice JSON/);
    // Inactive buttons don't have shadow-sm class
    expect(jsonButton).not.toHaveClass('shadow-sm');
  });

  it('should render with default props', () => {
    const { container } = render(<ViewModeToggle children={<div>Test Content</div>} />);
    expect(container.firstChild).toBeInTheDocument();
  });
});
