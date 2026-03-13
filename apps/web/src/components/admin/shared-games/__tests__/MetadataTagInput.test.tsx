import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { MetadataTagInput } from '../MetadataTagInput';

describe('MetadataTagInput', () => {
  it('renders existing tags as chips', () => {
    render(
      <MetadataTagInput label="Categories" tags={['Economic', 'Negotiation']} onChange={vi.fn()} />
    );
    expect(screen.getByText('Economic')).toBeInTheDocument();
    expect(screen.getByText('Negotiation')).toBeInTheDocument();
  });

  it('adds tag on Enter without normalizing case', async () => {
    const onChange = vi.fn();
    render(<MetadataTagInput label="Designers" tags={[]} onChange={onChange} />);
    const input = screen.getByPlaceholderText(/add/i);
    await userEvent.type(input, 'Klaus Teuber{Enter}');
    expect(onChange).toHaveBeenCalledWith(['Klaus Teuber']);
  });

  it('removes tag on chip X click', async () => {
    const onChange = vi.fn();
    render(
      <MetadataTagInput label="Categories" tags={['Economic', 'Strategy']} onChange={onChange} />
    );
    const removeButtons = screen.getAllByRole('button', { name: /remove/i });
    await userEvent.click(removeButtons[0]);
    expect(onChange).toHaveBeenCalledWith(['Strategy']);
  });

  it('prevents duplicate tags (case-insensitive)', async () => {
    const onChange = vi.fn();
    render(<MetadataTagInput label="Categories" tags={['Economic']} onChange={onChange} />);
    const input = screen.getByPlaceholderText(/add/i);
    await userEvent.type(input, 'economic{Enter}');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('respects maxTags limit', () => {
    const tags = Array.from({ length: 50 }, (_, i) => `Tag${i}`);
    render(<MetadataTagInput label="Categories" tags={tags} onChange={vi.fn()} maxTags={50} />);
    expect(screen.getByPlaceholderText(/add/i)).toBeDisabled();
  });
});
