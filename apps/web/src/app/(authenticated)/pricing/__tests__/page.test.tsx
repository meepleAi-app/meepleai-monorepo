/**
 * Pricing Page Test Suite (Game Night Improvvisata - E2-5)
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import PricingPage from '../page';

describe('PricingPage', () => {
  it('renders page title', () => {
    render(<PricingPage />);
    expect(screen.getByText('Scegli il tuo Piano')).toBeInTheDocument();
  });

  it('renders both pricing cards', () => {
    render(<PricingPage />);
    const cards = screen.getAllByTestId('pricing-card');
    expect(cards).toHaveLength(2);
  });

  it('renders Free tier with correct name and price', () => {
    render(<PricingPage />);
    expect(screen.getByText('Free')).toBeInTheDocument();
    expect(screen.getByText('Gratis')).toBeInTheDocument();
  });

  it('renders Premium tier with correct name and price', () => {
    render(<PricingPage />);
    expect(screen.getByText('Premium')).toBeInTheDocument();
    expect(screen.getByText('4,99/mese')).toBeInTheDocument();
  });

  it('renders correct Free tier limits', () => {
    render(<PricingPage />);
    expect(screen.getByText('3 giochi privati')).toBeInTheDocument();
    expect(screen.getByText('2 PDF al mese')).toBeInTheDocument();
    expect(screen.getByText('1 agente AI')).toBeInTheDocument();
  });

  it('renders correct Premium tier limits', () => {
    render(<PricingPage />);
    expect(screen.getByText('50 giochi privati')).toBeInTheDocument();
    expect(screen.getByText('20 PDF al mese')).toBeInTheDocument();
    expect(screen.getByText('10 agenti AI')).toBeInTheDocument();
  });

  it('renders Free CTA button as enabled', () => {
    render(<PricingPage />);
    const buttons = screen.getAllByTestId('pricing-cta');
    const freeButton = buttons[0];
    expect(freeButton).not.toBeDisabled();
    expect(freeButton).toHaveTextContent('Inizia Gratis');
  });

  it('renders Premium CTA as disabled with "Coming Soon"', () => {
    render(<PricingPage />);
    const buttons = screen.getAllByTestId('pricing-cta');
    const premiumButton = buttons[1];
    expect(premiumButton).toBeDisabled();
    expect(premiumButton).toHaveTextContent('Coming Soon');
  });

  it('shows coming soon message', () => {
    render(<PricingPage />);
    expect(screen.getByText(/I pagamenti saranno disponibili a breve/)).toBeInTheDocument();
  });

  it('renders pricing grid', () => {
    render(<PricingPage />);
    expect(screen.getByTestId('pricing-grid')).toBeInTheDocument();
  });
});
