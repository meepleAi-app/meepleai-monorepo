import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { PdfStatusTimeline } from '../PdfStatusTimeline';

describe('PdfStatusTimeline', () => {
  it('renders timeline with current state', () => {
    render(
      <PdfStatusTimeline
        currentState="uploading"
        completedStates={[]}
        startedAt={new Date().toISOString()}
      />
    );
    expect(screen.getByTestId('pdf-status-timeline')).toBeInTheDocument();
  });

  it('marks completed states with checkmarks', () => {
    render(
      <PdfStatusTimeline
        currentState="embedding"
        completedStates={['uploading', 'extracting', 'chunking']}
      />
    );

    const uploadStep = screen.getByTestId('timeline-step-uploading');
    expect(uploadStep.querySelector('svg[class*="lucide-check"]')).toBeInTheDocument();
  });

  it('shows loader for current state', () => {
    render(
      <PdfStatusTimeline currentState="embedding" completedStates={['uploading', 'extracting']} />
    );

    const embeddingStep = screen.getByTestId('timeline-step-embedding');
    expect(embeddingStep.querySelector('svg[class*="animate-spin"]')).toBeInTheDocument();
  });

  it('displays timestamps when provided', () => {
    const startedAt = new Date().toISOString();
    render(<PdfStatusTimeline currentState="uploading" completedStates={[]} startedAt={startedAt} />);

    expect(screen.getByText(/Started:/)).toBeInTheDocument();
  });

  it('displays ETA when provided and not completed', () => {
    const startedAt = new Date().toISOString();
    const eta = new Date(Date.now() + 60000).toISOString();
    render(
      <PdfStatusTimeline
        currentState="uploading"
        completedStates={[]}
        startedAt={startedAt}
        estimatedCompletion={eta}
      />
    );

    expect(screen.getByText(/ETA:/)).toBeInTheDocument();
  });

  it('hides ETA when state is ready', () => {
    render(
      <PdfStatusTimeline
        currentState="ready"
        completedStates={['uploading', 'extracting', 'chunking', 'embedding', 'indexing']}
        startedAt={new Date().toISOString()}
        estimatedCompletion={new Date().toISOString()}
      />
    );

    expect(screen.queryByText(/ETA:/)).not.toBeInTheDocument();
  });
});
