/**
 * TierBadge Story
 */

import { TierBadge } from '@/components/ui/feedback/tier-badge';
import type { UserTier } from '@/types/permissions';

import type { ShowcaseStory } from '../types';

type TierBadgeShowcaseProps = {
  tier: string;
  showIcon: boolean;
};

export const tierBadgeStory: ShowcaseStory<TierBadgeShowcaseProps> = {
  id: 'tier-badge',
  title: 'TierBadge',
  category: 'Feedback',
  description: 'Displays user subscription tier with color-coded styling.',

  component: function TierBadgeStory({ tier, showIcon }: TierBadgeShowcaseProps) {
    return (
      <div className="flex flex-col gap-4 items-start">
        <TierBadge tier={tier as UserTier} showIcon={showIcon} />
        <div className="flex flex-wrap gap-2 mt-4">
          {(['free', 'normal', 'premium', 'pro', 'enterprise'] as UserTier[]).map((t) => (
            <TierBadge key={t} tier={t} showIcon={showIcon} />
          ))}
        </div>
      </div>
    );
  },

  defaultProps: {
    tier: 'premium',
    showIcon: true,
  },

  controls: {
    tier: {
      type: 'select',
      label: 'tier',
      options: ['free', 'normal', 'premium', 'pro', 'enterprise'],
      default: 'premium',
    },
    showIcon: { type: 'boolean', label: 'showIcon', default: true },
  },

  presets: {
    free: { label: 'Free', props: { tier: 'free' } },
    normal: { label: 'Normal', props: { tier: 'normal' } },
    premium: { label: 'Premium', props: { tier: 'premium' } },
    enterprise: { label: 'Enterprise', props: { tier: 'enterprise' } },
    noIcon: { label: 'No Icon', props: { showIcon: false } },
  },
};
