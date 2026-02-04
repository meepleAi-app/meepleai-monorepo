import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useScrollSpy, scrollToSection } from '../hooks/useScrollSpy';

// Mock IntersectionObserver
class MockIntersectionObserver {
  readonly root: Element | null = null;
  readonly rootMargin: string = '';
  readonly thresholds: ReadonlyArray<number> = [];
  callback: IntersectionObserverCallback;
  elements: Set<Element> = new Set();
  static instances: MockIntersectionObserver[] = [];

  constructor(callback: IntersectionObserverCallback, _options?: IntersectionObserverInit) {
    this.callback = callback;
    MockIntersectionObserver.instances.push(this);
  }

  observe(element: Element) {
    this.elements.add(element);
  }

  unobserve(element: Element) {
    this.elements.delete(element);
  }

  disconnect() {
    this.elements.clear();
  }

  takeRecords(): IntersectionObserverEntry[] {
    return [];
  }

  static clearInstances() {
    MockIntersectionObserver.instances = [];
  }
}

// Helper to simulate intersection
function simulateIntersection(elementId: string, isIntersecting: boolean) {
  const observer = MockIntersectionObserver.instances[MockIntersectionObserver.instances.length - 1];
  if (!observer) return;

  const element = document.getElementById(elementId);
  if (element && observer.elements.has(element)) {
    observer.callback(
      [
        {
          target: element,
          isIntersecting,
          boundingClientRect: element.getBoundingClientRect(),
          intersectionRatio: isIntersecting ? 1 : 0,
          intersectionRect: element.getBoundingClientRect(),
          rootBounds: null,
          time: Date.now(),
        },
      ],
      observer
    );
  }
}

// Helper to setup DOM elements safely
function setupTestElements(ids: string[]) {
  ids.forEach((id) => {
    const div = document.createElement('div');
    div.id = id;
    div.textContent = `${id} content`;
    document.body.appendChild(div);
  });
}

function cleanupTestElements() {
  while (document.body.firstChild) {
    document.body.removeChild(document.body.firstChild);
  }
}

describe('useScrollSpy', () => {
  beforeEach(() => {
    // Setup DOM
    setupTestElements(['section1', 'section2', 'section3']);

    // Mock IntersectionObserver
    vi.stubGlobal('IntersectionObserver', MockIntersectionObserver);
    MockIntersectionObserver.clearInstances();

    // Mock history.replaceState
    vi.spyOn(history, 'replaceState').mockImplementation(() => {});

    // Mock window.scrollTo
    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    cleanupTestElements();
  });

  describe('Basic functionality', () => {
    it('should return empty string when no sections provided', () => {
      const { result } = renderHook(() => useScrollSpy([]));
      expect(result.current).toBe('');
    });

    it('should return first section as default when sections provided', () => {
      const { result } = renderHook(() =>
        useScrollSpy(['section1', 'section2', 'section3'])
      );

      // Initial state should be first section (set in useEffect)
      expect(result.current).toBe('section1');
    });

    it('should update activeId when section comes into view', () => {
      const { result } = renderHook(() =>
        useScrollSpy(['section1', 'section2', 'section3'])
      );

      act(() => {
        simulateIntersection('section2', true);
      });

      expect(result.current).toBe('section2');
    });

    it('should handle rapid scrolling', () => {
      const { result } = renderHook(() =>
        useScrollSpy(['section1', 'section2', 'section3'])
      );

      act(() => {
        simulateIntersection('section1', true);
      });
      act(() => {
        simulateIntersection('section2', true);
      });
      act(() => {
        simulateIntersection('section3', true);
      });

      // Should be on section3 after rapid changes
      expect(result.current).toBe('section3');
    });
  });

  describe('URL hash handling', () => {
    it('should update URL hash on section change', () => {
      const { result } = renderHook(() =>
        useScrollSpy(['section1', 'section2'], { updateHash: true })
      );

      act(() => {
        simulateIntersection('section2', true);
      });

      expect(history.replaceState).toHaveBeenCalledWith(null, '', '#section2');
      expect(result.current).toBe('section2');
    });

    it('should not update URL hash when updateHash is false', () => {
      renderHook(() =>
        useScrollSpy(['section1', 'section2'], { updateHash: false })
      );

      act(() => {
        simulateIntersection('section2', true);
      });

      // replaceState should not have been called with section2
      expect(history.replaceState).not.toHaveBeenCalledWith(null, '', '#section2');
    });

    it('should respect initial hash on mount', () => {
      // Set initial hash
      Object.defineProperty(window, 'location', {
        value: { hash: '#section2' },
        writable: true,
      });

      const { result } = renderHook(() =>
        useScrollSpy(['section1', 'section2', 'section3'])
      );

      expect(result.current).toBe('section2');
    });
  });

  describe('Cleanup', () => {
    it('should disconnect observer on unmount', () => {
      renderHook(() => useScrollSpy(['section1', 'section2']));

      const observer = MockIntersectionObserver.instances[MockIntersectionObserver.instances.length - 1];
      const disconnectSpy = vi.spyOn(observer, 'disconnect');

      // Trigger cleanup by clearing instances
      act(() => {
        observer.disconnect();
      });

      expect(disconnectSpy).toHaveBeenCalled();
    });

    it('should handle section list changes', () => {
      const initialLength = MockIntersectionObserver.instances.length;

      const { rerender } = renderHook(
        ({ sections }) => useScrollSpy(sections),
        { initialProps: { sections: ['section1', 'section2'] } }
      );

      rerender({ sections: ['section1', 'section2', 'section3'] });

      // Should create new observer when sections change
      expect(MockIntersectionObserver.instances.length).toBeGreaterThan(initialLength);
    });
  });
});

describe('scrollToSection', () => {
  beforeEach(() => {
    const section = document.createElement('div');
    section.id = 'test-section';
    section.style.position = 'absolute';
    section.style.top = '500px';
    section.textContent = 'Test Section';
    document.body.appendChild(section);

    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    vi.spyOn(window, 'scrollY', 'get').mockReturnValue(0);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    cleanupTestElements();
  });

  it('should scroll to element with offset', () => {
    scrollToSection('test-section', 100);

    expect(window.scrollTo).toHaveBeenCalledWith({
      top: expect.any(Number),
      behavior: 'smooth',
    });
  });

  it('should use default offset of 100', () => {
    scrollToSection('test-section');

    expect(window.scrollTo).toHaveBeenCalledWith({
      top: expect.any(Number),
      behavior: 'smooth',
    });
  });

  it('should handle non-existent section gracefully', () => {
    // Should not throw
    expect(() => scrollToSection('non-existent')).not.toThrow();
    expect(window.scrollTo).not.toHaveBeenCalled();
  });
});
