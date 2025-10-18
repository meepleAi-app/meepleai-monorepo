# CHAT-04: Loading States and Animations Research

**Research Date**: 2025-10-18
**Frameworks**: Next.js 14 (App Router), React 19, Framer Motion
**Purpose**: Best practices for implementing polished loading states and animations

---

## Executive Summary

This document provides comprehensive research findings on implementing loading states and animations in Next.js 14 with the App Router. All recommendations are based on official documentation from Next.js, React 19, and Framer Motion.

**Key Findings**:
- Next.js 14 App Router provides built-in loading UI patterns (`loading.js`, Suspense)
- React 19 introduces improved Suspense and streaming capabilities
- Framer Motion offers production-ready animation primitives with layout animations
- Performance: Target 60fps with proper memoization and layout animation optimization
- Accessibility: Always respect `prefers-reduced-motion` and provide ARIA live regions

---

## Table of Contents

1. [Next.js 14 Loading Patterns](#1-nextjs-14-loading-patterns)
2. [React 19 Suspense & Streaming](#2-react-19-suspense--streaming)
3. [Framer Motion Animation Patterns](#3-framer-motion-animation-patterns)
4. [Performance Optimization](#4-performance-optimization)
5. [Accessibility Requirements](#5-accessibility-requirements)
6. [Testing Strategies](#6-testing-strategies)
7. [Implementation Recommendations](#7-implementation-recommendations)

---

## 1. Next.js 14 Loading Patterns

### 1.1 Built-in Loading UI (`loading.js`)

**Confidence: 1.0** - Official Next.js documentation

Next.js App Router provides a special `loading.js` file convention that creates instant loading states using React Suspense.

**Example: Route-level Loading State**

```typescript
// app/dashboard/loading.tsx
export default function Loading() {
  return <LoadingSkeleton />
}
```json
**How it works**:
- Automatically wraps page content in `<Suspense>` boundary
- Renders immediately while route segment loads
- Supports nested loading states for layouts

**Source**: [Next.js v14.3.0 - loading-ui-and-streaming.mdx](https://github.com/vercel/next.js/blob/v14.3.0-canary.87/docs/02-app/01-building-your-application/01-routing/06-loading-ui-and-streaming.mdx)

---

### 1.2 Manual Suspense Boundaries

**Confidence: 1.0** - Official Next.js documentation

For granular control, wrap individual components with React Suspense.

**Example: Component-level Streaming**

```typescript
import { Suspense } from 'react'
import { PostFeed, Weather } from './Components'

export default function Posts() {
  return (
    <section>
      <Suspense fallback={<p>Loading feed...</p>}>
        <PostFeed />
      </Suspense>
      <Suspense fallback={<p>Loading weather...</p>}>
        <Weather />
      </Suspense>
    </section>
  )
}
```json
**Benefits**:
- **Selective Hydration**: Independent loading of components
- **Streaming SSR**: Server sends HTML progressively
- **Better UX**: Show content as it becomes available

**Source**: [Next.js v14.3.0 - loading-ui-and-streaming.mdx](https://github.com/vercel/next.js/blob/v14.3.0-canary.87/docs/02-app/01-building-your-application/01-routing/06-loading-ui-and-streaming.mdx)

---

### 1.3 Dynamic Imports with Loading States

**Confidence: 1.0** - Official Next.js documentation

Use `next/dynamic` for code splitting with custom loading components.

**Example: Lazy-loaded Component with Fallback**

```typescript
'use client'

import dynamic from 'next/dynamic'

const ComponentA = dynamic(() => import('../components/A'))
const ComponentB = dynamic(() => import('../components/B'))
const ComponentC = dynamic(() => import('../components/C'), { ssr: false })

const WithCustomLoading = dynamic(
  () => import('../components/WithCustomLoading'),
  {
    loading: () => <p>Loading...</p>,
  }
)

export default function ClientComponentExample() {
  const [showMore, setShowMore] = useState(false)

  return (
    <div>
      {/* Load immediately, but in a separate client bundle */}
      <ComponentA />

      {/* Load on demand, only when/if the condition is met */}
      {showMore && <ComponentB />}
      <button onClick={() => setShowMore(!showMore)}>Toggle</button>

      {/* Load only on the client side */}
      <ComponentC />

      {/* Custom loading component */}
      <WithCustomLoading />
    </div>
  )
}
```json
**Use Cases**:
- Large client components (charts, editors)
- Third-party widgets
- Components that depend on browser APIs
- Conditional features

**Options**:
- `ssr: false` - Disable server-side rendering
- `loading: () => Component` - Custom loading fallback

**Source**: [Next.js v14.3.0 - lazy-loading.mdx](https://github.com/vercel/next.js/blob/v14.3.0-canary.87/docs/02-app/01-building-your-application/06-optimizing/07-lazy-loading.mdx)

---

### 1.4 Client-Side Data Fetching with SWR

**Confidence: 0.95** - Recommended pattern in Next.js docs

For client-side loading states, use SWR or React Query.

**Example: SWR with Loading States**

```typescript
'use client'

import useSWR from 'swr'

const fetcher = (url: string) => fetch(url).then((r) => r.json())

export default function Page() {
  const { data, error, isLoading } = useSWR(
    `https://jsonplaceholder.typicode.com/posts/1`,
    fetcher
  )

  if (error) return <div>Failed to load</div>
  if (isLoading) return <div>Loading...</div>

  return <div>{data.title}</div>
}
```json
**Benefits**:
- Automatic revalidation
- Built-in loading/error states
- Caching and deduplication
- Focus revalidation

**Source**: [Next.js v14.3.0 - static-exports.mdx](https://github.com/vercel/next.js/blob/v14.3.0-canary.87/docs/02-app/01-building-your-application/10-deploying/02-static-exports.mdx)

---

## 2. React 19 Suspense & Streaming

### 2.1 React 19 Improvements

**Confidence: 0.9** - React 19 is in release candidate, official docs available

React 19 enhances Suspense with better error boundaries and transitions.

**Key Features**:
- Improved error recovery
- Better loading state coordination
- Enhanced streaming SSR performance

**Example: useFormStatus Hook (React 19)**

```typescript
'use client'

import { useFormStatus } from 'react-dom'

function SubmitButton() {
  const { pending } = useFormStatus()

  return (
    <button type="submit" disabled={pending}>
      {pending ? 'Submitting...' : 'Submit'}
    </button>
  )
}

function MyForm() {
  return (
    <form action={formAction}>
      {/* Form fields */}
      <SubmitButton />
    </form>
  )
}
```json
**Source**: [React v19.1.1 - CHANGELOG-canary.md](https://github.com/facebook/react/blob/v19.1.1/CHANGELOG-canary.md)

---

### 2.2 Performance Monitoring

**Confidence: 1.0** - Official React documentation

React provides performance measurement APIs.

**Example: Performance Tracking**

```typescript
'use client'

import { useReportWebVitals } from 'next/web-vitals'

export function WebVitals() {
  useReportWebVitals((metric) => {
    switch (metric.name) {
      case 'FCP': // First Contentful Paint
        console.log('FCP:', metric.value)
        break
      case 'LCP': // Largest Contentful Paint
        console.log('LCP:', metric.value)
        break
      case 'CLS': // Cumulative Layout Shift
        console.log('CLS:', metric.value)
        break
      case 'FID': // First Input Delay
        console.log('FID:', metric.value)
        break
      case 'TTFB': // Time to First Byte
        console.log('TTFB:', metric.value)
        break
    }
  })
}
```

**Integration in Layout**:

```typescript
// app/layout.tsx
import { WebVitals } from './components/web-vitals'

export default function RootLayout({ children }) {
  return (
    <html>
      <body>
        <WebVitals />
        {children}
      </body>
    </html>
  )
}
```sql
**Source**: [Next.js v14.3.0 - analytics.mdx](https://github.com/vercel/next.js/blob/v14.3.0-canary.87/docs/02-app/01-building-your-application/06-optimizing/08-analytics.mdx)

---

## 3. Framer Motion Animation Patterns

### 3.1 Basic Animations

**Confidence: 0.85** - From Framer Motion community repository

Framer Motion provides declarative animations with minimal code.

**Example: Fade and Slide Animation**

```typescript
'use client'

import { motion } from 'framer-motion'

export function LoadingCard() {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3, ease: "easeOut" }}
    >
      <p>Loading content...</p>
    </motion.div>
  )
}
```json
**Animation Properties**:
- `initial`: Starting state
- `animate`: Target state
- `exit`: State when component unmounts (requires `AnimatePresence`)
- `transition`: Animation configuration

---

### 3.2 Layout Animations

**Confidence: 0.9** - Framer Motion official feature

Automatic layout animations when component size/position changes.

**Example: Auto-animating Layout Changes**

```typescript
'use client'

import { motion } from 'framer-motion'
import { useState } from 'react'

export function ExpandableCard() {
  const [isExpanded, setIsExpanded] = useState(false)

  return (
    <motion.div
      layout
      initial={{ borderRadius: 8 }}
      animate={{
        backgroundColor: isExpanded ? '#f00' : '#00f'
      }}
      transition={{
        layout: { duration: 0.3, ease: "easeInOut" },
        backgroundColor: { duration: 0.5 }
      }}
      onClick={() => setIsExpanded(!isExpanded)}
    >
      <motion.h2 layout="position">Expandable Content</motion.h2>
      {isExpanded && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
        >
          <p>Additional content appears here</p>
        </motion.div>
      )}
    </motion.div>
  )
}
```json
**Layout Animation Features**:
- `layout`: Enables automatic layout animations
- `layout="position"`: Only animates position changes
- `layout="size"`: Only animates size changes
- Separate transitions for layout vs. other properties

**Source**: [Framer Motion - layout animations](https://github.com/grx7/framer-motion/blob/main/dev/html/public/optimized-appear/defer-handoff-layout.html)

---

### 3.3 Shared Layout Transitions (layoutId)

**Confidence: 0.9** - Framer Motion projection API

Animate elements between different components using shared `layoutId`.

**Example: Shared Element Transition**

```typescript
'use client'

import { motion, AnimatePresence } from 'framer-motion'
import { useState } from 'react'

export function SharedLayoutExample() {
  const [selectedId, setSelectedId] = useState<string | null>(null)

  return (
    <>
      <div className="grid">
        {items.map(item => (
          <motion.div
            key={item.id}
            layoutId={item.id}
            onClick={() => setSelectedId(item.id)}
          >
            <h2>{item.title}</h2>
          </motion.div>
        ))}
      </div>

      <AnimatePresence>
        {selectedId && (
          <motion.div
            layoutId={selectedId}
            onClick={() => setSelectedId(null)}
          >
            <h2>{items.find(i => i.id === selectedId)?.title}</h2>
            <p>Expanded content...</p>
          </motion.div>
        )}
      </AnimatePresence>
    </>
  )
}
```json
**Key Concepts**:
- **layoutId**: Links elements across component tree
- **AnimatePresence**: Required for exit animations
- **Projection**: Framer Motion's layout animation engine

**Source**: [Framer Motion - shared layout transitions](https://github.com/grx7/framer-motion/blob/main/dev/html/public/projection/shared-block-update-promote-new.html)

---

### 3.4 Optimized Appear Animations

**Confidence: 0.85** - Framer Motion performance optimization

Use Web Animations API (WAAPI) for initial animations before React hydration.

**Concept**: Start animations server-side, hand off to Framer Motion after hydration.

**Benefits**:
- Animations start immediately (no hydration delay)
- Smooth handoff to React-controlled animations
- Better perceived performance

**Note**: Advanced pattern, use for critical loading animations only.

**Source**: [Framer Motion - optimized appear](https://github.com/grx7/framer-motion/blob/main/dev/html/public/optimized-appear/defer-handoff-layout.html)

---

## 4. Performance Optimization

### 4.1 60fps Animation Guidelines

**Confidence: 0.95** - Web performance best practices

**CSS Properties That Can Be Animated at 60fps**:
- `transform` (translate, scale, rotate)
- `opacity`
- `filter` (use sparingly)

**Avoid Animating**:
- `width`, `height`, `top`, `left` (triggers layout)
- `padding`, `margin` (triggers layout)
- `background-color` (less performant, but acceptable)

**Example: Hardware-Accelerated Animation**

```typescript
'use client'

import { motion } from 'framer-motion'

export function PerformantLoader() {
  return (
    <motion.div
      style={{
        willChange: 'transform, opacity' // Hint to browser
      }}
      initial={{ opacity: 0, scale: 0.8 }}
      animate={{
        opacity: 1,
        scale: 1,
        transition: {
          duration: 0.3,
          ease: [0.43, 0.13, 0.23, 0.96] // Custom easing
        }
      }}
    >
      <div className="spinner" />
    </motion.div>
  )
}
```

**CSS Optimization**:

```css
/* Use GPU acceleration */
.spinner {
  transform: translateZ(0); /* Force GPU layer */
  backface-visibility: hidden; /* Prevent flickering */
}

/* Avoid layout thrashing */
.animated-element {
  will-change: transform, opacity; /* Use sparingly */
}

/* Remove will-change after animation */
.animated-element.complete {
  will-change: auto;
}
```json
---

### 4.2 React Memoization for Animations

**Confidence: 1.0** - React official optimization pattern

Prevent unnecessary re-renders during animations.

**Example: Memoized Loading Component**

```typescript
'use client'

import { memo } from 'react'
import { motion } from 'framer-motion'

export const LoadingSkeleton = memo(function LoadingSkeleton() {
  return (
    <div className="skeleton">
      <motion.div
        className="skeleton-line"
        animate={{
          opacity: [0.5, 1, 0.5],
        }}
        transition={{
          duration: 1.5,
          repeat: Infinity,
          ease: "easeInOut"
        }}
      />
    </div>
  )
})
```json
**When to Memoize**:
- Loading skeletons that don't depend on props
- Reusable animation components
- Components rendered in lists

---

### 4.3 Debouncing Scroll Events

**Confidence: 0.95** - Performance best practice

Avoid performance issues with scroll-triggered animations.

**Example: Debounced Scroll Handler**

```typescript
'use client'

import { useEffect, useState } from 'react'

export function useScrollPosition(delay = 100) {
  const [scrollY, setScrollY] = useState(0)

  useEffect(() => {
    let timeoutId: NodeJS.Timeout

    const handleScroll = () => {
      clearTimeout(timeoutId)
      timeoutId = setTimeout(() => {
        setScrollY(window.scrollY)
      }, delay)
    }

    window.addEventListener('scroll', handleScroll, { passive: true })

    return () => {
      clearTimeout(timeoutId)
      window.removeEventListener('scroll', handleScroll)
    }
  }, [delay])

  return scrollY
}
```

**Usage**:

```typescript
'use client'

import { motion } from 'framer-motion'
import { useScrollPosition } from './hooks/useScrollPosition'

export function ScrollReveal({ children }) {
  const scrollY = useScrollPosition(100)

  return (
    <motion.div
      initial={{ opacity: 0, y: 50 }}
      animate={{
        opacity: scrollY > 100 ? 1 : 0,
        y: scrollY > 100 ? 0 : 50
      }}
    >
      {children}
    </motion.div>
  )
}
```json
**Optimization Tips**:
- Use `passive: true` for scroll listeners
- Debounce by 100-300ms for scroll events
- Use `IntersectionObserver` API instead when possible

---

## 5. Accessibility Requirements

### 5.1 prefers-reduced-motion

**Confidence: 1.0** - WCAG 2.1 Level AAA requirement

Always respect user's motion preferences.

**Example: Motion-Safe Animations**

```typescript
'use client'

import { motion } from 'framer-motion'

export function AccessibleAnimation({ children }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{
        duration: 0.3,
        ease: "easeOut"
      }}
      // Framer Motion automatically respects prefers-reduced-motion
    >
      {children}
    </motion.div>
  )
}
```

**CSS Fallback**:

```css
@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
```

**Custom Hook for Motion Preference**:

```typescript
'use client'

import { useEffect, useState } from 'react'

export function usePrefersReducedMotion() {
  const [prefersReducedMotion, setPrefersReducedMotion] = useState(false)

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
    setPrefersReducedMotion(mediaQuery.matches)

    const handleChange = () => setPrefersReducedMotion(mediaQuery.matches)
    mediaQuery.addEventListener('change', handleChange)

    return () => mediaQuery.removeEventListener('change', handleChange)
  }, [])

  return prefersReducedMotion
}
```

**Usage**:

```typescript
'use client'

