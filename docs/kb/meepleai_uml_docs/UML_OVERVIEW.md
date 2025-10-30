# UML Overview — MeepleAI (estratto dal codice)

**Origine**: analisi esclusivamente del codice nel repository `meepleai-monorepo` (C# API + MCP Python).  
**Generato**: 2025-10-30T06:16:38.179959Z

## Mappa dei diagrammi
- `diagrams/class_model.puml` — Class Diagram (PlantUML) delle entità principali
- `diagrams/class_model.mmd` — Class Diagram (Mermaid) equivalente
- `diagrams/sequence_qa.puml` — Sequence Diagram: RAG QA `/agents/qa`
- `diagrams/sequence_explain.puml` — Sequence Diagram: Explain `/agents/explain`
- `diagrams/sequence_pdf_index.puml` — Sequence Diagram: PDF indicizzazione (servizi PDF/Qdrant) [Inference su nomi operazioni]
- `diagrams/component_architecture.puml` — Component Diagram (API, DB, Vector DB, MCP)
- `diagrams/deployment_cloud.puml` — Deployment Diagram (API, PostgreSQL, Qdrant, Storage, MCP containers)

## Ambito modellato (verificato nel codice)
- **Entità**: `GameEntity`, `AgentEntity`, `ChatEntity`, `ChatLogEntity`, `RuleSpecEntity`, `RuleAtomEntity`, `PdfDocumentEntity`, `TextChunkEntity`, `UserEntity`.
- **Endpoint AI**: `/agents/qa`, `/agents/qa/stream`, `/agents/explain`, `/agents/setup`, `/agents/feedback`, `/bgg/search`, `/bgg/games/{id}`, `/chess/*` (da `AiEndpoints`).  
- **Servizi** (tipi visti nelle dipendenze endpoint): `IRagService`, `IStreamingRagService`, `IStreamingQaService`, `ChatService`, `AiRequestLogService`, `IResponseQualityService`, `IFollowUpQuestionService`, `IFeatureFlagService`, `IBggApiService`, `IChessAgentService`, `IChessKnowledgeService`.

> Nota veridicità: le strutture e relazioni riportate sono derivate da file sorgente (C# e Python). Dove esplicitamente marcato **[Inference]**, si tratta di deduzioni non direttamente confermate da firme/metodi nel codice analizzato.
