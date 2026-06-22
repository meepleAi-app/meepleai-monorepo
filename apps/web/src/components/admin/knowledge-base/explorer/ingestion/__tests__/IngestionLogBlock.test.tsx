import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { IngestionLogBlock } from '../IngestionLogBlock';
import type { IngestionLogEntry, IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

const entry = (
  level: 'Info' | 'Warning' | 'Error',
  message: string,
  secondsAfter = 0
): IngestionLogEntry => ({
  id: `00000000-0000-0000-0000-00000000${level[0]}${secondsAfter.toString().padStart(2, '0')}`,
  timestamp: new Date(2026, 4, 29, 10, 0, secondsAfter).toISOString(),
  level,
  message,
});

const step = (name: IngestionStep['stepName'], entries: IngestionLogEntry[]): IngestionStep => ({
  id: `step-${name}`,
  stepName: name,
  status: 'Done',
  startedAt: null,
  completedAt: null,
  durationMs: null,
  metadataJson: null,
  logEntries: entries,
});

describe('IngestionLogBlock', () => {
  it('renders empty placeholder when no entries', () => {
    const { getByText } = render(<IngestionLogBlock steps={[]} />);
    expect(getByText(/nessun log/i)).toBeInTheDocument();
  });

  it('concatenates entries from all steps in timestamp order', () => {
    const { container } = render(
      <IngestionLogBlock
        steps={[
          step('Upload', [entry('Info', 'second', 1)]),
          step('Extract', [entry('Info', 'first', 0)]),
        ]}
      />
    );
    const text = container.textContent ?? '';
    expect(text.indexOf('first')).toBeLessThan(text.indexOf('second'));
  });

  it('applies error color class to Error entries', () => {
    const { container } = render(
      <IngestionLogBlock steps={[step('Index', [entry('Error', 'boom')])]} />
    );
    expect(container.querySelector('[data-log-level="Error"]')?.className).toContain('text-rose');
  });

  it('applies warning color class to Warning entries', () => {
    const { container } = render(
      <IngestionLogBlock steps={[step('Index', [entry('Warning', 'careful')])]} />
    );
    expect(container.querySelector('[data-log-level="Warning"]')?.className).toContain(
      'text-amber'
    );
  });

  it('formats timestamps with seconds precision', () => {
    const { container } = render(
      <IngestionLogBlock steps={[step('Upload', [entry('Info', 'hello', 5)])]} />
    );
    expect(container.textContent).toMatch(/10:00:05/);
  });
});
