/**
 * Test utilities for useConfirmDialog hook
 * Issue #1435 - Custom ConfirmDialog testing helpers
 */

/**
 * Mock useConfirmDialog to return a confirm function that resolves to a given value
 *
 * @param confirmValue - The value to resolve the confirm promise with (true/false)
 * @returns The mock confirm function for further assertions
 *
 * @example
 * ```ts
 * // User confirms
 * const mockConfirm = mockUseConfirmDialog(true);
 * // ... trigger action that needs confirmation
 * expect(mockConfirm).toHaveBeenCalled();
 *
 * // User cancels
 * mockUseConfirmDialog(false);
 * // ... trigger action
 * // ... expect action was not performed
 * ```
 */
export function mockUseConfirmDialog(confirmValue: boolean = true) {
  const mockConfirm = jest.fn().mockResolvedValue(confirmValue);
  const { useConfirmDialog } = require("@/hooks/useConfirmDialog");

  (useConfirmDialog as jest.Mock).mockReturnValue({
    confirm: mockConfirm,
    ConfirmDialogComponent: () => null,
  });

  return mockConfirm;
}

/**
 * Reset useConfirmDialog mock to default behavior
 * Call this in afterEach or when switching between test cases
 */
export function resetUseConfirmDialogMock() {
  const { useConfirmDialog } = require("@/hooks/useConfirmDialog");

  (useConfirmDialog as jest.Mock).mockReturnValue({
    confirm: jest.fn().mockResolvedValue(true),
    ConfirmDialogComponent: () => null,
  });
}