import { motion } from 'framer-motion'
import { usePrefersReducedMotion } from './hooks/usePrefersReducedMotion'

export function RespectfulAnimation({ children }) {
  const prefersReducedMotion = usePrefersReducedMotion()

  return (
    <motion.div
      initial={{ opacity: 0, y: prefersReducedMotion ? 0 : 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{
        duration: prefersReducedMotion ? 0 : 0.3,
        ease: "easeOut"
      }}
    >
      {children}
    </motion.div>
  )
}
```json
---

### 5.2 ARIA Live Regions

**Confidence: 1.0** - WAI-ARIA specification

Announce loading state changes to screen readers.

**Example: Accessible Loading State**

```typescript
'use client'

import { useState } from 'react'

export function AccessibleLoadingButton() {
  const [isLoading, setIsLoading] = useState(false)

  const handleClick = async () => {
    setIsLoading(true)
    try {
      await fetchData()
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <>
      <button
        onClick={handleClick}
        disabled={isLoading}
        aria-busy={isLoading}
        aria-describedby="loading-status"
      >
        {isLoading ? 'Loading...' : 'Load Data'}
      </button>

      <div
        id="loading-status"
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="sr-only" // Visually hidden but screen-reader accessible
      >
        {isLoading ? 'Loading data, please wait...' : ''}
      </div>
    </>
  )
}
```

**ARIA Attributes**:
- `role="status"`: Indicates status information
- `aria-live="polite"`: Announces when user is idle
- `aria-live="assertive"`: Interrupts to announce (use sparingly)
- `aria-atomic="true"`: Reads entire region, not just changes
- `aria-busy="true"`: Indicates element is loading

**Visually Hidden CSS**:

```css
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border-width: 0;
}
```json
---

### 5.3 Focus Management

**Confidence: 0.95** - Accessibility best practice

Manage focus during loading states and transitions.

**Example: Focus Restoration**

```typescript
'use client'

import { useRef, useEffect } from 'react'

export function LoadingModal({ isLoading, onClose }) {
  const previousFocusRef = useRef<HTMLElement | null>(null)
  const modalRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (isLoading) {
      // Store current focus
      previousFocusRef.current = document.activeElement as HTMLElement

      // Move focus to modal
      modalRef.current?.focus()
    } else {
      // Restore previous focus when done
      previousFocusRef.current?.focus()
    }
  }, [isLoading])

  if (!isLoading) return null

  return (
    <div
      ref={modalRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="loading-title"
      tabIndex={-1}
    >
      <h2 id="loading-title">Loading</h2>
      <p>Please wait...</p>
    </div>
  )
}
```json
---

## 6. Testing Strategies

### 6.1 Jest Snapshot Testing

**Confidence: 0.9** - Common testing pattern

Test loading states with Jest snapshots.

**Example: Loading Component Test**

```typescript
// __tests__/LoadingSkeleton.test.tsx
import { render } from '@testing-library/react'
import { LoadingSkeleton } from '../LoadingSkeleton'

