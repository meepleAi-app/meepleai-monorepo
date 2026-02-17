# MeepleAI RAG Architecture — Design Philosophy

## The Style

**Warm Boardgame Aesthetic** - The visual language draws from MeepleAI's brand identity: warm, friendly, and approachable while maintaining technical credibility. This isn't a cold technical diagram—it's an invitation to understand how the system works.

## Color Palette

The palette reflects MeepleAI's brand tokens:

| Color | Hex | Usage |
|-------|-----|-------|
| **Orange Primary** | `#d2691e` | Main accent, flow arrows, primary actions |
| **Purple Accent** | `#8b5cf6` | AI/Intelligence elements, LLM blocks |
| **Warm Background** | `#faf8f3` | Page background (cream/paper feel) |
| **Blue Info** | `#3b82f6` | Vector/Semantic operations |
| **Amber Warning** | `#f59e0b` | Keyword/FTS operations |
| **Green Success** | `#22c55e` | Local services, success states |
| **Red External** | `#ef4444` | External APIs, paid services |

## Typography

- **Headings**: Outfit Bold - friendly geometric sans-serif
- **Body**: WorkSans - clean, readable body text
- **Code/Data**: JetBrains Mono - monospace for technical values

## Visual Principles

### 1. Flow-Based Layout
The architecture follows a left-to-right flow with numbered steps (1-6), making the data journey immediately comprehensible:
1. User Query → 2. Embedding → 3. Hybrid Search → 4. Context → 5. LLM Generation → 6. Response

### 2. Card-Based Components
Each processing stage is a distinct card with:
- Subtle shadow for depth
- Color-coded top bar indicating function
- Icon identifier
- Multi-line description of what happens

### 3. Hierarchy Through Size
- Main flow blocks: Large, prominent
- Database layer: Medium, supporting
- Service blocks: Compact, reference

### 4. Connection Lines
- Solid orange arrows: Main data flow
- Dashed colored lines: Database connections
- Internal gray dashes: Sub-component relationships

## Infographic Contents

### Main Flow (6 Steps)

**1. User Query**
- Natural language question from user
- Example: "Quali sono le regole del combattimento in Gloomhaven?"

**2. Embedding**
- Converts text to 3072-dimensional vector
- Model: text-embedding-3-large
- Metric: cosine similarity

**3. Hybrid Search**
- **Semantic**: Qdrant vector search (HNSW index)
- **Keyword**: PostgreSQL FTS (tsvector + GIN)
- **RRF Fusion**: k=60, vector:0.7, keyword:0.3

**4. Context Assembly**
- Assembles Top-K relevant chunks
- Orders by RRF score
- Removes duplicates
- Formats for LLM consumption

**5. LLM Generation**
- **Ollama** (LOCAL): llama3.3:70b - FREE, streaming
- **OpenRouter** (EXTERNAL): gpt-4o-mini, Claude - paid fallback
- Circuit Breaker: 5 fails → 30s open → auto-failover

**6. Response**
- Streamed AI response
- Includes citations and page references

### Data Layer
- **Qdrant** :6333 - Vector DB (3072 dims, HNSW)
- **PostgreSQL** :5432 - FTS + Storage (tsvector, GIN)
- **Redis** :6379 - Response cache (TTL per tier)

### Docker Services
- **Ollama** :11434 - Local LLM
- **Embedding** :8000 - text-embedding-3-large
- **SmolDocling** :8002 - VLM PDF extraction
- **Reranker** :8001 - bge-reranker-v2-m3

## File Locations

- **PDF**: `docs/03-api/rag/meepleai-rag-architecture.pdf`
- **Web Public**: `apps/web/public/docs/meepleai-rag-architecture.pdf`
- **Access URL**: `/docs/meepleai-rag-architecture.pdf`

---

*Generated: 2026-02-04*
*Style: MeepleAI Brand Guidelines*
