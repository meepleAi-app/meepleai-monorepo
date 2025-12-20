import React, { useState } from 'react';

import { CollapsibleSection } from '../../lib/diffProcessor';

export interface CollapsibleUnchangedSectionProps {
  section: CollapsibleSection;
  onToggle: () => void;
  side: 'old' | 'new';
}

/**
 * Collapsible section for unchanged lines (>5 consecutive)
 * Reduces visual clutter in large diffs
 */
export const CollapsibleUnchangedSection = React.memo<CollapsibleUnchangedSectionProps>(
  ({ section, onToggle, side: _side }) => {
    const [isAnimating, setIsAnimating] = useState(false);

    const handleToggle = () => {
      setIsAnimating(true);
      onToggle();
      setTimeout(() => setIsAnimating(false), 300); // Match CSS transition
    };

    return (
      <div className="collapsible-section">
        <button
          onClick={handleToggle}
          className={`collapsible-section-toggle ${isAnimating ? 'animating' : ''}`}
          aria-expanded={!section.isCollapsed}
          aria-label={`${section.isCollapsed ? 'Expand' : 'Collapse'} ${section.lineCount} unchanged lines`}
        >
          <span className="collapsible-icon">{section.isCollapsed ? '⬇' : '⬆'}</span>
          <span className="collapsible-text">
            {section.isCollapsed
              ? `⬇ Expand ${section.lineCount} unchanged lines (${section.startLine}-${section.endLine})`
              : `⬆ Collapse ${section.lineCount} unchanged lines`}
          </span>
        </button>
      </div>
    );
  }
);

CollapsibleUnchangedSection.displayName = 'CollapsibleUnchangedSection';
