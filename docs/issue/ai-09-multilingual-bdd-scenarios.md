# AI-09: Multi-language Embeddings - BDD Scenarios

## Feature: Multi-language PDF Upload and Search

### Scenario 1: Auto-detect Italian PDF and index with correct language
**Given** a user uploads an Italian PDF rulebook without specifying language
**When** the system processes the PDF
**Then** the language detection service identifies the language as "it"
**And** the PDF is indexed in Qdrant with language metadata "it"
**And** embeddings are generated using the multilingual model
**And** the user sees "Detected: Italiano" in the UI

### Scenario 2: Manual language selection overrides auto-detection
**Given** a user uploads a German PDF
**And** manually selects "Deutsch" from the language dropdown
**When** the system processes the PDF
**Then** the language detection is skipped
**And** the PDF is indexed with language "de"
**And** the system uses the manually selected language

### Scenario 3: Language-filtered search returns only matching documents
**Given** the system has indexed PDFs in multiple languages:
  - Game A: English PDF
  - Game B: Italian PDF
  - Game C: Spanish PDF
**When** a user asks a question in Italian for Game B
**Then** the RAG service searches only Italian documents
**And** returns results from Game B's Italian PDF
**And** does not return results from English or Spanish PDFs

### Scenario 4: Local embedding service generates multilingual embeddings
**Given** the local Python embedding service is running
**And** a user uploads a French PDF
**When** the system generates embeddings for text chunks
**Then** the local embedding service is called with language="fr"
**And** embeddings are generated using multilingual-e5-large model
**And** embeddings are stored in Qdrant

### Scenario 5: Fallback to OpenRouter when local service unavailable
**Given** the local Python embedding service is not running
**And** a user uploads a Spanish PDF
**When** the system tries to generate embeddings
**Then** the local embedding attempt fails
**And** the system automatically falls back to OpenRouter
**And** OpenRouter generates embeddings with multilingual-e5-large
**And** the PDF is successfully indexed
**And** a warning is logged: "Local embedding failed, falling back to OpenRouter"

### Scenario 6: Unsupported language defaults to English
**Given** a user uploads a PDF in Japanese (unsupported language)
**When** the language detection service analyzes the text
**Then** the system detects an unsupported language
**And** defaults to language="en"
**And** uses the standard English embedding model
**And** logs a message: "Unsupported language 'ja', defaulting to 'en'"

### Scenario 7: Empty or invalid text returns error
**Given** a user uploads a corrupted PDF with no extractable text
**When** the language detection service tries to analyze the text
**Then** the service returns an error
**And** the upload fails with message: "Unable to detect language from PDF"

### Scenario 8: Multi-language support maintains backward compatibility
**Given** existing English PDFs are already indexed without language metadata
**When** a user queries an old English document
**Then** the system assumes language="en" for documents without metadata
**And** searches successfully return results
**And** no existing functionality is broken

## Test Data Requirements

### Sample Texts for Language Detection Tests

**English**:
> "Players take turns placing their pieces on the board. The first player to align three pieces in a row wins the game."

**Italian**:
> "I giocatori a turno posizionano i loro pezzi sulla scacchiera. Il primo giocatore ad allineare tre pezzi in fila vince la partita."

**German**:
> "Die Spieler setzen abwechselnd ihre Spielfiguren auf das Brett. Der erste Spieler, der drei Figuren in einer Reihe ausrichtet, gewinnt das Spiel."

**French**:
> "Les joueurs placent à tour de rôle leurs pièces sur le plateau. Le premier joueur à aligner trois pièces dans une rangée remporte la partie."

**Spanish**:
> "Los jugadores colocan sus piezas en el tablero por turnos. El primer jugador que alinee tres piezas en una fila gana el juego."

## Acceptance Criteria Mapping

| AC# | Description | Scenario Coverage |
|-----|-------------|-------------------|
| AC1 | Language detection during ingestion | Scenario 1, 6 |
| AC2 | Language-specific embedding models | Scenario 4, 5 |
| AC3 | Language metadata in Qdrant | Scenario 1, 3 |
| AC4 | Language filter in RAG search | Scenario 3 |
| AC5 | Language selector in upload wizard | Scenario 2 |
| AC6 | Detected language display | Scenario 1 |
| AC7 | Fallback to OpenRouter | Scenario 5 |
| AC8 | Backward compatibility | Scenario 8 |

## Edge Cases

1. **Mixed-language documents**: PDF con testo in più lingue → Detect dominant language
2. **Very short text**: < 50 chars → May fail detection → Default to EN
3. **Scanned PDF with OCR**: OCR text quality impacts language detection
4. **Model download failure**: First startup → Timeout → Fallback to OpenRouter
5. **GPU not available**: Embedding service falls back to CPU (slower but functional)

## Performance Targets

| Metric | Target | Measured By |
|--------|--------|-------------|
| Language detection accuracy | > 95% | Unit tests with sample texts |
| Local embedding time (CPU) | < 3s per chunk | Integration tests |
| Local embedding time (GPU) | < 1s per chunk | Integration tests |
| Fallback success rate | 100% | Integration tests |
| Search precision (multilingual) | ≥ English baseline | RAG evaluation (AI-06) |

---

**Generated**: 2025-10-19
**Issue**: AI-09
**Status**: BDD Planning Complete
