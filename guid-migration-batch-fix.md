# GUID Migration Batch Fix Strategy

## Error Pattern Analysis
All errors follow the same pattern:
- **Method signatures**: `string userId` → `Guid userId`
- **Entity assignments**: Remove `.ToString()` from `Guid.NewGuid().ToString()` → `Guid.NewGuid()`
- **DTO mappings**: Add `.ToString()` when mapping Guid entity properties to string DTOs
- **Comparisons**: Remove `.ToString()` from Guid comparisons
- **Claims/Logging**: Add `.ToString()` only for claims and logging contexts

## Files to Fix (Priority Order by Error Count)

### High Priority (20+ errors each)
1. ✅ **RuleSpecService.cs** (~25 errors)
2. ✅ **PromptTemplateService.cs** (~20 errors)
3. ✅ **PromptManagementService.cs** (~15 errors)
4. ✅ **PdfStorageService.cs** (~12 errors)
5. ✅ **PromptEvaluationService.cs** (~10 errors)
6. ✅ **SetupGuideService.cs** (~10 errors)
7. ✅ **RuleSpecCommentService.cs** (~8 errors)
8. ✅ **RuleCommentService.cs** (~5 errors)
9. ✅ **TempSessionService.cs** (~5 errors)
10. ✅ **PasswordResetService.cs** (~3 errors)

Total estimated errors: ~117

## Fix Application Strategy
Due to token limits, I'll:
1. Create comprehensive fix for each file
2. Execute fixes in priority order
3. Validate after each batch
4. Run build to confirm error reduction

## Next Steps
1. Fix RuleSpecService.cs (25 errors)
2. Fix PromptTemplateService.cs (20 errors)
3. Fix PromptManagementService.cs (15 errors)
4. Run build → validate ~60 errors eliminated
5. Continue with remaining files
