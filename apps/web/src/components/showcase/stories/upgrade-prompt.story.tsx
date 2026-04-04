/**
 * UpgradePrompt Story
 * Demonstrates tier-locked feature prompts in inline and modal variants.
 */

'use client';

import { UpgradePrompt } from '@/components/ui/feedback/upgrade-prompt';
import type { UserTier } from '@/types/permissions';

import type { ShowcaseStory } from '../types';

type UpgradePromptShowcaseProps = {
  requiredTier: string;
  featureName: string;
  variant: string;
};

export const upgradePromptStory: ShowcaseStory<UpgradePromptShowcaseProps> = {
  id: 'upgrade-prompt',
  title: 'UpgradePrompt',
  category: 'Gates',
  description: 'Upgrade CTA shown when a user tries to access a tier-locked feature.',

  component: function UpgradePromptStory({
    requiredTier,
    featureName,
    variant,
  }: UpgradePromptShowcaseProps) {
    return (
      <div className="p-6 max-w-md">
        <UpgradePrompt
          requiredTier={requiredTier as Exclude<UserTier, 'free'>}
          featureName={featureName || undefined}
          variant={variant as 'inline' | 'modal'}
        />
      </div>
    );
  },

  defaultProps: {
    requiredTier: 'premium',
    featureName: 'Agent Creation',
    variant: 'inline',
  },

  controls: {
    requiredTier: {
      type: 'select',
      label: 'requiredTier',
      options: ['normal', 'premium', 'enterprise'],
      default: 'premium',
    },
    featureName: { type: 'text', label: 'featureName', default: 'Agent Creation' },
    variant: {
      type: 'select',
      label: 'variant',
      options: ['inline', 'modal'],
      default: 'inline',
    },
  },

  presets: {
    premiumFeature: {
      label: 'Premium Feature',
      props: { requiredTier: 'premium', featureName: 'Agent Creation', variant: 'inline' },
    },
    enterpriseFeature: {
      label: 'Enterprise',
      props: { requiredTier: 'enterprise', featureName: 'Analytics', variant: 'inline' },
    },
    modal: {
      label: 'Modal Variant',
      props: { requiredTier: 'premium', featureName: 'Bulk Upload', variant: 'modal' },
    },
  },
};
