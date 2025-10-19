# TEST-05 Phase 3: RuleSpec Comments API Tests

**Date:** 2025-10-19
**Status:** ✅ COMPLETED (ruleSpecComments API Fully Covered)
**Issue:** #444
**Branch:** `feature/test-05-phase3-rulespec-comments`

## Summary

Successfully added 17 comprehensive tests for api-enhanced.ts, achieving **85.41% coverage** (up from 80.2%). The primary goal was to cover the untested ruleSpecComments API (lines 354-386), which is now fully tested.

## Objective

Cover the ruleSpecComments API functions (getComments, createComment, updateComment, deleteComment) to push api-enhanced.ts coverage closer to the 90% target.

## Results

### Coverage Impact

**api-enhanced.ts:**
- **Before**: 80.2%
- **After**: **85.41%** (+5.21%)
- Statements: **85.41%**
- Branches: **87.5%**
- Functions: **93.33%**
- Lines: **86.81%**

### Test Results

✅ **All 42 tests passing** (12 skipped timer tests from main branch)
✅ **Total: 54 tests** (42 passing + 12 skipped)
✅ **No failures**

### Tests Added

**Total: 17 new tests**
- 12 ruleSpecComments API tests
- 5 edge case tests

## Implementation Details

### A. RuleSpec Comments API Tests (12 tests)

Added comprehensive BDD-style tests for all four ruleSpecComments API methods.

#### 1. getComments() - 3 tests

**Test 1: Fetch comments successfully with multiple comments**
```typescript
it('should fetch comments for a game and version successfully', async () => {
  // Given: API returns comments for a game with 2 comments (one with atomId, one without)
  // When: getComments is called
  // Then: Comments are returned with correct structure
});
```

**Test 2: Return null on 401 Unauthorized**
```typescript
it('should return null on 401 Unauthorized', async () => {
  // Given: API returns 401 Unauthorized
  // When: getComments is called without authentication
  // Then: null is returned (follows apiEnhanced.get() 401 handling)
});
```

**Test 3: Return empty comments array**
```typescript
it('should return empty comments array when no comments exist', async () => {
  // Given: API returns empty comments array (totalComments: 0)
  // When: getComments is called
  // Then: Empty comments array is returned
});
```

#### 2. createComment() - 3 tests

**Test 4: Create comment successfully with atomId**
```typescript
it('should create a comment successfully', async () => {
  // Given: API accepts comment creation with atomId
  // When: createComment is called with atomId and commentText
  // Then: Comment is created and returned with proper structure
});
```

**Test 5: Create general comment without atomId**
```typescript
it('should create a comment without atomId (general comment)', async () => {
  // Given: API accepts general comment (atomId = null)
  // When: createComment is called with null atomId
  // Then: General comment is created (not tied to specific rule atom)
});
```

**Test 6: Throw ApiError on 400 Bad Request**
```typescript
it('should throw ApiError on 400 Bad Request (validation error)', async () => {
  // Given: API rejects comment with validation error (empty commentText)
  // When/Then: createComment throws ApiError
});
```

#### 3. updateComment() - 3 tests

**Test 7: Update comment successfully**
```typescript
it('should update a comment successfully', async () => {
  // Given: API accepts comment update
  // When: updateComment is called with new commentText
  // Then: Comment is updated with updatedAt timestamp
});
```

**Test 8: Throw ApiError on 404 Not Found**
```typescript
it('should throw ApiError on 404 Not Found (comment not exists)', async () => {
  // Given: API returns 404 for non-existent comment
  // When/Then: updateComment throws ApiError
});
```

**Test 9: Throw ApiError on 403 Forbidden**
```typescript
it('should throw ApiError on 403 Forbidden (not owner)', async () => {
  // Given: API returns 403 when user tries to update another user's comment
  // When/Then: updateComment throws ApiError
});
```

#### 4. deleteComment() - 3 tests

**Test 10: Delete comment successfully**
```typescript
it('should delete a comment successfully', async () => {
  // Given: API accepts comment deletion (204 No Content)
  // When: deleteComment is called
  // Then: Comment is deleted (no return value)
});
```

**Test 11: Throw ApiError on 404 Not Found**
```typescript
it('should throw ApiError on 404 Not Found (comment not exists)', async () => {
  // Given: API returns 404 for non-existent comment
  // When/Then: deleteComment throws ApiError
});
```

**Test 12: Throw ApiError on 403 Forbidden**
```typescript
it('should throw ApiError on 403 Forbidden (not owner or admin)', async () => {
  // Given: API returns 403 when user tries to delete another user's comment
  // When/Then: deleteComment throws ApiError
});
```

### B. Edge Case Tests (5 tests)

Added edge case tests to increase coverage of utility functions and error handling paths.

#### Test 13: getApiBase() with "undefined" string
```typescript
it('should use fallback API base when NEXT_PUBLIC_API_BASE is "undefined" string', async () => {
  // Given: Env var is set to string "undefined" (not actual undefined)
  // When: Request made
  // Then: Request uses fallback base URL (http://localhost:8080)
});
```

#### Test 14: getApiBase() with "null" string
```typescript
it('should use fallback API base when NEXT_PUBLIC_API_BASE is "null" string', async () => {
  // Given: Env var is set to string "null" (not actual null)
  // When: Request made
  // Then: Request uses fallback base URL
});
```

#### Test 15: NetworkError after max retries exhausted
```typescript
it('should throw NetworkError after max retries exhausted on network failure', async () => {
  // Given: Network failures persist beyond max retries
  // When/Then: Throws NetworkError after exhausting retries
  // Verify: fetch was called multiple times (maxAttempts = 2)
});
```

