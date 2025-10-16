/**
 * AccessibleSkipLink Component (UI-05)
 *
 * A "Skip to main content" link that allows keyboard users to bypass
 * repetitive navigation and jump directly to the main content.
 *
 * This is a **WCAG 2.1 AA requirement** for keyboard accessibility.
 *
 * Features:
 * - Visually hidden until focused (appears on Tab)
 * - High contrast focus indicator
 * - Smooth scroll to target
 * - Proper ARIA attributes
 *
 * @example
 * ```tsx
 * // In _app.tsx or layout component
 * <AccessibleSkipLink href="#main-content" />
 *
 * // In page component
 * <main id="main-content" tabIndex={-1}>
 *   <h1>Page Title</h1>
 *   ...
 * </main>
 * ```
 */

import { AnchorHTMLAttributes } from 'react';

export interface AccessibleSkipLinkProps
  extends Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'href'> {
  /**
   * Target ID to skip to (must include #)
   * @example "#main-content"
   */
  href: `#${string}`;

  /**
   * Link text
   * @default "Skip to main content"
   */
  children?: string;

  /**
   * Additional CSS classes
   */
  className?: string;
}

/**
 * AccessibleSkipLink component with full WCAG 2.1 AA compliance
 */
export function AccessibleSkipLink({
  href,
  children = 'Skip to main content',
  className = '',
  ...props
}: AccessibleSkipLinkProps) {
  const handleClick = (e: React.MouseEvent<HTMLAnchorElement>) => {
    e.preventDefault();

    // Get target element
    const targetId = href.substring(1); // Remove #
    const targetElement = document.getElementById(targetId);

    if (targetElement) {
      // Focus target element
      targetElement.focus();

      // If target is not naturally focusable, set tabindex temporarily
      if (!targetElement.hasAttribute('tabindex')) {
        targetElement.setAttribute('tabindex', '-1');
        targetElement.focus();

        // Remove tabindex after blur to keep DOM clean
        targetElement.addEventListener(
          'blur',
          () => {
            targetElement.removeAttribute('tabindex');
          },
          { once: true }
        );
      }

      // Smooth scroll to target
      targetElement.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      });
    } else if (process.env.NODE_ENV === 'development') {
      console.error(
        `AccessibleSkipLink: Target element "${href}" not found. Make sure the target element exists with id="${targetId}".`
      );
    }
  };

  return (
    <a
      href={href}
      onClick={handleClick}
      className={`
        skip-link
        sr-only
        focus:not-sr-only
        focus:absolute
        focus:top-4
        focus:left-4
        focus:z-50
        focus:px-6
        focus:py-3
        focus:bg-primary-600
        focus:text-white
        focus:font-medium
        focus:rounded-lg
        focus:shadow-lg
        focus:outline-none
        focus:ring-4
        focus:ring-primary-300
        transition-all
        duration-200
        ${className}
      `}
      {...props}
    >
      {children}
    </a>
  );
}
