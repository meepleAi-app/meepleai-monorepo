/**
 * Unit tests for mention-demo page
 * Tests MentionInput demo page functionality
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MentionDemo from '@/pages/mention-demo';

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
      const { container } = render(<MentionDemo />);

      // Get the first ul (instructions list, not test data list)
      const instructionsList = container.querySelector('ul');
      const listItems = instructionsList?.querySelectorAll('li');

      expect(listItems).toHaveLength(5);

      // Verify key instruction text is present in the instructions list
      const allText = Array.from(listItems || []).map(li => li.textContent).join(' ');
      expect(allText).toContain('Type');
      expect(allText).toContain('@');
      expect(allText).toContain('at least 2 characters');
      expect(allText).toContain('Arrow Keys');
      expect(allText).toContain('navigate');
      expect(allText).toContain('Enter');
      expect(allText).toContain('select');
      expect(allText).toContain('Escape');
      expect(allText).toContain('close');
      expect(allText).toContain('Click on a suggestion');
    });

    it('should have blue info styling', () => {
      const { container } = render(<MentionDemo />);

      // Check for div with blue background (instructions section)
      const divs = container.querySelectorAll('div');
      const infoBox = Array.from(divs).find((el: Element) => {
        const styles = (el as HTMLElement).style;
        return styles.background === '#f0f9ff' || styles.background === 'rgb(240, 249, 255)';
      });

      expect(infoBox).toBeTruthy();
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

      // Check for div with yellow background (test data section)
      const divs = container.querySelectorAll('div');
      const testDataBox = Array.from(divs).find((el: Element) => {
        const styles = (el as HTMLElement).style;
        return styles.background === '#fff9db' || styles.background === 'rgb(255, 249, 219)';
      });

      expect(testDataBox).toBeTruthy();
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

      const { container } = render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');
      await user.type(input, 'Hello @user');

      // Check the preview pre element, not the input
      const preview = container.querySelector('pre');
      expect(preview).toHaveTextContent('Hello @user');
    });

    it('should show "(empty)" when no value', () => {
      render(<MentionDemo />);

      expect(screen.getByText('(empty)')).toBeInTheDocument();
    });

    it('should update preview as user types', async () => {
      const user = userEvent.setup();

      const { container } = render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');
      const preview = container.querySelector('pre');

      await user.type(input, 'Test');
      expect(preview).toHaveTextContent('Test');

      await user.clear(input);
      expect(preview).toHaveTextContent('(empty)');
    });
  });

  describe('Layout and Styling', () => {
    it('should have centered layout', () => {
      const { container } = render(<MentionDemo />);

      // The main div is the first child of the rendered component
      const mainDiv = container.firstElementChild as HTMLElement;

      expect(mainDiv).toBeInTheDocument();
      expect(mainDiv).toHaveStyle({
        maxWidth: '800px',
        margin: '0 auto',
        padding: '40px'
      });
    });

    it('should have proper spacing', () => {
      const { container } = render(<MentionDemo />);

      const mainDiv = container.firstElementChild as HTMLElement;

      expect(mainDiv).toHaveStyle({ padding: '40px' });
    });

    it('should render sections with proper borders', () => {
      const { container } = render(<MentionDemo />);

      // Check for info boxes which have borders (there are 3: instructions, preview, test data)
      const infoBoxes = container.querySelectorAll('div');
      const boxesWithBorders = Array.from(infoBoxes).filter((el: Element) => {
        const styles = (el as HTMLElement).style;
        // Check for any border property
        return styles.border.length > 0 || styles.borderWidth.length > 0;
      });

      expect(boxesWithBorders.length).toBeGreaterThanOrEqual(3);
    });

    it('should have rounded corners on boxes', () => {
      const { container } = render(<MentionDemo />);

      // Check for elements with borderRadius (there are 3: instructions, preview, test data)
      const styledDivs = container.querySelectorAll('div');
      const roundedBoxes = Array.from(styledDivs).filter((el: Element) => {
        const styles = (el as HTMLElement).style;
        // borderRadius can be '4px' or just '4' depending on how React sets it
        return styles.borderRadius.includes('4');
      });

      expect(roundedBoxes.length).toBeGreaterThanOrEqual(3);
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

      const { container } = render(<MentionDemo />);

      const input = screen.getByLabelText('Mention input');
      const preview = container.querySelector('pre');

      // Type something
      await user.type(input, 'First message');
      expect(preview).toHaveTextContent('First message');

      // Clear and type again
      await user.clear(input);
      await user.type(input, 'Second message');
      expect(preview).toHaveTextContent('Second message');

      // Verify first message is gone from preview
      expect(preview).not.toHaveTextContent('First message');
    });
  });
});
