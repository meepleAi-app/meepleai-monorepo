import { describe, it, expect, beforeEach } from 'vitest';
import { useContextBarStore } from '@/lib/stores/context-bar-store';

describe('useContextBarStore', () => {
  beforeEach(() => {
    useContextBarStore.setState({ content: null, options: { alwaysVisible: false } });
  });

  it('starts with null content and default options', () => {
    const state = useContextBarStore.getState();
    expect(state.content).toBeNull();
    expect(state.options).toEqual({ alwaysVisible: false });
  });

  it('setContent updates content', () => {
    useContextBarStore.getState().setContent('test-content');
    expect(useContextBarStore.getState().content).toBe('test-content');
  });

  it('setOptions merges with existing options', () => {
    useContextBarStore.getState().setOptions({ alwaysVisible: true });
    expect(useContextBarStore.getState().options.alwaysVisible).toBe(true);
  });

  it('clear resets content and options', () => {
    useContextBarStore.getState().setContent('test');
    useContextBarStore.getState().setOptions({ alwaysVisible: true });
    useContextBarStore.getState().clear();
    expect(useContextBarStore.getState().content).toBeNull();
    expect(useContextBarStore.getState().options).toEqual({ alwaysVisible: false });
  });
});
