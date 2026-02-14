/**
 * PdfProgressBar Stories (Issue #4217)
 * Showcases progress bar with all states and progress values
 */

import { PdfProgressBar } from './PdfProgressBar';

import type { Meta, StoryObj } from '@storybook/react';


const meta: Meta<typeof PdfProgressBar> = {
  title: 'Components/PDF/PdfProgressBar',
  component: PdfProgressBar,
  tags: ['autodocs'],
  argTypes: {
    state: {
      control: 'select',
      options: ['pending', 'uploading', 'extracting', 'chunking', 'embedding', 'indexing', 'ready', 'failed'],
      description: 'Current PDF processing state',
    },
    progress: {
      control: { type: 'range', min: 0, max: 100, step: 1 },
      description: 'Progress percentage (0-100)',
    },
    showLabel: {
      control: 'boolean',
      description: 'Show state label above progress bar',
    },
  },
};

export default meta;
type Story = StoryObj<typeof PdfProgressBar>;

// ============================================================================
// All States with Default Progress
// ============================================================================

export const AllStatesDefault: Story = {
  name: 'All States (Default Progress)',
  render: () => (
    <div className="space-y-6 p-4 max-w-md">
      <PdfProgressBar state="pending" />
      <PdfProgressBar state="uploading" />
      <PdfProgressBar state="extracting" />
      <PdfProgressBar state="chunking" />
      <PdfProgressBar state="embedding" />
      <PdfProgressBar state="indexing" />
      <PdfProgressBar state="ready" />
      <PdfProgressBar state="failed" />
    </div>
  ),
};

// ============================================================================
// Custom Progress Values
// ============================================================================

export const CustomProgressValues: Story = {
  name: 'Custom Progress Values',
  render: () => (
    <div className="space-y-6 p-4 max-w-md">
      <PdfProgressBar state="uploading" progress={25} />
      <PdfProgressBar state="extracting" progress={45} />
      <PdfProgressBar state="chunking" progress={65} />
      <PdfProgressBar state="embedding" progress={80} />
      <PdfProgressBar state="indexing" progress={95} />
    </div>
  ),
};

// ============================================================================
// Without Labels
// ============================================================================

export const WithoutLabels: Story = {
  name: 'Without Labels',
  render: () => (
    <div className="space-y-4 p-4 max-w-md">
      <PdfProgressBar state="uploading" showLabel={false} />
      <PdfProgressBar state="extracting" progress={50} showLabel={false} />
      <PdfProgressBar state="embedding" progress={75} showLabel={false} />
      <PdfProgressBar state="ready" showLabel={false} />
    </div>
  ),
};

// ============================================================================
// Individual Examples
// ============================================================================

export const Uploading: Story = {
  args: {
    state: 'uploading',
    progress: 25,
    showLabel: true,
  },
};

export const Extracting: Story = {
  args: {
    state: 'extracting',
    progress: 45,
    showLabel: true,
  },
};

export const Embedding: Story = {
  args: {
    state: 'embedding',
    progress: 75,
    showLabel: true,
  },
};

export const Ready: Story = {
  args: {
    state: 'ready',
    progress: 100,
    showLabel: true,
  },
};

export const Failed: Story = {
  args: {
    state: 'failed',
    progress: 60,
    showLabel: true,
  },
};

// ============================================================================
// Animated Progress
// ============================================================================

export const AnimatedProgress: Story = {
  name: 'Simulated Upload Progress',
  render: () => {
    const [progress, setProgress] = React.useState(0);

    React.useEffect(() => {
      const timer = setInterval(() => {
        setProgress((prev) => {
          if (prev >= 100) {
            clearInterval(timer);
            return 100;
          }
          return prev + 1;
        });
      }, 50);

      return () => clearInterval(timer);
    }, []);

    const currentState =
      progress < 20
        ? 'uploading'
        : progress < 40
          ? 'extracting'
          : progress < 60
            ? 'chunking'
            : progress < 80
              ? 'embedding'
              : progress < 100
                ? 'indexing'
                : 'ready';

    return (
      <div className="p-4 max-w-md">
        <PdfProgressBar state={currentState} progress={progress} />
      </div>
    );
  },
};

// ============================================================================
// Dark Mode
// ============================================================================

export const DarkMode: Story = {
  name: 'All States (Dark Mode)',
  render: () => (
    <div className="dark bg-gray-900 p-6 rounded-lg space-y-6 max-w-md">
      <PdfProgressBar state="uploading" />
      <PdfProgressBar state="extracting" progress={45} />
      <PdfProgressBar state="embedding" progress={75} />
      <PdfProgressBar state="ready" />
      <PdfProgressBar state="failed" />
    </div>
  ),
};

// ============================================================================
// In Modal Context
// ============================================================================

export const InModalContext: Story = {
  name: 'In Upload Modal',
  render: () => (
    <div className="border rounded-lg p-6 max-w-md bg-card">
      <h3 className="text-lg font-semibold mb-4">Uploading PDF</h3>
      <p className="text-sm text-muted-foreground mb-4">Catan Rulebook.pdf (2.3 MB)</p>
      <PdfProgressBar state="embedding" progress={72} />
      <p className="text-xs text-muted-foreground mt-2 text-center">
        Estimated time remaining: 1m 30s
      </p>
    </div>
  ),
};

// Add React import for AnimatedProgress story
import React from 'react';
