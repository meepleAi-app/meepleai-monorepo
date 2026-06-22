import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ReindexModal, type ReindexCostRow } from '../ReindexModal';

const baseLabels = {
  title: 'Re-index full KB',
  subtitle: 'Gloomhaven · 12 documenti',
  costHeader: 'Stima costo operazione',
  description: 'Questa operazione rielabora tutti i chunk e rigenera gli embedding.',
  reindexCta: 'Re-index',
  cancelCta: 'Annulla',
  runningTitle: 'Re-index in corso',
  jobIdLabel: 'Job ID',
  progressTemplate: '{processed} / {total} chunks',
  doneTitle: 'Re-index completato',
  doneSummaryTemplate: '{chunks} chunks · {embeddings} embeddings · costo reale {cost}',
  closeCta: 'Chiudi',
};

const baseCostRows: ReindexCostRow[] = [
  { key: 'chunks', label: 'Chunks da re-embed', value: '1,247' },
  { key: 'total', label: 'Costo stimato', value: '~$0.45', bold: true },
];

describe('ReindexModal (Issue #1481)', () => {
  it('does not render content when open=false', () => {
    render(
      <ReindexModal
        open={false}
        phase="confirm"
        labels={baseLabels}
        costRows={baseCostRows}
        onConfirm={() => {}}
        onClose={() => {}}
      />
    );
    expect(screen.queryByText('Re-index full KB')).not.toBeInTheDocument();
  });

  it('phase=confirm: renders title, cost table, description, both CTAs', () => {
    render(
      <ReindexModal
        open
        phase="confirm"
        labels={baseLabels}
        costRows={baseCostRows}
        onConfirm={() => {}}
        onClose={() => {}}
      />
    );
    expect(screen.getByText('Re-index full KB')).toBeInTheDocument();
    // DialogContent renders in a Radix Portal — query document, not container.
    expect(document.querySelector('[data-slot="kb-hub-reindex-cost-table"]')).toBeInTheDocument();
    expect(screen.getByText('Chunks da re-embed')).toBeInTheDocument();
    expect(screen.getByText('1,247')).toBeInTheDocument();
    expect(screen.getByText('~$0.45')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Re-index' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();
  });

  it('phase=confirm: invokes onConfirm when Re-index button clicked', () => {
    const onConfirm = vi.fn();
    render(
      <ReindexModal
        open
        phase="confirm"
        labels={baseLabels}
        costRows={baseCostRows}
        onConfirm={onConfirm}
        onClose={() => {}}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Re-index' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('phase=running: renders spinner + jobId + progress', () => {
    render(
      <ReindexModal
        open
        phase="running"
        labels={baseLabels}
        costRows={baseCostRows}
        progress={{ current: 474, total: 1247, jobId: 'job_xk92m3p' }}
        onConfirm={() => {}}
        onClose={() => {}}
      />
    );
    expect(screen.getByText('Re-index in corso')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="kb-hub-reindex-running"]')).toBeInTheDocument();
    expect(screen.getByText('job_xk92m3p')).toBeInTheDocument();
    expect(screen.getByText('474 / 1247 chunks')).toBeInTheDocument();
  });

  it('phase=done: renders success badge, summary, close CTA', () => {
    const onClose = vi.fn();
    render(
      <ReindexModal
        open
        phase="done"
        labels={baseLabels}
        costRows={baseCostRows}
        summary={{ chunks: 1247, embeddings: 4891, actualCost: '$0.43' }}
        onConfirm={() => {}}
        onClose={onClose}
      />
    );
    expect(screen.getByText('Re-index completato')).toBeInTheDocument();
    // Locale-tolerant: vitest jsdom may use en-US fallback even when toLocaleString('it-IT')
    // is requested. Assert via text content predicate that survives "1.247" vs "1,247".
    expect(
      screen.getByText(
        text => text.includes('chunks') && text.includes('embeddings') && text.includes('$0.43')
      )
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Chiudi' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('phase=running: progress optional (no progress prop → no chunks line)', () => {
    render(
      <ReindexModal
        open
        phase="running"
        labels={baseLabels}
        costRows={baseCostRows}
        onConfirm={() => {}}
        onClose={() => {}}
      />
    );
    expect(screen.queryByText(/chunks$/)).not.toBeInTheDocument();
  });

  it('FSM data-phase attribute reflects current phase', () => {
    const { rerender } = render(
      <ReindexModal
        open
        phase="confirm"
        labels={baseLabels}
        costRows={baseCostRows}
        onConfirm={() => {}}
        onClose={() => {}}
      />
    );
    // DialogContent is rendered through a Radix Portal, so RTL's container scope
    // does not include it. Query document directly (or via screen.getByRole('dialog')).
    expect(document.querySelector('[data-phase="confirm"]')).toBeInTheDocument();

    rerender(
      <ReindexModal
        open
        phase="running"
        labels={baseLabels}
        costRows={baseCostRows}
        onConfirm={() => {}}
        onClose={() => {}}
      />
    );
    expect(document.querySelector('[data-phase="running"]')).toBeInTheDocument();
  });
});
