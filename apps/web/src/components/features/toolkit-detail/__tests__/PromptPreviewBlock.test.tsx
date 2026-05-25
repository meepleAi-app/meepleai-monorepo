/**
 * PromptPreviewBlock - Unit tests (Issue #1479).
 *
 * Pure presentational system-prompt preview block. Maps from the mockup
 * AgentTab "System prompt (preview)" section (sp4-toolkit-detail.jsx:560-592).
 * Source data: ToolkitAgentSummary.systemPromptPreview. `tokenCount` is NOT in
 * the v1 backend schema → optional, graceful-hide (P83).
 *
 * Test matrix (Crispin):
 *   T1. Renders heading + the system prompt text in a <pre>.
 *   T2. data-slot attribute present.
 *   T3. tokenCount provided → renders "{n} {suffix}" badge.
 *   T4. tokenCount omitted/null → no token badge (graceful hide).
 *   T5. Multiline prompt content preserved verbatim.
 *   T6. DS-15 tokens on the <pre> (bg-muted, border-border, text-foreground).
 *   T7. className composition on root.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PromptPreviewBlock } from '../PromptPreviewBlock';

const labels = {
  heading: 'System prompt (preview)',
  tokenCountSuffix: 'token',
};

const PROMPT = 'Sei "Azul Rules Expert".\nRispondi sempre in italiano.';

describe('PromptPreviewBlock (Issue #1479)', () => {
  // T1
  it('renders the heading and the system prompt text', () => {
    render(<PromptPreviewBlock systemPrompt={PROMPT} labels={labels} />);
    expect(screen.getByRole('heading', { name: 'System prompt (preview)' })).toBeInTheDocument();
    expect(screen.getByText(/Azul Rules Expert/)).toBeInTheDocument();
  });

  // T2
  it('exposes a data-slot on the root', () => {
    const { container } = render(<PromptPreviewBlock systemPrompt={PROMPT} labels={labels} />);
    expect(
      container.querySelector('[data-slot="toolkit-detail-prompt-preview"]')
    ).toBeInTheDocument();
  });

  // T3
  it('renders the token-count badge when tokenCount is provided', () => {
    render(<PromptPreviewBlock systemPrompt={PROMPT} tokenCount={342} labels={labels} />);
    expect(screen.getByText('342 token')).toBeInTheDocument();
  });

  // T4
  it('hides the token-count badge when tokenCount is omitted', () => {
    render(<PromptPreviewBlock systemPrompt={PROMPT} labels={labels} />);
    expect(screen.queryByText(/token/)).not.toBeInTheDocument();
  });

  it('hides the token-count badge when tokenCount is null', () => {
    render(<PromptPreviewBlock systemPrompt={PROMPT} tokenCount={null} labels={labels} />);
    expect(screen.queryByText(/token/)).not.toBeInTheDocument();
  });

  // T5
  it('preserves the multiline prompt content verbatim', () => {
    const { container } = render(<PromptPreviewBlock systemPrompt={PROMPT} labels={labels} />);
    const pre = container.querySelector('pre');
    expect(pre).not.toBeNull();
    expect(pre?.textContent).toBe(PROMPT);
  });

  // T6
  it('uses DS-15 tokens on the pre block', () => {
    const { container } = render(<PromptPreviewBlock systemPrompt={PROMPT} labels={labels} />);
    const pre = container.querySelector('pre');
    expect(pre).toHaveClass('bg-muted');
    expect(pre).toHaveClass('border-border');
    expect(pre).toHaveClass('text-foreground');
  });

  // T7
  it('composes custom className with base classes on the root', () => {
    const { container } = render(
      <PromptPreviewBlock systemPrompt={PROMPT} labels={labels} className="extra" />
    );
    const root = container.querySelector('[data-slot="toolkit-detail-prompt-preview"]');
    expect(root).toHaveClass('extra');
  });
});
