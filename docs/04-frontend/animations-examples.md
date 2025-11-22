# Animation Utilities - Usage Examples

Complete examples for using the animation utilities library in CHAT-04 and beyond.

## Table of Contents

1. [Basic Animations](#basic-animations)
2. [Loading States](#loading-states)
3. [List Animations](#list-animations)
4. [Modal and Overlay](#modal-and-overlay)
5. [Button Interactions](#button-interactions)
6. [Chat Message Animations](#chat-message-animations)
7. [Accessibility (Reduced Motion)](#accessibility-reduced-motion)

---

## Basic Animations

### Simple Fade In

```tsx
import { motion } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function Card() {
  return (
    <motion.div
      variants={VARIANTS.fadeIn}
      initial="initial"
      animate="animate"
      exit="exit"
      className="card"
    >
      <h2>Card Title</h2>
      <p>Card content</p>
    </motion.div>
  );
}
```

### Slide Up from Bottom

```tsx
import { motion } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function Notification() {
  return (
    <motion.div
      variants={VARIANTS.slideUp}
      initial="initial"
      animate="animate"
      exit="exit"
      className="notification"
    >
      Message sent successfully!
    </motion.div>
  );
}
```

### Pop In with Spring

```tsx
import { motion } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function ActionButton({ onClick }) {
  return (
    <motion.button
      variants={VARIANTS.popIn}
      initial="initial"
      animate="animate"
      whileHover={{ scale: 1.05 }}
      whileTap={{ scale: 0.95 }}
      onClick={onClick}
    >
      Click Me!
    </motion.button>
  );
}
```

---

## Loading States

### Spinning Loader

```tsx
import { motion } from 'framer-motion';
import { useReducedMotion, TRANSITIONS } from '@/lib/animations';

function SpinningLoader() {
  const shouldReduceMotion = useReducedMotion();

  if (shouldReduceMotion) {
    return <div className="loader-text">Loading...</div>;
  }

  return (
    <motion.div
      className="spinner"
      animate={{ rotate: 360 }}
      transition={{
        ...TRANSITIONS.spring,
        repeat: Infinity,
        duration: 1,
      }}
    >
      ⟳
    </motion.div>
  );
}
```

### Pulsing Dots

```tsx
import { motion } from 'framer-motion';
import { TRANSITIONS, useReducedMotion } from '@/lib/animations';

function LoadingDots() {
  const shouldReduceMotion = useReducedMotion();

  const dotVariants = {
    initial: { opacity: 0.3, scale: 1 },
    animate: { opacity: 1, scale: 1.2 },
  };

  const transition = shouldReduceMotion
    ? { duration: 0 }
    : {
        ...TRANSITIONS.spring,
        repeat: Infinity,
        repeatType: 'reverse' as const,
      };

  return (
    <div className="loading-dots">
      {[0, 1, 2].map((index) => (
        <motion.span
          key={index}
          variants={dotVariants}
          initial="initial"
          animate="animate"
          transition={{
            ...transition,
            delay: index * 0.2,
          }}
        >
          •
        </motion.span>
      ))}
    </div>
  );
}
```

### Skeleton Loader

```tsx
import { motion } from 'framer-motion';
import { useReducedMotion, DURATIONS } from '@/lib/animations';

function SkeletonLoader() {
  const shouldReduceMotion = useReducedMotion();

  const shimmerVariants = {
    initial: { backgroundPosition: '-200% 0' },
    animate: { backgroundPosition: '200% 0' },
  };

  return (
    <motion.div
      className="skeleton"
      variants={shouldReduceMotion ? undefined : shimmerVariants}
      initial="initial"
      animate="animate"
      transition={{
        duration: shouldReduceMotion ? 0 : 1.5,
        repeat: Infinity,
        ease: 'linear',
      }}
      style={{
        background: 'linear-gradient(90deg, #f0f0f0 25%, #e0e0e0 50%, #f0f0f0 75%)',
        backgroundSize: '200% 100%',
      }}
    />
  );
}
```

---

## List Animations

### Staggered List

```tsx
import { motion } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

interface Item {
  id: string;
  name: string;
}

function ItemList({ items }: { items: Item[] }) {
  return (
    <motion.ul
      variants={VARIANTS.staggerContainer}
      initial="initial"
      animate="animate"
      className="item-list"
    >
      {items.map((item) => (
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

function FastList({ items }: { items: Item[] }) {
  // Very fast stagger: 200ms delay before starting, 50ms between items
  const customStagger = createStaggerContainer(0.2, 0.05);

  return (
    <motion.ul
      variants={customStagger}
      initial="initial"
      animate="animate"
    >
      {items.map((item) => (
        <motion.li key={item.id} variants={VARIANTS.slideUp}>
          {item.name}
        </motion.li>
      ))}
    </motion.ul>
  );
}
```

### Grid Animation

```tsx
import { motion } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function GameGrid({ games }: { games: Game[] }) {
  return (
    <motion.div
      variants={VARIANTS.staggerContainerFast}
      initial="initial"
      animate="animate"
      className="grid grid-cols-3 gap-4"
    >
      {games.map((game) => (
        <motion.div key={game.id} variants={VARIANTS.scaleIn}>
          <GameCard game={game} />
        </motion.div>
      ))}
    </motion.div>
  );
}
```

---

## Modal and Overlay

### Modal with Backdrop

```tsx
import { motion, AnimatePresence } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function Modal({ isOpen, onClose, children }) {
  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            variants={VARIANTS.fadeIn}
            initial="initial"
            animate="animate"
            exit="exit"
            onClick={onClose}
            className="fixed inset-0 bg-black/50 z-40"
          />

          {/* Modal */}
          <motion.div
            variants={VARIANTS.popIn}
            initial="initial"
            animate="animate"
            exit="exit"
            className="fixed top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 z-50 bg-white rounded-lg p-6"
          >
            {children}
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
```

### Slide-out Drawer

```tsx
import { motion, AnimatePresence } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function Drawer({ isOpen, onClose, children }) {
  const drawerVariants = {
    initial: { x: '100%' },
    animate: { x: 0 },
    exit: { x: '100%' },
  };

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
            className="fixed inset-0 bg-black/50 z-40"
          />
          <motion.div
            variants={drawerVariants}
            initial="initial"
            animate="animate"
            exit="exit"
            className="fixed right-0 top-0 bottom-0 w-80 bg-white shadow-lg z-50"
          >
            {children}
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
```

---

## Button Interactions

### Hover and Tap

```tsx
import { motion } from 'framer-motion';
import { TRANSITIONS } from '@/lib/animations';

function InteractiveButton({ onClick, children }) {
  return (
    <motion.button
      onClick={onClick}
      whileHover={{ scale: 1.05 }}
      whileTap={{ scale: 0.95 }}
      transition={TRANSITIONS.springGentle}
      className="px-4 py-2 bg-blue-500 text-white rounded"
    >
      {children}
    </motion.button>
  );
}
```

### Loading Button

```tsx
import { motion, AnimatePresence } from 'framer-motion';
import { VARIANTS, useReducedMotion } from '@/lib/animations';

function LoadingButton({ isLoading, onClick, children }) {
  const shouldReduceMotion = useReducedMotion();

  return (
    <motion.button
      onClick={isLoading ? undefined : onClick}
      disabled={isLoading}
      whileHover={isLoading ? undefined : { scale: 1.05 }}
      whileTap={isLoading ? undefined : { scale: 0.95 }}
      className="relative px-4 py-2 bg-blue-500 text-white rounded"
    >
      <AnimatePresence mode="wait">
        {isLoading ? (
          <motion.span
            key="loading"
            variants={VARIANTS.fadeIn}
            initial="initial"
            animate="animate"
            exit="exit"
          >
            {shouldReduceMotion ? 'Loading...' : (
              <motion.span
                animate={{ rotate: 360 }}
                transition={{ repeat: Infinity, duration: 1 }}
              >
                ⟳
              </motion.span>
            )}
          </motion.span>
        ) : (
          <motion.span
            key="content"
            variants={VARIANTS.fadeIn}
            initial="initial"
            animate="animate"
            exit="exit"
          >
            {children}
          </motion.span>
        )}
      </AnimatePresence>
    </motion.button>
  );
}
```

---

## Chat Message Animations

### Message Bubble

```tsx
import { motion } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function ChatMessage({ message, isUser }) {
  return (
    <motion.div
      variants={isUser ? VARIANTS.slideLeft : VARIANTS.slideRight}
      initial="initial"
      animate="animate"
      className={`chat-message ${isUser ? 'user' : 'ai'}`}
    >
      {message.text}
    </motion.div>
  );
}
```

### Typing Indicator

```tsx
import { motion } from 'framer-motion';
import { useReducedMotion, TRANSITIONS } from '@/lib/animations';

function TypingIndicator() {
  const shouldReduceMotion = useReducedMotion();

  const dotVariants = {
    initial: { y: 0 },
    animate: { y: -8 },
  };

  if (shouldReduceMotion) {
    return <div className="typing-indicator">AI is typing...</div>;
  }

  return (
    <div className="typing-indicator">
      {[0, 1, 2].map((index) => (
        <motion.span
          key={index}
          variants={dotVariants}
          initial="initial"
          animate="animate"
          transition={{
            ...TRANSITIONS.spring,
            repeat: Infinity,
            repeatType: 'reverse',
            delay: index * 0.15,
          }}
          className="dot"
        />
      ))}
    </div>
  );
}
```

### Streaming Text

```tsx
import { motion } from 'framer-motion';
import { VARIANTS } from '@/lib/animations';

function StreamingMessage({ tokens }: { tokens: string[] }) {
  return (
    <motion.div
      variants={VARIANTS.staggerContainerFast}
      initial="initial"
      animate="animate"
    >
      {tokens.map((token, index) => (
        <motion.span key={index} variants={VARIANTS.fadeIn}>
          {token}
        </motion.span>
      ))}
    </motion.div>
  );
}
```

---

## Accessibility (Reduced Motion)

### Conditional Animations

```tsx
import { motion } from 'framer-motion';
import { useReducedMotion, VARIANTS } from '@/lib/animations';

function AccessibleCard() {
  const shouldReduceMotion = useReducedMotion();

  return (
    <motion.div
      variants={shouldReduceMotion ? VARIANTS.fadeIn : VARIANTS.slideUp}
      initial="initial"
      animate="animate"
    >
      Content respects user preferences
    </motion.div>
  );
}
```

### Complete Disable

```tsx
import { motion } from 'framer-motion';
import { useReducedMotion } from '@/lib/animations';

function ConditionalAnimation({ children }) {
  const shouldReduceMotion = useReducedMotion();

  if (shouldReduceMotion) {
    return <div>{children}</div>;
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
    >
      {children}
    </motion.div>
  );
}
```

### Instant Transitions

```tsx
import { motion } from 'framer-motion';
import { useReducedMotion, VARIANTS, TRANSITIONS } from '@/lib/animations';

function FlexibleAnimation() {
  const shouldReduceMotion = useReducedMotion();

  return (
    <motion.div
      variants={VARIANTS.slideUp}
      initial="initial"
      animate="animate"
      transition={shouldReduceMotion ? { duration: 0 } : TRANSITIONS.spring}
    >
      Transitions respect motion preferences
    </motion.div>
  );
}
```

---

## Best Practices

1. **Always import from `@/lib/animations`** for consistency
2. **Use `useReducedMotion()` for accessibility** - respect user preferences
3. **Provide exit animations** with AnimatePresence for smooth unmounts
4. **Choose appropriate variants** - match the UI intent (slide, fade, scale)
5. **Keep animations subtle** - use fast/normal durations for most UI
6. **Test with reduced motion** - ensure UI is still functional without animations
7. **Avoid motion sickness** - small movements, gentle springs
8. **Use stagger for lists** - creates visual hierarchy and flow
9. **Combine with Tailwind** - animations enhance utility classes
10. **Profile performance** - avoid animating expensive properties

---

## Integration with CHAT-04

### Loading State Component

```tsx
import { motion, AnimatePresence } from 'framer-motion';
import { VARIANTS, useReducedMotion } from '@/lib/animations';

export function ChatLoadingState({ stage }: { stage: 'thinking' | 'typing' }) {
  const shouldReduceMotion = useReducedMotion();

  return (
    <AnimatePresence mode="wait">
      <motion.div
        key={stage}
        variants={VARIANTS.fadeIn}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        {stage === 'thinking' ? (
          <ThinkingIndicator reducedMotion={shouldReduceMotion} />
        ) : (
          <TypingIndicator reducedMotion={shouldReduceMotion} />
        )}
      </motion.div>
    </AnimatePresence>
  );
}
```

This library provides all the animation primitives needed for CHAT-04 and future features!
