/**
 * Tests for LoadingButton component
 * Button with integrated loading state and spinner
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { LoadingButton } from '../LoadingButton';

describe('LoadingButton', () => {
  describe('Basic rendering', () => {
    it('should render children when not loading', () => {
      render(<LoadingButton>Click me</LoadingButton>);
      expect(screen.getByRole('button')).toHaveTextContent('Click me');
    });

    it('should render as a button element', () => {
      render(<LoadingButton>Submit</LoadingButton>);
      const button = screen.getByRole('button');
      expect(button.tagName).toBe('BUTTON');
    });

    it('should apply default button type', () => {
      render(<LoadingButton>Submit</LoadingButton>);
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('type', 'button');
    });
  });

  describe('Loading state', () => {
    it('should show spinner when isLoading is true', () => {
      const { container } = render(<LoadingButton isLoading={true}>Submit</LoadingButton>);
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });

    it('should not show spinner when isLoading is false', () => {
      const { container } = render(<LoadingButton isLoading={false}>Submit</LoadingButton>);
      const spinner = container.querySelector('svg');
      expect(spinner).not.toBeInTheDocument();
    });

    it('should hide children when loading and no loadingText provided', () => {
      render(<LoadingButton isLoading={true}>Original Text</LoadingButton>);
      expect(screen.queryByText('Original Text')).not.toBeInTheDocument();
    });

    it('should show loadingText when provided and loading', () => {
      render(
        <LoadingButton isLoading={true} loadingText="Processing...">
          Submit
        </LoadingButton>
      );
      expect(screen.getByText('Processing...')).toBeInTheDocument();
      expect(screen.queryByText('Submit')).not.toBeInTheDocument();
    });

    it('should show children when not loading even with loadingText prop', () => {
      render(
        <LoadingButton isLoading={false} loadingText="Loading...">
          Submit
        </LoadingButton>
      );
      expect(screen.getByText('Submit')).toBeInTheDocument();
      expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
    });
  });

  describe('Button disabled state', () => {
    it('should disable button when loading', () => {
      render(<LoadingButton isLoading={true}>Submit</LoadingButton>);
      expect(screen.getByRole('button')).toBeDisabled();
    });

    it('should not disable button when not loading', () => {
      render(<LoadingButton isLoading={false}>Submit</LoadingButton>);
      expect(screen.getByRole('button')).not.toBeDisabled();
    });

    it('should respect disabled prop when not loading', () => {
      render(<LoadingButton disabled={true}>Submit</LoadingButton>);
      expect(screen.getByRole('button')).toBeDisabled();
    });

    it('should be disabled when both disabled and isLoading are true', () => {
      render(
        <LoadingButton disabled={true} isLoading={true}>
          Submit
        </LoadingButton>
      );
      expect(screen.getByRole('button')).toBeDisabled();
    });
  });

  describe('Opacity styling', () => {
    it('should reduce opacity when loading', () => {
      render(<LoadingButton isLoading={true}>Submit</LoadingButton>);
      const button = screen.getByRole('button');
      expect(button).toHaveClass('opacity-70');
    });

    it('should have full opacity when not loading', () => {
      render(<LoadingButton isLoading={false}>Submit</LoadingButton>);
      const button = screen.getByRole('button');
      expect(button).not.toHaveClass('opacity-70');
    });
  });

  describe('Accessibility', () => {
    it('should have aria-busy attribute when loading', () => {
      render(<LoadingButton isLoading={true}>Submit</LoadingButton>);
      expect(screen.getByRole('button')).toHaveAttribute('aria-busy', 'true');
    });

    it('should have aria-busy=false when not loading', () => {
      render(<LoadingButton isLoading={false}>Submit</LoadingButton>);
      expect(screen.getByRole('button')).toHaveAttribute('aria-busy', 'false');
    });

    it('should have aria-live attribute', () => {
      render(<LoadingButton isLoading={true}>Submit</LoadingButton>);
      expect(screen.getByRole('button')).toHaveAttribute('aria-live', 'polite');
    });
  });

  describe('Event handlers', () => {
    it('should call onClick when not loading', () => {
      const handleClick = jest.fn();
      render(<LoadingButton onClick={handleClick}>Submit</LoadingButton>);
      fireEvent.click(screen.getByRole('button'));
      expect(handleClick).toHaveBeenCalledTimes(1);
    });

    it('should not call onClick when loading (button is disabled)', () => {
      const handleClick = jest.fn();
      render(
        <LoadingButton onClick={handleClick} isLoading={true}>
          Submit
        </LoadingButton>
      );
      fireEvent.click(screen.getByRole('button'));
      expect(handleClick).not.toHaveBeenCalled();
    });
  });

  describe('Spinner size', () => {
    it('should render spinner with custom size', () => {
      const { container } = render(
        <LoadingButton isLoading={true} spinnerSize="lg">
          Submit
        </LoadingButton>
      );
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveAttribute('width', '32'); // lg = 32px
    });

    it('should default to medium spinner size', () => {
      const { container } = render(<LoadingButton isLoading={true}>Submit</LoadingButton>);
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveAttribute('width', '24'); // md = 24px
    });
  });

  describe('Props forwarding', () => {
    it('should forward className to button', () => {
      render(<LoadingButton className="custom-class">Submit</LoadingButton>);
      expect(screen.getByRole('button')).toHaveClass('custom-class');
    });

    it('should forward type prop', () => {
      render(<LoadingButton type="submit">Submit</LoadingButton>);
      expect(screen.getByRole('button')).toHaveAttribute('type', 'submit');
    });

    it('should forward other HTML button attributes', () => {
      render(
        <LoadingButton data-testid="my-button" aria-label="Submit form">
          Submit
        </LoadingButton>
      );
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('data-testid', 'my-button');
      expect(button).toHaveAttribute('aria-label', 'Submit form');
    });
  });

  describe('Snapshot tests', () => {
    it('should match snapshot in default state', () => {
      const { container } = render(<LoadingButton>Submit</LoadingButton>);
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot in loading state', () => {
      const { container } = render(
        <LoadingButton isLoading={true} loadingText="Processing...">
          Submit
        </LoadingButton>
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });
});
