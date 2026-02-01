# Appendix A: Research Sources Bibliography

Complete list of sources used in TOMAC-RAG research and design.

---

## Academic Papers (arXiv)

### RAG Architecture Evolution
1. **Modular RAG: Transforming RAG Systems into LEGO-like Reconfigurable Frameworks**
   - URL: https://arxiv.org/html/2407.21059v1
   - Key Contribution: 6 flow patterns, 3-tier modular design

2. **Agentic Retrieval-Augmented Generation: A Survey on Agentic RAG**
   - URL: https://arxiv.org/html/2501.09136v1
   - Key Contribution: Agentic RAG taxonomy and frameworks

3. **Agentic RAG with Knowledge Graphs for Complex Multi-Hop Reasoning**
   - URL: https://arxiv.org/abs/2507.16507
   - Key Contribution: Graph-based multi-hop retrieval

4. **Rerank Before You Reason: Analyzing Reranking Tradeoffs**
   - URL: https://arxiv.org/html/2601.14224
   - Key Contribution: Cost-accuracy analysis of reranking approaches

5. **Efficient Multi-Model Orchestration for Self-Hosted LLMs**
   - URL: https://arxiv.org/html/2512.22402v1
   - Key Contribution: Multi-objective model selection (21.7% accuracy, 33% latency, 25% cost improvements)

6. **A Hybrid RAG System with Comprehensive Enhancement on Complex Reasoning**
   - URL: https://arxiv.org/html/2408.05141v1
   - Key Contribution: Hybrid search with RRF (Reciprocal Rank Fusion)

7. **RQ-RAG: Learning to Refine Queries for Retrieval Augmented Generation**
   - URL: https://arxiv.org/html/2404.00610v1
   - Key Contribution: Learned query refinement through rewriting, decomposing, disambiguating

8. **Engineering the RAG Stack: A Comprehensive Review**
   - URL: https://arxiv.org/html/2601.05264
   - Key Contribution: RAG taxonomy across 5 dimensions (adaptivity, trust, modality, fusion, retrieval)

9. **Creating a Taxonomy for Retrieval Augmented Generation Applications**
   - URL: https://arxiv.org/html/2408.02854v4
   - Key Contribution: Practitioner-oriented RAG pattern classification

10. **Retrieval-Augmented Generation: Comprehensive Survey of Architectures**
    - URL: https://arxiv.org/html/2506.00054v1
    - Key Contribution: Complete RAG architecture survey

---

## Board Game Agent Research (from PDF)

### Multi-Agent Systems
11. **Belle et al. (2025)** - Agents of Change: Self-Evolving LLM Agents for Strategic Planning
    - URL: https://nbelle1.github.io/agents-of-change/
    - Key Contribution: HexMachina multi-agent system (Analyst, Researcher, Coder, Strategist, Player)

12. **Belle et al. (2025)** - HexMachina: Self-Evolving Multi-Agent System for Continual Learning of Catan
    - URL: https://openreview.net/forum?id=V0Fb4pwhS4
    - Key Contribution: Claude 3.7 and GPT-4o outperform static agents

### Search-Based Planning
13. **DeepMind (2025)** - Mastering Board Games by External and Internal Planning
    - URL: https://arxiv.org/abs/2412.12119
    - Key Contribution: LLM + MCTS achieves Grandmaster-level chess

### Rule Induction
14. **Celotti et al. (2026)** - Cogito, Ergo Ludo: An Agent that Learns to Play by Reasoning
    - URL: https://openreview.net/forum?id=w2vEo7NJ18
    - Key Contribution: LLM builds linguistic model of game world, induces rules from history

### Frameworks
15. **Baker et al. (2025)** - Boardwalk: Framework for Creating Board Games with LLMs
    - URL: https://chatpaper.com/paper/182874
    - Key Contribution: Abstract Game class with validate_move, Python for LLM familiarity

16. **Cipolina-Kun et al. (2025)** - Board Game Arena: Framework and Benchmark
    - URL: https://arxiv.org/html/2508.03368v1
    - Key Contribution: OpenSpiel integration, structured state prompts

17. **GoodStartLabs** - AI_Diplomacy: Frontier Models playing Diplomacy
    - URL: https://github.com/GoodStartLabs/AI_Diplomacy
    - Key Contribution: Multi-tier memory (diary + annual synthesis)

---

## Industry Articles & Blogs

### RAG Evolution
18. **The Rise and Evolution of RAG in 2024: A Year in Review**
    - URL: https://ragflow.io/blog/the-rise-and-evolution-of-rag-in-2024-a-year-in-review
    - Publisher: RAGFlow
    - Key Contribution: 2024 declared "year of the agent"

19. **Evolution of RAGs: Naive, Advanced, Modular**
    - URL: https://www.marktechpost.com/2024/04/01/evolution-of-rags-naive-rag-advanced-rag-and-modular-rag-architectures/
    - Publisher: MarkTechPost
    - Key Contribution: Clear paradigm evolution explanation