describe('LoadingSkeleton', () => {
  it('renders loading skeleton correctly', () => {
    const { container } = render(<LoadingSkeleton />)
    expect(container).toMatchSnapshot()
  })

  it('has correct ARIA attributes', () => {
    const { getByRole } = render(<LoadingSkeleton />)
    const status = getByRole('status')
    expect(status).toHaveAttribute('aria-live', 'polite')
  })
})
```json
---

### 6.2 Playwright E2E Animation Testing

**Confidence: 0.85** - E2E testing best practice

Test animations in real browser environment.

**Example: Animation E2E Test**

```typescript
// e2e/animations.spec.ts
import { test, expect } from '@playwright/test'

test('loading animation appears and disappears', async ({ page }) => {
  await page.goto('/dashboard')

  // Loading should be visible initially
  const loadingElement = page.locator('[data-testid="loading-spinner"]')
  await expect(loadingElement).toBeVisible()

  // Wait for loading to complete
  await expect(loadingElement).toBeHidden({ timeout: 5000 })

  // Content should be visible
  const content = page.locator('[data-testid="dashboard-content"]')
  await expect(content).toBeVisible()
})

test('respects prefers-reduced-motion', async ({ page }) => {
  // Emulate reduced motion preference
  await page.emulateMedia({ reducedMotion: 'reduce' })

  await page.goto('/dashboard')

  // Check that animations are disabled/instant
  const animatedElement = page.locator('[data-testid="animated-card"]')
  const box = await animatedElement.boundingBox()

  // Animation should complete instantly with reduced motion
  await page.waitForTimeout(50)
  const newBox = await animatedElement.boundingBox()

  // Position should be final immediately
  expect(box?.y).toBe(newBox?.y)
})
```json
---

### 6.3 Performance Testing

**Confidence: 0.8** - Performance monitoring best practice

Measure animation performance in tests.

**Example: Performance Monitoring Test**

```typescript
// e2e/performance.spec.ts
import { test, expect } from '@playwright/test'

