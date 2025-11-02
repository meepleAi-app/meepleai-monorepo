/**
 * Unit tests for locale-aware testing utilities
 * Tests custom RTL queries for localized content
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  getByLocalizedNumber,
  getByLocalizedNumberScreen,
  getByTextInDialog,
  queryByTextInDialog,
  getByRoleInDialog,
  createLocaleFlexiblePattern,
} from '../locale-queries';

describe('Locale Queries', () => {
  describe('getByLocalizedNumber', () => {
    it('should find number with comma separator (en-US format)', () => {
      const { container } = render(<div>Total: 1,000</div>);

      const element = getByLocalizedNumber(container, 1000);

      expect(element).toBeInTheDocument();
      expect(element.textContent).toContain('1,000');
    });

    it('should find number with period separator (de-DE format)', () => {
      const { container } = render(<div>Total: 1.000</div>);

      const element = getByLocalizedNumber(container, 1000);

      expect(element).toBeInTheDocument();
      expect(element.textContent).toContain('1.000');
    });

    it('should find number without separator', () => {
      const { container } = render(<div>Total: 1000</div>);

      const element = getByLocalizedNumber(container, 1000);

      expect(element).toBeInTheDocument();
      expect(element.textContent).toContain('1000');
    });

    it('should find large numbers with multiple separators', () => {
      const { container } = render(<div>Count: 1,000,000</div>);

      const element = getByLocalizedNumber(container, 1000000);

      expect(element).toBeInTheDocument();
    });

    it('should handle small numbers without separators', () => {
      const { container } = render(<div>Count: 100</div>);

      const element = getByLocalizedNumber(container, 100);

      expect(element).toBeInTheDocument();
      expect(element.textContent).toContain('100');
    });

    it('should handle single digit numbers', () => {
      const { container } = render(<div>Count: 5</div>);

      const element = getByLocalizedNumber(container, 5);

      expect(element).toBeInTheDocument();
      expect(element.textContent).toContain('5');
    });

    it('should throw error when number not found', () => {
      const { container } = render(<div>Total: 500</div>);

      expect(() => getByLocalizedNumber(container, 1000)).toThrow();
    });

    it('should find number in specific container, not globally', () => {
      render(
        <div>
          <div data-testid="container1">Count: 100</div>
          <div data-testid="container2">Count: 200</div>
        </div>
      );

      const container1 = screen.getByTestId('container1');

      const element = getByLocalizedNumber(container1, 100);

      expect(element).toBeInTheDocument();
      expect(element.textContent).toContain('100');
    });
  });

  describe('getByLocalizedNumberScreen', () => {
    it('should find number using document.body as container', () => {
      render(<div>Total: 1,000</div>);

      const element = getByLocalizedNumberScreen(1000);

      expect(element).toBeInTheDocument();
      expect(element.textContent).toContain('1,000');
    });

    it('should work with numbers formatted differently', () => {
      render(<div>Value: 5000</div>);

      const element = getByLocalizedNumberScreen(5000);

      expect(element).toBeInTheDocument();
    });

    it('should throw when number not found in document', () => {
      render(<div>Nothing here</div>);

      expect(() => getByLocalizedNumberScreen(999)).toThrow();
    });
  });

  describe('getByTextInDialog', () => {
    it('should find text within dialog', () => {
      render(
        <div role="dialog">
          <h2>Confirm Action</h2>
          <p>Are you sure?</p>
        </div>
      );

      const heading = getByTextInDialog('Confirm Action');

      expect(heading).toBeInTheDocument();
      expect(heading.textContent).toBe('Confirm Action');
    });

    it('should not find text outside dialog', () => {
      render(
        <>
          <div>Outside text</div>
          <div role="dialog">
            <p>Inside text</p>
          </div>
        </>
      );

      expect(() => getByTextInDialog('Outside text')).toThrow();
    });

    it('should work with regex patterns', () => {
      render(
        <div role="dialog">
          <p>Are you sure you want to continue?</p>
        </div>
      );

      const element = getByTextInDialog(/Are you sure/i);

      expect(element).toBeInTheDocument();
    });

    it('should throw when dialog not found', () => {
      render(<div>No dialog here</div>);

      expect(() => getByTextInDialog('Any text')).toThrow();
    });

    it('should throw when text not in dialog', () => {
      render(
        <div role="dialog">
          <p>Different text</p>
        </div>
      );

      expect(() => getByTextInDialog('Missing text')).toThrow();
    });

    it('should scope to dialog when text appears multiple times', () => {
      render(
        <>
          <button>Confirm</button>
          <div role="dialog">
            <h2>Confirm</h2>
            <button>Cancel</button>
          </div>
        </>
      );

      const dialogText = getByTextInDialog('Confirm');

      // Should find the one inside dialog (h2), not the button outside
      expect(dialogText.tagName).toBe('H2');
    });
  });

  describe('queryByTextInDialog', () => {
    it('should return element when text found in dialog', () => {
      render(
        <div role="dialog">
          <p>Found text</p>
        </div>
      );

      const element = queryByTextInDialog('Found text');

      expect(element).toBeInTheDocument();
    });

    it('should return null when dialog not found', () => {
      render(<div>No dialog</div>);

      const element = queryByTextInDialog('Any text');

      expect(element).toBeNull();
    });

    it('should return null when text not in dialog', () => {
      render(
        <div role="dialog">
          <p>Different text</p>
        </div>
      );

      const element = queryByTextInDialog('Missing text');

      expect(element).toBeNull();
    });

    it('should work with regex patterns', () => {
      render(
        <div role="dialog">
          <p>Pattern matching text</p>
        </div>
      );

      const element = queryByTextInDialog(/Pattern/i);

      expect(element).toBeInTheDocument();
    });

    it('should handle errors gracefully', () => {
      render(<div>Empty</div>);

      const element = queryByTextInDialog('Test');

      expect(element).toBeNull();
    });
  });

  describe('getByRoleInDialog', () => {
    it('should find button in dialog by role', () => {
      render(
        <div role="dialog">
          <button>Confirm</button>
          <button>Cancel</button>
        </div>
      );

      const button = getByRoleInDialog('button', { name: 'Confirm' });

      expect(button).toBeInTheDocument();
      expect(button).toHaveTextContent('Confirm');
    });

    it('should find input in dialog by role', () => {
      render(
        <div role="dialog">
          <label htmlFor="username">Username</label>
          <input id="username" type="text" />
        </div>
      );

      const input = getByRoleInDialog('textbox', { name: 'Username' });

      expect(input).toBeInTheDocument();
      expect(input).toHaveAttribute('type', 'text');
    });

    it('should throw when dialog not found', () => {
      render(<div>No dialog</div>);

      expect(() => getByRoleInDialog('button')).toThrow();
    });

    it('should throw when role not found in dialog', () => {
      render(
        <div role="dialog">
          <p>No buttons here</p>
        </div>
      );

      expect(() => getByRoleInDialog('button')).toThrow();
    });

    it('should scope to dialog only', () => {
      render(
        <>
          <button>Outside Button</button>
          <div role="dialog">
            <button>Inside Button</button>
          </div>
        </>
      );

      const button = getByRoleInDialog('button', { name: 'Inside Button' });

      expect(button).toBeInTheDocument();
      expect(button).toHaveTextContent('Inside Button');
    });
  });

  describe('createLocaleFlexiblePattern', () => {
    it('should create basic pattern without options', () => {
      const pattern = createLocaleFlexiblePattern('Total:');

      expect(pattern).toBeInstanceOf(RegExp);
      expect(pattern.source).toBe('Total:');
      expect(pattern.flags).not.toContain('i');
    });

    it('should create pattern with number option', () => {
      const pattern = createLocaleFlexiblePattern('Total:', { number: 1000 });

      expect(pattern).toBeInstanceOf(RegExp);
      expect(pattern.source).toContain('1');
      expect(pattern.source).toContain('0');
    });

    it('should match numbers with comma separator', () => {
      const pattern = createLocaleFlexiblePattern('Count:', { number: 1000 });

      expect(pattern.test('Count: 1,000')).toBe(true);
    });

    it('should match numbers with period separator', () => {
      const pattern = createLocaleFlexiblePattern('Count:', { number: 1000 });

      expect(pattern.test('Count: 1.000')).toBe(true);
    });

    it('should match numbers without separator', () => {
      const pattern = createLocaleFlexiblePattern('Count:', { number: 1000 });

      expect(pattern.test('Count: 1000')).toBe(true);
    });

    it('should create case-insensitive pattern', () => {
      const pattern = createLocaleFlexiblePattern('Total:', {
        caseInsensitive: true,
      });

      expect(pattern.flags).toContain('i');
      expect(pattern.test('TOTAL:')).toBe(true);
      expect(pattern.test('total:')).toBe(true);
    });

    it('should combine number and case-insensitive options', () => {
      const pattern = createLocaleFlexiblePattern('Total:', {
        number: 500,
        caseInsensitive: true,
      });

      expect(pattern.test('TOTAL: 500')).toBe(true);
      expect(pattern.test('total: 500')).toBe(true);
    });

    it('should handle zero as number', () => {
      const pattern = createLocaleFlexiblePattern('Count:', { number: 0 });

      expect(pattern.test('Count: 0')).toBe(true);
    });

    it('should work in RTL queries', () => {
      render(<div>Total: 1,000 items</div>);

      const pattern = createLocaleFlexiblePattern('Total:', {
        number: 1000,
        caseInsensitive: true,
      });

      expect(screen.getByText(pattern)).toBeInTheDocument();
    });
  });

  describe('Integration Tests', () => {
    it('should work with real component rendering localized numbers', () => {
      const Component = () => {
        const value = 1000;
        return <div>Total Requests: {value.toLocaleString()}</div>;
      };

      render(<Component />);

      // Works regardless of how toLocaleString() formats the number
      const element = getByLocalizedNumberScreen(1000);

      expect(element).toBeInTheDocument();
    });

    it('should handle dialog with localized content', () => {
      const Dialog = () => (
        <div role="dialog">
          <h2>Statistics</h2>
          <p>Total: {(5000).toLocaleString()}</p>
          <button>Close</button>
        </div>
      );

      const { container } = render(<Dialog />);

      const title = getByTextInDialog('Statistics');
      const closeBtn = getByRoleInDialog('button', { name: 'Close' });
      const dialog = screen.getByRole('dialog');
      const totalElement = getByLocalizedNumber(dialog, 5000);

      expect(title).toBeInTheDocument();
      expect(closeBtn).toBeInTheDocument();
      expect(totalElement).toBeInTheDocument();
    });
  });
});
