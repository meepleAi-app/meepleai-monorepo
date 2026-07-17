/**
 * #3083 — the per-node "Configure" gear must open the config panel by invoking
 * the `onConfigure` callback threaded into the node's data (which PipelineCanvas
 * wires to node selection → BlockConfigPanel). Previously the gear was a no-op
 * that also blocked the working canvas-selection path via stopPropagation.
 *
 * React Flow is mocked so the nodes can render standalone (no real canvas).
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('@xyflow/react', () => ({
  Handle: () => null,
  Position: { Left: 'left', Right: 'right', Top: 'top', Bottom: 'bottom' },
  useReactFlow: () => ({ setNodes: vi.fn() }),
}));

import { BLOCKS_BY_TYPE } from '../block-definitions';
import { VectorSearchNode } from '../nodes';
import { RagBlockNode } from '../RagBlockNode';

import type { RagNodeData } from '../types';

const SAMPLE_BLOCK = Object.values(BLOCKS_BY_TYPE)[0];

function makeData(onConfigure: (nodeId: string) => void): RagNodeData {
  return {
    block: SAMPLE_BLOCK,
    params: {},
    status: 'idle',
    onConfigure,
  };
}

describe('node "Configure" gear (#3083)', () => {
  it('RagBlockNode: clicking the Configure gear invokes onConfigure with the node id', () => {
    const onConfigure = vi.fn();
    render(<RagBlockNode id="node-a" data={makeData(onConfigure)} selected={false} />);

    fireEvent.click(screen.getByRole('button', { name: /configure/i }));

    expect(onConfigure).toHaveBeenCalledWith('node-a');
  });

  it('specialized node (VectorSearch): clicking the Configure gear invokes onConfigure with the node id', () => {
    const onConfigure = vi.fn();
    render(<VectorSearchNode id="node-b" data={makeData(onConfigure)} selected={false} />);

    fireEvent.click(screen.getByRole('button', { name: /configure/i }));

    expect(onConfigure).toHaveBeenCalledWith('node-b');
  });
});
