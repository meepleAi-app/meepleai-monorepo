/**
 * Unit tests for mention-demo page
 * Tests MentionInput demo page functionality
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MentionDemo from '../mention-demo';

// Mock MentionInput component
jest.mock('@/components/MentionInput', () => ({
  MentionInput: ({ value, onChange, placeholder }: any) => (
    <textarea
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      aria-label="Mention input"
    />
  ),
}));

describe('MentionDemo Page', () => {
  describe('Rendering', () => {
    it('should render page title', () => {
      render(<MentionDemo />);

      expect(screen.getByText('MentionInput Component Demo')).toBeInTheDocument();
    });

    it('should render instructions section', () => {
      render(<MentionDemo />);

      expect(screen.getByText('Instructions:')).toBeInTheDocument();
    });

    it('should render MentionInput component', () => {
      render(<MentionDemo />);

      expect(screen.getByLabelText('Mention input')).toBeInTheDocument();
    });

    it('should render current value display', () => {
      render(<MentionDemo />);

      expect(screen.getByText('Current Value:')).toBeInTheDocument();
    });

    it('should render test data section', () => {
      render(<MentionDemo />);

      expect(screen.getByText('Test Data:')).toBeInTheDocument();
    });
  });

  describe('Instructions Section', () => {
    it('should display all instruction items', () => {
      render(<MentionDemo />);

      expect(screen.getByText(/Type.*@ followed by at least 2 characters/)).toBeInTheDocument();
      expect(screen.getByText(/Use.*Arrow Keys.*to navigate/)).toBeInTheDocument();
      expect(screen.getByText(/Press.*Enter.*to select/)).toBeInTheDocument();
      expect(screen.getByText(/Press.*Escape.*to close/)).toBeInTheDocument();
      expect(screen.getByText(/Click on a suggestion/)).toBeInTheDocument();
    });

    it('should have blue info styling', () => {
      const { container } = render(<MentionDemo />);

      const infoBox = container.querySelector('[style*="background: #f0f9ff"]');

      expect(infoBox).toBeInTheDocument();
    });
  });

  describe('Test Data Section', () => {
    it('should display demo users information', () => {
      render(<MentionDemo />);

      expect(screen.getByText(/admin@meepleai.dev/)).toBeInTheDocument();
      expect(screen.getByText(/editor@meepleai.dev/)).toBeInTheDocument();
      expect(screen.getByText(/user@meepleai.dev/)).toBeInTheDocument();
    });

    it('should display user roles', () => {
      render(<MentionDemo />);

      expect(screen.getByText(/Admin User/)).toBeInTheDocument();
      expect(screen.getByText(/Editor User/)).toBeInTheDocument();
      expect(screen.getByText(/Regular User/)).toBeInTheDocument();
    });

    it('should display search examples', () => {
      render(<MentionDemo />);

      expect(screen.getByText(/Try searching: @admin, @editor, @user/)).toBeInTheDocument();
    });

    it('should have yellow info styling', () => {
      const { container } = render(<MentionDemo />);

      const testDataBox = container.querySelector('[style*="background: #fff9db"]');

      expect(testDataBox).toBeInTheDocument();
    });
  });

  describe('MentionInput Integration', () => {
    it('should render with correct placeholder', () => {
      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');

      expect(input).toHaveAttribute(
        'placeholder',
        'Try typing @admin or @user to see autocomplete...'
      );
    });

    it('should update value when typing', async () => {
      const user = userEvent.setup();

      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');
      await user.type(input, '@admin');

      expect(input).toHaveValue('@admin');
    });

    it('should display current value in preview', async () => {
      const user = userEvent.setup();

      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');
      await user.type(input, 'Hello @user');

      expect(screen.getByText('Hello @user')).toBeInTheDocument();
    });

    it('should show "(empty)" when no value', () => {
      render(<MentionDemo />);

      expect(screen.getByText('(empty)')).toBeInTheDocument();
    });

    it('should update preview as user types', async () => {
      const user = userEvent.setup();

      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');

      await user.type(input, 'Test');
      expect(screen.getByText('Test')).toBeInTheDocument();

      await user.clear(input);
      expect(screen.getByText('(empty)')).toBeInTheDocument();
    });
  });

  describe('Layout and Styling', () => {
    it('should have centered layout', () => {
      const { container } = render(<MentionDemo />);

      const mainDiv = container.querySelector('[style*="maxWidth: 800"]');

      expect(mainDiv).toBeInTheDocument();
      expect(mainDiv).toHaveStyle({ margin: '0 auto' });
    });

    it('should have proper spacing', () => {
      const { container } = render(<MentionDemo />);

      const mainDiv = container.querySelector('[style*="padding: 40"]');

      expect(mainDiv).toBeInTheDocument();
    });

    it('should render sections with proper borders', () => {
      const { container } = render(<MentionDemo />);

      const borderedSections = container.querySelectorAll('[style*="border"]');

      expect(borderedSections.length).toBeGreaterThan(0);
    });

    it('should have rounded corners on boxes', () => {
      const { container } = render(<MentionDemo />);

      const roundedBoxes = container.querySelectorAll('[style*="borderRadius: 4"]');

      expect(roundedBoxes.length).toBeGreaterThan(0);
    });
  });

  describe('Code Snippets', () => {
    it('should display code elements for emails', () => {
      const { container } = render(<MentionDemo />);

      const codeElements = container.querySelectorAll('code');

      expect(codeElements.length).toBeGreaterThan(0);
    });

    it('should show code for demo users', () => {
      render(<MentionDemo />);

      // Check that email addresses are wrapped in code tags
      const adminCode = screen.getByText('admin@meepleai.dev');
      const editorCode = screen.getByText('editor@meepleai.dev');
      const userCode = screen.getByText('user@meepleai.dev');

      expect(adminCode.closest('code')).toBeInTheDocument();
      expect(editorCode.closest('code')).toBeInTheDocument();
      expect(userCode.closest('code')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty initial state', () => {
      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');

      expect(input).toHaveValue('');
      expect(screen.getByText('(empty)')).toBeInTheDocument();
    });

    it('should handle long text input', async () => {
      const user = userEvent.setup();
      const longText = 'This is a very long message '.repeat(10) + '@admin';

      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');
      await user.type(input, longText);

      expect(input).toHaveValue(longText);
    });

    it('should handle special characters', async () => {
      const user = userEvent.setup();

      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');
      await user.type(input, '@user!@#$%');

      expect(input).toHaveValue('@user!@#$%');
    });

    it('should handle rapid typing', async () => {
      const user = userEvent.setup();

      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');
      await user.type(input, '@admin @editor @user');

      expect(input).toHaveValue('@admin @editor @user');
    });
  });

  describe('Accessibility', () => {
    it('should have heading hierarchy', () => {
      const { container } = render(<MentionDemo />);

      const h1 = container.querySelector('h1');
      const h2s = container.querySelectorAll('h2');
      const h3s = container.querySelectorAll('h3');

      expect(h1).toBeInTheDocument();
      expect(h2s.length).toBeGreaterThan(0);
      expect(h3s.length).toBeGreaterThan(0);
    });

    it('should have proper semantic HTML', () => {
      const { container } = render(<MentionDemo />);

      expect(container.querySelector('ul')).toBeInTheDocument();
      expect(container.querySelector('pre')).toBeInTheDocument();
    });

    it('should have labeled input', () => {
      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');

      expect(input).toBeInTheDocument();
    });
  });

  describe('Integration', () => {
    it('should maintain state across interactions', async () => {
      const user = userEvent.setup();

      render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');

      // Type something
      await user.type(input, 'First message');
      expect(screen.getByText('First message')).toBeInTheDocument();

      // Clear and type again
      await user.clear(input);
      await user.type(input, 'Second message');
      expect(screen.getByText('Second message')).toBeInTheDocument();

      // Verify first message is gone
      expect(screen.queryByText('First message')).not.toBeInTheDocument();
    });
  });
});
