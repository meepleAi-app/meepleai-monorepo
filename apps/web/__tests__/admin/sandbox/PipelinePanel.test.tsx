import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PipelineProvider } from '@/components/admin/sandbox/contexts/PipelineContext';
import type { PipelineStepInfo } from '@/components/admin/sandbox/contexts/PipelineContext';
import { PipelineOverview } from '@/components/admin/sandbox/PipelineOverview';
import { PipelineStepCard, type StepDetails } from '@/components/admin/sandbox/PipelineStepCard';
import {
  PipelineDeepMetrics,
  type DeepMetricsData,
} from '@/components/admin/sandbox/PipelineDeepMetrics';
import { PipelinePanel } from '@/components/admin/sandbox/PipelinePanel';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function withProvider(ui: React.ReactElement) {
  return <PipelineProvider>{ui}</PipelineProvider>;
}

function makeSteps(
  overrides?: Partial<Record<string, PipelineStepInfo['status']>>
): PipelineStepInfo[] {
  const defaults: PipelineStepInfo[] = [
    { step: 'upload', status: 'pending' },
    { step: 'extraction', status: 'pending' },
    { step: 'chunking', status: 'pending' },
    { step: 'embedding', status: 'pending' },
    { step: 'ready', status: 'pending' },
  ];
  if (!overrides) return defaults;
  return defaults.map(s => ({
    ...s,
    status: overrides[s.step] ?? s.status,
  }));
}

function makeCompletedSteps(): PipelineStepInfo[] {
  return makeSteps({
    upload: 'completed',
    extraction: 'completed',
    chunking: 'completed',
    embedding: 'completed',
    ready: 'completed',
  });
}

// ---------------------------------------------------------------------------
// PipelineOverview
// ---------------------------------------------------------------------------