20. **14 Types of RAG (Retrieval-Augmented Generation)**
    - URL: https://www.meilisearch.com/blog/rag-types
    - Publisher: Meilisearch
    - Key Contribution: Comprehensive variant catalog

### Query Routing
21. **Building an Intelligent RAG System with Query Routing**
    - URL: https://dev.to/exploredataaiml/building-an-intelligent-rag-system-with-query-routing-validation-and-self-correction-2e4k
    - Publisher: DEV Community
    - Key Contribution: Production routing implementation patterns

22. **How Intent Classification Works in RAG Systems**
    - URL: https://alixaprodev.medium.com/how-intent-classification-works-in-rag-systems-15054d0ec5ce
    - Publisher: Medium
    - Key Contribution: Intent classification methods (semantic, LLM, keyword)

23. **Query-Adaptive RAG Routing Cuts Latency 35% While Improving Accuracy**
    - URL: https://ascii.co.uk/news/article/news-20260122-9ccbfc03/query-adaptive-rag-routing-cuts-latency-35-while-improving-a
    - Publisher: ASCII News
    - Key Contribution: 85-92% accuracy at <1ms latency benchmark

### CRAG
24. **Corrective RAG (CRAG) Implementation With LangGraph**
    - URL: https://www.datacamp.com/tutorial/corrective-rag-crag
    - Publisher: DataCamp
    - Key Contribution: Step-by-step CRAG implementation guide

25. **Corrective RAG (CRAG) Tutorial**
    - URL: https://langchain-ai.github.io/langgraph/tutorials/rag/langgraph_crag/
    - Publisher: LangChain
    - Key Contribution: Official LangGraph CRAG pattern

26. **Corrective RAG: Workflow, Implementation, and More**
    - URL: https://www.meilisearch.com/blog/corrective-rag
    - Publisher: Meilisearch
    - Key Contribution: CRAG workflow diagrams and examples

### Token Optimization
27. **RAG Cost Optimization: Cut Spending by 90%**
    - URL: https://app.ailog.fr/en/blog/guides/rag-cost-optimization
    - Publisher: Ailog
    - Key Contribution: 95% cost reduction strategies

28. **Context-Aware RAG System to Cut Token Costs and Boost Accuracy**
    - URL: https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/context-aware-rag-system-with-azure-ai-search-to-cut-token-costs-and-boost-accur/4456810
    - Publisher: Microsoft
    - Key Contribution: Anthropic prompt caching, contextual embeddings

29. **Best Practices for Optimizing Token Consumption**
    - URL: https://www.soeasie.com/blog/best-practices-for-optimizing-token-consumption-for-ai-chatbots-using-retrieval-augmented-generation-rag
    - Publisher: Easie
    - Key Contribution: 97% input token finding, optimization priorities

30. **The Hidden Cost of LangChain**
    - URL: https://dev.to/himanjan/the-hidden-cost-of-langchain-why-my-simple-rag-system-cost-27x-more-than-expected-4hk9
    - Publisher: DEV Community
    - Key Contribution: Framework overhead analysis (2.7x cost increase)

### Reranking & Embeddings
31. **How Using a Reranking Microservice Can Improve Accuracy and Costs**
    - URL: https://developer.nvidia.com/blog/how-using-a-reranking-microservice-can-improve-accuracy-and-costs-of-information-retrieval/
    - Publisher: NVIDIA
    - Key Contribution: 75x cost savings (cross-encoder vs LLM)

32. **Best Open-Source Embedding Models Benchmarked**
    - URL: https://supermemory.ai/blog/best-open-source-embedding-models-benchmarked-and-ranked/
    - Publisher: Supermemory
    - Key Contribution: MiniLM (14.7ms), E5/BGE (79-82ms) benchmarks

33. **Production Retrievers: ColBERT, SPLADE, E5/BGE**
    - URL: https://machine-mind-ml.medium.com/production-rag-that-works-hybrid-search-re-ranking-colbert-splade-e5-bge-624e9703fa2b
    - Publisher: Medium
    - Key Contribution: State-of-art retriever comparison

34. **Magic Behind Anthropic's Contextual RAG**
    - URL: https://www.analyticsvidhya.com/blog/2024/11/anthropics-contextual-rag/
    - Publisher: Analytics Vidhya
    - Key Contribution: Contextual embedding technique (30% token reduction)

### Production Best Practices
35. **Best Practices for Production-Scale RAG Systems**
    - URL: https://orkes.io/blog/rag-best-practices/
    - Publisher: Orkes
    - Key Contribution: Enterprise deployment patterns

36. **RAG Best Practices: Lessons from 100+ Technical Teams**
    - URL: https://www.kapa.ai/blog/rag-best-practices
    - Publisher: kapa.ai
    - Key Contribution: Real-world production learnings

