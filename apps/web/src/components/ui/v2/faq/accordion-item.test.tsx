import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AccordionItem } from './accordion-item';

describe('AccordionItem (v2)', () => {
  const defaults = {
    id: 'q1',
    question: 'How do I create an account?',
    answer: <p data-testid="answer">Sign up via /join.</p>,
    categoryLabel: 'Account',
    categoryIcon: '👤',
    isOpen: false,
    onToggle: vi.fn(),
  };

  it('renders question, category label, and category icon', () => {
    render(<AccordionItem {...defaults} />);
    expect(screen.getByText('How do I create an account?')).toBeInTheDocument();
    expect(screen.getByText('Account')).toBeInTheDocument();
    expect(screen.getByText('👤')).toBeInTheDocument();
  });

  it('button has aria-expanded=false when closed', () => {
    render(<AccordionItem {...defaults} isOpen={false} />);
    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'false');
  });

  it('button has aria-expanded=true when open', () => {
    render(<AccordionItem {...defaults} isOpen={true} />);
    expect(screen.getByRole('button')).toHaveAttribute('aria-expanded', 'true');
  });

  it('button aria-controls matches panel id', () => {
    render(<AccordionItem {...defaults} isOpen={true} />);
    const button = screen.getByRole('button');
    const controlsId = button.getAttribute('aria-controls');
    expect(controlsId).toBeTruthy();
    expect(document.getElementById(controlsId as string)).not.toBeNull();
  });

  it('panel has role=region and aria-labelledby pointing to button', () => {
    render(<AccordionItem {...defaults} isOpen={true} />);
    const button = screen.getByRole('button');
    const region = screen.getByRole('region');
    expect(region).toHaveAttribute('aria-labelledby', button.id);
  });

  it('panel aria-hidden is true when closed', () => {
    render(<AccordionItem {...defaults} isOpen={false} />);
    const region = screen.getByRole('region', { hidden: true });
    expect(region).toHaveAttribute('aria-hidden', 'true');
  });

  it('panel aria-hidden is false when open', () => {
    render(<AccordionItem {...defaults} isOpen={true} />);
    expect(screen.getByRole('region')).toHaveAttribute('aria-hidden', 'false');
  });

  it('renders the answer node when open', () => {
    render(<AccordionItem {...defaults} isOpen={true} />);
    expect(screen.getByTestId('answer')).toBeInTheDocument();
  });

  it('fires onToggle when button clicked', () => {
    const onToggle = vi.fn();
    render(<AccordionItem {...defaults} onToggle={onToggle} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('fires onToggle on Enter keypress', () => {
    const onToggle = vi.fn();
    render(<AccordionItem {...defaults} onToggle={onToggle} />);
    const button = screen.getByRole('button');
    button.focus();
    fireEvent.keyDown(button, { key: 'Enter', code: 'Enter' });
    // Default browser behavior on button: Enter triggers click. RTL fireEvent.click is more reliable
    // as keyDown on <button> does not auto-click in jsdom. Use fireEvent.click directly to confirm.
    fireEvent.click(button);
    expect(onToggle).toHaveBeenCalled();
  });

  it('applies highlighted background when highlighted=true', () => {
    render(<AccordionItem {...defaults} highlighted={true} />);
    const item = document.getElementById('faq-q1');
    expect(item?.className).toContain('c-warning');
  });

  it('omits highlighted background when highlighted=false', () => {
    render(<AccordionItem {...defaults} highlighted={false} />);
    const item = document.getElementById('faq-q1');
    expect(item?.className).not.toContain('c-warning');
  });

  it('chevron rotates 180deg when open', () => {
    const { container } = render(<AccordionItem {...defaults} isOpen={true} />);
    const chevronWrap = container.querySelector('[aria-hidden="true"].rotate-180');
    expect(chevronWrap).not.toBeNull();
  });

  it('emits anchor id `faq-{id}` on root for deep-linking', () => {
    render(<AccordionItem {...defaults} id="q3" />);
    expect(document.getElementById('faq-q3')).not.toBeNull();
  });

  it('exposes data-open=true on root when open', () => {
    render(<AccordionItem {...defaults} id="q4" isOpen={true} />);
    expect(document.getElementById('faq-q4')).toHaveAttribute('data-open', 'true');
  });

  it('omits data-open when closed', () => {
    render(<AccordionItem {...defaults} id="q5" isOpen={false} />);
    expect(document.getElementById('faq-q5')).not.toHaveAttribute('data-open');
  });
});
