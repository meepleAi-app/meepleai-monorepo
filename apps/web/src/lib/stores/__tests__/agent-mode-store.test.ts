/**
 * Agent Mode Store Tests - Issue #2413
 *
 * Test coverage:
 * - Initial state
 * - Mode selection
 * - Reset functionality
 * - Utility getters
 * - Persistence (localStorage)
 * - Previous mode tracking
 *
 * Target: >90% coverage
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { useAgentModeStore } from '../agent-mode-store';

import type { AgentMode } from '@/components/agent';

// Mock localStorage
const mockLocalStorage: Record<string, string> = {};
beforeEach(() => {
  // Clear all mocks and storage
  Object.keys(mockLocalStorage).forEach(key => delete mockLocalStorage[key]);
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(key => mockLocalStorage[key] ?? null);
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation((key, value) => {
    mockLocalStorage[key] = value;
  });
  vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(key => {
    delete mockLocalStorage[key];
  });

  // Reset Zustand store state
  useAgentModeStore.setState({
    mode: 'RulesClarifier',
    previousMode: null,
    lastChanged: null,
  });
});

describe('useAgentModeStore', () => {
  it('initializes with RulesClarifier as default mode', () => {
    const { result } = renderHook(() => useAgentModeStore());

    expect(result.current.mode).toBe('RulesClarifier');
    expect(result.current.previousMode).toBeNull();
    expect(result.current.lastChanged).toBeNull();
  });

  it('sets mode correctly', () => {
    const { result } = renderHook(() => useAgentModeStore());

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });

    expect(result.current.mode).toBe('StrategyAdvisor');
    expect(result.current.previousMode).toBe('RulesClarifier');
    expect(result.current.lastChanged).toBeTruthy();
  });

  it('tracks previous mode when changing modes', () => {
    const { result } = renderHook(() => useAgentModeStore());

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });

    expect(result.current.previousMode).toBe('RulesClarifier');

    act(() => {
      result.current.setMode('SetupAssistant');
    });

    expect(result.current.previousMode).toBe('StrategyAdvisor');
  });

  it('updates lastChanged timestamp on mode change', () => {
    const { result } = renderHook(() => useAgentModeStore());

    const beforeChange = new Date();

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });

    const lastChanged = result.current.lastChanged;
    expect(lastChanged).toBeTruthy();

    const changedDate = new Date(lastChanged!);
    expect(changedDate.getTime()).toBeGreaterThanOrEqual(beforeChange.getTime());
  });

  it('resets mode to default', () => {
    const { result } = renderHook(() => useAgentModeStore());

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });

    expect(result.current.mode).toBe('StrategyAdvisor');

    act(() => {
      result.current.resetMode();
    });

    expect(result.current.mode).toBe('RulesClarifier');
    expect(result.current.previousMode).toBe('StrategyAdvisor');
  });

  it('isRulesClarifier returns correct boolean', () => {
    const { result } = renderHook(() => useAgentModeStore());

    expect(result.current.isRulesClarifier()).toBe(true);

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });

    expect(result.current.isRulesClarifier()).toBe(false);
  });

  it('isStrategyAdvisor returns correct boolean', () => {
    const { result } = renderHook(() => useAgentModeStore());

    expect(result.current.isStrategyAdvisor()).toBe(false);

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });

    expect(result.current.isStrategyAdvisor()).toBe(true);
  });

  it('isSetupAssistant returns correct boolean', () => {
    const { result } = renderHook(() => useAgentModeStore());

    expect(result.current.isSetupAssistant()).toBe(false);

    act(() => {
      result.current.setMode('SetupAssistant');
    });

    expect(result.current.isSetupAssistant()).toBe(true);
  });

  it('handles all mode types correctly', () => {
    const { result } = renderHook(() => useAgentModeStore());

    const modes: AgentMode[] = ['RulesClarifier', 'StrategyAdvisor', 'SetupAssistant'];

    modes.forEach(mode => {
      act(() => {
        result.current.setMode(mode);
      });

      expect(result.current.mode).toBe(mode);
    });
  });

  it('persists mode to localStorage', () => {
    const { result } = renderHook(() => useAgentModeStore());

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });

    // Check localStorage was called
    expect(Storage.prototype.setItem).toHaveBeenCalled();
  });

  it('maintains state consistency across multiple changes', () => {
    const { result } = renderHook(() => useAgentModeStore());

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });
    expect(result.current.mode).toBe('StrategyAdvisor');
    expect(result.current.previousMode).toBe('RulesClarifier');

    act(() => {
      result.current.setMode('SetupAssistant');
    });
    expect(result.current.mode).toBe('SetupAssistant');
    expect(result.current.previousMode).toBe('StrategyAdvisor');

    act(() => {
      result.current.resetMode();
    });
    expect(result.current.mode).toBe('RulesClarifier');
    expect(result.current.previousMode).toBe('SetupAssistant');
  });

  it('updates lastChanged on reset', async () => {
    const { result } = renderHook(() => useAgentModeStore());

    act(() => {
      result.current.setMode('StrategyAdvisor');
    });

    const firstChange = result.current.lastChanged;

    // Small delay to ensure different timestamp
    await new Promise(resolve => setTimeout(resolve, 10));

    act(() => {
      result.current.resetMode();
    });

    const secondChange = result.current.lastChanged;

    expect(secondChange).not.toBe(firstChange);
    expect(secondChange).toBeTruthy();
  });
});
