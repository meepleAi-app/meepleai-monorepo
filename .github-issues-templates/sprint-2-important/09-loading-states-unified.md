# [ENHANCEMENT] Standardize Loading States

## 🎯 Objective
Consistent loading UX across all components using LoadingButton and SkeletonLoader.

## 📋 Current Issues
- 4 different loading patterns (boolean, object, LoadingButton, none)
- Some operations have no loading feedback
- Skeleton loaders inconsistent
- No optimistic updates

## ✅ Acceptance Criteria
- [ ] All async buttons use `<LoadingButton>`
- [ ] All data loading uses `<SkeletonLoader>`
- [ ] Optimistic updates for chat messages
- [ ] Loading states announced to screen readers
- [ ] Consistent spinner/skeleton design

## 🏗️ Implementation
1. Replace all loading buttons:
   ```tsx
   // Before
   <button disabled={loading}>{loading ? 'Loading...' : 'Submit'}</button>

   // After
   <LoadingButton isLoading={loading}>Submit</LoadingButton>
   ```
2. Add skeleton loaders:
   ```tsx
   {loading ? (
     <SkeletonLoader variant="card" count={3} />
   ) : (
     <GameList games={games} />
   )}
   ```
3. Implement optimistic updates:
   ```tsx
   const { mutate } = useSWR('/api/chats');

   const sendMessage = async (content) => {
     // Optimistic update
     mutate([...messages, tempMessage], false);

     try {
       await api.post('/messages', { content });
       mutate(); // Revalidate
     } catch (err) {
       mutate(); // Rollback
     }
   };
   ```

## 📦 Files
- Update all form components
- Update data-loading components
- Enhance `SkeletonLoader` variants

## ⏱️ Effort: **0.5 day** | **Sprint 2** | **Priority**: 🟡 Medium
