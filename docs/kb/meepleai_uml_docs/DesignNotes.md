# Design Notes

## Pattern e scelte principali
- **Endpoint-driven services**: gli endpoint in `AiEndpoints` orchestrano servizi specializzati (RAG, Streaming, Feedback, BGG, Chess). Ciò riduce accoppiamento nei controller e rende testabili i casi d’uso.
- **Hybrid Search** (AI-14): `AskWithHybridSearchAsync` usa una modalità di ricerca configurabile che combina retriever vettoriale + full‑text (supportata da `TextChunkEntity.SearchVector`).
- **Quality Scoring**: calcolo punteggi qualitativi su risposta combinando confidenza RAG/LLM/citazioni (via `IResponseQualityService`). Utile per metriche SLO di risposta.
- **Chat persistence**: ogni interazione è tracciata su `ChatEntity` e `ChatLogEntity`, con soft delete, invalidation e audit trail.
- **Feature Flags**: gating per streaming e setup guide (riduce rischi di regressioni).

## Alternative valutate (alto livello)
- **Controller MVC classici** vs Minimal APIs: gli endpoint minimal riducono boilerplate e semplificano dipendenze. Contro: minor separazione “controller/service”.  
- **Solo vettoriale** vs **ibrido**: l’ibrido migliora recall/precision in presenza di PDF non sempre omogenei. Contro: maggior complessità operativa (trigger, sincronizzazione chunk).  
- **Persistenza chat su store NoSQL**: avrebbe semplificato append-only; EF Core garantisce invece integrità e join con entità di dominio.

## Implicazioni per manutenzione/scalabilità/sicurezza
- **Scalabilità**: separare RAG/Streaming/Feedback consente scaling indipendente. Qdrant può essere scalato con shard/replica.  
- **Manutenzione**: entità coese e naming consistente facilitano migrazioni EF. Attenzione a `ProcessingStatus` nei PDF (stati transitori).  
- **Sicurezza**: enforcement di sessione in tutti gli endpoint; attenzione ai log con dati sensibili (limitare snippet a 500 char). 2FA supportato in `UserEntity`.
