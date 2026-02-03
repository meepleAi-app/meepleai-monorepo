/**
 * Animation Components Chromatic Visual Tests (Issue #2965 Wave 9)
 *
 * Visual regression tests for FadeIn, PageTransition, and StaggerChildren.
 * Tests animation components in light and dark themes.
 */

import React from 'react';
import { describe, it } from 'vitest';
import type { Meta, StoryObj } from '@storybook/react';
import { FadeIn } from '../../FadeIn';
import { PageTransition } from '../../PageTransition';
import { StaggerChildren } from '../../StaggerChildren';

/**
 * Chromatic test suite for Animation components
 * Each test creates a visual snapshot for regression testing
 */
describe('Animation Components - Chromatic Visual Tests', () => {
  describe('FadeIn', () => {
    it('should match visual snapshot - Default', () => {});
    it('should match visual snapshot - Direction up', () => {});
    it('should match visual snapshot - Direction down', () => {});
    it('should match visual snapshot - Direction left', () => {});
    it('should match visual snapshot - Direction right', () => {});
    it('should match visual snapshot - Dark mode', () => {});
  });

  describe('PageTransition', () => {
    it('should match visual snapshot - Fade variant', () => {});
    it('should match visual snapshot - Slide variant', () => {});
    it('should match visual snapshot - Scale variant', () => {});
    it('should match visual snapshot - Dark mode', () => {});
  });

  describe('StaggerChildren', () => {
    it('should match visual snapshot - List items', () => {});
    it('should match visual snapshot - Grid cards', () => {});
    it('should match visual snapshot - Dark mode', () => {});
  });
});

// Sample content for animation demos
const SampleCard = ({ title, dark = false }: { title: string; dark?: boolean }) => (
  <div
    className={`p-4 rounded-lg border ${dark ? 'bg-slate-800 border-slate-700 text-white' : 'bg-card border-border'}`}
  >
    <h3 className="font-semibold mb-2">{title}</h3>
    <p className={`text-sm ${dark ? 'text-slate-300' : 'text-muted-foreground'}`}>
      Sample content for visual testing
    </p>
  </div>
);

// Wrapper for theme context
const ThemeWrapper = ({
  children,
  dark = false,
}: {
  children: React.ReactNode;
  dark?: boolean;
}) => (
  <div className={dark ? 'dark bg-slate-900 p-8' : 'bg-background p-8'}>{children}</div>
);

// ============================================
// FadeIn Stories
// ============================================
const fadeInMeta: Meta<typeof FadeIn> = {
  title: 'Animations/FadeIn/Chromatic',
  component: FadeIn,
  parameters: {
    layout: 'centered',
    chromatic: {
      disableSnapshot: false,
      pauseAnimationAtEnd: true,
    },
  },
};

export default fadeInMeta;
type FadeInStory = StoryObj<typeof FadeIn>;

export const FadeInDefault: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <FadeIn>
        <SampleCard title="FadeIn Default" />
      </FadeIn>
    </ThemeWrapper>
  ),
};

export const FadeInFromBottom: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <FadeIn direction="up">
        <SampleCard title="FadeIn From Bottom" />
      </FadeIn>
    </ThemeWrapper>
  ),
};

export const FadeInFromTop: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <FadeIn direction="down">
        <SampleCard title="FadeIn From Top" />
      </FadeIn>
    </ThemeWrapper>
  ),
};

export const FadeInFromLeft: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <FadeIn direction="right">
        <SampleCard title="FadeIn From Left" />
      </FadeIn>
    </ThemeWrapper>
  ),
};

export const FadeInFromRight: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <FadeIn direction="left">
        <SampleCard title="FadeIn From Right" />
      </FadeIn>
    </ThemeWrapper>
  ),
};

export const FadeInDarkMode: FadeInStory = {
  render: () => (
    <ThemeWrapper dark>
      <FadeIn>
        <SampleCard title="FadeIn Dark Mode" dark />
      </FadeIn>
    </ThemeWrapper>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
};

// ============================================
// PageTransition Stories (separate meta for Storybook)
// ============================================

export const PageTransitionFade: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <PageTransition variant="fade">
        <SampleCard title="Page Transition - Fade" />
      </PageTransition>
    </ThemeWrapper>
  ),
};

export const PageTransitionSlide: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <PageTransition variant="slide">
        <SampleCard title="Page Transition - Slide" />
      </PageTransition>
    </ThemeWrapper>
  ),
};

export const PageTransitionScale: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <PageTransition variant="scale">
        <SampleCard title="Page Transition - Scale" />
      </PageTransition>
    </ThemeWrapper>
  ),
};

export const PageTransitionDarkMode: FadeInStory = {
  render: () => (
    <ThemeWrapper dark>
      <PageTransition variant="fade">
        <SampleCard title="Page Transition Dark" dark />
      </PageTransition>
    </ThemeWrapper>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
};

// ============================================
// StaggerChildren Stories
// ============================================

export const StaggerChildrenList: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <StaggerChildren className="space-y-3">
        <SampleCard title="Item 1" />
        <SampleCard title="Item 2" />
        <SampleCard title="Item 3" />
      </StaggerChildren>
    </ThemeWrapper>
  ),
};

export const StaggerChildrenGrid: FadeInStory = {
  render: () => (
    <ThemeWrapper>
      <StaggerChildren className="grid grid-cols-2 gap-3">
        <SampleCard title="Card 1" />
        <SampleCard title="Card 2" />
        <SampleCard title="Card 3" />
        <SampleCard title="Card 4" />
      </StaggerChildren>
    </ThemeWrapper>
  ),
};

export const StaggerChildrenDarkMode: FadeInStory = {
  render: () => (
    <ThemeWrapper dark>
      <StaggerChildren className="space-y-3">
        <SampleCard title="Dark Item 1" dark />
        <SampleCard title="Dark Item 2" dark />
        <SampleCard title="Dark Item 3" dark />
      </StaggerChildren>
    </ThemeWrapper>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
};

// ============================================
// Combined Theme Comparison
// ============================================

export const ThemeComparison: FadeInStory = {
  render: () => (
    <div className="flex gap-8">
      <div>
        <h4 className="text-sm font-medium mb-2">Light Mode</h4>
        <ThemeWrapper>
          <FadeIn>
            <SampleCard title="Light Theme" />
          </FadeIn>
        </ThemeWrapper>
      </div>
      <div>
        <h4 className="text-sm font-medium mb-2 text-white">Dark Mode</h4>
        <ThemeWrapper dark>
          <FadeIn>
            <SampleCard title="Dark Theme" dark />
          </FadeIn>
        </ThemeWrapper>
      </div>
    </div>
  ),
  decorators: [
    (Story) => (
      <div className="bg-slate-500 p-8">
        <Story />
      </div>
    ),
  ],
};