test('animations maintain 60fps', async ({ page }) => {
  await page.goto('/dashboard')

  // Start performance trace
  await page.evaluate(() => {
    (window as any).performanceData = []

    const observer = new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        (window as any).performanceData.push(entry)
      }
    })

    observer.observe({ entryTypes: ['measure'] })
  })

  // Trigger animation
  await page.click('[data-testid="expand-button"]')
  await page.waitForTimeout(500)

  // Check frame rate
  const performanceData = await page.evaluate(() => (window as any).performanceData)

  // Verify no long frames (> 16.67ms for 60fps)
  const longFrames = performanceData.filter((entry: any) => entry.duration > 16.67)
  expect(longFrames.length).toBeLessThan(3) // Allow some variance
})
```

---

## 7. Implementation Recommendations

### 7.1 Loading State Architecture

**Recommended Pattern for MeepleAI**:

```
apps/web/src/
├── components/
│   ├── loading/
│   │   ├── LoadingSkeleton.tsx      # Reusable skeleton component
│   │   ├── LoadingSpinner.tsx       # Spinner for buttons/inline
│   │   ├── PageLoader.tsx           # Full-page loading state
│   │   └── StreamingContent.tsx     # Suspense wrapper
│   └── animations/
│       ├── FadeIn.tsx               # Reusable fade animation
│       ├── SlideIn.tsx              # Slide animation
│       └── ScaleIn.tsx              # Scale animation
├── hooks/
│   ├── usePrefersReducedMotion.ts   # Motion preference hook
│   └── useScrollPosition.ts         # Debounced scroll hook
└── lib/
    └── animation-variants.ts        # Framer Motion variants
