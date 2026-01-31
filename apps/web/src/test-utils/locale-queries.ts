/**
 * Custom queries for testing localized content.
 *
 * Handles variations in number formatting and other locale-specific rendering
 * across different environments (developer machines, CI runners, etc.).
 *
 * @module test-utils/locale-queries
 */

import { screen, within as rtlWithin, type ByRoleMatcher, type ByRoleOptions } from '@testing-library/react';

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
  role: ByRoleMatcher,
  options?: ByRoleOptions
): HTMLElement {
  const dialog = screen.getByRole('dialog');
  return rtlWithin(dialog).getByRole(role, options);
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

// =============================================================================
// Role-Based Queries (Issue #2680)
// =============================================================================

/**
 * Queries for a menu item by name pattern.
 *
 * Use this instead of getByText for dropdown menu items. Radix UI's
 * DropdownMenuItem automatically has role="menuitem".
 *
 * @param name - Text or regex pattern to match the menu item name
 * @returns The menu item element
 * @throws {Error} If menu item not found
 *
 * @example
 * // ❌ Fragile - breaks on text changes, not accessible:
 * await user.click(screen.getByText('CSV - Base'));
 *
 * // ✅ Semantic, accessible, resilient:
 * await user.click(getMenuItem(/csv.*base/i));
 */
export function getMenuItem(name: string | RegExp): HTMLElement {
  return screen.getByRole('menuitem', { name });
}

/**
 * Queries for a menu item by name pattern, returning null if not found.
 *
 * @param name - Text or regex pattern to match the menu item name
 * @returns The menu item element, or null if not found
 */
export function queryMenuItem(name: string | RegExp): HTMLElement | null {
  return screen.queryByRole('menuitem', { name });
}

/**
 * Queries for a dialog heading by name pattern.
 *
 * Use this for dialog/modal titles instead of getByText.
 *
 * @param name - Text or regex pattern to match the heading
 * @returns The heading element within the dialog
 * @throws {Error} If dialog or heading not found
 *
 * @example
 * // ❌ May match multiple elements:
 * expect(screen.getByText('Rimuovi dalla Libreria?')).toBeInTheDocument();
 *
 * // ✅ Scoped to dialog heading:
 * expect(getDialogHeading(/rimuovi dalla libreria/i)).toBeInTheDocument();
 */
export function getDialogHeading(name: string | RegExp): HTMLElement {
  const dialog = screen.getByRole('dialog');
  return rtlWithin(dialog).getByRole('heading', { name });
}

/**
 * Queries for a dialog heading by name pattern, returning null if not found.
 *
 * Non-throwing version of getDialogHeading for negative assertions or optional queries.
 *
 * @param name - Text or regex pattern to match the heading
 * @returns The heading element, or null if dialog or heading not found
 *
 * @example
 * // Check dialog is NOT rendered
 * expect(queryDialogHeading(/share chat thread/i)).not.toBeInTheDocument();
 */
export function queryDialogHeading(name: string | RegExp): HTMLElement | null {
  try {
    const dialog = screen.queryByRole('dialog');
    if (!dialog) return null;
    return rtlWithin(dialog).queryByRole('heading', { name });
  } catch {
    return null;
  }
}

/**
 * Queries for an alert dialog heading by name pattern.
 *
 * Use this for destructive/confirmation dialogs that use alertdialog role.
 *
 * @param name - Text or regex pattern to match the heading
 * @returns The heading element within the alert dialog
 * @throws {Error} If alert dialog or heading not found
 *
 * @example
 * // For destructive confirmation dialogs
 * expect(getAlertDialogHeading(/rimuovi dalla libreria/i)).toBeInTheDocument();
 */
export function getAlertDialogHeading(name: string | RegExp): HTMLElement {
  const dialog = screen.getByRole('alertdialog');
  return rtlWithin(dialog).getByRole('heading', { name });
}

/**
 * Queries for alert dialog heading, returning null if not found.
 *
 * @param name - Text or regex pattern to match the heading
 * @returns The heading element, or null if not found
 */
export function queryAlertDialogHeading(name: string | RegExp): HTMLElement | null {
  try {
    const dialog = screen.queryByRole('alertdialog');
    if (!dialog) return null;
    return rtlWithin(dialog).queryByRole('heading', { name });
  } catch {
    return null;
  }
}

/**
 * Queries for a button within an alert dialog by name pattern.
 *
 * Useful for finding confirm/cancel buttons in destructive dialogs.
 *
 * @param name - Text or regex pattern to match the button
 * @returns The button element within the alert dialog
 * @throws {Error} If alert dialog or button not found
 *
 * @example
 * const confirmButton = getAlertDialogButton(/rimuovi/i);
 * await user.click(confirmButton);
 */
export function getAlertDialogButton(name: string | RegExp): HTMLElement {
  const dialog = screen.getByRole('alertdialog');
  return rtlWithin(dialog).getByRole('button', { name });
}

/**
 * Queries for status messages (loading indicators, progress, etc.)
 *
 * @returns The status element
 * @throws {Error} If no status element found
 *
 * @example
 * // ❌ Fragile - breaks on text changes:
 * expect(screen.getByText('Caricamento...')).toBeInTheDocument();
 *
 * // ✅ Semantic:
 * expect(getStatusMessage()).toBeInTheDocument();
 */
export function getStatusMessage(): HTMLElement {
  return screen.getByRole('status');
}

/**
 * Queries for status message, returning null if not found.
 *
 * @returns The status element, or null if not found
 */
export function queryStatusMessage(): HTMLElement | null {
  return screen.queryByRole('status');
}
