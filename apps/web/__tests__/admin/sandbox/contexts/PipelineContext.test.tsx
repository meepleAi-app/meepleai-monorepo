import { render, screen, act } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import {
  PipelineProvider,
  usePipeline,
  type DocumentPipelineStatus,
} from '@/components/admin/sandbox/contexts/PipelineContext';

// TestConsumer renders context values as data-testid spans for assertion
function TestConsumer() {
  const {
    isAnyProcessing,
    isAllReady,
    processingStatus,
    setProcessingStatus,
    clearProcessingStatus,
    pollingEnabled,
    setPollingEnabled,
  } = usePipeline();

  return (
    <div>
      <span data-testid="is-any-processing">{String(isAnyProcessing)}</span>
      <span data-testid="is-all-ready">{String(isAllReady)}</span>
      <span data-testid="status-map-size">{processingStatus.size}</span>
      <span data-testid="polling-enabled">{String(pollingEnabled)}</span>
      <button
        data-testid="set-processing"
        onClick={() =>
          setProcessingStatus('doc-1', {
            documentId: 'doc-1',
            steps: [],
            overallStatus: 'processing',
          } satisfies DocumentPipelineStatus)
        }
      >
        Set Processing
      </button>
      <button
        data-testid="set-completed-1"
        onClick={() =>
          setProcessingStatus('doc-1', {
            documentId: 'doc-1',
            steps: [],
            overallStatus: 'completed',
          } satisfies DocumentPipelineStatus)
        }
      >
        Set Completed 1
      </button>
      <button
        data-testid="set-completed-2"
        onClick={() =>
          setProcessingStatus('doc-2', {
            documentId: 'doc-2',
            steps: [],
            overallStatus: 'completed',
          } satisfies DocumentPipelineStatus)
        }
      >
        Set Completed 2
      </button>
      <button data-testid="clear" onClick={clearProcessingStatus}>
        Clear
      </button>
      <button data-testid="toggle-polling" onClick={() => setPollingEnabled(!pollingEnabled)}>
        Toggle Polling
      </button>
    </div>
  );
}

function ThrowingConsumer() {
  usePipeline();
  return null;
}

describe('PipelineContext', () => {
  it('defaults to not processing and not ready', () => {
    render(
      <PipelineProvider>
        <TestConsumer />
      </PipelineProvider>
    );

    expect(screen.getByTestId('is-any-processing').textContent).toBe('false');
    expect(screen.getByTestId('is-all-ready').textContent).toBe('false');
    expect(screen.getByTestId('status-map-size').textContent).toBe('0');
    expect(screen.getByTestId('polling-enabled').textContent).toBe('false');
  });

  it('isAnyProcessing true when document processing', () => {
    render(
      <PipelineProvider>
        <TestConsumer />
      </PipelineProvider>
    );

    act(() => {
      screen.getByTestId('set-processing').click();
    });

    expect(screen.getByTestId('is-any-processing').textContent).toBe('true');
    expect(screen.getByTestId('is-all-ready').textContent).toBe('false');
    expect(screen.getByTestId('status-map-size').textContent).toBe('1');
  });

  it('isAllReady true when all documents completed', () => {
    render(
      <PipelineProvider>
        <TestConsumer />
      </PipelineProvider>
    );

    act(() => {
      screen.getByTestId('set-completed-1').click();
    });
    act(() => {
      screen.getByTestId('set-completed-2').click();
    });

    expect(screen.getByTestId('is-any-processing').textContent).toBe('false');
    expect(screen.getByTestId('is-all-ready').textContent).toBe('true');
    expect(screen.getByTestId('status-map-size').textContent).toBe('2');
  });

  it('isAllReady false when map is empty', () => {
    render(
      <PipelineProvider>
        <TestConsumer />
      </PipelineProvider>
    );

    // Initially empty — even with no failures, empty map means not ready
    expect(screen.getByTestId('is-all-ready').textContent).toBe('false');
    expect(screen.getByTestId('status-map-size').textContent).toBe('0');

    // Add and then clear — back to not ready
    act(() => {
      screen.getByTestId('set-completed-1').click();
    });
    expect(screen.getByTestId('is-all-ready').textContent).toBe('true');

    act(() => {
      screen.getByTestId('clear').click();
    });
    expect(screen.getByTestId('is-all-ready').textContent).toBe('false');
    expect(screen.getByTestId('status-map-size').textContent).toBe('0');
  });

  it('throws error when used outside provider', () => {
    // Suppress expected console error from React error boundary
    const consoleError = console.error;
    console.error = () => {};

    expect(() => render(<ThrowingConsumer />)).toThrow(
      'usePipeline must be used within PipelineProvider'
    );

    console.error = consoleError;
  });
});
