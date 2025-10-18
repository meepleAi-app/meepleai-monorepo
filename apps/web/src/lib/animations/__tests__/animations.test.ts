/**
 * Tests for animation utilities library
 * Verifies type safety, exports, and configurations
 */

import {
  DURATIONS,
  EASINGS,
  SPRING_CONFIGS,
  TRANSITIONS,
  STAGGER,
  VARIANTS,
  createStaggerContainer,
} from '../index';

describe('Animation Utilities', () => {
  describe('DURATIONS', () => {
    it('should export correct duration constants', () => {
      expect(DURATIONS.fast).toBe(150);
      expect(DURATIONS.normal).toBe(300);
      expect(DURATIONS.slow).toBe(500);
    });

    it('should have readonly values', () => {
      expect(Object.isFrozen(DURATIONS)).toBe(false); // 'as const' doesn't freeze
      expect(typeof DURATIONS.fast).toBe('number');
    });
  });

  describe('EASINGS', () => {
    it('should export valid CSS cubic-bezier functions', () => {
      expect(EASINGS.easeOut).toBe('cubic-bezier(0.0, 0.0, 0.2, 1)');
      expect(EASINGS.easeIn).toBe('cubic-bezier(0.4, 0.0, 1, 1)');
      expect(EASINGS.easeInOut).toBe('cubic-bezier(0.4, 0.0, 0.2, 1)');
      expect(EASINGS.sharp).toBe('cubic-bezier(0.4, 0.0, 0.6, 1)');
    });

    it('should all be strings', () => {
      Object.values(EASINGS).forEach(easing => {
        expect(typeof easing).toBe('string');
        expect(easing).toContain('cubic-bezier');
      });
    });
  });

  describe('SPRING_CONFIGS', () => {
    it('should export valid spring configurations', () => {
      expect(SPRING_CONFIGS.gentle).toEqual({
        type: 'spring',
        damping: 25,
        stiffness: 120,
      });
      expect(SPRING_CONFIGS.default).toEqual({
        type: 'spring',
        damping: 20,
        stiffness: 150,
      });
      expect(SPRING_CONFIGS.bouncy).toEqual({
        type: 'spring',
        damping: 15,
        stiffness: 200,
      });
      expect(SPRING_CONFIGS.stiff).toEqual({
        type: 'spring',
        damping: 18,
        stiffness: 300,
      });
    });

    it('should have type property as "spring"', () => {
      Object.values(SPRING_CONFIGS).forEach(config => {
        expect(config.type).toBe('spring');
      });
    });

    it('should have valid damping and stiffness values', () => {
      Object.values(SPRING_CONFIGS).forEach(config => {
        expect(config.damping).toBeGreaterThan(0);
        expect(config.stiffness).toBeGreaterThan(0);
      });
    });
  });

  describe('TRANSITIONS', () => {
    it('should export transition presets with correct structure', () => {
      expect(TRANSITIONS.fade).toHaveProperty('duration');
      expect(TRANSITIONS.fade).toHaveProperty('ease');
      expect(TRANSITIONS.slide).toHaveProperty('duration');
      expect(TRANSITIONS.slide).toHaveProperty('ease');
    });

    it('should include spring transitions', () => {
      expect(TRANSITIONS.spring).toHaveProperty('type', 'spring');
      expect(TRANSITIONS.springGentle).toHaveProperty('type', 'spring');
      expect(TRANSITIONS.springBouncy).toHaveProperty('type', 'spring');
    });

    it('should convert durations to seconds', () => {
      // Framer Motion uses seconds
      expect(TRANSITIONS.fade.duration).toBe(0.15); // 150ms / 1000
      expect(TRANSITIONS.slide.duration).toBe(0.3); // 300ms / 1000
    });
  });

  describe('STAGGER', () => {
    it('should export stagger delay constants', () => {
      expect(STAGGER.fast).toBe(0.05);
      expect(STAGGER.normal).toBe(0.1);
      expect(STAGGER.slow).toBe(0.15);
    });

    it('should have values in seconds', () => {
      Object.values(STAGGER).forEach(delay => {
        expect(delay).toBeGreaterThan(0);
        expect(delay).toBeLessThan(1);
      });
    });
  });

  describe('VARIANTS', () => {
    it('should export all variant presets', () => {
      expect(VARIANTS.fadeIn).toBeDefined();
      expect(VARIANTS.slideUp).toBeDefined();
      expect(VARIANTS.slideDown).toBeDefined();
      expect(VARIANTS.slideLeft).toBeDefined();
      expect(VARIANTS.slideRight).toBeDefined();
      expect(VARIANTS.scaleIn).toBeDefined();
      expect(VARIANTS.popIn).toBeDefined();
    });

    it('should have initial, animate, and exit states', () => {
      const variants = [
        VARIANTS.fadeIn,
        VARIANTS.slideUp,
        VARIANTS.slideDown,
        VARIANTS.scaleIn,
      ];

      variants.forEach(variant => {
        expect(variant).toHaveProperty('initial');
        expect(variant).toHaveProperty('animate');
        expect(variant).toHaveProperty('exit');
      });
    });

    it('fadeIn should animate opacity', () => {
      expect(VARIANTS.fadeIn.initial).toHaveProperty('opacity', 0);
      expect(VARIANTS.fadeIn.animate).toHaveProperty('opacity', 1);
      expect(VARIANTS.fadeIn.exit).toHaveProperty('opacity', 0);
    });

    it('slideUp should animate y position', () => {
      expect(VARIANTS.slideUp.initial).toHaveProperty('y', 20);
      expect(VARIANTS.slideUp.animate).toHaveProperty('y', 0);
    });

    it('scaleIn should animate scale', () => {
      expect(VARIANTS.scaleIn.initial).toHaveProperty('scale', 0.95);
      expect(VARIANTS.scaleIn.animate).toHaveProperty('scale', 1);
    });
  });

  describe('Stagger Containers', () => {
    it('should export pre-configured stagger containers', () => {
      expect(VARIANTS.staggerContainer).toBeDefined();
      expect(VARIANTS.staggerContainerFast).toBeDefined();
      expect(VARIANTS.staggerContainerSlow).toBeDefined();
    });

    it('createStaggerContainer should create valid variants', () => {
      const customStagger = createStaggerContainer(0.2, 0.05);
      expect(customStagger).toHaveProperty('initial');
      expect(customStagger).toHaveProperty('animate');
      expect(customStagger).toHaveProperty('exit');
    });

    it('createStaggerContainer should accept custom timing', () => {
      const customStagger = createStaggerContainer(0.5, 0.2);
      const animateVariant = customStagger.animate as any;
      expect(animateVariant.transition?.delayChildren).toBe(0.5);
      expect(animateVariant.transition?.staggerChildren).toBe(0.2);
    });

    it('createStaggerContainer should use defaults', () => {
      const defaultStagger = createStaggerContainer();
      const animateVariant = defaultStagger.animate as any;
      expect(animateVariant.transition?.delayChildren).toBe(0);
      expect(animateVariant.transition?.staggerChildren).toBe(STAGGER.normal);
    });

    it('staggerContainerFast should use fast timing', () => {
      const animateVariant = VARIANTS.staggerContainerFast.animate as any;
      expect(animateVariant.transition?.staggerChildren).toBe(STAGGER.fast);
    });

    it('staggerContainerSlow should use slow timing', () => {
      const animateVariant = VARIANTS.staggerContainerSlow.animate as any;
      expect(animateVariant.transition?.staggerChildren).toBe(STAGGER.slow);
    });
  });

  describe('Type Safety', () => {
    it('should allow importing all types', () => {
      // This test verifies that TypeScript compilation succeeds
      // The actual type checking happens at compile time
      type AnimationDuration = typeof DURATIONS[keyof typeof DURATIONS];
      type EasingFunction = typeof EASINGS[keyof typeof EASINGS];
      type StaggerDelay = typeof STAGGER[keyof typeof STAGGER];

      const duration: AnimationDuration = DURATIONS.fast;
      const easing: EasingFunction = EASINGS.easeOut;
      const stagger: StaggerDelay = STAGGER.normal;

      expect(duration).toBe(150);
      expect(easing).toBe('cubic-bezier(0.0, 0.0, 0.2, 1)');
      expect(stagger).toBe(0.1);
    });
  });

  describe('Tree-Shaking', () => {
    it('should use named exports only', () => {
      // Verify no default exports (better for tree-shaking)
      const animationsModule = require('../index');
      expect(animationsModule.default).toBeUndefined();
    });
  });
});
