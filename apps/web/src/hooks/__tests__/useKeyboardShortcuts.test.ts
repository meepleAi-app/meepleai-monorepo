/**
 * #4986: «?» digitato in un campo di testo apriva l'aiuto delle scorciatoie e il carattere non
 * arrivava al campo (preventDefault), quindi nessuna domanda al Tutor poteva finire con «?».
 */
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

import { useKeyboardShortcuts, type KeyboardShortcut } from '../useKeyboardShortcuts';

function press(target: EventTarget, init: KeyboardEventInit): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function mount(): { help: ReturnType<typeof vi.fn>; escape: ReturnType<typeof vi.fn> } {
  const help = vi.fn();
  const escape = vi.fn();
  const shortcuts: KeyboardShortcut[] = [
    {
      key: '?',
      shift: true,
      description: 'help',
      action: help,
      preventDefault: true,
      category: 'system',
    },
    { key: 'Escape', description: 'close', action: escape, category: 'system' },
  ];
  renderHook(() => useKeyboardShortcuts(shortcuts));
  return { help, escape };
}

function field(tag: 'input' | 'textarea' | 'div'): HTMLElement {
  const el = document.createElement(tag);
  if (tag === 'div') {
    el.contentEditable = 'true';
    // jsdom non calcola isContentEditable; il browser sì.
    Object.defineProperty(el, 'isContentEditable', { value: true });
  }
  document.body.appendChild(el);
  el.focus();
  return el;
}

describe('useKeyboardShortcuts — «?» nei campi di testo (#4986)', () => {
  afterEach(() => {
    document.body.replaceChildren();
  });

  it.each(['input', 'textarea', 'div'] as const)(
    'non apre l’aiuto e non blocca il carattere in un %s',
    tag => {
      const { help } = mount();
      const target = field(tag);

      const event = press(target, { key: '?', shiftKey: true });

      expect(help).not.toHaveBeenCalled();
      expect(event.defaultPrevented).toBe(false);
    }
  );

  it('apre l’aiuto quando il focus non è in un campo', () => {
    const { help } = mount();

    const event = press(document.body, { key: '?', shiftKey: true });

    expect(help).toHaveBeenCalledTimes(1);
    expect(event.defaultPrevented).toBe(true);
  });

  it('lascia funzionare Escape anche dentro un campo', () => {
    const { escape } = mount();
    const target = field('input');

    press(target, { key: 'Escape' });

    expect(escape).toHaveBeenCalledTimes(1);
  });
});
