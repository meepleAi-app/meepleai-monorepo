# CHESS-03: Chess Knowledge Indexing Implementation

## Overview

This document describes the implementation of CHESS-03, which adds chess knowledge indexing to the MeepleAI vector database to support RAG (Retrieval-Augmented Generation) queries.

## Issue Details

- **Issue**: #310
- **Title**: CHESS-03 - Indicizzazione conoscenza scacchistica
- **Type**: Feature
- **Priority**: P1
- **Effort**: 3
- **Dependencies**: AI-01, CHESS-02

### Description
Generate embeddings and index chess knowledge (rules, openings, tactics, and middle-game strategies) into Qdrant to support RAG.

### Acceptance Criteria
- Vector queries filtered by "chess" category
- Retrieval testing on 10 sample questions with precision >0.8

## Implementation Details

### 1. Chess Knowledge Data Structure

Created comprehensive chess knowledge in JSON format at `apps/api/src/Api/Data/ChessKnowledge.json`:

- **Rules**: 11 items covering basic moves, special rules, and game endings
- **Openings**: 8 items covering popular opening systems (Italian, Ruy Lopez, Sicilian, French, Queen's Gambit, etc.)
- **Tactics**: 10 items covering fundamental tactical patterns (fork, pin, skewer, discovered attack, etc.)
- **Middlegame Strategies**: 11 items covering positional concepts (pawn structures, piece coordination, strategic plans)

**Total**: 40 comprehensive chess knowledge items

### 2. Vector Database Enhancement

#### QdrantService Updates (`QdrantService.cs`)

Added category-based filtering support:

1. **New payload index**: Added "category" field index for efficient filtering
2. **New methods**:
   - `IndexChunksWithMetadataAsync()`: Index chunks with custom metadata including category
   - `SearchByCategoryAsync()`: Search filtered by category field
   - `DeleteByCategoryAsync()`: Delete all vectors in a category

#### Interface Update (`IQdrantService.cs`)

Extended the interface with three new methods to support category-based operations.

### 3. Chess Knowledge Service

Created `ChessKnowledgeService` (see`ChessKnowledgeService.cs` and `IChessKnowledgeService.cs`):

**Key Features**:
- Loads chess knowledge from JSON file
- Chunks text into 512-character segments with 50-character overlap
- Generates embeddings via OpenRouter API
- Indexes into Qdrant with metadata:
  - `category`: "chess"
  - `subcategory`: Knowledge category (e.g., "Basic Rules", "King Pawn Openings")
  - `title`: Knowledge item title
  - `knowledge_type`: Type (rule, opening, tactic, middlegame_strategy)

**Methods**:
- `IndexChessKnowledgeAsync()`: Index all chess knowledge
- `SearchChessKnowledgeAsync()`: Search chess knowledge by query
- `DeleteChessKnowledgeAsync()`: Remove all chess knowledge

### 4. API Endpoints

Added three new endpoints in `Program.cs`:

#### POST /chess/index
- **Auth**: Admin only
- **Action**: Index all chess knowledge from JSON file
- **Returns**: Success status, total items indexed, total chunks, category counts

#### GET /chess/search?q={query}&limit={limit}
- **Auth**: Authenticated users
- **Action**: Search chess knowledge with semantic similarity
- **Parameters**:
  - `q`: Query string (required)
  - `limit`: Max results (optional, default: 5)
- **Returns**: Search results with scores, text, and metadata

#### DELETE /chess/index
- **Auth**: Admin only
- **Action**: Delete all chess knowledge from vector database
- **Returns**: Success status

### 5. Testing

Created comprehensive test suite (`ChessKnowledgeServiceTests.cs`):

#### Unit Tests
- `IndexChessKnowledgeAsync_WhenFileNotFound_ReturnsFailure`: Validates error handling
- `SearchChessKnowledgeAsync_WithEmptyQuery_ReturnsFailure`: Validates input validation
- `SearchChessKnowledgeAsync_WithValidQuery_CallsEmbeddingAndQdrantServices`: Validates service interaction
- `DeleteChessKnowledgeAsync_CallsQdrantService`: Validates deletion

#### Sample Questions (10 questions for precision testing)

1. **Rules**:
   - How does a pawn move and capture?
   - What is castling and when can I do it?
   - Can you explain en passant?

2. **Openings**:
   - What is the Italian Game opening?
   - How should I play the Sicilian Defense?

3. **Tactics**:
   - What is a fork in chess?
   - Explain the difference between a pin and a skewer
   - What is a discovered attack?

4. **Strategies**:
   - What is an isolated queen pawn?
   - How should I use the bishop pair?

#### Precision Validation Test
- Integration test (skipped by default, requires real Qdrant + OpenRouter)
- Validates precision >0.8 requirement on 10 sample questions
- Should be run manually after indexing chess knowledge

### 6. Service Registration

Registered `IChessKnowledgeService` in `Program.cs` line 145.

## Usage Guide

### Indexing Chess Knowledge

```bash
# 1. Ensure Qdrant is running (port 6333)
docker compose up qdrant

# 2. Start the API
cd apps/api/src/Api
dotnet run

# 3. Login as admin and get session cookie
# 4. Call the index endpoint
POST http://localhost:8080/chess/index
Cookie: session=<your-session-token>

# Response:
{
  "success": true,
  "totalItems": 40,
  "totalChunks": 120,  # Approximate, depends on chunking
  "categoryCounts": {
    "Basic Rules": 6,
    "Special Moves": 2,
    "Game Endings": 3,
    "King Pawn Openings": 4,
    "Queen Pawn Openings": 3,
    "Flank Openings": 1,
    "Basic Tactics": 5,
    "Advanced Tactics": 3,
    "Mating Patterns": 2,
    "Pawn Structure": 3,
    "Piece Coordination": 3,
    "Strategic Plans": 3,
    "King Safety": 2
  }
}
```

### Searching Chess Knowledge

```bash
GET http://localhost:8080/chess/search?q=How%20does%20the%20knight%20move&limit=3
Cookie: session=<your-session-token>

# Response:
{
  "success": true,
  "results": [
    {
      "score": 0.92,
      "text": "Knights move in an L-shape: two squares in one direction...",
      "page": 1,
      "chunkIndex": 0
    },
    {
      "score": 0.85,
      "text": "The knight is especially effective at forking...",
      "page": 1,
      "chunkIndex": 1
    }
  ]
}
```

### Deleting Chess Knowledge

```bash
DELETE http://localhost:8080/chess/index
Cookie: session=<your-session-token>

# Response:
{
  "success": true
}
```json
## Testing Precision

To validate the precision >0.8 requirement:

1. Index the chess knowledge using `POST /chess/index`
2. Run each of the 10 sample questions through `GET /chess/search`
3. For each question, check that at least one result has `score >= 0.8`
4. Calculate precision: `(questions with relevant results) / 10`
5. Validate precision > 0.8

## Files Modified/Created

### Created
- `apps/api/src/Api/Data/ChessKnowledge.json` - Chess knowledge data
- `apps/api/src/Api/Services/IChessKnowledgeService.cs` - Service interface
- `apps/api/src/Api/Services/ChessKnowledgeService.cs` - Service implementation
- `apps/api/tests/Api.Tests/ChessKnowledgeServiceTests.cs` - Test suite
- `docs/chess-03-implementation.md` - This document

### Modified
- `apps/api/src/Api/Services/IQdrantService.cs` - Added category-based methods
- `apps/api/src/Api/Services/QdrantService.cs` - Implemented category support
- `apps/api/src/Api/Program.cs` - Registered service and added endpoints
- `apps/api/tests/Api.Tests/PdfStorageServiceIntegrationTests.cs` - Updated test mocks

## Architecture Notes

### Vector Storage Structure

Each chess knowledge chunk is stored in Qdrant with the following payload:

```json
{
  "category": "chess",
  "subcategory": "Basic Rules",
  "title": "Piece Movement - Knight",
  "knowledge_type": "rule",
  "chunk_index": 0,
  "text": "Knights move in an L-shape...",
  "page": 1,
  "char_start": 0,
  "char_end": 512,
  "indexed_at": "2025-10-09T12:00:00Z"
}
```

### Chunking Strategy

- **Chunk Size**: 512 characters
- **Overlap**: 50 characters
- **Boundary Detection**: Prefers sentence boundaries, falls back to word boundaries
- **Rationale**: Balances context preservation with embedding quality

### Metadata Strategy

- **category**: "chess" - Enables filtering all chess-related queries
- **subcategory**: Original category from JSON - Enables fine-grained filtering
- **title**: Knowledge item title - Useful for display/debugging
- **knowledge_type**: Type classification - Enables filtering by knowledge type

## Future Enhancements

1. **Multi-language support**: Add Italian translations for chess terms
2. **Advanced filtering**: Filter by knowledge_type (rules, openings, tactics, strategy)
3. **Knowledge updates**: Versioning system for updating chess knowledge
4. **User feedback**: Track which answers are most helpful
5. **RAG integration**: Direct integration with QA endpoint for chess queries
6. **Expanded content**: Add endgame theory, famous games, player strategies
7. **Visual diagrams**: Link to chess diagrams for better understanding

## Related Issues

- **AI-01**: Vector search infrastructure (dependency)
- **CHESS-02**: Chess game support (dependency)
- **CHESS-04**: Chess-specific RAG queries (potential next issue)

## Testing Checklist

- [x] Service compiles without errors
- [x] Unit tests pass
- [x] Mock interfaces updated
- [x] Endpoints registered and authenticated
- [x] 10 sample questions documented
- [x] Precision validation test created
- [ ] Integration test with real Qdrant (manual)
- [ ] Precision >0.8 validated (manual)
- [ ] Documentation complete