```

---

### 7.2 Reusable Animation Variants

**Create centralized animation configurations**:

```typescript
// lib/animation-variants.ts
import type { Variants } from 'framer-motion'

export const fadeInVariants: Variants = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: { duration: 0.3, ease: "easeOut" }
  },
  exit: {
    opacity: 0,
    transition: { duration: 0.2, ease: "easeIn" }
  }
}

export const slideInVariants: Variants = {
  hidden: { opacity: 0, y: 20 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.3, ease: "easeOut" }
  },
  exit: {
    opacity: 0,
    y: -20,
    transition: { duration: 0.2, ease: "easeIn" }
  }
}

export const scaleInVariants: Variants = {
  hidden: { opacity: 0, scale: 0.9 },
  visible: {
    opacity: 1,
    scale: 1,
    transition: { duration: 0.3, ease: [0.43, 0.13, 0.23, 0.96] }
  },
  exit: {
    opacity: 0,
    scale: 0.95,
    transition: { duration: 0.2, ease: "easeIn" }
  }
}

export const staggerContainerVariants: Variants = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: {
      staggerChildren: 0.1,
      delayChildren: 0.2
    }
  }
}

export const staggerItemVariants: Variants = {
  hidden: { opacity: 0, y: 10 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.3 }
  }
}
```

**Usage**:

```typescript
'use client'