#### Test 16: NetworkError with skipRetry=true
```typescript
it('should not retry network error when skipRetry is true', async () => {
  // Given: Network failure with skipRetry option
  // When/Then: Throws NetworkError immediately without retries
  // Verify: fetch called only once (no retries)
});
```

#### Test 17: AbortError handling
```typescript
it('should handle AbortError and throw NetworkError with timeout message', async () => {
  // Given: Request times out (AbortError)
  // When/Then: Throws NetworkError with timeout message
});
```

## Files Modified

- `apps/web/src/lib/__tests__/api-enhanced.test.ts` (+356 lines)
  - Added "Scenario: RuleSpec Comments API" describe block
  - Added "Scenario: Edge Cases and Utility Functions" describe block
  - 17 new comprehensive BDD-style tests
  - All tests follow Given-When-Then structure

## Coverage Analysis

### Covered Lines (✅)

**Lines 354-386: ruleSpecComments API**
- ✅ getComments() - all paths covered
- ✅ createComment() - all paths covered
- ✅ updateComment() - all paths covered
- ✅ deleteComment() - all paths covered

**Lines 10-12: getApiBase() partial**
- ✅ Env var with "undefined" string
- ✅ Env var with "null" string

**Lines 174-200: Network error handling**
- ✅ NetworkError after max retries
- ✅ NetworkError with skipRetry
- ✅ AbortError handling

### Remaining Uncovered Lines (⚠️)

**Line 13:** getApiBase() with empty string
- Edge case: env var is empty string ""
- Impact: Minor (fallback behavior)

**Lines 53-55:** createTimeoutController with existing signal
- Edge case: abort listener when existing signal is passed
- Impact: Minor (timeout + external abort coordination)

**Lines 219-239:** ApiError retry edge cases
- Complex retry logic paths with retryable ApiErrors
- Impact: Medium (error handling refinement)

## Gap Analysis: 85.41% → 90%

**Current Gap:** 4.59%

**To reach 90%, need to cover:**
1. **getApiBase() edge case** (line 13)
   - Test with empty string env var
   - Estimated impact: +0.26%

2. **createTimeoutController with existing signal** (lines 53-55)
   - Test timeout with external abort signal
   - Estimated impact: +0.78%

3. **ApiError retry paths** (lines 219-239)
   - Test retryable ApiError scenarios
   - Test retry logic with varying maxAttempts
   - Estimated impact: +3.55%

**Recommendation:** Adding 8-10 more edge case tests could reach 90% coverage. However, the current 85.41% represents significant progress and covers all business-critical paths (ruleSpecComments API).

## Test Patterns Used

### 1. BDD Structure (Given-When-Then)
All tests follow clear Given-When-Then comments for readability:
```typescript
it('should fetch comments for a game and version successfully', async () => {
  // Given: API returns comments for a game
  // When: getComments is called
  // Then: Comments are returned
});
```

### 2. Mock Setup with beforeEach
```typescript
describe('Scenario: RuleSpec Comments API', () => {
  beforeEach(() => {
    fetchMock = global.fetch as jest.MockedFunction<typeof fetch>;
    fetchMock.mockClear();
  });
  // ...
});
```

### 3. Dynamic Import for ruleSpecComments
```typescript
const { ruleSpecComments } = await import('../api-enhanced');
const result = await ruleSpecComments.getComments('chess-123', '1.0.0');
```

### 4. Comprehensive Mock Data
```typescript
const mockResponse = {
  gameId: 'chess-123',
  version: '1.0.0',
  comments: [
    {
      id: 'comment-1',
      atomId: 'atom-1',  // Comment on specific rule atom
      commentText: 'Great rule!',
      // ... full structure
    },
    {
      id: 'comment-2',
      atomId: null,      // General comment (not tied to atom)
      commentText: 'Needs clarification',
      // ... full structure
    }
  ],
  totalComments: 2
};
```

## Next Steps (for 90% Coverage)

### Phase 4 Recommendations

1. **Add getApiBase() edge case test**
   ```typescript
   it('should use fallback when env var is empty string', async () => {
     process.env.NEXT_PUBLIC_API_BASE = '';
     // ...
   });
   ```

2. **Add createTimeoutController tests**
   ```typescript
   it('should handle existing signal abort during timeout', async () => {
     const controller = new AbortController();
     // Test timeout + external abort coordination
   });
   ```

3. **Add ApiError retry edge cases**
   ```typescript
   it('should retry on 503 Service Unavailable until max attempts', async () => {
     // ...
   });

   it('should apply exponential backoff on ApiError retries', async () => {
     // ...
   });
   ```

Estimated effort: 2-3 hours to reach 90% coverage.

## Conclusion

✅ **Primary Objective Achieved**: ruleSpecComments API fully covered (lines 354-386)
✅ **Coverage Improved**: 80.2% → 85.41% (+5.21%)
✅ **Quality Maintained**: All 42 tests passing, BDD structure, comprehensive scenarios
⚠️ **Coverage Gap**: 85.41% (not 90%) - remaining 4.59% requires edge case tests

The ruleSpecComments API coverage was the primary goal and has been successfully completed. The remaining uncovered lines are complex edge cases that would require additional mocking effort.

---

**Prepared by:** Claude Code
**Methodology:** BDD testing with Given-When-Then structure
**Test Count:** 17 new tests (12 ruleSpecComments + 5 edge cases)
**Quality:** Production-ready, all tests passing
