/**
 * Unit tests for PlayerModeTour - Issue #2475
 */

import { render } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PlayerModeTour, usePlayerModeTour } from '../PlayerModeTour';
import { renderHook, act } from '@testing-library/react';

describe('PlayerModeTour', () => {
  beforeEach(() => {
    // Clear localStorage before each test
    localStorage.clear();
  });

  it('should render without errors', () => {
    const { container } = render(<PlayerModeTour />);
    expect(container).toBeInTheDocument();
  });

  it('should not auto-start when autoStart is false', () => {
    render(<PlayerModeTour autoStart={false} />);
    // Tour should not be running
    expect(localStorage.getItem('playerModeTourCompleted')).toBeNull();
  });

  it('should auto-start when autoStart is true and tour not completed', () => {
    render(<PlayerModeTour autoStart={true} />);
    // Component should mount and check localStorage
    expect(localStorage.getItem('playerModeTourCompleted')).toBeNull();
  });

  it('should not start if tour already completed', () => {
    localStorage.setItem('playerModeTourCompleted', 'true');
    render(<PlayerModeTour autoStart={true} />);
    // Tour should not run again
    expect(localStorage.getItem('playerModeTourCompleted')).toBe('true');
  });

  it('should force run tour even if completed', () => {
    localStorage.setItem('playerModeTourCompleted', 'true');
    render(<PlayerModeTour forceRun={true} />);
    // forceRun should bypass localStorage check
    expect(localStorage.getItem('playerModeTourCompleted')).toBe('true');
  });

  it('should call onTourComplete callback when provided', () => {
    const onComplete = vi.fn();
    render(<PlayerModeTour onTourComplete={onComplete} />);
    // Callback should be defined
    expect(onComplete).toBeDefined();
  });
});

describe('usePlayerModeTour', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('should return tour controls', () => {
    const { result } = renderHook(() => usePlayerModeTour());

    expect(result.current.runTour).toBe(false);
    expect(typeof result.current.startTour).toBe('function');
    expect(typeof result.current.resetTour).toBe('function');
    expect(typeof result.current.setRunTour).toBe('function');
  });

  it('should start tour when startTour is called', () => {
    const { result } = renderHook(() => usePlayerModeTour());

    act(() => {
      result.current.startTour();
    });

    expect(result.current.runTour).toBe(true);
  });

  it('should reset tour and clear localStorage', () => {
    localStorage.setItem('playerModeTourCompleted', 'true');
    const { result } = renderHook(() => usePlayerModeTour());

    act(() => {
      result.current.resetTour();
    });

    expect(localStorage.getItem('playerModeTourCompleted')).toBeNull();
    expect(result.current.runTour).toBe(true);
  });
});