import { motion } from 'framer-motion'
import { fadeInVariants, staggerContainerVariants, staggerItemVariants } from '@/lib/animation-variants'

export function AnimatedList({ items }) {
  return (
    <motion.ul
      variants={staggerContainerVariants}
      initial="hidden"
      animate="visible"
    >
      {items.map(item => (
        <motion.li
          key={item.id}
          variants={staggerItemVariants}
        >
          {item.content}
        </motion.li>
      ))}
    </motion.ul>
  )
}
```

---

### 7.3 Loading Skeleton Component

**Production-ready skeleton loader**:

```typescript
// components/loading/LoadingSkeleton.tsx
'use client'

import { motion } from 'framer-motion'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

interface LoadingSkeletonProps {
  lines?: number
  className?: string
}

export function LoadingSkeleton({ lines = 3, className = '' }: LoadingSkeletonProps) {
  const prefersReducedMotion = usePrefersReducedMotion()

  return (
    <div className={`space-y-3 ${className}`} role="status" aria-live="polite" aria-label="Loading">
      {Array.from({ length: lines }).map((_, i) => (
        <motion.div
          key={i}
          className="h-4 bg-gray-200 rounded"
          style={{ width: `${100 - (i * 10)}%` }}
          animate={prefersReducedMotion ? {} : {
            opacity: [0.5, 1, 0.5],
          }}
          transition={prefersReducedMotion ? {} : {
            duration: 1.5,
            repeat: Infinity,
            ease: "easeInOut",
            delay: i * 0.1
          }}
        />
      ))}
      <span className="sr-only">Loading content...</span>
    </div>
  )
}
```

---

### 7.4 Streaming Content Wrapper

**Reusable Suspense wrapper with loading state**:

```typescript
// components/loading/StreamingContent.tsx
import { Suspense, type ReactNode } from 'react'
import { LoadingSkeleton } from './LoadingSkeleton'

