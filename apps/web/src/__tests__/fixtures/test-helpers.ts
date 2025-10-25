/**
 * Common test helper functions for page component testing
 *
 * This file provides reusable helper functions for common testing patterns:
 * - Authentication state waiting
 * - Page loading state waiting
 * - Element querying and interaction helpers
 * - Async operation helpers
 *
 * @example
 * import { waitForAuthComplete, waitForPageReady, getTextarea } from '../fixtures/test-helpers';
 *
 * test('loads data after auth', async () => {
 *   await waitForAuthComplete();
 *   await waitForPageReady();
 *   expect(screen.getByText('Data loaded')).toBeInTheDocument();
 * });
 */

import { screen, waitFor, within } from '@testing-library/react';

// =============================================================================
// AUTHENTICATION HELPERS
// =============================================================================

/**
 * Waits for authentication check to complete (login gate disappears)
 *
 * Most authenticated pages show a "Login Required" or "Devi effettuare l'accesso"
 * message while checking auth. This helper waits for that to disappear.
 *
 * @param timeout - Maximum wait time in milliseconds (default: 3000ms)
 *
 * @example
 * await waitForAuthComplete();
 * // Now safe to interact with authenticated page content
 */
export const waitForAuthComplete = async (timeout = 3000): Promise<void> => {
  await waitFor(
    () => {
      // Check for English login message
      expect(screen.queryByText(/Login Required/i)).not.toBeInTheDocument();
      // Check for Italian login message
      expect(screen.queryByText(/Devi effettuare l'accesso/i)).not.toBeInTheDocument();
    },
    { timeout }
  );
};

/**
 * Waits for unauthenticated state to be displayed
 *
 * Useful for testing auth guards and login redirects
 *
 * @param timeout - Maximum wait time in milliseconds (default: 3000ms)
 *
 * @example
 * mockApi.get.mockResolvedValue(null); // No auth
 * render(<ProtectedPage />);
 * await waitForUnauthenticated();
 * expect(screen.getByRole('link', { name: 'Go to Login' })).toBeInTheDocument();
 */
export const waitForUnauthenticated = async (timeout = 3000): Promise<void> => {
  await waitFor(
    () => {
      const loginRequired =
        screen.queryByText(/Login Required/i) || screen.queryByText(/Devi effettuare l'accesso/i);
      expect(loginRequired).toBeInTheDocument();
    },
    { timeout }
  );
};

// =============================================================================
// LOADING STATE HELPERS
// =============================================================================

/**
 * Waits for loading indicators to disappear
 *
 * Checks for common loading messages in both English and Italian
 *
 * @param timeout - Maximum wait time in milliseconds (default: 3000ms)
 *
 * @example
 * await waitForLoadingComplete();
 * // Now safe to interact with loaded content
 */
export const waitForLoadingComplete = async (timeout = 3000): Promise<void> => {
  await waitFor(
    () => {
      // Check for common loading messages
      expect(screen.queryByText(/Loading/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/Caricamento/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/Generating/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/Processing/i)).not.toBeInTheDocument();
    },
    { timeout }
  );
};

/**
 * Waits for page to be fully ready (auth complete + loading complete)
 *
 * This is a composite helper that combines auth and loading state checks.
 * Use this for pages that have two-phase loading: auth check then data fetch.
 *
 * @param timeout - Maximum wait time in milliseconds (default: 5000ms)
 *
 * @example
 * render(<ChatPage />);
 * await waitForPageReady();
 * // Page is fully loaded and ready for interaction
 */
export const waitForPageReady = async (timeout = 5000): Promise<void> => {
  // Wait for auth check first
  await waitForAuthComplete(Math.floor(timeout / 2));

  // Then wait for any loading states to complete
  await waitForLoadingComplete(Math.floor(timeout / 2));
};

/**
 * Waits for editor-specific page to be ready (auth + RuleSpec loading)
 *
 * Editor pages load in two phases:
 * 1. Auth check (shows login gate if not authenticated)
 * 2. RuleSpec loading (shows "Caricamento..." spinner)
 *
 * @param timeout - Maximum wait time in milliseconds (default: 5000ms)
 *
 * @example
 * render(<RuleSpecEditor />);
 * await waitForEditorReady();
 * // Editor is fully loaded with RuleSpec data
 */
export const waitForEditorReady = async (timeout = 5000): Promise<void> => {
  const halfTimeout = Math.floor(timeout / 2);

  // Wait for auth to complete
  await waitFor(
    () => {
      expect(screen.queryByText(/Devi effettuare l'accesso/i)).not.toBeInTheDocument();
    },
    { timeout: halfTimeout }
  );

  // Wait for RuleSpec loading to complete
  await waitFor(
    () => {
      expect(screen.queryByText('Caricamento...')).not.toBeInTheDocument();
    },
    { timeout: halfTimeout }
  );
};

// =============================================================================
// ELEMENT QUERY HELPERS
// =============================================================================

/**
 * Gets the main editor textarea element
 *
 * Useful for pages with multiple textboxes where you need the large textarea
 *
 * @returns HTMLTextAreaElement
 * @throws Error if textarea not found
 *
 * @example
 * const textarea = getEditorTextarea();
 * fireEvent.change(textarea, { target: { value: '{ "test": true }' } });
 */
export const getEditorTextarea = (): HTMLTextAreaElement => {
  const textarea = screen.queryAllByRole('textbox').find((el) => el.tagName === 'TEXTAREA');
  if (!textarea) {
    throw new Error('Editor textarea not found');
  }
  return textarea as HTMLTextAreaElement;
};

/**
 * Gets all textareas on the page
 *
 * @returns Array of HTMLTextAreaElement
 *
 * @example
 * const textareas = getAllTextareas();
 * expect(textareas).toHaveLength(2);
 */
export const getAllTextareas = (): HTMLTextAreaElement[] => {
  return screen.queryAllByRole('textbox').filter((el) => el.tagName === 'TEXTAREA') as HTMLTextAreaElement[];
};

/**
 * Gets a specific button by name with better error messages
 *
 * @param name - Button accessible name or text
 * @returns HTMLButtonElement
 * @throws Error with available button names if not found
 *
 * @example
 * const saveButton = getButtonByName('Save');
 * await user.click(saveButton);
 */
export const getButtonByName = (name: string | RegExp): HTMLButtonElement => {
  try {
    return screen.getByRole('button', { name }) as HTMLButtonElement;
  } catch (error) {
    // Provide helpful error with available buttons
    const allButtons = screen.queryAllByRole('button');
    const buttonNames = allButtons.map((btn) => btn.textContent || btn.getAttribute('aria-label'));
    throw new Error(
      `Button with name "${name}" not found. Available buttons: ${buttonNames.join(', ')}`
    );
  }
};

/**
 * Checks if an element is currently visible (not hidden)
 *
 * @param element - HTMLElement to check
 * @returns boolean indicating visibility
 *
 * @example
 * const modal = screen.getByRole('dialog');
 * expect(isVisible(modal)).toBe(true);
 */
export const isVisible = (element: HTMLElement): boolean => {
  return element.style.display !== 'none' && !element.hidden;
};

// =============================================================================
// DATA WAITING HELPERS
// =============================================================================

/**
 * Waits for specific text to appear on the page
 *
 * @param text - Text or regex to wait for
 * @param timeout - Maximum wait time in milliseconds (default: 3000ms)
 *
 * @example
 * await waitForText('Data loaded successfully');
 * await waitForText(/\d+ items found/);
 */
export const waitForText = async (text: string | RegExp, timeout = 3000): Promise<void> => {
  await waitFor(() => {
    expect(screen.getByText(text)).toBeInTheDocument();
  }, { timeout });
};

/**
 * Waits for specific text to disappear from the page
 *
 * @param text - Text or regex to wait for removal
 * @param timeout - Maximum wait time in milliseconds (default: 3000ms)
 *
 * @example
 * await waitForTextRemoval('Processing...');
 */
export const waitForTextRemoval = async (text: string | RegExp, timeout = 3000): Promise<void> => {
  await waitFor(() => {
    expect(screen.queryByText(text)).not.toBeInTheDocument();
  }, { timeout });
};

/**
 * Waits for element by role to appear
 *
 * @param role - ARIA role
 * @param options - Query options (name, etc.)
 * @param timeout - Maximum wait time in milliseconds (default: 3000ms)
 *
 * @example
 * await waitForRole('button', { name: 'Submit' });
 * await waitForRole('alert'); // Wait for error alert
 */
export const waitForRole = async (
  role: string,
  options?: { name?: string | RegExp; [key: string]: any },
  timeout = 3000
): Promise<void> => {
  await waitFor(() => {
    expect(screen.getByRole(role as any, options)).toBeInTheDocument();
  }, { timeout });
};

/**
 * Waits for API call to be made
 *
 * @param mockFn - Jest mock function to check
 * @param endpoint - Expected endpoint (optional)
 * @param timeout - Maximum wait time in milliseconds (default: 3000ms)
 *
 * @example
 * await waitForApiCall(mockApi.get, '/api/v1/games');
 */
export const waitForApiCall = async (
  mockFn: any,
  endpoint?: string,
  timeout = 3000
): Promise<void> => {
  await waitFor(() => {
    if (endpoint) {
      expect(mockFn).toHaveBeenCalledWith(endpoint);
    } else {
      expect(mockFn).toHaveBeenCalled();
    }
  }, { timeout });
};

// =============================================================================
// ERROR HANDLING HELPERS
// =============================================================================

/**
 * Waits for error message to appear
 *
 * @param errorText - Error message text or regex (optional, matches any error if not provided)
 * @param timeout - Maximum wait time in milliseconds (default: 3000ms)
 *
 * @example
 * await waitForError('Failed to load data');
 * await waitForError(); // Wait for any error
 */
export const waitForError = async (errorText?: string | RegExp, timeout = 3000): Promise<void> => {
  await waitFor(() => {
    // Try to find error by role first (alerts, status)
    const errorByRole = screen.queryByRole('alert');

    if (errorText) {
      // If specific text provided, search for it
      const errorByText = screen.queryByText(errorText);
      expect(errorByRole || errorByText).toBeInTheDocument();
    } else {
      // Otherwise just check for any alert
      expect(errorByRole).toBeInTheDocument();
    }
  }, { timeout });
};

/**
 * Suppresses console errors for a test
 *
 * Useful when testing error states that log to console
 *
 * @returns Cleanup function to restore console.error
 *
 * @example
 * const restoreConsole = suppressConsoleErrors();
 * // ... test that logs errors ...
 * restoreConsole();
 *
 * // Or with try/finally:
 * const restoreConsole = suppressConsoleErrors();
 * try {
 *   // ... test code ...
 * } finally {
 *   restoreConsole();
 * }
 */
export const suppressConsoleErrors = (): (() => void) => {
  const originalError = console.error;
  console.error = jest.fn();
  return () => {
    console.error = originalError;
  };
};

// =============================================================================
// INTERACTION HELPERS
// =============================================================================

/**
 * Fills a form field by label
 *
 * @param label - Field label text
 * @param value - Value to fill
 * @param user - userEvent instance
 *
 * @example
 * const user = userEvent.setup();
 * await fillField('Email', 'test@example.com', user);
 */
export const fillField = async (
  label: string | RegExp,
  value: string,
  user: ReturnType<typeof import('@testing-library/user-event').default.setup>
): Promise<void> => {
  const field = screen.getByLabelText(label);
  await user.clear(field);
  await user.type(field, value);
};

/**
 * Selects an option from a select/dropdown by label
 *
 * @param label - Select field label
 * @param optionText - Option text to select
 * @param user - userEvent instance
 *
 * @example
 * const user = userEvent.setup();
 * await selectOption('Game:', 'Chess', user);
 */
export const selectOption = async (
  label: string | RegExp,
  optionText: string,
  user: ReturnType<typeof import('@testing-library/user-event').default.setup>
): Promise<void> => {
  const select = screen.getByLabelText(label);
  await user.selectOptions(select, optionText);
};

/**
 * Clicks a button and waits for it to be enabled again (loading complete)
 *
 * @param buttonName - Button name/text
 * @param user - userEvent instance
 * @param timeout - Maximum wait time for re-enable (default: 5000ms)
 *
 * @example
 * const user = userEvent.setup();
 * await clickAndWaitForReady('Submit', user);
 */
export const clickAndWaitForReady = async (
  buttonName: string | RegExp,
  user: ReturnType<typeof import('@testing-library/user-event').default.setup>,
  timeout = 5000
): Promise<void> => {
  const button = getButtonByName(buttonName);
  await user.click(button);

  // Wait for button to be re-enabled (operation complete)
  await waitFor(() => {
    expect(button).not.toBeDisabled();
  }, { timeout });
};
