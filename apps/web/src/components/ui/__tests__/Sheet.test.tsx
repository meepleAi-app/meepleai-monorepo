/**
 * Tests for Sheet component
 * Issue #1951: Add coverage for shadcn/ui sheet (dialog/drawer)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  Sheet,
  SheetTrigger,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetFooter,
  SheetClose,
} from '../sheet';

describe('Sheet', () => {
  describe('Rendering', () => {
    it('renders sheet trigger', () => {
      render(
        <Sheet>
          <SheetTrigger>Open</SheetTrigger>
          <SheetContent>Content</SheetContent>
        </Sheet>
      );

      expect(screen.getByText('Open')).toBeInTheDocument();
    });

    it('does not render content initially (closed)', () => {
      render(
        <Sheet>
          <SheetTrigger>Open</SheetTrigger>
          <SheetContent>
            <div data-testid="sheet-content">Hidden Content</div>
          </SheetContent>
        </Sheet>
      );

      expect(screen.queryByTestId('sheet-content')).not.toBeInTheDocument();
    });

    it('renders content when open prop is true', () => {
      render(
        <Sheet open={true}>
          <SheetContent>
            <div data-testid="sheet-content">Visible Content</div>
          </SheetContent>
        </Sheet>
      );

      expect(screen.getByTestId('sheet-content')).toBeInTheDocument();
    });
  });

  describe('Sheet Header', () => {
    it('renders sheet header with title', () => {
      render(
        <Sheet open={true}>
          <SheetContent>
            <SheetHeader>
              <SheetTitle>Test Title</SheetTitle>
            </SheetHeader>
          </SheetContent>
        </Sheet>
      );

      expect(screen.getByText('Test Title')).toBeInTheDocument();
    });

    it('renders sheet description', () => {
      render(
        <Sheet open={true}>
          <SheetContent>
            <SheetHeader>
              <SheetTitle>Title</SheetTitle>
              <SheetDescription>Test Description</SheetDescription>
            </SheetHeader>
          </SheetContent>
        </Sheet>
      );

      expect(screen.getByText('Test Description')).toBeInTheDocument();
    });
  });

  describe('Sheet Footer', () => {
    it('renders footer content', () => {
      render(
        <Sheet open={true}>
          <SheetContent>
            <SheetFooter>
              <div data-testid="footer">Footer Content</div>
            </SheetFooter>
          </SheetContent>
        </Sheet>
      );

      expect(screen.getByTestId('footer')).toBeInTheDocument();
    });
  });

  describe('Close Button', () => {
    it('renders close button in content', () => {
      render(
        <Sheet open={true}>
          <SheetContent>
            <SheetClose asChild>
              <button data-testid="custom-close">Close Dialog</button>
            </SheetClose>
          </SheetContent>
        </Sheet>
      );

      expect(screen.getByTestId('custom-close')).toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('renders SheetContent with styling', () => {
      const { container } = render(
        <Sheet open={true}>
          <SheetContent>
            <div data-testid="content">Styled Content</div>
          </SheetContent>
        </Sheet>
      );

      expect(screen.getByTestId('content')).toBeInTheDocument();
    });
  });
});
