/**
 * @vitest-environment jsdom
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '../accordion';

// #4916. Radix wraps the trigger in a header it renders as an h3, whatever the page around it. On the legal pages those
// are the top-level sections, right after the h1: axe's heading-order rule reads h1 → h3 as a skipped level. The level
// is chosen by the caller, and stays 3 for the callers that have an h2 above.

function renderAccordion(headingLevel?: 2 | 3 | 4) {
  return render(
    <Accordion type="single" collapsible>
      <AccordionItem value="one">
        <AccordionTrigger {...(headingLevel ? { headingLevel } : {})}>
          Prima sezione
        </AccordionTrigger>
        <AccordionContent>Contenuto della prima sezione</AccordionContent>
      </AccordionItem>
    </Accordion>
  );
}

describe('AccordionTrigger heading level', () => {
  it('is an h3 by default, as before', () => {
    renderAccordion();

    expect(screen.getByRole('heading', { name: 'Prima sezione', level: 3 })).toBeInTheDocument();
  });

  it.each([2, 4] as const)('is an h%s when the caller asks for it', level => {
    renderAccordion(level);

    expect(screen.getByRole('heading', { name: 'Prima sezione', level })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 3 })).not.toBeInTheDocument();
  });

  it('still wraps a button that opens and closes the section, and keeps the state on the heading', () => {
    renderAccordion(2);
    const heading = screen.getByRole('heading', { name: 'Prima sezione', level: 2 });
    const trigger = screen.getByRole('button', { name: 'Prima sezione' });

    expect(heading).toContainElement(trigger);
    expect(heading).toHaveAttribute('data-state', 'closed');
    expect(trigger).toHaveAttribute('aria-expanded', 'false');

    fireEvent.click(trigger);

    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(heading).toHaveAttribute('data-state', 'open');
  });
});
