/**
 * AccessibleButton Component (UI-05) - Migrated to shadcn/ui
 *
 * A fully accessible button component following WCAG 2.1 AA standards.
 * Now uses shadcn Button internally while preserving all accessibility features.
 *
 * Features:
 * - Proper ARIA attributes
 * - Keyboard navigation (Enter, Space)
 * - Focus indicators (2px outline)
 * - Loading states with announcements
 * - Support for icon-only buttons
 * - High contrast support
 * - Multiple variants and sizes
 *
 * @example
 * ```tsx
 * // Primary button
 * <AccessibleButton variant="primary" onClick={handleClick}>
 *   Save Changes
 * </AccessibleButton>
 *
 * // Icon-only button (aria-label required)
 * <AccessibleButton
 *   variant="ghost"
 *   iconOnly
 *   aria-label="Close dialog"
 *   onClick={handleClose}
 * >
 *   ✕
 * </AccessibleButton>
 *
 * // Loading state
 * <AccessibleButton isLoading loadingText="Saving...">
 *   Save
 * </AccessibleButton>
 *
 * // Toggle button
 * <AccessibleButton
 *   isPressed={isPressed}
 *   onClick={() => setIsPressed(!isPressed)}
 *   aria-label="Toggle notifications"
 * >
 *   🔔
 * </AccessibleButton>
 * ```
 */

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { Loader2 } from "lucide-react";
import { ButtonHTMLAttributes, forwardRef } from "react";

export interface AccessibleButtonProps
  extends ButtonHTMLAttributes<HTMLButtonElement> {
  /**
   * Button visual variant
   * @default 'primary'
   */
  variant?: "primary" | "secondary" | "danger" | "ghost";

  /**
   * Button size
   * @default 'md'
   */
  size?: "sm" | "md" | "lg";

  /**
   * Whether the button is icon-only (requires aria-label)
   * @default false
   */
  iconOnly?: boolean;

  /**
   * Loading state - disables button and shows loading indicator
   * @default false
   */
  isLoading?: boolean;

  /**
   * Text to announce when loading (via aria-live)
   * @default 'Loading...'
   */
  loadingText?: string;

  /**
   * For toggle buttons - indicates pressed state
   * Sets aria-pressed attribute
   */
  isPressed?: boolean;

  /**
   * Additional CSS classes
   */
  className?: string;

  /**
   * Content of the button
   */
  children: React.ReactNode;
}

/**
 * Map custom variants to shadcn variants
 */
const variantMap = {
  primary: "default" as const,
  secondary: "secondary" as const,
  danger: "destructive" as const,
  ghost: "ghost" as const,
};

/**
 * Map custom sizes to shadcn sizes
 */
const sizeMap = {
  sm: "sm" as const,
  md: "default" as const,
  lg: "lg" as const,
};

/**
 * AccessibleButton component with full WCAG 2.1 AA compliance
 * Now powered by shadcn/ui Button
 */
export const AccessibleButton = forwardRef<
  HTMLButtonElement,
  AccessibleButtonProps
>(
  (
    {
      variant = "primary",
      size = "md",
      iconOnly = false,
      isLoading = false,
      loadingText = "Loading...",
      isPressed,
      className = "",
      children,
      disabled,
      "aria-label": ariaLabel,
      ...props
    },
    ref
  ) => {
    // Validation: icon-only buttons must have aria-label
    if (iconOnly && !ariaLabel && process.env.NODE_ENV === "development") {
      console.error(
        "AccessibleButton: Icon-only buttons must have an aria-label prop for screen reader accessibility"
      );
    }

    // Map to shadcn variant and size
    const shadcnVariant = variantMap[variant];
    const shadcnSize = iconOnly ? "icon" : sizeMap[size];

    // Pressed state classes (for toggle buttons)
    const pressedClasses = isPressed
      ? "ring-2 ring-ring ring-offset-2 ring-offset-background"
      : "";

    // ARIA attributes
    const ariaAttributes: React.AriaAttributes = {
      "aria-label": ariaLabel,
      "aria-disabled": disabled || isLoading,
      "aria-pressed": typeof isPressed === "boolean" ? isPressed : undefined,
      "aria-live": isLoading ? "polite" : undefined,
      "aria-busy": isLoading || undefined,
    };

    return (
      <>
        <Button
          ref={ref}
          variant={shadcnVariant}
          size={shadcnSize}
          className={cn(
            "transition-all duration-200",
            // Enhanced focus ring for WCAG 2.1 AA
            "focus-visible:ring-2 focus-visible:ring-offset-2",
            pressedClasses,
            className
          )}
          disabled={disabled || isLoading}
          {...ariaAttributes}
          {...props}
        >
          {isLoading ? (
            <>
              {/* Loading spinner (accessible) */}
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              {/* Loading text (announced by screen reader via aria-live) */}
              <span>{loadingText}</span>
            </>
          ) : (
            children
          )}
        </Button>

        {/* Screen reader announcement for loading state */}
        {isLoading && (
          <div
            className="sr-only"
            role="status"
            aria-live="polite"
            aria-atomic="true"
          >
            {loadingText}
          </div>
        )}
      </>
    );
  }
);

AccessibleButton.displayName = "AccessibleButton";
