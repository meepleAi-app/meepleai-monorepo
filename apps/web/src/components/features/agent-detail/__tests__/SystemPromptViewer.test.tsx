/**
 * SystemPromptViewer unit tests — Wave C.2 Task 2
 *
 * 3 tests: data-slot, prompt text, empty state.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { SystemPromptViewer } from '../SystemPromptViewer';

const LABELS = {
  title: 'System Prompt',
  empty: 'Nessun system prompt configurato.',
  hidden: 'Il system prompt non è disponibile.',
};

describe('SystemPromptViewer', () => {
  it('renders data-slot attribute', () => {
    render(<SystemPromptViewer prompt="Sei un esperto di Wingspan." labels={LABELS} />);
    expect(document.querySelector('[data-slot="agent-detail-system-prompt-viewer"]')).toBeTruthy();
  });

  it('renders prompt text when provided', () => {
    render(<SystemPromptViewer prompt="Sei un esperto di Wingspan." labels={LABELS} />);
    expect(screen.getByText(/esperto di wingspan/i)).toBeInTheDocument();
  });

  it('renders empty state when prompt is null', () => {
    render(<SystemPromptViewer prompt={null} labels={LABELS} />);
    expect(screen.getByText(/nessun system prompt/i)).toBeInTheDocument();
  });
});
