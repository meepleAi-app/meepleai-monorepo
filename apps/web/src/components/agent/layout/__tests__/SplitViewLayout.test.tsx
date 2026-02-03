/**
 * SplitViewLayout Component Tests
 * Issue #3254: [FRONT-016] Split View Layout for Chat + PDF
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import React from 'react';

import { SplitViewLayout } from '../SplitViewLayout';

describe('SplitViewLayout', () => {
  const mockChatPanel = <div data-testid="chat-panel">Chat Content</div>;
  const mockPdfPanel = <div data-testid="pdf-panel">PDF Content</div>;

  beforeEach(() => {
    // Reset any DOM changes
  });

  describe('Desktop View (Split)', () => {
    it('renders both panels in desktop view', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      // Panels appear in both desktop and mobile views
      expect(screen.getAllByTestId('chat-panel').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByTestId('pdf-panel').length).toBeGreaterThanOrEqual(1);
    });

    it('renders desktop split container with grid layout', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      const desktopContainer = document.querySelector('.lg\\:grid');
      expect(desktopContainer).toBeInTheDocument();
      expect(desktopContainer?.className).toContain('lg:grid-cols-2');
    });

    it('applies border to chat panel', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      const chatContainer = document.querySelector('.border-r');
      expect(chatContainer).toBeInTheDocument();
    });
  });

  describe('Mobile View (Tabs)', () => {
    it('renders mobile tab container', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      const mobileContainer = document.querySelector('.lg\\:hidden');
      expect(mobileContainer).toBeInTheDocument();
    });

    it('renders Chat and PDF tabs', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('PDF')).toBeInTheDocument();
    });

    it('shows chat panel by default', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      // Chat tab should be active by default
      const chatTab = screen.getByText('Chat');
      expect(chatTab.className).toContain('text-cyan-400');
    });

    it('switches to PDF panel when PDF tab is clicked', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      fireEvent.click(screen.getByText('PDF'));

      // PDF tab should now be active
      const pdfTab = screen.getByText('PDF');
      expect(pdfTab.className).toContain('text-cyan-400');
    });

    it('switches back to chat panel when Chat tab is clicked', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      // Switch to PDF first
      fireEvent.click(screen.getByText('PDF'));

      // Switch back to Chat
      fireEvent.click(screen.getByText('Chat'));

      // Chat tab should be active again
      const chatTab = screen.getByText('Chat');
      expect(chatTab.className).toContain('text-cyan-400');
    });

    it('applies inactive styling to non-selected tab', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      // Chat is selected by default, so PDF should be inactive
      const pdfTab = screen.getByText('PDF');
      expect(pdfTab.className).toContain('text-slate-400');
    });
  });

  describe('Panel Content', () => {
    it('renders custom chat panel content', () => {
      const customChat = <div data-testid="custom-chat">Custom Chat</div>;
      render(<SplitViewLayout chatPanel={customChat} pdfPanel={mockPdfPanel} />);

      // Chat panel appears in both desktop and mobile views
      const chatPanels = screen.getAllByTestId('custom-chat');
      expect(chatPanels.length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('Custom Chat').length).toBeGreaterThanOrEqual(1);
    });

    it('renders custom PDF panel content', () => {
      const customPdf = <div data-testid="custom-pdf">Custom PDF</div>;
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={customPdf} />);

      // PDF panel appears in desktop view always
      const pdfPanels = screen.getAllByTestId('custom-pdf');
      expect(pdfPanels.length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('Custom PDF').length).toBeGreaterThanOrEqual(1);
    });
  });

  describe('Accessibility', () => {
    it('tabs are focusable buttons', () => {
      render(<SplitViewLayout chatPanel={mockChatPanel} pdfPanel={mockPdfPanel} />);

      const buttons = screen.getAllByRole('button');
      expect(buttons.length).toBeGreaterThanOrEqual(2);
    });
  });
});