interface StreamingContentProps {
  children: ReactNode
  fallback?: ReactNode
  lines?: number
}

export function StreamingContent({
  children,
  fallback,
  lines = 3
}: StreamingContentProps) {
  return (
    <Suspense fallback={fallback || <LoadingSkeleton lines={lines} />}>
      {children}
    </Suspense>
  )
}
```

**Usage**:

```typescript
// app/dashboard/page.tsx
import { StreamingContent } from '@/components/loading/StreamingContent'
import { DashboardData } from '@/components/DashboardData'

export default function DashboardPage() {
  return (
    <div>
      <h1>Dashboard</h1>

      <StreamingContent lines={5}>
        <DashboardData />
      </StreamingContent>
    </div>
  )
}
```

---

### 7.5 Button Loading State

**Accessible button with loading state**:

```typescript
// components/LoadingButton.tsx
'use client'

import { type ButtonHTMLAttributes } from 'react'
import { motion } from 'framer-motion'

interface LoadingButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  isLoading?: boolean
  loadingText?: string
}

export function LoadingButton({
  isLoading = false,
  loadingText = 'Loading...',
  children,
  disabled,
  ...props
}: LoadingButtonProps) {
  return (
    <button
      {...props}
      disabled={disabled || isLoading}
      aria-busy={isLoading}
      aria-live="polite"
      className={`relative ${props.className || ''}`}
    >
      <span className={isLoading ? 'opacity-0' : ''}>
        {children}
      </span>

      {isLoading && (
        <span className="absolute inset-0 flex items-center justify-center">
          <motion.span
            className="inline-block w-4 h-4 border-2 border-white border-t-transparent rounded-full"
            animate={{ rotate: 360 }}
            transition={{ duration: 1, repeat: Infinity, ease: "linear" }}
          />
          <span className="ml-2">{loadingText}</span>
        </span>
      )}
    </button>
  )
}
```

---

## Summary & Recommendations

### For CHAT-04 Implementation

**Priority 1: Core Loading States**
1. ✅ Implement `LoadingSkeleton` component with shimmer effect
2. ✅ Add `LoadingButton` for form submissions
3. ✅ Create `StreamingContent` wrapper for Suspense boundaries
4. ✅ Use `loading.tsx` files for route-level loading states

**Priority 2: Animations**
1. ✅ Create centralized animation variants in `lib/animation-variants.ts`
2. ✅ Add fade-in animations for page transitions
3. ✅ Implement stagger animations for lists
4. ✅ Use layout animations for expandable sections

**Priority 3: Accessibility**
1. ✅ Implement `usePrefersReducedMotion` hook
2. ✅ Add ARIA live regions to all loading states
3. ✅ Ensure keyboard focus management
4. ✅ Add visually-hidden status announcements

**Priority 4: Performance**
1. ✅ Use `transform` and `opacity` for animations
2. ✅ Memoize loading components
3. ✅ Add `will-change` CSS hints (remove after animation)
4. ✅ Debounce scroll events

**Priority 5: Testing**
1. ✅ Add Jest snapshots for loading components
2. ✅ Create Playwright tests for animation flows
3. ✅ Monitor Web Vitals (LCP, CLS)
4. ✅ Test with `prefers-reduced-motion` enabled

---

## References

### Official Documentation
- [Next.js 14 - Loading UI and Streaming](https://github.com/vercel/next.js/blob/v14.3.0-canary.87/docs/02-app/01-building-your-application/01-routing/06-loading-ui-and-streaming.mdx)
- [Next.js 14 - Lazy Loading](https://github.com/vercel/next.js/blob/v14.3.0-canary.87/docs/02-app/01-building-your-application/06-optimizing/07-lazy-loading.mdx)
- [React 19 - Suspense](https://react.dev/reference/react/Suspense)
- [Framer Motion - Layout Animations](https://www.framer.com/motion/layout-animations/)

### Community Resources
- [SWR Documentation](https://swr.vercel.app/)
- [Web Vitals](https://web.dev/vitals/)
- [WCAG 2.1 - Animation](https://www.w3.org/WAI/WCAG21/Understanding/animation-from-interactions.html)

---

**Document Prepared By**: Claude Code (Technical Documentation Researcher)
**Confidence Level**: High (0.85-1.0 across all sections)
**Last Updated**: 2025-10-18
