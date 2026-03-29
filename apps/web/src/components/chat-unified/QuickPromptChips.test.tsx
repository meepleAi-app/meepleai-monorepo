/**
 * QuickPromptChips Tests
 *
 * Tests:
 * 1. Renders all prompts
 * 2. Calls onSelect with correct text
 * 3. Returns null when prompts is empty
 * 4. Returns null when hidden is true
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { QuickPromptChips } from './QuickPromptChips';

const PROMPTS = ['Come si vince?', 'Quante carte?', 'Regola del jolly'];

describe('QuickPromptChips', () => {
  it('renders all prompts', () => {
    render(<QuickPromptChips prompts={PROMPTS} onSelect={vi.fn()} />);
    PROMPTS.forEach(prompt => {
      expect(screen.getByText(prompt)).toBeInTheDocument();
    });
  });

  it('calls onSelect with correct text when chip clicked', () => {
    const onSelect = vi.fn();
    render(<QuickPromptChips prompts={PROMPTS} onSelect={onSelect} />);
    fireEvent.click(screen.getByText('Quante carte?'));
    expect(onSelect).toHaveBeenCalledWith('Quante carte?');
    expect(onSelect).toHaveBeenCalledTimes(1);
  });

  it('returns null when prompts is empty', () => {
    const { container } = render(<QuickPromptChips prompts={[]} onSelect={vi.fn()} />);
    expect(container.firstChild).toBeNull();
  });

  it('returns null when hidden is true', () => {
    const { container } = render(<QuickPromptChips prompts={PROMPTS} onSelect={vi.fn()} hidden />);
    expect(container.firstChild).toBeNull();
  });
});
