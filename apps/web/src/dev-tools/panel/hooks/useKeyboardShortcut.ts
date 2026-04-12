import { useEffect } from 'react';

export interface KeyboardShortcut {
  ctrl?: boolean;
  shift?: boolean;
  alt?: boolean;
  key: string;
}

export function useKeyboardShortcut(combo: KeyboardShortcut, handler: () => void): void {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key.toLowerCase() !== combo.key.toLowerCase()) return;
      if (combo.ctrl && !event.ctrlKey && !event.metaKey) return;
      if (combo.shift && !event.shiftKey) return;
      if (combo.alt && !event.altKey) return;
      handler();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
    };
  }, [combo.ctrl, combo.shift, combo.alt, combo.key, handler]);
}
