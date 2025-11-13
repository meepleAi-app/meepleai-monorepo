# [REFACTOR] Unified Error Handling System

## 🎯 Objective
Standardize error display across the application using ErrorDisplay component.

## 📋 Current Issues
- 3 different error patterns (string, inline div, ErrorDisplay)
- Inconsistent error messages
- Missing correlation IDs in some errors
- No error boundaries on routes
- No retry mechanism for some operations

## ✅ Acceptance Criteria
- [ ] All errors use `<ErrorDisplay>` component
- [ ] Error boundaries on all routes
- [ ] Correlation IDs in all error displays
- [ ] Retry button for retryable errors
- [ ] Toast notifications for transient errors
- [ ] Error logging to backend (optional)

## 🏗️ Implementation
1. Create `<RouteErrorBoundary>`:
   ```tsx
   export function RouteErrorBoundary({ children }) {
     return (
       <ErrorBoundary
         fallback={(error, reset) => (
           <ErrorDisplay error={error} onRetry={reset} />
         )}
       >
         {children}
       </ErrorBoundary>
     );
   }
   ```
2. Replace all error string displays:
   ```tsx
   // Before
   {error && <div className="text-red-500">{error}</div>}

   // After
   {error && <ErrorDisplay error={categorizeError(error)} />}
   ```
3. Add error boundaries to pages:
   ```tsx
   // _app.tsx
   <RouteErrorBoundary>
     <Component {...pageProps} />
   </RouteErrorBoundary>
   ```
4. Enhance ErrorDisplay with retry logic
5. Add toast for transient errors

## 📦 Files
- Update all components with inline error displays
- Add error boundaries to `_app.tsx`
- Enhance `components/ErrorDisplay.tsx`

## ⏱️ Effort: **0.5 day** | **Sprint 2** | **Priority**: 🟡 High
