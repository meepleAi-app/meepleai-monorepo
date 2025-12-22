/**
 * HeroSection Storybook Stories
 *
 * Visual regression testing variants for Chromatic.
 * Issue #1841 (PAGE-005)
 */

import { HeroSection } from './HeroSection';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof HeroSection> = {
  title: 'Game Detail/HeroSection',
  component: HeroSection,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024, 1920], // Test responsive breakpoints
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof HeroSection>;

// ============================================================================
// Stories
// ============================================================================

export const Default: Story = {
  args: {
    title: 'Catan',
    publisher: 'Kosmos',
    imageUrl:
      'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL2xl2BJE2bw=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
  },
};

export const WithoutImage: Story = {
  args: {
    title: 'Gioco Senza Immagine',
    publisher: 'Test Publisher',
    imageUrl: null,
  },
};

export const LongTitle: Story = {
  args: {
    title:
      'Un Gioco da Tavolo con un Titolo Estremamente Lungo che Potrebbe Causare Problemi di Layout',
    publisher: 'Publisher con Nome Lungo',
    imageUrl: null,
  },
};

export const WithoutPublisher: Story = {
  args: {
    title: 'Wingspan',
    publisher: null,
    imageUrl:
      'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__imagepage/img/uIjeoKgHMcRtzRSR4MoUYl3nXxs=/fit-in/900x600/filters:no_upscale():strip_icc()/pic4458123.jpg',
  },
};

// Mobile viewport story
export const Mobile: Story = {
  args: {
    title: 'Pandemic',
    publisher: 'Z-Man Games',
    imageUrl:
      'https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXL3RVw__imagepage/img/kIBu-2Ljb_ml5n-S8uIbE6ehGFc=/fit-in/900x600/filters:no_upscale():strip_icc()/pic1534148.jpg',
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

// Tablet viewport story
export const Tablet: Story = {
  args: {
    title: 'Azul',
    publisher: 'Plan B Games',
    imageUrl:
      'https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__imagepage/img/q4uWd9a5h2HldJfvOYNQq0YbxbA=/fit-in/900x600/filters:no_upscale():strip_icc()/pic6973671.png',
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
