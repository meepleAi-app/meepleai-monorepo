/**
 * ConfidenceBadge Story
 */

import { ConfidenceBadge } from '@/components/ui/data-display/confidence-badge';

import type { ShowcaseStory } from '../types';

type ConfidenceBadgeShowcaseProps = {
  confidence: number;
  showTooltip: boolean;
};

export const confidenceBadgeStory: ShowcaseStory<ConfidenceBadgeShowcaseProps> = {
  id: 'confidence-badge',
  title: 'ConfidenceBadge',
  category: 'Feedback',
  description: 'Displays AI confidence score with color-coded visual feedback.',

  component: function ConfidenceBadgeStory({ confidence, showTooltip }: ConfidenceBadgeShowcaseProps) {
    return (
      <div className="flex flex-col gap-4 items-start">
        <ConfidenceBadge confidence={confidence} showTooltip={showTooltip} />
        <div className="text-sm text-muted-foreground bg-white/80 px-3 py-2 rounded-lg border border-border/40">
          Score: <strong>{confidence}</strong>
          {confidence >= 80 ? ' → High' : confidence >= 50 ? ' → Medium' : ' → Low'}
        </div>
        {/* All levels */}
        <div className="flex flex-wrap gap-3 mt-2">
          <ConfidenceBadge confidence={90} showTooltip={false} />
          <ConfidenceBadge confidence={65} showTooltip={false} />
          <ConfidenceBadge confidence={30} showTooltip={false} />
        </div>
      </div>
    );
  },

  defaultProps: {
    confidence: 85,
    showTooltip: true,
  },

  controls: {
    confidence: { type: 'range', label: 'confidence', min: 0, max: 100, step: 1, default: 85 },
    showTooltip: { type: 'boolean', label: 'showTooltip', default: true },
  },

  presets: {
    high: { label: 'High (90)', props: { confidence: 90 } },
    medium: { label: 'Medium (65)', props: { confidence: 65 } },
    low: { label: 'Low (30)', props: { confidence: 30 } },
    noTooltip: { label: 'No Tooltip', props: { showTooltip: false } },
  },
};
