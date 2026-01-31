# Animation Utilities Library

Comprehensive animation utilities for CHAT-04 loading states and consistent motion design across the MeepleAI application.

## Features

- **Type-safe**: Full TypeScript support with strict mode compliance
- **Tree-shakeable**: Named exports for optimal bundle size
- **Accessible**: Built-in support for `prefers-reduced-motion`
- **Framer Motion**: Pre-configured variants and transitions
- **Consistent**: Standardized durations, easings, and spring configurations

## Installation

The library is already available in the project. Import from `@/lib/animations`:

```tsx
import { useReducedMotion, VARIANTS, TRANSITIONS } from '@/lib/animations';
```

## API Reference

### Duration Constants

```typescript
import { DURATIONS } from '@/lib/animations';

DURATIONS.fast    // 150ms - for micro-interactions
DURATIONS.normal  // 300ms - default for most UI
DURATIONS.slow    // 500ms - for page transitions
```

### Easing Functions

```typescript
import { EASINGS } from '@/lib/animations';

EASINGS.easeOut    // cubic-bezier(0.0, 0.0, 0.2, 1)
EASINGS.easeIn     // cubic-bezier(0.4, 0.0, 1, 1)
EASINGS.easeInOut  // cubic-bezier(0.4, 0.0, 0.2, 1)
EASINGS.sharp      // cubic-bezier(0.4, 0.0, 0.6, 1)
```

### Spring Configurations

```typescript
import { SPRING_CONFIGS } from '@/lib/animations';

SPRING_CONFIGS.gentle  // Smooth and subtle
SPRING_CONFIGS.default // Balanced motion
SPRING_CONFIGS.bouncy  // Playful, energetic
SPRING_CONFIGS.stiff   // Quick, responsive
```

### Transition Presets

```typescript
import { TRANSITIONS } from '@/lib/animations';

TRANSITIONS.fade          // Fast opacity change
TRANSITIONS.slide         // Normal speed slide
TRANSITIONS.scale         // Fast scale
TRANSITIONS.spring        // Default spring
TRANSITIONS.springGentle  // Smooth spring
TRANSITIONS.springBouncy  // Bouncy spring
```

### Animation Variants

```typescript
import { VARIANTS } from '@/lib/animations';

// Available variants:
VARIANTS.fadeIn
VARIANTS.slideUp
VARIANTS.slideDown
VARIANTS.slideLeft
VARIANTS.slideRight
VARIANTS.scaleIn
VARIANTS.popIn

// Stagger containers:
VARIANTS.staggerContainer        // Normal timing
VARIANTS.staggerContainerFast    // Fast timing
VARIANTS.staggerContainerSlow    // Slow timing
VARIANTS.createStaggerContainer(delay, stagger)  // Custom timing
```

### useReducedMotion Hook

```typescript
import { useReducedMotion } from '@/lib/animations';

function MyComponent() {
  const shouldReduceMotion = useReducedMotion();

  // Returns true if user prefers reduced motion
  // Automatically updates when system preference changes
}
```

## Usage Examples

### Basic Animation

```tsx
import { motion } from 'framer-motion';
import { VARIANTS, TRANSITIONS } from '@/lib/animations';

function Card() {
  return (
    <motion.div
      variants={VARIANTS.slideUp}
      initial="initial"
      animate="animate"
      exit="exit"
    >
      Card content
    </motion.div>
  );
}
```

### Respecting Reduced Motion

```tsx
import { motion } from 'framer-motion';
import { useReducedMotion, VARIANTS } from '@/lib/animations';

function AnimatedCard() {
  const shouldReduceMotion = useReducedMotion();

  return (
    <motion.div
      variants={shouldReduceMotion ? VARIANTS.fadeIn : VARIANTS.slideUp}
      initial="initial"
      animate="animate"
    >
      Card content
    </motion.div>
  );
}
```

### List with Stagger

```tsx
import { motion } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function List({ items }) {
  return (
    <motion.ul
      variants={VARIANTS.staggerContainer}
      initial="initial"
      animate="animate"
    >
      {items.map(item => (
        <motion.li key={item.id} variants={VARIANTS.fadeIn}>
          {item.name}
        </motion.li>
      ))}
    </motion.ul>
  );
}
```

### Custom Stagger Timing

```tsx
import { motion } from 'framer-motion';
import { VARIANTS, createStaggerContainer } from '@/lib/animations';

function CustomList({ items }) {
  return (
    <motion.ul
      variants={createStaggerContainer(0.2, 0.05)}
      initial="initial"
      animate="animate"
    >
      {items.map(item => (
        <motion.li key={item.id} variants={VARIANTS.slideUp}>
          {item.name}
        </motion.li>
      ))}
    </motion.ul>
  );
}
```

### Loading States

```tsx
import { motion } from 'framer-motion';
import { useReducedMotion, TRANSITIONS } from '@/lib/animations';

function LoadingSpinner() {
  const shouldReduceMotion = useReducedMotion();

  if (shouldReduceMotion) {
    return <div>Loading...</div>;
  }

  return (
    <motion.div
      animate={{ rotate: 360 }}
      transition={{
        ...TRANSITIONS.spring,
        repeat: Infinity,
      }}
    >
      ⟳
    </motion.div>
  );
}
```

### Modal with Spring

```tsx
import { motion, AnimatePresence } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function Modal({ isOpen, onClose, children }) {
  return (
    <AnimatePresence>
      {isOpen && (
        <>
          <motion.div
            variants={VARIANTS.fadeIn}
            initial="initial"
            animate="animate"
            exit="exit"
            onClick={onClose}
            style={{
              position: 'fixed',
              inset: 0,
              backgroundColor: 'rgba(0, 0, 0, 0.5)',
            }}
          />
          <motion.div
            variants={VARIANTS.popIn}
            initial="initial"
            animate="animate"
            exit="exit"
            style={{
              position: 'fixed',
              top: '50%',
              left: '50%',
              transform: 'translate(-50%, -50%)',
            }}
          >
            {children}
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
```

## Best Practices

1. **Always respect reduced motion**: Use `useReducedMotion()` for accessibility
2. **Use semantic variants**: Choose variants that match the UI intent (e.g., `slideUp` for upward motion)
3. **Keep animations subtle**: Use `DURATIONS.fast` or `DURATIONS.normal` for most UI
4. **Provide exit animations**: Always define exit states for AnimatePresence
5. **Avoid motion sickness**: Keep movements small and purposeful

## File Structure

```
src/lib/animations/
├── index.ts              # Barrel export (main entry point)
├── transitions.ts        # Durations, easings, spring configs
├── variants.ts           # Framer Motion variant presets
└── useReducedMotion.ts   # React hook for motion preferences
```

## TypeScript Types

All exports are fully typed:

```typescript
type AnimationDuration = 150 | 300 | 500;
type EasingFunction = string;
type SpringConfig = { type: 'spring'; damping: number; stiffness: number };
type TransitionPreset = {...};
type StaggerDelay = 0.05 | 0.1 | 0.15;
type VariantPreset = Variants;
type UseReducedMotionReturn = boolean;
```

## Browser Compatibility

- Modern browsers: Full support
- Legacy browsers: Graceful fallback for `prefers-reduced-motion`
- SSR: Safe for server-side rendering (Next.js compatible)

## Related Issues

- CHAT-04: Loading states and animations implementation
- A11Y: Accessibility improvements with reduced motion support
