# Phase 3: KnowledgeBase - Mapping Layer TODOs

**Status**: Domain + Application layers complete, Infrastructure layer needs mapping implementation
**Build Status**: 7 compilation errors (expected - mapping not yet implemented)
**Estimated Time**: 3-4 hours to complete mapping layer

---

## Current Situation

### ✅ Completed (75%)
- **Domain Layer**: Entities, Value Objects, Domain Services - fully functional
- **Application Layer**: Commands, Queries, DTOs, Handlers - well structured
- **Infrastructure Interfaces**: Repository and adapter interfaces defined

### ⏳ Remaining (25%)
- **Mapping Layer**: Bridge between domain entities (Guid IDs) and persistence entities (string IDs)
- **Infrastructure Implementation**: Complete repository implementations with mapping logic

---

## Compilation Errors (7 total)

### 1. Type Mismatch: Guid vs String IDs (4 errors)

**VectorDocumentRepository.cs:61,77**
```csharp
// ERROR: Cannot apply '==' to 'Guid' and 'string'
.FirstOrDefaultAsync(vd => vd.Id == id.ToString(), cancellationToken);
```

**Fix Required**:
```csharp
// Convert Guid to string for EF query
.FirstOrDefaultAsync(vd => vd.Id == id.ToString(), cancellationToken);
```

**EmbeddingRepository.cs:87**
```csharp
// ERROR: Cannot apply '==' to 'Guid' and 'string'
.Where(vd => vd.GameId == gameId.ToString())
```

**Fix Required**:
```csharp
.Where(vd => vd.GameId == gameId.ToString())
```

### 2. Missing Property: SourceDocumentId (1 error)

**VectorDocumentRepository.cs:78**
```csharp
// ERROR: VectorDocumentEntity does not contain 'SourceDocumentId'
vd.SourceDocumentId == sourceDocumentId.ToString()
```

**Investigation Needed**:
- Check actual VectorDocumentEntity schema in Infrastructure/Entities/
- Property might be named differently (e.g., PdfDocumentId, SourceId)
- May need to add property to entity if missing

### 3. Type Conversion: EmbeddingResult to float[] (1 error)

**SearchQueryHandler.cs:57**
```csharp
// ERROR: Cannot convert from 'EmbeddingResult' to 'float[]'
var queryVector = new Vector(queryEmbedding);
```

**Fix Required**:
```csharp
// EmbeddingResult likely has a property like .Embedding or .Vector
var queryVector = new Vector(queryEmbedding.Embedding); // or .Vector
```

### 4. Missing Method: KeywordSearchAsync (1 error)

**SearchQueryHandler.cs:116**
```csharp
// ERROR: IHybridSearchService does not contain 'KeywordSearchAsync'
var keywordSearchResults = await _hybridSearchService.KeywordSearchAsync(...)
```

**Investigation Needed**:
- Check IHybridSearchService interface in Services/
- Method might have different name (e.g., SearchByKeywordAsync, PerformKeywordSearch)
- May need to extract keyword search logic from existing methods

### 5. Missing Method: GenerateResponseAsync (1 error)

**AskQuestionQueryHandler.cs:81**
```csharp
// ERROR: ILlmService does not contain 'GenerateResponseAsync'
var llmResponse = await _llmService.GenerateResponseAsync(...)
```

**Investigation Needed**:
- Check ILlmService interface in Services/
- Method likely named differently (e.g., GenerateAsync, CallAsync, CompleteAsync)
- Adjust handler to use actual method signature

---

## Implementation Checklist

### Step 1: Investigate Existing Infrastructure (30 min)

- [ ] Read `Infrastructure/Entities/VectorDocumentEntity.cs` - verify properties
- [ ] Read `Services/IQdrantService.cs` - understand actual interface
- [ ] Read `Services/IHybridSearchService.cs` - find keyword search method
- [ ] Read `Services/ILlmService.cs` - find LLM generation method
- [ ] Read `Services/IEmbeddingService.cs` - check EmbeddingResult structure

### Step 2: Create Mapping Extensions (1 hour)

Create: `Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs`

```csharp
public static class KnowledgeBaseMappers
{
    // Domain VectorDocument → EF VectorDocumentEntity
    public static VectorDocumentEntity ToEntity(this VectorDocument domain);

    // EF VectorDocumentEntity → Domain VectorDocument
    public static VectorDocument ToDomain(this VectorDocumentEntity entity);

    // Qdrant search result → Domain Embedding
    public static Embedding ToEmbedding(this QdrantSearchResult result);

    // Domain Embedding → Qdrant point
    public static QdrantPoint ToQdrantPoint(this Embedding embedding);
}
```

