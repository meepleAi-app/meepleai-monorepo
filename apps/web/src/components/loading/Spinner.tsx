/**
 * Spinner Component
 *
 * A simple loading spinner with size variants.
 * Uses CSS animation (Tailwind animate-spin) for performance.
 *
 * @example
 * ```tsx
 * <Spinner size="md" />
 * <Spinner size="lg" className="text-blue-500" />
 * ```
 */

export interface SpinnerProps {
  /**
   * Size of the spinner
   * @default 'md'
   */
  size?: 'sm' | 'md' | 'lg';

  /**
   * Additional CSS classes to apply
   */
  className?: string;
}

/**
 * Size mapping to pixel dimensions
 */
const SIZE_MAP = {
  sm: 16,
  md: 24,
  lg: 32,
} as const;

/**
 * Spinner component for loading indicators
 */
export function Spinner({ size = 'md', className = '' }: SpinnerProps) {
  const dimension = SIZE_MAP[size];

  return (
    <div className={`inline-block animate-spin ${className}`}>
      <svg
        width={dimension}
        height={dimension}
        viewBox="0 0 24 24"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
        aria-hidden="true"
      >
        <circle
          className="opacity-25"
          cx="12"
          cy="12"
          r="10"
          stroke="currentColor"
          strokeWidth="4"
        />
        <path
          className="opacity-75"
          fill="currentColor"
          d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
        />
      </svg>
    </div>
  );
}
