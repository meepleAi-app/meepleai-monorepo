/**
 * BrandMark Story
 * Canonical MeepleAI brand mark — icon "M" gradient + wordmark.
 * Replaces the legacy MeepleLogo SVG (issue #2057).
 */

'use client';

import { BrandMark } from '@/components/ui/brand';

import type { ShowcaseStory } from '../types';

type BrandMarkShowcaseProps = {
  variant: string;
  size: string;
  adminBadge: boolean;
};

export const brandMarkStory: ShowcaseStory<BrandMarkShowcaseProps> = {
  id: 'brand-mark',
  title: 'BrandMark',
  category: 'Meeple',
  description:
    'Canonical MeepleAI brand mark with icon/wordmark/full variants and sm/md/lg sizing. Single source of truth used by AppTopBar, UnifiedHeader, AuthLayout, and PublicFooter.',

  component: function BrandMarkStory({ variant, size, adminBadge }: BrandMarkShowcaseProps) {
    return (
      <div className="flex items-center gap-4 p-6">
        <BrandMark
          variant={variant as 'icon' | 'wordmark' | 'full'}
          size={size as 'sm' | 'md' | 'lg'}
          adminBadge={adminBadge}
        />
        <div className="text-sm">
          <div className="font-medium capitalize">{variant}</div>
          <div className="text-muted-foreground">
            Size: {size}
            {adminBadge ? ' · admin' : ''}
          </div>
        </div>
      </div>
    );
  },

  defaultProps: {
    variant: 'full',
    size: 'sm',
    adminBadge: false,
  },

  controls: {
    variant: {
      type: 'select',
      label: 'variant',
      options: ['icon', 'wordmark', 'full'],
      default: 'full',
    },
    size: {
      type: 'select',
      label: 'size',
      options: ['sm', 'md', 'lg'],
      default: 'sm',
    },
    adminBadge: {
      type: 'boolean',
      label: 'adminBadge',
      default: false,
    },
  },

  presets: {
    iconSm: {
      label: 'Icon · sm (UnifiedHeader)',
      props: { variant: 'icon', size: 'sm', adminBadge: false },
    },
    fullSm: {
      label: 'Full · sm (PublicFooter)',
      props: { variant: 'full', size: 'sm', adminBadge: false },
    },
    fullMd: {
      label: 'Full · md (AuthLayout)',
      props: { variant: 'full', size: 'md', adminBadge: false },
    },
    fullSmAdmin: {
      label: 'Full · sm · admin (AppTopBar admin)',
      props: { variant: 'full', size: 'sm', adminBadge: true },
    },
    wordmarkLg: {
      label: 'Wordmark · lg',
      props: { variant: 'wordmark', size: 'lg', adminBadge: false },
    },
  },
};
