/**
 * Custom queries for testing localized content.
 *
 * Handles variations in number formatting and other locale-specific rendering
 * across different environments (developer machines, CI runners, etc.).
 *
 * @module test-utils/locale-queries
 */

import { screen, within as rtlWithin, type BoundFunction, type GetByText } from '@testing-library/react';

/**
 * Queries for an element containing a localized number.
 *
 * Handles different number formatting styles:
 * - "1,000" (en-US with comma thousands separator)
 * - "1000" (plain number without separator)
 * - "1.000" (de-DE with period thousands separator)
 *
 * Uses regex pattern to match all common variations, making tests portable
 * across different locale configurations.
 *
 * @param container - The container element to search within (or use screen)
 * @param value - The numeric value to find
 * @returns The element containing the localized number
 * @throws {Error} If element not found (via getByText)
 *
 * @example
 * // Component renders: <div>Total: {totalRequests.toLocaleString()}</div>
 * // May render as "1,000" (en-US) or "1000" (other locales)
 *
 * // ❌ Fails in some environments:
 * expect(screen.getByText('1,000')).toBeInTheDocument();
 *
 * // ✅ Works in all environments:
 * expect(getByLocalizedNumber(document.body, 1000)).toBeInTheDocument();
 */
export function getByLocalizedNumber(
  container: HTMLElement,
  value: number
): HTMLElement {
  // Create regex that matches both "1,000", "1000", and "1.000"
  // The ,? makes the comma optional, .? makes the period optional
  const stringValue = value.toString();

  // Insert optional separators between thousands
  // For 1000: matches "1,000", "1.000", or "1000"
  // For 10000: matches "10,000", "10.000", or "10000"
  let pattern = '';
  const digits = stringValue.split('').reverse();

  for (let i = 0; i < digits.length; i++) {
    // Add optional separator every 3 digits (from right)
    if (i > 0 && i % 3 === 0) {
      pattern = `[,.]?${pattern}`;
    }
    pattern = `${digits[i]}${pattern}`;
  }

  const regex = new RegExp(pattern);
  return rtlWithin(container).getByText(regex);
}

/**
 * Queries for an element containing a localized number using screen.
 *
 * Convenience wrapper around getByLocalizedNumber that uses screen as container.
 *
 * @param value - The numeric value to find
 * @returns The element containing the localized number
 *
 * @example
 * expect(getByLocalizedNumberScreen(1000)).toBeInTheDocument();
 */
export function getByLocalizedNumberScreen(value: number): HTMLElement {
  return getByLocalizedNumber(document.body, value);
}

/**
 * Queries for text within a specific dialog.
 *
 * Prevents "Found multiple elements" errors when querying for text that appears
 * in multiple places on the page (e.g., dialog title that matches a button label).
 *
 * This is more robust and accessible than global text queries, as it:
 * 1. Uses semantic role-based query (getByRole('dialog'))
 * 2. Scopes subsequent queries to the dialog only
 * 3. Avoids ambiguity when multiple elements have same text
 *
 * @param text - Text or regex to find within the dialog
 * @returns The element containing the text within the dialog
 * @throws {Error} If dialog not found or text not found within dialog
 *
 * @example
 * // Component has dialog with title "Confirm" and a button also labeled "Confirm"
 *
 * // ❌ Fails with "Found multiple elements":
 * expect(screen.getByText('Confirm')).toBeInTheDocument();
 *
 * // ✅ Correctly scopes to dialog:
 * expect(getByTextInDialog('Confirm')).toBeInTheDocument();
 *
 * @example
 * // With regex pattern
 * expect(getByTextInDialog(/Are you sure/i)).toBeInTheDocument();
 */
export function getByTextInDialog(text: string | RegExp): HTMLElement {
  const dialog = screen.getByRole('dialog');
  return rtlWithin(dialog).getByText(text);
}

/**
 * Queries for text within a specific dialog, returning null if not found.
 *
 * Non-throwing version of getByTextInDialog for optional queries.
 *
 * @param text - Text or regex to find within the dialog
 * @returns The element containing the text, or null if not found
 *
 * @example
 * const errorMessage = queryByTextInDialog('Error occurred');
 * if (errorMessage) {
 *   expect(errorMessage).toBeVisible();
 * }
 */
export function queryByTextInDialog(text: string | RegExp): HTMLElement | null {
  try {
    const dialog = screen.queryByRole('dialog');
    if (!dialog) return null;
    return rtlWithin(dialog).queryByText(text);
  } catch {
    return null;
  }
}

/**
 * Queries for an element within a specific dialog by role.
 *
 * Useful for finding buttons, inputs, etc. within dialogs without ambiguity.
 *
 * @param role - ARIA role to find
 * @param options - Query options (e.g., { name: 'Submit' })
 * @returns The element with the specified role within the dialog
 *
 * @example
 * const confirmButton = getByRoleInDialog('button', { name: 'Confirm' });
 * await user.click(confirmButton);
 */
export function getByRoleInDialog(
  role: string,
  options?: Parameters<BoundFunction<GetByText>>[1]
): HTMLElement {
  const dialog = screen.getByRole('dialog');
  return rtlWithin(dialog).getByRole(role as any, options);
}

/**
 * Creates a regex pattern for flexible text matching with locale variations.
 *
 * Useful for custom queries where you need to match text that may vary slightly
 * due to locale-specific formatting.
 *
 * @param baseText - The base text to match
 * @param options - Options for pattern generation
 * @returns A RegExp that matches locale variations
 *
 * @example
 * // Match "Total: 1,000" or "Total: 1000"
 * const pattern = createLocaleFlexiblePattern('Total:', { number: 1000 });
 * expect(screen.getByText(pattern)).toBeInTheDocument();
 */
export function createLocaleFlexiblePattern(
  baseText: string,
  options?: {
    /** Numeric value that may have locale-specific formatting */
    number?: number;
    /** Make matching case-insensitive */
    caseInsensitive?: boolean;
  }
): RegExp {
  let pattern = baseText;

  if (options?.number !== undefined) {
    const numPattern = options.number.toString().split('').join('[,.]?');
    pattern = `${pattern}\\s*${numPattern}`;
  }

  const flags = options?.caseInsensitive ? 'i' : '';
  return new RegExp(pattern, flags);
}
