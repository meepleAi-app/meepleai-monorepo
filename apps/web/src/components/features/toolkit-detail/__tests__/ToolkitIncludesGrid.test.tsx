/**
 * ToolkitIncludesGrid - Unit tests (Issue #1479).
 *
 * "Cosa include" 3-cell grid for the Overview tab of `/toolkits/[id]`.
 * Renders three fixed cells — agent name, KB documents count, tools count —
 * each backed by the private `Cell` component. Pure presentational.
 * Source: PR #1163 (closes #1145).
 *
 * Test matrix (Crispin):
 *   T1. Renders 3 cells (data-slot="toolkit-detail-includes-cell").
 *   T2. Root grid data-slot present.
 *   T3. Agent cell shows labels.agent and agentName value.
 *   T4. KB docs cell shows labels.kbDocs and String(kbDocsCount).
 *   T5. Tools cell shows labels.tools and String(toolsCount).
 *   T6. Counts rendered as strings — 0 renders "0".
 *   T7. Large count numbers rendered correctly via String() (not toLocaleString).
 *   T8. All three label keys are rendered in the document.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ToolkitIncludesGrid, type ToolkitIncludesGridLabels } from '../ToolkitIncludesGrid';

const labels: ToolkitIncludesGridLabels = {
  agent: 'Agente',
  kbDocs: 'Documenti KB',
  tools: 'Strumenti',
};

const baseProps = {
  agentName: 'MeepleBot',
  kbDocsCount: 5,
  toolsCount: 3,
  labels,
};

describe('ToolkitIncludesGrid (Issue #1479)', () => {
  // T1
  it('renders exactly 3 cells with data-slot="toolkit-detail-includes-cell"', () => {
    const { container } = render(<ToolkitIncludesGrid {...baseProps} />);
    expect(container.querySelectorAll('[data-slot="toolkit-detail-includes-cell"]')).toHaveLength(
      3
    );
  });

  // T2
  it('exposes data-slot on the root grid', () => {
    const { container } = render(<ToolkitIncludesGrid {...baseProps} />);
    expect(
      container.querySelector('[data-slot="toolkit-detail-includes-grid"]')
    ).toBeInTheDocument();
  });

  // T3
  it('renders the agent label and agentName value', () => {
    render(<ToolkitIncludesGrid {...baseProps} agentName="MeepleBot" />);
    expect(screen.getByText('Agente')).toBeInTheDocument();
    expect(screen.getByText('MeepleBot')).toBeInTheDocument();
  });

  // T4
  it('renders the kbDocs label and String(kbDocsCount) value', () => {
    render(<ToolkitIncludesGrid {...baseProps} kbDocsCount={5} />);
    expect(screen.getByText('Documenti KB')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  // T5
  it('renders the tools label and String(toolsCount) value', () => {
    render(<ToolkitIncludesGrid {...baseProps} toolsCount={3} />);
    expect(screen.getByText('Strumenti')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  // T6
  it('renders "0" when counts are zero', () => {
    render(<ToolkitIncludesGrid {...baseProps} kbDocsCount={0} toolsCount={0} />);
    // Both cells render "0"; getAllByText covers both
    const zeros = screen.getAllByText('0');
    expect(zeros).toHaveLength(2);
  });

  // T7
  it('renders large counts as plain strings (no locale formatting)', () => {
    render(<ToolkitIncludesGrid {...baseProps} kbDocsCount={1000} toolsCount={999} />);
    // String(1000) = "1000", NOT "1,000"
    expect(screen.getByText('1000')).toBeInTheDocument();
    expect(screen.getByText('999')).toBeInTheDocument();
  });

  // T8
  it('renders all three label keys in the document', () => {
    render(<ToolkitIncludesGrid {...baseProps} />);
    expect(screen.getByText('Agente')).toBeInTheDocument();
    expect(screen.getByText('Documenti KB')).toBeInTheDocument();
    expect(screen.getByText('Strumenti')).toBeInTheDocument();
  });
});
