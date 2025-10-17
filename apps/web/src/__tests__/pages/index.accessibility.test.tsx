/**
 * Accessibility tests for Landing Page (index.tsx)
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import React from 'react';
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import Index from '../../pages/index';

// Extend Jest matchers
expect.extend(toHaveNoViolations);

// Mock Next.js router
jest.mock('next/router', () => ({
  useRouter: () => ({
    push: jest.fn(),
    pathname: '/',
    query: {},
    asPath: '/',
  }),
}));

// Mock API client
jest.mock('../../lib/api', () => ({
  api: {
    post: jest.fn(),
    get: jest.fn(),
  },
}));

describe('Landing Page Accessibility (UI-05)', () => {
  beforeEach(() => {
    // Clear all mocks before each test
    jest.clearAllMocks();
  });

  it('should not have any accessibility violations (WCAG 2.1 AA)', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      runOnly: {
        type: 'tag',
        values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'],
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should have proper document language attribute', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      rules: {
        'html-has-lang': { enabled: true },
        'valid-lang': { enabled: true },
        'heading-order': { enabled: false }, // Not a WCAG 2.1 AA requirement
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should have sufficient color contrast ratios', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      rules: {
        'color-contrast': { enabled: true },
        'heading-order': { enabled: false },
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should have proper ARIA attributes', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      rules: {
        'aria-allowed-attr': { enabled: true },
        'aria-prohibited-attr': { enabled: true },
        'aria-valid-attr': { enabled: true },
        'aria-valid-attr-value': { enabled: true },
        'heading-order': { enabled: false },
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should have accessible form elements', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      rules: {
        'label': { enabled: true },
        'button-name': { enabled: true },
        'input-button-name': { enabled: true },
        'heading-order': { enabled: false },
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should have proper semantic structure', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      rules: {
        'page-has-heading-one': { enabled: true },
        'heading-order': { enabled: false }, // Best practice, not WCAG 2.1 AA requirement
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should have accessible landmark regions', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      rules: {
        'landmark-one-main': { enabled: true },
        'region': { enabled: true },
        'heading-order': { enabled: false },
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should have keyboard-accessible interactive elements', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      rules: {
        'tabindex': { enabled: true },
        'heading-order': { enabled: false },
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should have accessible links with discernible text', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      rules: {
        'link-name': { enabled: true },
        'heading-order': { enabled: false },
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should pass WCAG 2.1 Level A rules', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      runOnly: {
        type: 'tag',
        values: ['wcag2a', 'wcag21a'],
      },
    });

    expect(results).toHaveNoViolations();
  });

  it('should pass WCAG 2.1 Level AA rules', async () => {
    const { container } = render(<Index />);
    const results = await axe(container, {
      runOnly: {
        type: 'tag',
        values: ['wcag2aa', 'wcag21aa'],
      },
    });

    expect(results).toHaveNoViolations();
  });
});
