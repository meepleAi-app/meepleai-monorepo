# Issue: Extract MotionButton Component

**ID**: UX-002
**Category**: UI/UX (DRY)
**Priority**: 🟢 **MEDIUM**
**Status**: 🔴 Open
**Created**: 2025-11-19

---

## 📋 Summary

Eliminare duplicazione di motion wrappers estraendo un componente MotionButton riusabile.

---

## 🎯 Problem Statement

### Current Code (3x Duplication)
```typescript
// ❌ DUPLICATO 3 volte in HomePage.tsx
<motion.div whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}>
  <Button>Get Started</Button>
</motion.div>
<motion.div whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}>
  <Button variant="outline">Try Demo</Button>
</motion.div>
<motion.div whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}>
  <Button variant="outline" asChild><a href="#features">See How</a></Button>
</motion.div>
```

---

## 🔧 Solution

### Create MotionButton Component

**File**: `apps/web/src/components/ui/motion-button.tsx`
```typescript
import { motion } from 'framer-motion';
import { Button, type ButtonProps } from './button';

export function MotionButton({ children, ...props }: ButtonProps) {
  return (
    <motion.div
      whileHover={{ scale: 1.05 }}
      whileTap={{ scale: 0.95 }}
      style={{ display: 'inline-block' }}
    >
      <Button {...props}>{children}</Button>
    </motion.div>
  );
}
```

### Usage (DRY)
```typescript
// ✅ AFTER: No duplication
<MotionButton>Get Started</MotionButton>
<MotionButton variant="outline">Try Demo</MotionButton>
<MotionButton variant="outline" asChild>
  <a href="#features">See How</a>
</MotionButton>
```

---

## 📝 Implementation Checklist

- [ ] Create MotionButton component
- [ ] Replace 3 duplicated instances in HomePage
- [ ] Add Storybook story
- [ ] Verify visual appearance unchanged

---

## ✅ Acceptance Criteria

- [ ] MotionButton component created
- [ ] All duplications replaced
- [ ] Visual appearance unchanged
- [ ] Performance unchanged

---

## 📊 Effort: 3 hours

---

**Last Updated**: 2025-11-19
**Status**: 🔴 Open
