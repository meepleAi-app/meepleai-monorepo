/**
 * Accessibility Tests for Select (UI-06)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
  SelectGroup,
  SelectLabel,
} from '../select';

describe('Select - Accessibility', () => {
  it('should have no accessibility violations (default select)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-select-default">Select Option</label>
        <Select>
          <SelectTrigger id="test-select-default">
            <SelectValue placeholder="Choose an option" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="option1">Option 1</SelectItem>
            <SelectItem value="option2">Option 2</SelectItem>
          </SelectContent>
        </Select>
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (disabled select)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-select-disabled">Select Option</label>
        <Select>
          <SelectTrigger id="test-select-disabled" disabled>
            <SelectValue placeholder="Disabled select" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="option1">Option 1</SelectItem>
          </SelectContent>
        </Select>
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (grouped select)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-select-grouped">Select Grouped Option</label>
        <Select>
          <SelectTrigger id="test-select-grouped">
            <SelectValue placeholder="Select grouped option" />
          </SelectTrigger>
          <SelectContent>
            <SelectGroup>
              <SelectLabel>Group 1</SelectLabel>
              <SelectItem value="option1">Option 1</SelectItem>
            </SelectGroup>
          </SelectContent>
        </Select>
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with aria-label)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-select-aria">Select Item</label>
        <Select>
          <SelectTrigger id="test-select-aria" aria-label="Select an item">
            <SelectValue placeholder="Choose" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="option1">Option 1</SelectItem>
          </SelectContent>
        </Select>
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper focus ring for keyboard navigation', () => {
    const { getByRole } = render(
      <div>
        <label htmlFor="test-select-focus">Select Option</label>
        <Select>
          <SelectTrigger id="test-select-focus">
            <SelectValue placeholder="Focus test" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="option1">Option 1</SelectItem>
          </SelectContent>
        </Select>
      </div>
    );

    const trigger = getByRole('combobox');
    expect(trigger.className).toContain('focus:ring-2');
  });

  it('should have proper ARIA attributes', () => {
    const { getByRole } = render(
      <div>
        <label htmlFor="test-select-aria-attrs">Select Option</label>
        <Select>
          <SelectTrigger id="test-select-aria-attrs" aria-label="Select option">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="option1">Option 1</SelectItem>
          </SelectContent>
        </Select>
      </div>
    );

    const trigger = getByRole('combobox');
    expect(trigger).toHaveAttribute('aria-label');
  });

  it('should be disabled when disabled prop is true', () => {
    const { getByRole } = render(
      <div>
        <label htmlFor="test-select-disabled-check">Select Option</label>
        <Select disabled>
          <SelectTrigger id="test-select-disabled-check">
            <SelectValue placeholder="Disabled" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="option1">Option 1</SelectItem>
          </SelectContent>
        </Select>
      </div>
    );

    const trigger = getByRole('combobox');
    expect(trigger).toBeDisabled();
  });
});
