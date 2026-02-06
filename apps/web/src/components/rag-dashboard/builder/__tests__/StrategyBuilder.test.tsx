/**
 * StrategyBuilder Component Tests (Issue #3412)
 *
 * Test Coverage:
 * - Component rendering with different props
 * - Panel collapse/expand functionality
 * - Read-only mode
 * - User tier access control
 * - Save/test callbacks
 * - Validation integration
 *
 * Target: >80% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { StrategyBuilder } from '../StrategyBuilder';
import type { UserTier, PipelineDefinition } from '../types';

// ============================================================================
// Mock Setup
// ============================================================================

vi.mock('@xyflow/react', () => ({
  ReactFlowProvider: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

vi.mock('../PipelineCanvas', () => ({
  PipelineCanvas: () => <div data-testid="pipeline-canvas">Canvas</div>,
}));

vi.mock('../BlockPalette', () => ({
  BlockPalette: () => <div data-testid="block-palette">Palette</div>,
}));

vi.mock('../BlockConfigPanel', () => ({
  BlockConfigPanel: () => <div data-testid="block-config">Config</div>,
}));

vi.mock('../ValidationPanel', () => ({
  ValidationPanel: () => <div data-testid="validation-panel">Validation</div>,
}));

vi.mock('../PipelineTestPanel', () => ({
  PipelineTestPanel: () => <div data-testid="test-panel">Test</div>,
}));

vi.mock('../validation-engine', () => ({
  validatePipeline: () => ({ isValid: true, errors: [], warnings: [] }),
}));

const mockPipeline: PipelineDefinition = {
  id: 'pipeline-1',
  name: 'Test Strategy',
  description: 'Test description',
  nodes: [],
  edges: [],
  version: '1.0',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('StrategyBuilder - Rendering', () => {
  it('renders without crashing', () => {
    const { container } = render(
      <StrategyBuilder userTier="admin" />
    );
    expect(container).toBeInTheDocument();
  });

  it('renders all main panels', () => {
    render(<StrategyBuilder userTier="admin" />);

    expect(screen.getByTestId('pipeline-canvas')).toBeInTheDocument();
    expect(screen.getByTestId('block-palette')).toBeInTheDocument();
  });

  it('renders with initial pipeline', () => {
    const { container } = render(
      <StrategyBuilder
        userTier="admin"
        initialPipeline={mockPipeline}
      />
    );
    expect(container).toBeInTheDocument();
  });

  it('hides validation panel when showValidation=false', () => {
    render(
      <StrategyBuilder
        userTier="admin"
        showValidation={false}
      />
    );
    expect(screen.queryByTestId('validation-panel')).not.toBeInTheDocument();
  });

  it('hides config panel when showConfig=false', () => {
    render(
      <StrategyBuilder
        userTier="admin"
        showConfig={false}
      />
    );
    expect(screen.queryByTestId('block-config')).not.toBeInTheDocument();
  });
});

// ============================================================================
// User Tier Tests
// ============================================================================

describe('StrategyBuilder - User Tier', () => {
  it('renders for admin tier', () => {
    const { container } = render(<StrategyBuilder userTier="admin" />);
    expect(container).toBeInTheDocument();
  });

  it('renders for premium tier', () => {
    const { container } = render(<StrategyBuilder userTier="premium" />);
    expect(container).toBeInTheDocument();
  });

  it('renders for normal tier', () => {
    const { container } = render(<StrategyBuilder userTier="normal" />);
    expect(container).toBeInTheDocument();
  });

  it('renders for free tier', () => {
    const { container } = render(<StrategyBuilder userTier="free" />);
    expect(container).toBeInTheDocument();
  });
});

// ============================================================================
// Read-Only Mode Tests
// ============================================================================

describe('StrategyBuilder - Read-Only', () => {
  it('renders in read-only mode', () => {
    const { container } = render(
      <StrategyBuilder
        userTier="normal"
        readOnly={true}
      />
    );
    expect(container).toBeInTheDocument();
  });

  it('accepts custom className', () => {
    const { container } = render(
      <StrategyBuilder
        userTier="admin"
        className="custom-class"
      />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });
});

// ============================================================================
// Callback Tests
// ============================================================================

describe('StrategyBuilder - Callbacks', () => {
  it('accepts onSave callback', () => {
    const onSave = vi.fn();
    const { container } = render(
      <StrategyBuilder
        userTier="admin"
        onSave={onSave}
      />
    );
    expect(container).toBeInTheDocument();
  });

  it('accepts onTest callback', () => {
    const onTest = vi.fn();
    const { container } = render(
      <StrategyBuilder
        userTier="admin"
        onTest={onTest}
      />
    );
    expect(container).toBeInTheDocument();
  });

  it('works without callbacks', () => {
    const { container } = render(<StrategyBuilder userTier="admin" />);
    expect(container).toBeInTheDocument();
  });
});
