import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { FAQSearchBar } from './faq-search-bar';

describe('FAQSearchBar', () => {
  it('renders input with provided value', () => {
    render(<FAQSearchBar value="hello" onChange={() => {}} />);
    const input = screen.getByRole('searchbox');
    expect(input).toHaveValue('hello');
  });

  it('uses provided placeholder', () => {
    render(<FAQSearchBar value="" onChange={() => {}} placeholder="Cerca..." />);
    expect(screen.getByPlaceholderText('Cerca...')).toBeInTheDocument();
  });

  it('uses provided ariaLabel', () => {
    render(<FAQSearchBar value="" onChange={() => {}} ariaLabel="Search FAQs IT" />);
    expect(screen.getByLabelText('Search FAQs IT')).toBeInTheDocument();
  });

  it('calls onChange when user types', async () => {
    const onChange = vi.fn();
    render(<FAQSearchBar value="" onChange={onChange} />);
    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'p');
    expect(onChange).toHaveBeenCalledWith('p');
  });

  it('does not show clear button when value is empty', () => {
    render(<FAQSearchBar value="" onChange={() => {}} />);
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument();
  });

  it('shows clear button when value has content', () => {
    render(<FAQSearchBar value="x" onChange={() => {}} />);
    expect(screen.getByRole('button', { name: /clear/i })).toBeInTheDocument();
  });

  it('uses custom clearLabel for the clear button aria-label', () => {
    render(<FAQSearchBar value="x" onChange={() => {}} clearLabel="Cancella" />);
    expect(screen.getByRole('button', { name: 'Cancella' })).toBeInTheDocument();
  });

  it('clear button calls onChange with empty string', () => {
    const onChange = vi.fn();
    render(<FAQSearchBar value="hello" onChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /clear/i }));
    expect(onChange).toHaveBeenCalledWith('');
  });

  it('hides clear button when disabled', () => {
    render(<FAQSearchBar value="x" onChange={() => {}} disabled />);
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument();
  });

  it('disables the input when disabled', () => {
    render(<FAQSearchBar value="x" onChange={() => {}} disabled />);
    expect(screen.getByRole('searchbox')).toBeDisabled();
  });

  it('input has data-dynamic attribute for hybrid masking', () => {
    render(<FAQSearchBar value="hi" onChange={() => {}} />);
    expect(screen.getByRole('searchbox')).toHaveAttribute('data-dynamic', 'search-input');
  });

  it('applies custom className to outer wrapper', () => {
    const { container } = render(
      <FAQSearchBar value="" onChange={() => {}} className="my-extra" />
    );
    expect(container.querySelector('[data-slot="faq-search-bar"]')?.className).toContain(
      'my-extra'
    );
  });
});
