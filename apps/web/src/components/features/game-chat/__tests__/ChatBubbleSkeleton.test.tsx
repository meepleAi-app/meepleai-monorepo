/**
 * ChatBubbleSkeleton — pure component tests
 * Spec: docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md §3.2
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatBubbleSkeleton } from '../ChatBubbleSkeleton';

describe('ChatBubbleSkeleton', () => {
  it('renders 3 skeleton bubbles by default', () => {
    render(<ChatBubbleSkeleton />);
    expect(screen.getAllByTestId('chat-bubble-skeleton')).toHaveLength(3);
  });

  it('renders custom count of bubbles', () => {
    render(<ChatBubbleSkeleton count={5} />);
    expect(screen.getAllByTestId('chat-bubble-skeleton')).toHaveLength(5);
  });
});
