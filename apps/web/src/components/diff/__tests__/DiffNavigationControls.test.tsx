import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DiffNavigationControls } from '../DiffNavigationControls';

describe('DiffNavigationControls', () => {
  const mockOnPrev = vi.fn();
  const mockOnNext = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render navigation controls with position indicator', () => {
      render(
        <DiffNavigationControls
          currentIndex={0}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      expect(screen.getByText('1 / 5 changes')).toBeInTheDocument();
      expect(screen.getByRole('navigation', { name: 'Diff navigation' })).toBeInTheDocument();
    });

    it('should render prev and next buttons', () => {
      render(
        <DiffNavigationControls
          currentIndex={1}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      expect(screen.getByLabelText('Previous change')).toBeInTheDocument();
      expect(screen.getByLabelText('Next change')).toBeInTheDocument();
    });

    it('should not render when totalChanges is 0', () => {
      const { container } = render(
        <DiffNavigationControls
          currentIndex={0}
          totalChanges={0}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should show correct position for middle index', () => {
      render(
        <DiffNavigationControls
          currentIndex={2}
          totalChanges={10}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      expect(screen.getByText('3 / 10 changes')).toBeInTheDocument();
    });
  });

  describe('Button States', () => {
    it('should disable prev button at first change', () => {
      render(
        <DiffNavigationControls
          currentIndex={0}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      expect(prevButton).toBeDisabled();
    });

    it('should enable prev button when not at first change', () => {
      render(
        <DiffNavigationControls
          currentIndex={1}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      expect(prevButton).not.toBeDisabled();
    });

    it('should disable next button at last change', () => {
      render(
        <DiffNavigationControls
          currentIndex={4}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const nextButton = screen.getByLabelText('Next change');
      expect(nextButton).toBeDisabled();
    });

    it('should enable next button when not at last change', () => {
      render(
        <DiffNavigationControls
          currentIndex={0}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const nextButton = screen.getByLabelText('Next change');
      expect(nextButton).not.toBeDisabled();
    });

    it('should disable both buttons when disabled prop is true', () => {
      render(
        <DiffNavigationControls
          currentIndex={2}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
          disabled={true}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      const nextButton = screen.getByLabelText('Next change');

      expect(prevButton).toBeDisabled();
      expect(nextButton).toBeDisabled();
    });

    it('should respect boundaries when disabled is false', () => {
      render(
        <DiffNavigationControls
          currentIndex={2}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
          disabled={false}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      const nextButton = screen.getByLabelText('Next change');

      expect(prevButton).not.toBeDisabled();
      expect(nextButton).not.toBeDisabled();
    });
  });

  describe('User Interactions', () => {
    it('should call onPrev when prev button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <DiffNavigationControls
          currentIndex={1}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      await user.click(prevButton);

      expect(mockOnPrev).toHaveBeenCalledTimes(1);
    });

    it('should call onNext when next button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <DiffNavigationControls
          currentIndex={0}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const nextButton = screen.getByLabelText('Next change');
      await user.click(nextButton);

      expect(mockOnNext).toHaveBeenCalledTimes(1);
    });

    it('should not call onPrev when prev button is disabled', async () => {
      const user = userEvent.setup();

      render(
        <DiffNavigationControls
          currentIndex={0}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      await user.click(prevButton);

      expect(mockOnPrev).not.toHaveBeenCalled();
    });

    it('should not call onNext when next button is disabled', async () => {
      const user = userEvent.setup();

      render(
        <DiffNavigationControls
          currentIndex={4}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const nextButton = screen.getByLabelText('Next change');
      await user.click(nextButton);

      expect(mockOnNext).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA labels', () => {
      render(
        <DiffNavigationControls
          currentIndex={0}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      expect(screen.getByLabelText('Previous change')).toBeInTheDocument();
      expect(screen.getByLabelText('Next change')).toBeInTheDocument();
      expect(screen.getByRole('navigation', { name: 'Diff navigation' })).toBeInTheDocument();
    });

    it('should have keyboard shortcut hints in titles', () => {
      render(
        <DiffNavigationControls
          currentIndex={2}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      const nextButton = screen.getByLabelText('Next change');

      expect(prevButton).toHaveAttribute('title', 'Previous change (Alt+Up)');
      expect(nextButton).toHaveAttribute('title', 'Next change (Alt+Down)');
    });
  });

  describe('Edge Cases', () => {
    it('should handle single change (currentIndex=0, totalChanges=1)', () => {
      render(
        <DiffNavigationControls
          currentIndex={0}
          totalChanges={1}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      expect(screen.getByText('1 / 1 changes')).toBeInTheDocument();

      const prevButton = screen.getByLabelText('Previous change');
      const nextButton = screen.getByLabelText('Next change');

      expect(prevButton).toBeDisabled();
      expect(nextButton).toBeDisabled();
    });

    it('should handle large number of changes', () => {
      render(
        <DiffNavigationControls
          currentIndex={499}
          totalChanges={1000}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      expect(screen.getByText('500 / 1000 changes')).toBeInTheDocument();
    });

    it('should default disabled to false', () => {
      render(
        <DiffNavigationControls
          currentIndex={2}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      const nextButton = screen.getByLabelText('Next change');

      expect(prevButton).not.toBeDisabled();
      expect(nextButton).not.toBeDisabled();
    });
  });

  describe('Button Text and Icons', () => {
    it('should display arrow icons in buttons', () => {
      render(
        <DiffNavigationControls
          currentIndex={2}
          totalChanges={5}
          onPrev={mockOnPrev}
          onNext={mockOnNext}
        />
      );

      // Updated: shadcn Button uses Lucide icons (ChevronUp, ChevronDown) instead of text arrows
      const prevButton = screen.getByLabelText('Previous change');
      const nextButton = screen.getByLabelText('Next change');

      expect(prevButton).toHaveTextContent('Prev');
      expect(nextButton).toHaveTextContent('Next');

      // Icons are rendered as SVG
      expect(prevButton.querySelector('svg')).toBeInTheDocument();
      expect(nextButton.querySelector('svg')).toBeInTheDocument();
    });
  });
});
