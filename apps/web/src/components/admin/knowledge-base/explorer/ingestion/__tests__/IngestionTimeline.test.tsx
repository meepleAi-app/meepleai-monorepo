import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { IngestionTimeline } from '../IngestionTimeline';
import type { IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

function buildStep(
  name: IngestionStep['stepName'],
  status: string,
  durationMs: number | null = 100
): IngestionStep {
  return {
    id: `00000000-0000-0000-0000-00000000000${name[0]}`,
    stepName: name,
    status,
    startedAt: '2026-05-29T10:00:00.000Z',
    completedAt: status === 'Done' ? '2026-05-29T10:00:01.000Z' : null,
    durationMs,
    metadataJson: null,
    logEntries: [],
  };
}

describe('IngestionTimeline', () => {
  it('renders all 5 step slots even if backend returns fewer', () => {
    const { getByTestId } = render(<IngestionTimeline steps={[buildStep('Upload', 'Done')]} />);
    expect(getByTestId('ingestion-step-upload')).toBeInTheDocument();
    expect(getByTestId('ingestion-step-extract')).toBeInTheDocument();
    expect(getByTestId('ingestion-step-chunk')).toBeInTheDocument();
    expect(getByTestId('ingestion-step-embed')).toBeInTheDocument();
    expect(getByTestId('ingestion-step-index')).toBeInTheDocument();
  });

  it('synthesizes a pending placeholder for missing steps', () => {
    const { getByTestId } = render(<IngestionTimeline steps={[buildStep('Upload', 'Done')]} />);
    expect(getByTestId('ingestion-step-extract').getAttribute('data-step-status')).toBe('pending');
  });

  it('preserves backend status when the step is present', () => {
    const { getByTestId } = render(
      <IngestionTimeline steps={[buildStep('Embed', 'Running', null)]} />
    );
    expect(getByTestId('ingestion-step-embed').getAttribute('data-step-status')).toBe('running');
  });

  it('renders in fixed pipeline order regardless of input order', () => {
    const { getAllByTestId } = render(
      <IngestionTimeline steps={[buildStep('Index', 'Done'), buildStep('Upload', 'Done')]} />
    );
    const items = getAllByTestId(/^ingestion-step-/);
    expect(items[0].getAttribute('data-testid')).toBe('ingestion-step-upload');
    expect(items[4].getAttribute('data-testid')).toBe('ingestion-step-index');
  });
});
