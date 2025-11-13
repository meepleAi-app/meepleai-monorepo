# [POLISH] Landing Page Performance and UX

## 🎯 Objective
Optimize landing page for faster load and better UX.

## ✅ Acceptance Criteria
- [ ] Reduce Framer Motion overhead (consolidate useInView)
- [ ] Lazy load below-fold content
- [ ] Optimize images (Next.js Image component)
- [ ] Add "Try Demo" button (auto-fills credentials)
- [ ] Improve mobile hero section
- [ ] Add testimonials section (optional)

## 🏗️ Implementation
1. Consolidate Intersection Observers:
   ```tsx
   const { ref, inView } = useInView({ threshold: [0.1, 0.5, 0.9] });
   ```
2. Lazy load sections:
   ```tsx
   const FeaturesSection = dynamic(() => import('./FeaturesSection'));
   ```
3. Add demo button:
   ```tsx
   <Button onClick={() => {
     setLoginForm({ email: 'user@meepleai.dev', password: 'Demo123!' });
     setShowAuthModal(true);
   }}>
     Try Demo Account
   </Button>
   ```

## ⏱️ Effort: **0.5 day** | **Sprint 3** | **Priority**: 🟢 Low
