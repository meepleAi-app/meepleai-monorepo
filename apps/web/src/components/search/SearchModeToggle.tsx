import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';
import { cn } from '@/lib/utils';

/**
 * AI-14: Search mode enum matching backend SearchMode
 */
export enum SearchMode {
  Semantic = 'Semantic',
  Keyword = 'Keyword',
  Hybrid = 'Hybrid'
}

/**
 * Props for SearchModeToggle component
 */
interface SearchModeToggleProps {
  /** Currently selected search mode */
  value: SearchMode;
  /** Callback when search mode changes */
  onChange: (mode: SearchMode) => void;
  /** Optional CSS class name */
  className?: string;
  /** Disabled state */
  disabled?: boolean;
}

/**
 * AI-14: Search mode toggle component for hybrid search feature.
 * Allows users to switch between Semantic, Keyword, and Hybrid search modes.
 *
 * - Semantic: Vector similarity search (natural language understanding)
 * - Keyword: PostgreSQL full-text search (exact terminology matching)
 * - Hybrid: RRF fusion of both (default, best results)
 */
export default function SearchModeToggle({
  value,
  onChange,
  className = '',
  disabled = false
}: SearchModeToggleProps) {
  const modes: Array<{ mode: SearchMode; label: string; description: string; icon: string }> = [
    {
      mode: SearchMode.Semantic,
      label: 'Semantic',
      description: 'Natural language understanding (AI embeddings)',
      icon: '🧠'
    },
    {
      mode: SearchMode.Hybrid,
      label: 'Hybrid',
      description: 'Best of both worlds (default, 70% semantic + 30% keyword)',
      icon: '⚡'
    },
    {
      mode: SearchMode.Keyword,
      label: 'Keyword',
      description: 'Exact term matching (faster, more precise)',
      icon: '🔍'
    }
  ];

  return (
    <div className={cn('flex flex-col gap-2 mb-4', className)}>
      <label className="text-sm font-semibold text-gray-700 mb-1">Search Mode:</label>
      <div className="mode-buttons" role="group" aria-label="Search mode selection">
        <ToggleGroup
          type="single"
          value={value}
          onValueChange={(newValue) => {
            // ToggleGroup allows deselecting, but we always want a value
            if (newValue) {
              onChange(newValue as SearchMode);
            }
          }}
          disabled={disabled}
          className="flex gap-2 flex-wrap sm:flex-nowrap"
        >
          {modes.map(({ mode, label, description, icon }) => (
            <ToggleGroupItem
              key={mode}
              value={mode}
              aria-label={`${label} search mode: ${description}`}
              aria-pressed={value === mode}
              title={description}
              className={cn(
                'mode-button',
                'flex items-center gap-2 px-4 py-2',
                'border-2 rounded-lg transition-all',
                'text-sm font-medium',
                'hover:border-blue-500 hover:bg-blue-50 hover:-translate-y-0.5',
                'active:translate-y-0',
                'disabled:opacity-50 disabled:cursor-not-allowed',
                'focus:outline-2 focus:outline-blue-500 focus:outline-offset-2',
                'focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:border-ring',
                'data-[state=on]:border-blue-500 data-[state=on]:bg-blue-500 data-[state=on]:text-white data-[state=on]:font-semibold',
                'sm:w-auto w-full sm:justify-center justify-start',
                value === mode ? 'active' : ''
              )}
              type="button"
            >
              <span className="mode-icon text-xl leading-none" aria-hidden="true">
                {icon}
              </span>
              <span className="mode-text">{label}</span>
              {value === mode && (
                <span className="active-indicator ml-1 font-bold" aria-label="Currently selected">
                  ✓
                </span>
              )}
            </ToggleGroupItem>
          ))}
        </ToggleGroup>
      </div>
    </div>
  );
}
