import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SuggestedPrompts } from '../SuggestedPrompts';

describe('SuggestedPrompts', () => {
  it('renders nothing when prompts empty', () => {
    const { container } = render(<SuggestedPrompts prompts={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders prompts with category tags', () => {
    render(
      <SuggestedPrompts
        groupLabel="Continua"
        prompts={[
          { category: 'A', text: 'E se sono 2 uccelli stessa carta?', onClick: vi.fn() },
          { category: 'F', text: 'Edge case carta predatore', onClick: vi.fn() },
        ]}
      />
    );
    expect(screen.getByText(/Continua/)).toBeInTheDocument();
    expect(screen.getByText(/E se sono 2 uccelli/)).toBeInTheDocument();
    expect(screen.getAllByText(/^[ABCEF]$/)).toHaveLength(2);
  });

  it('calls onClick with right callback when prompt clicked', () => {
    const onClickA = vi.fn();
    const onClickF = vi.fn();
    render(
      <SuggestedPrompts
        prompts={[
          { category: 'A', text: 'P-A', onClick: onClickA },
          { category: 'F', text: 'P-F', onClick: onClickF },
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /P-A/ }));
    expect(onClickA).toHaveBeenCalledOnce();
    expect(onClickF).not.toHaveBeenCalled();
  });
});
