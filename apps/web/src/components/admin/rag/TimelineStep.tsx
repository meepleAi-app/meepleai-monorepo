/**
 * TimelineStep Component - Expandable RAG pipeline step
 *
 * Displays a single step in the RAG pipeline execution timeline with:
 * - Icon, name, and horizontal duration bar
 * - Percentage of total time
 * - Expandable detail section
 * - Active state highlighting
 *
 * Based on admin-mockups/agents-pipeline.html timeline structure
 *
 * @module components/admin/rag/TimelineStep
 */

import { ChevronDown } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface TimelineStepDetail {
  /** Detail label (e.g., "Model", "Tokens") */
  label: string;
  /** Detail value */
  value: string;
  /** Use monospace font for technical values */
  mono?: boolean;
  /** Badge variant for styled values */
  badge?: 'primary' | 'green' | 'amber' | 'red';
  /** Span full width in grid */
  wide?: boolean;
  /** Custom CSS style */
  style?: React.CSSProperties;
}

export interface TimelineStepProps {
  /** Step name (e.g., "Query", "Embedding") */
  name: string;
  /** Step icon (emoji or component) */
  icon: string | React.ReactNode;
  /** Duration in milliseconds */
  durationMs: number;
  /** Percentage of total pipeline time (0-100) */
  percentOfTotal: number;
  /** Step detail items */
  details?: TimelineStepDetail[];
  /** Active/selected state */
  isActive?: boolean;
  /** Expanded state */
  isOpen?: boolean;
  /** Color for duration bar (CSS color value) */
  barColor?: string;
  /** Latency class for duration text */
  latencyClass?: 'green' | 'amber' | 'red';
  /** Toggle expanded state */
  onToggle?: () => void;
  /** Additional className */
  className?: string;
}

/**
 * Badge component for detail values
 */
function DetailBadge({
  variant,
  children,
}: {
  variant: 'primary' | 'green' | 'amber' | 'red';
  children: React.ReactNode;
}) {
  const variantClasses = {
    primary: 'bg-amber-100 text-amber-900',
    green: 'bg-green-100 text-green-700',
    amber: 'bg-amber-100 text-amber-700',
    red: 'bg-red-100 text-red-700',
  };

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5',
        'text-xs font-semibold font-mono tracking-tight',
        variantClasses[variant]
      )}
    >
      {children}
    </span>
  );
}

/**
 * TimelineStep - Expandable pipeline execution step
 *
 * Click header to toggle detail section with smooth max-height animation.
 * Active state shows amber border matching the mockup design.
 */
export function TimelineStep({
  name,
  icon,
  durationMs,
  percentOfTotal,
  details = [],
  isActive = false,
  isOpen = false,
  barColor = 'rgb(251, 191, 36)', // amber-400
  latencyClass = 'amber',
  onToggle,
  className,
}: TimelineStepProps) {
  const latencyColors = {
    green: 'text-green-600',
    amber: 'text-amber-600',
    red: 'text-red-600',
  };

  return (
    <div
      className={cn(
        'rounded-xl overflow-hidden transition-all duration-250',
        'bg-white/90 backdrop-blur-md border',
        isActive ? 'border-amber-500' : 'border-black/10',
        className
      )}
    >
      {/* Header */}
      <button
        onClick={onToggle}
        className={cn(
          'w-full flex items-center gap-3 px-4.5 py-3.5',
          'transition-colors hover:bg-white/70',
          'focus:outline-none focus-visible:ring-2 focus-visible:ring-amber-500'
        )}
      >
        {/* Icon */}
        <span className="text-xl flex-shrink-0 leading-none">
          {typeof icon === 'string' ? icon : icon}
        </span>

        {/* Step Name */}
        <span className="font-quicksand font-bold text-sm flex-shrink-0">{name}</span>

        {/* Duration Bar */}
        <div className="flex-1 mx-3">
          <div className="h-1.5 bg-black/8 rounded-full overflow-hidden">
            <div
              className="h-full rounded-full transition-all duration-500"
              style={{
                width: `${percentOfTotal}%`,
                backgroundColor: barColor,
              }}
            />
          </div>
        </div>

        {/* Duration */}
        <span
          className={cn(
            'font-mono text-sm font-semibold flex-shrink-0 min-w-[60px] text-right',
            latencyColors[latencyClass]
          )}
        >
          {durationMs}ms
        </span>

        {/* Percentage */}
        <span className="text-xs text-gray-500 font-mono flex-shrink-0 min-w-[36px] text-right">
          {percentOfTotal.toFixed(1)}%
        </span>

        {/* Expand Icon */}
        <ChevronDown
          className={cn(
            'w-3.5 h-3.5 text-gray-400 flex-shrink-0',
            'transition-transform duration-300',
            isOpen && 'rotate-180'
          )}
        />
      </button>

      {/* Detail Section */}
      <div
        className={cn(
          'overflow-hidden transition-all duration-400',
          isOpen ? 'max-h-[500px] px-4.5 pb-4.5' : 'max-h-0 px-4.5 pb-0'
        )}
      >
        {details.length > 0 && (
          <div className="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-3 mt-3">
            {details.map((detail, idx) => (
              <div
                key={idx}
                className={cn('flex flex-col gap-0.5', detail.wide && 'col-span-full')}
              >
                {/* Label */}
                <span className="text-[11px] text-gray-400 uppercase tracking-wider font-semibold">
                  {detail.label}
                </span>

                {/* Value */}
                {detail.badge ? (
                  <div>
                    <DetailBadge variant={detail.badge}>{detail.value}</DetailBadge>
                  </div>
                ) : (
                  <span
                    className={cn('text-sm font-semibold', detail.mono && 'font-mono text-[13px]')}
                    style={detail.style}
                  >
                    {detail.value}
                  </span>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
