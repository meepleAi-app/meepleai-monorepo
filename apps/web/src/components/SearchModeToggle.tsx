import { useState } from 'react';

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
    <div className={`search-mode-toggle ${className}`}>
      <label className="search-mode-label">Search Mode:</label>
      <div className="mode-buttons" role="group" aria-label="Search mode selection">
        {modes.map(({ mode, label, description, icon }) => (
          <button
            key={mode}
            type="button"
            className={`mode-button ${value === mode ? 'active' : ''}`}
            onClick={() => !disabled && onChange(mode)}
            disabled={disabled}
            title={description}
            aria-pressed={value === mode}
            aria-label={`${label} search mode: ${description}`}
          >
            <span className="mode-icon" aria-hidden="true">{icon}</span>
            <span className="mode-text">{label}</span>
            {value === mode && (
              <span className="active-indicator" aria-label="Currently selected">
                ✓
              </span>
            )}
          </button>
        ))}
      </div>

      <style jsx>{`
        .search-mode-toggle {
          display: flex;
          flex-direction: column;
          gap: 0.5rem;
          margin-bottom: 1rem;
        }

        .search-mode-label {
          font-size: 0.875rem;
          font-weight: 600;
          color: #374151;
          margin-bottom: 0.25rem;
        }

        .mode-buttons {
          display: flex;
          gap: 0.5rem;
          flex-wrap: wrap;
        }

        .mode-button {
          display: flex;
          align-items: center;
          gap: 0.5rem;
          padding: 0.5rem 1rem;
          border: 2px solid #e5e7eb;
          border-radius: 0.5rem;
          background-color: white;
          color: #374151;
          font-size: 0.875rem;
          font-weight: 500;
          cursor: pointer;
          transition: all 0.2s ease;
          position: relative;
        }

        .mode-button:hover:not(:disabled) {
          border-color: #3b82f6;
          background-color: #eff6ff;
          transform: translateY(-1px);
        }

        .mode-button:active:not(:disabled) {
          transform: translateY(0);
        }

        .mode-button.active {
          border-color: #3b82f6;
          background-color: #3b82f6;
          color: white;
          font-weight: 600;
        }

        .mode-button:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }

        .mode-icon {
          font-size: 1.25rem;
          line-height: 1;
        }

        .mode-text {
          font-family: inherit;
        }

        .active-indicator {
          margin-left: 0.25rem;
          font-weight: bold;
        }

        /* Responsive design for mobile */
        @media (max-width: 640px) {
          .mode-buttons {
            flex-direction: column;
          }

          .mode-button {
            width: 100%;
            justify-content: flex-start;
          }
        }

        /* Focus styles for accessibility */
        .mode-button:focus {
          outline: 2px solid #3b82f6;
          outline-offset: 2px;
        }

        .mode-button:focus:not(:focus-visible) {
          outline: none;
        }
      `}</style>
    </div>
  );
}
