/**
 * ToolSheetContent — S5 functional tool UIs
 */
import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ToolSheetContent } from '../ToolSheetContent';

describe('ToolSheetContent', () => {
  it('renderizza null per activeTool null', () => {
    const { container } = render(<ToolSheetContent activeTool={null} />);
    expect(container.firstChild).toBeNull();
  });

  // ── Dadi ──────────────────────────────────────────────────────────────
  describe('Dadi', () => {
    beforeEach(() => {
      vi.spyOn(Math, 'random').mockReturnValue(0.5); // → value = 4
    });
    afterEach(() => vi.restoreAllMocks());

    it('mostra tasto Lancia inizialmente', () => {
      render(<ToolSheetContent activeTool="dadi" />);
      expect(screen.getByRole('button', { name: /lancia/i })).toBeInTheDocument();
    });

    it('mostra il valore dopo il roll', async () => {
      vi.useFakeTimers();
      render(<ToolSheetContent activeTool="dadi" />);
      fireEvent.click(screen.getByRole('button', { name: /lancia/i }));
      await act(async () => {
        vi.advanceTimersByTime(500);
      });
      expect(screen.getByText('4')).toBeInTheDocument();
      vi.useRealTimers();
    });
  });

  // ── Moneta ────────────────────────────────────────────────────────────
  describe('Moneta', () => {
    it('mostra tasto Lancia inizialmente', () => {
      render(<ToolSheetContent activeTool="moneta" />);
      expect(screen.getByRole('button', { name: /lancia/i })).toBeInTheDocument();
    });

    it('mostra testa o croce dopo il lancio', async () => {
      vi.useFakeTimers();
      vi.spyOn(Math, 'random').mockReturnValue(0.1); // < 0.5 → testa
      render(<ToolSheetContent activeTool="moneta" />);
      fireEvent.click(screen.getByRole('button', { name: /lancia/i }));
      await act(async () => {
        vi.advanceTimersByTime(500);
      });
      expect(screen.getByText(/testa|croce/i)).toBeInTheDocument();
      vi.useRealTimers();
      vi.restoreAllMocks();
    });
  });

  // ── Contatore ─────────────────────────────────────────────────────────
  describe('Contatore', () => {
    it('mostra valore 0 inizialmente', () => {
      render(<ToolSheetContent activeTool="contatore" />);
      expect(screen.getByText('0')).toBeInTheDocument();
    });

    it('incrementa con +', () => {
      render(<ToolSheetContent activeTool="contatore" />);
      fireEvent.click(screen.getByRole('button', { name: /incrementa/i }));
      expect(screen.getByText('1')).toBeInTheDocument();
    });

    it('decrementa con - ma non scende sotto 0', () => {
      render(<ToolSheetContent activeTool="contatore" />);
      fireEvent.click(screen.getByRole('button', { name: /decrementa/i }));
      expect(screen.getByText('0')).toBeInTheDocument();
    });

    it('azzera il contatore', () => {
      render(<ToolSheetContent activeTool="contatore" />);
      fireEvent.click(screen.getByRole('button', { name: /incrementa/i }));
      fireEvent.click(screen.getByRole('button', { name: /incrementa/i }));
      fireEvent.click(screen.getByRole('button', { name: /azzera/i }));
      expect(screen.getByText('0')).toBeInTheDocument();
    });
  });

  // ── Timer ─────────────────────────────────────────────────────────────
  describe('Timer', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });
    afterEach(() => {
      vi.useRealTimers();
    });

    it('mostra 00:00 inizialmente', () => {
      render(<ToolSheetContent activeTool="timer" />);
      expect(screen.getByText('00:00')).toBeInTheDocument();
    });

    it('avvia il timer al click su Avvia', async () => {
      render(<ToolSheetContent activeTool="timer" />);
      fireEvent.click(screen.getByRole('button', { name: /avvia/i }));
      await act(async () => {
        vi.advanceTimersByTime(3000);
      });
      expect(screen.getByText('00:03')).toBeInTheDocument();
    });

    it('mette in pausa il timer', async () => {
      render(<ToolSheetContent activeTool="timer" />);
      fireEvent.click(screen.getByRole('button', { name: /avvia/i }));
      await act(async () => {
        vi.advanceTimersByTime(2000);
      });
      fireEvent.click(screen.getByRole('button', { name: /pausa/i }));
      await act(async () => {
        vi.advanceTimersByTime(2000);
      });
      expect(screen.getByText('00:02')).toBeInTheDocument();
    });

    it('resetta il timer', async () => {
      render(<ToolSheetContent activeTool="timer" />);
      fireEvent.click(screen.getByRole('button', { name: /avvia/i }));
      await act(async () => {
        vi.advanceTimersByTime(5000);
      });
      fireEvent.click(screen.getByRole('button', { name: /reset/i }));
      expect(screen.getByText('00:00')).toBeInTheDocument();
    });
  });
});