37. **Building Production-Ready RAG Systems: Best Practices and Latest Tools**
    - URL: https://medium.com/@meeran03/building-production-ready-rag-systems-best-practices-and-latest-tools-581cae9518e7
    - Publisher: Medium
    - Key Contribution: 2024-2025 tooling recommendations

38. **Multi-provider LLM Orchestration in Production: A 2026 Guide**
    - URL: https://dev.to/ash_dubai/multi-provider-llm-orchestration-in-production-a-2026-guide-1g10
    - Publisher: DEV Community
    - Key Contribution: Multi-model routing patterns

### Query Augmentation
39. **Query Transformations: Multi-Query, Decomposition, Step-Back**
    - URL: https://dev.to/jamesli/in-depth-understanding-of-rag-query-transformation-optimization-multi-query-problem-decomposition-and-step-back-27jg
    - Publisher: DEV Community
    - Key Contribution: Pre-retrieval optimization techniques

40. **Advanced RAG: Query Expansion**
    - URL: https://haystack.deepset.ai/blog/query-expansion
    - Publisher: Haystack
    - Key Contribution: Synonym and related term expansion

41. **Advanced RAG: Query Decomposition & Reasoning**
    - URL: https://haystack.deepset.ai/blog/query-decomposition
    - Publisher: Haystack
    - Key Contribution: Breaking complex queries into sub-queries

### Advanced Techniques
42. **Document Hierarchy in RAG: Boosting AI Retrieval Efficiency**
    - URL: https://medium.com/@nay1228/document-hierarchy-in-rag-boosting-ai-retrieval-efficiency-aa23f21b5fb9
    - Publisher: Medium
    - Key Contribution: Parent-child document architecture

43. **Metadata Filtering for Better Contextual Results**
    - URL: https://unstructured.io/insights/how-to-use-metadata-in-rag-for-better-contextual-results
    - Publisher: Unstructured
    - Key Contribution: Self-query retrieval with structured filters

44. **5 Advanced RAG Architectures Beyond Traditional Methods**
    - URL: https://machinelearningmastery.com/5-advanced-rag-architectures-beyond-traditional-methods/
    - Publisher: Machine Learning Mastery
    - Key Contribution: Memory-augmented, context-aware feedback loops

45. **RAG Variants Explained: Classic, Graph, HyDE, RAG-Fusion**
    - URL: https://www.jellyfishtechnologies.com/rag-variants-explained-classic-rag-graph-rag-hyde-and-ragfusion/
    - Publisher: Jellyfish Technologies
    - Key Contribution: Variant comparison with use cases

### Multi-Agent Systems
46. **Multi-Agent RAG Framework for Entity Resolution**
    - URL: https://www.mdpi.com/2073-431X/14/12/525
    - Publisher: MDPI
    - Key Contribution: Sequential orchestration with specialized agents

47. **What is Agentic RAG**
    - URL: https://weaviate.io/blog/what-is-agentic-rag
    - Publisher: Weaviate
    - Key Contribution: Agent-controlled retrieval definition

48. **Building Agentic RAG Systems with LangGraph: The 2026 Guide**
    - URL: https://rahulkolekar.com/building-agentic-rag-systems-with-langgraph/
    - Publisher: Rahul Kolekar
    - Key Contribution: LangGraph implementation patterns

---

## Code Repositories & Tutorials

49. **RAG_Techniques - Comprehensive GitHub Repository**
    - URL: https://github.com/NirDiamant/RAG_Techniques
    - Key Contribution: 30+ RAG technique implementations with Jupyter notebooks

50. **Board Game Rules Explainer (Haystack)**
    - URL: https://github.com/rafaljanwojcik/board-game-rules-explainer
    - Key Contribution: Rulebook Q&A with Elasticsearch integration

51. **LangChain Plan-and-Execute Agents**
    - URL: https://blog.langchain.com/planning-agents/
    - Key Contribution: Planner LLM + executor pattern

52. **Mastering RAG: Build with LangChain and LangGraph in 2025**
    - URL: https://md-hadi.medium.com/mastering-rag-build-smarter-ai-with-langchain-and-langgraph-in-2025-cc126fb8a552
    - Publisher: Medium
    - Key Contribution: 2025 LangChain/LangGraph patterns

---

## PDF Research (Internal)

53. **"Approcci LLM per agenti di giochi da tavolo"**
    - Location: `data/pdfDocs/Approcci LLM per agenti di giochi da tavolo.pdf`
    - Key Contributions:
      - Belle et al. multi-agent architectures
      - DeepMind external/internal search
      - Cogito Ergo Ludo rule induction
      - RAG for rulebooks (Sam Miller)
      - Framework comparisons (Boardwalk, Board Game Arena, AI_Diplomacy)

---

## Total Source Count

- **Academic Papers**: 10
- **Industry Articles**: 30+
- **Code Repositories**: 5
- **Internal Research**: 1 PDF
- **Total**: 53 sources

**Research Depth**: Advanced (weeks of comprehensive investigation)
**Confidence Level**: High (multiple corroborating sources for key findings)
