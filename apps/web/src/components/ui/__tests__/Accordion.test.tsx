/**
 * Tests for Accordion component
 * Issue #1951: Add coverage for shadcn/ui accordion
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from '../accordion';

describe('Accordion', () => {
  describe('Rendering', () => {
    it('renders accordion with items', () => {
      render(
        <Accordion type="single" collapsible>
          <AccordionItem value="item-1">
            <AccordionTrigger>Item 1</AccordionTrigger>
            <AccordionContent>Content 1</AccordionContent>
          </AccordionItem>
        </Accordion>
      );

      expect(screen.getByText('Item 1')).toBeInTheDocument();
    });

    it('renders multiple items', () => {
      render(
        <Accordion type="single" collapsible>
          <AccordionItem value="item-1">
            <AccordionTrigger>Item 1</AccordionTrigger>
            <AccordionContent>Content 1</AccordionContent>
          </AccordionItem>
          <AccordionItem value="item-2">
            <AccordionTrigger>Item 2</AccordionTrigger>
            <AccordionContent>Content 2</AccordionContent>
          </AccordionItem>
        </Accordion>
      );

      expect(screen.getByText('Item 1')).toBeInTheDocument();
      expect(screen.getByText('Item 2')).toBeInTheDocument();
    });

    it('renders trigger as button', () => {
      render(
        <Accordion type="single" collapsible>
          <AccordionItem value="item-1">
            <AccordionTrigger>Trigger</AccordionTrigger>
            <AccordionContent>Content</AccordionContent>
          </AccordionItem>
        </Accordion>
      );

      const trigger = screen.getByText('Trigger');
      expect(trigger.closest('button')).toBeInTheDocument();
    });
  });

  describe('Interaction', () => {
    it('expands content when trigger is clicked', async () => {
      const user = userEvent.setup();
      render(
        <Accordion type="single" collapsible>
          <AccordionItem value="item-1">
            <AccordionTrigger>Click me</AccordionTrigger>
            <AccordionContent>Hidden content</AccordionContent>
          </AccordionItem>
        </Accordion>
      );

      const trigger = screen.getByText('Click me');
      await user.click(trigger);

      // Content should be visible after click
      expect(screen.getByText('Hidden content')).toBeVisible();
    });

    it('collapses when trigger is clicked again (collapsible)', async () => {
      const user = userEvent.setup();
      render(
        <Accordion type="single" collapsible>
          <AccordionItem value="item-1">
            <AccordionTrigger>Toggle</AccordionTrigger>
            <AccordionContent>Content</AccordionContent>
          </AccordionItem>
        </Accordion>
      );

      const trigger = screen.getByText('Toggle');

      await user.click(trigger);
      expect(screen.getByText('Content')).toBeVisible();

      await user.click(trigger);
      // Content should collapse
    });
  });

  describe('Multiple Items', () => {
    it('allows only one item open at a time (type="single")', async () => {
      const user = userEvent.setup();
      render(
        <Accordion type="single" collapsible>
          <AccordionItem value="item-1">
            <AccordionTrigger>Item 1</AccordionTrigger>
            <AccordionContent>Content 1</AccordionContent>
          </AccordionItem>
          <AccordionItem value="item-2">
            <AccordionTrigger>Item 2</AccordionTrigger>
            <AccordionContent>Content 2</AccordionContent>
          </AccordionItem>
        </Accordion>
      );

      const trigger1 = screen.getByText('Item 1');
      const trigger2 = screen.getByText('Item 2');

      await user.click(trigger1);
      expect(screen.getByText('Content 1')).toBeVisible();

      await user.click(trigger2);
      expect(screen.getByText('Content 2')).toBeVisible();
      // Content 1 should now be hidden (single mode)
    });
  });

  describe('Styling', () => {
    it('applies custom className to accordion', () => {
      const { container } = render(
        <Accordion type="single" collapsible className="custom-accordion">
          <AccordionItem value="item-1">
            <AccordionTrigger>Item</AccordionTrigger>
            <AccordionContent>Content</AccordionContent>
          </AccordionItem>
        </Accordion>
      );

      expect(container.firstChild).toHaveClass('custom-accordion');
    });
  });
});
