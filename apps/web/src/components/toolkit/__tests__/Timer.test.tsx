import { render, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { Timer } from '../Timer';

// ── AudioContext stub ───────────────────────────────────────────────

const mockOscillatorStart = vi.fn();
const mockOscillatorStop = vi.fn();
const mockOscillatorConnect = vi.fn();
const mockGainConnect = vi.fn();
const mockContextClose = vi.fn();

function makeAudioContextMock() {
  const gainNode = {
    gain: { setValueAtTime: vi.fn(), linearRampToValueAtTime: vi.fn() },
    connect: mockGainConnect,
  };
  const oscillator = {
    type: 'sine' as OscillatorType,
    frequency: { setValueAtTime: vi.fn() },
    connect: mockOscillatorConnect,
    start: mockOscillatorStart,
    stop: mockOscillatorStop,
  };
  return {
    createOscillator: vi.fn().mockReturnValue(oscillator),
    createGain: vi.fn().mockReturnValue(gainNode),
    destination: {},
    currentTime: 0,
    close: mockContextClose,
  };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('AudioContext', vi.fn().mockImplementation(makeAudioContextMock));
  vi.stubGlobal('navigator', { vibrate: vi.fn() });
  mockOscillatorStart.mockClear();
  mockOscillatorStop.mockClear();
  mockOscillatorConnect.mockClear();
  mockGainConnect.mockClear();
  mockContextClose.mockClear();
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('Timer — suono + vibrazione a 10s', () => {
  it('chiama navigator.vibrate quando il countdown raggiunge 10s', () => {
    // 12s timer: advance 2s → remaining = 10s → alert fires
    const { getByRole } = render(<Timer name="Test" defaultSeconds={12} type="countdown" />);
    act(() => {
      getByRole('button', { name: /avvia/i }).click();
    });
    act(() => {
      vi.advanceTimersByTime(2000);
    });
    expect(navigator.vibrate).toHaveBeenCalledWith([200, 100, 200]);
  });

  it('NON vibra se il tipo è countup', () => {
    const { getByRole } = render(<Timer name="Test" defaultSeconds={0} type="countup" />);
    act(() => {
      getByRole('button', { name: /avvia/i }).click();
    });
    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(navigator.vibrate).not.toHaveBeenCalled();
  });

  it('NON vibra una seconda volta se già vibrato', () => {
    const { getByRole } = render(<Timer name="Test" defaultSeconds={12} type="countdown" />);
    act(() => {
      getByRole('button', { name: /avvia/i }).click();
    });
    act(() => {
      vi.advanceTimersByTime(2000); // → 10s, fires
    });
    act(() => {
      vi.advanceTimersByTime(1000); // → 9s, must NOT fire again
    });
    expect(navigator.vibrate).toHaveBeenCalledTimes(1);
  });

  it('ripristina alertFired dopo reset — alert può riscattarsi al prossimo avvio', () => {
    const { getByRole } = render(<Timer name="Test" defaultSeconds={12} type="countdown" />);
    // Prima run
    act(() => {
      getByRole('button', { name: /avvia/i }).click();
    });
    act(() => {
      vi.advanceTimersByTime(2000); // → 10s, alert #1
    });
    // Reset
    act(() => {
      getByRole('button', { name: /reset/i }).click();
    });
    // Seconda run
    act(() => {
      getByRole('button', { name: /avvia/i }).click();
    });
    act(() => {
      vi.advanceTimersByTime(2000); // → 10s, alert #2
    });
    expect(navigator.vibrate).toHaveBeenCalledTimes(2);
  });
});