### Step 3: Update Repository Implementations (1.5 hours)

- [ ] **VectorDocumentRepository.cs**:
  - Implement GetByIdAsync with entity → domain mapping
  - Implement GetByGameAndSourceAsync with proper property names
  - Implement AddAsync with domain → entity mapping
  - Implement UpdateAsync with domain → entity mapping
  - Fix DeleteAsync and ExistsAsync Guid → string conversions

- [ ] **EmbeddingRepository.cs**:
  - Fix GetCountByGameIdAsync Guid → string conversion
  - Implement GetByIdAsync using QdrantAdapter
  - Implement GetByVectorDocumentIdAsync
  - Implement GetByGameIdAsync

### Step 4: Implement Adapter (1 hour)

- [ ] **QdrantVectorStoreAdapter.cs**:
  - Map Vector domain object to float[] for Qdrant
  - Map Qdrant search results to Embedding domain entities
  - Implement SearchAsync with proper type conversions
  - Implement IndexBatchAsync with domain → Qdrant mapping
  - Implement DeleteByVectorDocumentIdAsync
  - Implement collection management methods

### Step 5: Fix Handler Issues (30 min)

- [ ] **SearchQueryHandler.cs**:
  - Fix line 57: Extract embedding array from EmbeddingResult
  - Fix line 116: Use correct IHybridSearchService method name
  - Adjust parameter types to match existing interfaces

- [ ] **AskQuestionQueryHandler.cs**:
  - Fix line 81: Use correct ILlmService method name
  - Adjust parameters to match existing interface signature

### Step 6: Testing (1 hour)

- [ ] Build verification: `dotnet build` should succeed
- [ ] Add unit tests for mappers
- [ ] Add integration tests for repositories
- [ ] Verify handlers work with existing services

---

## Expected Final Structure

```
BoundedContexts/KnowledgeBase/
├── Domain/                              ✅ COMPLETE
│   ├── Entities/                        ✅ (VectorDocument, Embedding, SearchResult)
│   ├── ValueObjects/                    ✅ (Vector, Confidence, Citation)
│   ├── Services/                        ✅ (VectorSearch, RrfFusion, QualityTracking)
│   └── Repositories/                    ✅ (Interfaces only)
├── Application/                         ✅ COMPLETE
│   ├── Commands/                        ✅ (IndexDocumentCommand)
│   ├── Queries/                         ✅ (SearchQuery, AskQuestionQuery)
│   ├── DTOs/                            ✅ (SearchResultDto, QaResponseDto, etc.)
│   └── Handlers/                        ⏳ (Need method name fixes)
└── Infrastructure/                      ⏳ IN PROGRESS
    ├── Persistence/
    │   ├── Mappers/                     ❌ TODO: KnowledgeBaseMappers.cs
    │   ├── VectorDocumentRepository.cs  ⏳ (Need mapping implementation)
    │   └── EmbeddingRepository.cs       ⏳ (Need mapping implementation)
    └── External/
        ├── IQdrantVectorStoreAdapter.cs ✅ (Interface complete)
        └── QdrantVectorStoreAdapter.cs  ⏳ (Need implementation)
```

---

## Alternative: Pragmatic Completion

If full DDD mapping proves too complex, consider:

### Quick Win: Use Existing Services Directly

Instead of adapters and mapping, handlers could:
1. Inject existing `IRagService`, `IQdrantService`, `IEmbeddingService`
2. Use domain services for **business logic only** (search, fusion, quality)
3. Delegate to existing infrastructure services for **persistence**
4. Map only at handler boundaries (DTOs in/out)

**Pros**:
- Compiles immediately
- Domain logic still organized in services
- No mapping layer complexity
- Progressive migration possible

**Cons**:
- Not "pure" DDD (domain depends on infrastructure)
- Tighter coupling to existing services
- Less flexibility for future changes

---

## Next Session Plan

1. **Read existing infrastructure** (30 min) - understand actual types
2. **Choose approach**:
   - **A**: Full mapping layer (3-4 hours, pure DDD)
   - **B**: Pragmatic hybrid (1 hour, good enough for alpha)
3. **Implement chosen approach**
4. **Test and verify build**
5. **Commit Phase 3 complete**

---

**Document Created**: 2025-11-11
**Author**: DDD Phase 3 Recovery Session
**Estimated Completion**: 3-4 hours for full DDD, 1 hour for pragmatic hybrid