describe('PipelineOverview', () => {
  it('renders 5 step nodes with correct labels', () => {
    render(<PipelineOverview steps={makeSteps()} />);

    expect(screen.getByTestId('step-node-upload')).toBeInTheDocument();
    expect(screen.getByTestId('step-node-extraction')).toBeInTheDocument();
    expect(screen.getByTestId('step-node-chunking')).toBeInTheDocument();
    expect(screen.getByTestId('step-node-embedding')).toBeInTheDocument();
    expect(screen.getByTestId('step-node-ready')).toBeInTheDocument();

    expect(screen.getByText('Upload')).toBeInTheDocument();
    expect(screen.getByText('Estrazione')).toBeInTheDocument();
    expect(screen.getByText('Chunking')).toBeInTheDocument();
    expect(screen.getByText('Embedding')).toBeInTheDocument();
    expect(screen.getByText('Pronto')).toBeInTheDocument();
  });

  it('shows elapsed time when provided', () => {
    render(<PipelineOverview steps={makeSteps()} elapsedMs={125000} />);

    const elapsed = screen.getByTestId('elapsed-time');
    expect(elapsed).toBeInTheDocument();
    expect(elapsed.textContent).toContain('2m 5s');
  });

  it('does not show elapsed time when zero', () => {
    render(<PipelineOverview steps={makeSteps()} elapsedMs={0} />);

    expect(screen.queryByTestId('elapsed-time')).not.toBeInTheDocument();
  });

  it('all completed steps show emerald styling', () => {
    render(<PipelineOverview steps={makeCompletedSteps()} />);

    const uploadNode = screen.getByTestId('step-node-upload');
    expect(uploadNode.className).toContain('bg-emerald-500');
    const readyNode = screen.getByTestId('step-node-ready');
    expect(readyNode.className).toContain('bg-emerald-500');
  });

  it('in_progress step shows amber with pulse', () => {
    const steps = makeSteps({ upload: 'completed', extraction: 'in_progress' });
    render(<PipelineOverview steps={steps} />);

    const extractionNode = screen.getByTestId('step-node-extraction');
    expect(extractionNode.className).toContain('bg-amber-500');
    expect(extractionNode.className).toContain('animate-pulse');
  });

  it('renders connector lines between nodes', () => {
    render(<PipelineOverview steps={makeSteps()} />);

    // 5 nodes = 4 connectors
    expect(screen.getByTestId('connector-upload')).toBeInTheDocument();
    expect(screen.getByTestId('connector-extraction')).toBeInTheDocument();
    expect(screen.getByTestId('connector-chunking')).toBeInTheDocument();
    expect(screen.getByTestId('connector-embedding')).toBeInTheDocument();
    // No connector after 'ready' (last node)
    expect(screen.queryByTestId('connector-ready')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// PipelineStepCard
// ---------------------------------------------------------------------------

describe('PipelineStepCard', () => {
  it('renders step name and duration', () => {
    const step: PipelineStepInfo = {
      step: 'extraction',
      status: 'completed',
      durationMs: 2340,
    };
    render(<PipelineStepCard step={step} />);

    expect(screen.getByText('Estrazione')).toBeInTheDocument();
    expect(screen.getByText('2.3s')).toBeInTheDocument();
    expect(screen.getByText('Completato')).toBeInTheDocument();
  });

  it('shows error state with message', async () => {
    const step: PipelineStepInfo = {
      step: 'embedding',
      status: 'failed',
      error: 'Qdrant service unavailable',
    };
    const details: StepDetails = { vectorCount: 0 };
    const user = userEvent.setup();

    render(<PipelineStepCard step={step} details={details} />);

    // Card should have red border
    const card = screen.getByTestId('step-card-embedding');
    expect(card.className).toContain('border-red-300');

    // Expand to see error
    await user.click(screen.getByRole('button', { name: /dettagli step embedding/i }));
    expect(screen.getByTestId('step-error')).toHaveTextContent('Qdrant service unavailable');
  });

  it('expandable shows details when clicked', async () => {
    const step: PipelineStepInfo = { step: 'chunking', status: 'completed', durationMs: 500 };
    const details: StepDetails = {
      chunkSamples: ['First chunk content here', 'Second chunk text', 'Third chunk sample'],
    };
    const user = userEvent.setup();

    render(<PipelineStepCard step={step} details={details} />);

    // Initially chunk samples not visible
    expect(screen.queryByTestId('chunk-sample-0')).not.toBeInTheDocument();

    // Click to expand
    await user.click(screen.getByRole('button', { name: /dettagli step chunking/i }));

    expect(screen.getByTestId('chunk-sample-0')).toBeInTheDocument();
    expect(screen.getByTestId('chunk-sample-1')).toBeInTheDocument();
    expect(screen.getByTestId('chunk-sample-2')).toBeInTheDocument();
  });

  it('shows page count for extraction step', async () => {
    const step: PipelineStepInfo = { step: 'extraction', status: 'completed' };
    const details: StepDetails = { pageCount: 42 };
    const user = userEvent.setup();

    render(<PipelineStepCard step={step} details={details} />);

    await user.click(screen.getByRole('button', { name: /dettagli step estrazione/i }));
    expect(screen.getByTestId('page-count')).toHaveTextContent('42');
  });
});

// ---------------------------------------------------------------------------
// PipelineDeepMetrics
// ---------------------------------------------------------------------------

describe('PipelineDeepMetrics', () => {
  it('renders distribution bars', () => {
    const metrics: DeepMetricsData = {
      chunkSizeDistribution: [
        { rangeLabel: '0-200', count: 5 },
        { rangeLabel: '200-500', count: 15 },
        { rangeLabel: '500-1000', count: 8 },
      ],
    };

    render(<PipelineDeepMetrics metrics={metrics} />);

    expect(screen.getByTestId('chunk-distribution')).toBeInTheDocument();
    expect(screen.getByTestId('bar-0-200')).toBeInTheDocument();
    expect(screen.getByTestId('bar-200-500')).toBeInTheDocument();
    expect(screen.getByTestId('bar-500-1000')).toBeInTheDocument();

    // Largest bar should be 100% width
    const largestBar = screen.getByTestId('bar-200-500');
    expect(largestBar.style.width).toBe('100%');
  });

  it('renders vector stats', () => {
    const metrics: DeepMetricsData = {
      vectorStats: {
        vectorsCount: 1234,
        memoryUsageBytes: 5 * 1024 * 1024,
        collectionStatus: 'green',
      },
    };

    render(<PipelineDeepMetrics metrics={metrics} />);

    expect(screen.getByTestId('vector-stats')).toBeInTheDocument();
    expect(screen.getByTestId('vectors-count')).toHaveTextContent((1234).toLocaleString());
    expect(screen.getByTestId('memory-usage')).toHaveTextContent('5.0 MB');
    expect(screen.getByTestId('collection-status')).toHaveTextContent('Operativa');
  });

  it('renders quality indicators', () => {
    const metrics: DeepMetricsData = {
      qualityIndicators: {
        pageCoveragePercent: 95,
        emptyChunksCount: 2,
        duplicateDetectionCount: 0,
      },
    };

    render(<PipelineDeepMetrics metrics={metrics} />);

    expect(screen.getByTestId('quality-indicators')).toBeInTheDocument();
    expect(screen.getByTestId('page-coverage')).toHaveTextContent('95%');
    expect(screen.getByTestId('empty-chunks')).toHaveTextContent('2');
    expect(screen.getByTestId('duplicates')).toHaveTextContent('0');
  });

  it('shows fallback when no metrics provided', () => {
    render(<PipelineDeepMetrics />);

    expect(screen.getByText('Nessuna metrica disponibile')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// PipelinePanel (composition)
// ---------------------------------------------------------------------------

describe('PipelinePanel', () => {
  it('shows empty state without game selected', () => {
    render(withProvider(<PipelinePanel />));

    expect(screen.getByText('Seleziona un gioco per visualizzare la pipeline')).toBeInTheDocument();
  });

  it('shows pipeline title with Activity icon', () => {
    render(withProvider(<PipelinePanel hasGameSelected />));

    expect(screen.getByText('Pipeline')).toBeInTheDocument();
  });

  it('Level 1 is default, Level 2 hidden until toggled', async () => {
    const user = userEvent.setup();

    render(withProvider(<PipelinePanel hasGameSelected steps={makeCompletedSteps()} />));

    // Level 1: overview nodes visible
    expect(screen.getByTestId('step-node-upload')).toBeInTheDocument();

    // Level 2: step details hidden
    expect(screen.queryByTestId('step-details-section')).not.toBeInTheDocument();

    // Toggle details
    await user.click(screen.getByTestId('toggle-details'));
    expect(screen.getByTestId('step-details-section')).toBeInTheDocument();

    // Level 3: metrics hidden
    expect(screen.queryByTestId('deep-metrics-section')).not.toBeInTheDocument();
  });

  it('Level 3 toggles independently after Level 2 is open', async () => {
    const user = userEvent.setup();
    const metrics: DeepMetricsData = {
      chunkSizeDistribution: [{ rangeLabel: '0-500', count: 10 }],
    };

    render(
      withProvider(
        <PipelinePanel hasGameSelected steps={makeCompletedSteps()} deepMetrics={metrics} />
      )
    );

    // Open Level 2
    await user.click(screen.getByTestId('toggle-details'));
    expect(screen.getByTestId('step-details-section')).toBeInTheDocument();

    // Open Level 3
    await user.click(screen.getByTestId('toggle-metrics'));
    expect(screen.getByTestId('deep-metrics-section')).toBeInTheDocument();

    // Close Level 3
    await user.click(screen.getByTestId('toggle-metrics'));
    expect(screen.queryByTestId('deep-metrics-section')).not.toBeInTheDocument();
  });

  it('hides toggle buttons when no steps provided', () => {
    render(withProvider(<PipelinePanel hasGameSelected steps={[]} />));

    expect(screen.queryByTestId('toggle-details')).not.toBeInTheDocument();
  });
});
