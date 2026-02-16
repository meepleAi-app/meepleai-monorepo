# Changelog

All notable changes to MeepleAI are documented in this file.

## [Unreleased]

### Added

- **PDF Wizard** (Epic #4136): Multi-step wizard for creating games from PDF rulebooks
  - Upload PDF with automatic text extraction (3-stage pipeline: Unstructured, SmolDocling, Docnet)
  - AI-powered metadata extraction (title, year, players, time, age, description)
  - BoardGameGeek enrichment with conflict detection
  - Approval workflow for Editor role submissions
  - Chunked upload support for PDFs > 50 MB
  - 4 wizard endpoints: upload-pdf, extract-metadata, enrich-from-bgg, confirm-import

- **Playground POC** (Epics #4435, #4436): AI agent playground with SSE streaming
  - POC Strategy Selector for agent conversations
  - RAG Strategy Info Panel and debug console
  - Real-time cost tracking and pipeline timing

- **Enterprise Admin Dashboard** (Epic #3689): 7-section admin dashboard
  - Overview, Resources, Operations, AI Platform, Users & Content, Business, Simulations

- **MeepleCard System** (Epic #3820): Unified card component for all entity displays
  - 6 modular features: Wishlist, QuickActions, Status, HoverPreview, Drag, BulkSelect
  - Game detail page with glassmorphic design

### Performance

- PDF extraction: 29 performance tests covering stage timing, concurrent load (20 sessions), large file handling (#4143)
- Performance report: `docs/05-testing/performance/pdf-wizard-performance-report.md`

### Documentation

- PDF Wizard admin guide, developer docs, frontend component docs (#4144)
- SharedGameCatalog README updated with wizard section
