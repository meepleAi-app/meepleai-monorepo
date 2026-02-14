/**
 * PdfErrorCard Stories (Issue #4217)
 */

import { PdfErrorCard } from './PdfErrorCard';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PdfErrorCard> = {
  title: 'Components/PDF/PdfErrorCard',
  component: PdfErrorCard,
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof PdfErrorCard>;

export const AllCategories: Story = {
  render: () => (
    <div className="space-y-4 max-w-md">
      <PdfErrorCard category="network" error="Failed to upload PDF. Please check your internet connection." canRetry onRetry={() => console.log('Retry network')} />
      <PdfErrorCard category="parsing" error="Unable to extract text from PDF. File may be corrupted or password-protected." canRetry onRetry={() => console.log('Retry parsing')} />
      <PdfErrorCard category="quota" error="You have reached your PDF upload limit. Please upgrade your plan." canRetry={false} />
      <PdfErrorCard category="service" error="Embedding service is temporarily unavailable. Please try again later." canRetry onRetry={() => console.log('Retry service')} />
      <PdfErrorCard category="unknown" error="An unexpected error occurred while processing your PDF." canRetry onRetry={() => console.log('Retry unknown')} />
    </div>
  ),
};

export const NetworkError: Story = {
  args: {
    category: 'network',
    error: 'Failed to upload PDF. Please check your internet connection.',
    canRetry: true,
    onRetry: () => console.log('Retry clicked'),
  },
};

export const ParsingError: Story = {
  args: {
    category: 'parsing',
    error: 'Unable to extract text from PDF. File may be corrupted or password-protected.',
    canRetry: true,
  },
};

export const QuotaError: Story = {
  args: {
    category: 'quota',
    error: 'You have reached your PDF upload limit. Please upgrade your plan.',
    canRetry: false,
  },
};

export const ServiceError: Story = {
  args: {
    category: 'service',
    error: 'Embedding service is temporarily unavailable. Please try again later.',
    canRetry: true,
  },
};

export const UnknownError: Story = {
  args: {
    category: 'unknown',
    error: 'An unexpected error occurred while processing your PDF.',
    canRetry: true,
  },
};

export const DarkMode: Story = {
  render: () => (
    <div className="dark bg-gray-900 p-6 rounded-lg space-y-4 max-w-md">
      <PdfErrorCard category="network" error="Network connection lost during upload." canRetry onRetry={() => {}} />
      <PdfErrorCard category="parsing" error="PDF parsing failed." canRetry={false} />
    </div>
  ),
};
