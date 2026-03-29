/**
 * RAG Retrieval Strategy Detail Data
 *
 * Comprehensive documentation content for each retrieval strategy.
 * Used by the StrategyDetailModal component.
 */

import type { RetrievalStrategyType } from './types';

/**
 * Detailed strategy documentation content.
 */
export interface StrategyDetailContent {
  id: RetrievalStrategyType;
  title: string;
  icon: string;
  description: string[];
  pros: string[];
  cons: string[];
  useCases: string[];
  flowSteps: FlowStep[];
  example: StrategyExample;
  configurationLink?: string;
  technicalNotes?: string[];
}

/**
 * A step in the strategy flow diagram.
 */
export interface FlowStep {
  id: string;
  label: string;
  description: string;
  icon: string;
  color: string;
}

/**
 * Example query and response for a strategy.
 */
export interface StrategyExample {
  query: string;
  context: string;
  response: string;
  highlight?: string;
}

/**
 * Complete detail content for all retrieval strategies.
 */
export const STRATEGY_DETAILS: Record<RetrievalStrategyType, StrategyDetailContent> = {
  Hybrid: {
    id: 'Hybrid',
    title: 'Hybrid Search',
    icon: '🔀',
    description: [
      'Hybrid Search combines the strengths of both vector-based semantic search and traditional keyword-based search (BM25/TF-IDF) to deliver the best of both worlds.',
      'By merging results from both retrieval methods, Hybrid Search can understand the meaning behind queries while also matching exact terms and phrases that matter.',
      'This approach is particularly effective for board game rules where both conceptual understanding ("how to win") and exact terminology ("victory points") are important.',
    ],
    pros: [
      'Best overall retrieval quality for general queries',
      'Handles both conceptual and exact-match queries well',
      'Robust against vocabulary mismatch',
      'Good performance-to-cost ratio',
      'Fallback capability if one method fails',
    ],
    cons: [
      'Slightly higher latency than single-method approaches',
      'Requires tuning the alpha parameter for optimal results',
      'More complex to debug when results are unexpected',
      'Requires maintaining both vector and keyword indices',
    ],
    useCases: [
      'General game rule questions',
      'Setup instructions and component queries',
      'Strategy advice with specific game terminology',
      'FAQ-style queries about game mechanics',
    ],
    flowSteps: [
      {
        id: 'query',
        label: 'User Query',
        description: 'Natural language question',
        icon: '💬',
        color: 'blue',
      },
      {
        id: 'parallel',
        label: 'Parallel Search',
        description: 'Vector + Keyword simultaneously',
        icon: '⚡',
        color: 'purple',
      },
      {
        id: 'vector',
        label: 'Vector Search',
        description: 'Semantic embedding similarity',
        icon: '🧠',
        color: 'cyan',
      },
      {
        id: 'keyword',
        label: 'Keyword Search',
        description: 'BM25 term matching',
        icon: '🔑',
        color: 'green',
      },
      {
        id: 'fusion',
        label: 'Result Fusion',
        description: 'Reciprocal Rank Fusion (RRF)',
        icon: '🔀',
        color: 'orange',
      },
      {
        id: 'rerank',
        label: 'Reranking',
        description: 'Cross-encoder scoring',
        icon: '📊',
        color: 'red',
      },
    ],
    example: {
      query: 'How do I score points in Catan?',
      context: 'Player asking about victory conditions in Settlers of Catan',
      response:
        'In Catan, you score Victory Points (VPs) through: settlements (1 VP each), cities (2 VP each), Longest Road (2 VP), Largest Army (2 VP), and certain Development Cards. First to 10 VPs wins!',
      highlight:
        'Hybrid search found both semantic matches for "scoring" and exact matches for "Victory Points"',
    },
    technicalNotes: [
      'Default alpha=0.5 balances vector and keyword equally',
      'Uses pgvector (PostgreSQL) for vector search, PostgreSQL FTS for keywords',
      'RRF fusion with k=60 for result combination',
    ],
  },
  Semantic: {
    id: 'Semantic',
    title: 'Semantic Search',
    icon: '🧠',
    description: [
      'Semantic Search uses dense vector embeddings to find documents based on meaning rather than exact word matches.',
      'Queries and documents are converted into high-dimensional vectors using transformer models, then matched using cosine similarity or dot product.',
      'This approach excels at understanding paraphrased questions and finding conceptually related content.',
    ],
    pros: [
      'Excellent for meaning-based queries',
      'Handles paraphrasing and synonyms naturally',
      'Fast retrieval with optimized vector indices',
      'Low latency for simple queries',
      'Good for conceptual questions',
    ],
    cons: [
      'May miss exact terminology matches',
      'Sensitive to embedding model quality',
      'Can struggle with rare or domain-specific terms',
      'Requires quality embedding infrastructure',
    ],
    useCases: [
      'Conceptual questions about game mechanics',
      'Paraphrased rule queries',
      'Strategy and tactics questions',
      'Understanding game themes and objectives',
    ],
    flowSteps: [
      {
        id: 'query',
        label: 'User Query',
        description: 'Natural language question',
        icon: '💬',
        color: 'blue',
      },
      {
        id: 'embed',
        label: 'Query Embedding',
        description: 'Convert to vector',
        icon: '🔢',
        color: 'purple',
      },
      {
        id: 'search',
        label: 'Vector Search',
        description: 'ANN similarity search',
        icon: '🧠',
        color: 'cyan',
      },
      {
        id: 'retrieve',
        label: 'Retrieve Docs',
        description: 'Top-k similar chunks',
        icon: '📄',
        color: 'green',
      },
      {
        id: 'return',
        label: 'Return Results',
        description: 'Ranked by similarity',
        icon: '📊',
        color: 'orange',
      },
    ],
    example: {
      query: 'What happens when I run out of resources?',
      context: 'Player asking about resource management in various games',
      response:
        'When resources are depleted, you typically cannot perform actions requiring them until you acquire more. Some games have scarcity mechanics, trading systems, or resource regeneration phases.',
      highlight: 'Semantic search understood "run out" maps to scarcity and depletion concepts',
    },
    technicalNotes: [
      'Uses sentence-transformers/all-MiniLM-L6-v2 for embeddings',
      'pgvector HNSW index for fast ANN search',
      '768-dimensional vectors with cosine similarity (e5-base)',
    ],
  },
  Keyword: {
    id: 'Keyword',
    title: 'Keyword Search',
    icon: '🔑',
    description: [
      'Keyword Search uses traditional information retrieval algorithms like BM25 and TF-IDF to match documents based on exact term occurrences.',
      'This approach is highly effective for queries with specific terminology, names, or exact phrases that must appear in the results.',
      'Ideal for looking up specific rules, components, or game-specific vocabulary.',
    ],
    pros: [
      'Extremely fast retrieval',
      'Perfect for exact term matching',
      'No embedding computation required',
      'Predictable and debuggable results',
      'Low infrastructure cost',
    ],
    cons: [
      'Cannot understand synonyms or paraphrases',
      'Vocabulary mismatch reduces recall',
      'Less effective for conceptual queries',
      'Requires good keyword presence in documents',
    ],
    useCases: [
      'Looking up specific card names or components',
      'Finding rules with exact terminology',
      'Searching for specific phase or action names',
      'Component counts and specifications',
    ],
    flowSteps: [
      { id: 'query', label: 'User Query', description: 'Query text', icon: '💬', color: 'blue' },
      {
        id: 'tokenize',
        label: 'Tokenization',
        description: 'Extract terms',
        icon: '✂️',
        color: 'purple',
      },
      {
        id: 'bm25',
        label: 'BM25 Scoring',
        description: 'Term frequency analysis',
        icon: '📊',
        color: 'green',
      },
      {
        id: 'retrieve',
        label: 'Retrieve Docs',
        description: 'Ranked by score',
        icon: '📄',
        color: 'orange',
      },
      {
        id: 'return',
        label: 'Return Results',
        description: 'Top matches',
        icon: '✅',
        color: 'cyan',
      },
    ],
    example: {
      query: 'What is the "Robber" in Catan?',
      context: 'Player asking about a specific game component',
      response:
        'The Robber is a piece that blocks resource production on a hex. When a 7 is rolled, the active player must move the Robber to any hex (except desert) and steal one resource from an opponent with a settlement or city there.',
      highlight: 'Keyword search found exact "Robber" term matches in rulebook',
    },
    technicalNotes: [
      'PostgreSQL Full-Text Search with tsvector',
      'BM25 ranking with k1=1.2, b=0.75',
      'Supports phrase matching and boolean operators',
    ],
  },
  Contextual: {
    id: 'Contextual',
    title: 'Contextual Search',
    icon: '💬',
    description: [
      'Contextual Search extends standard retrieval by incorporating conversation history and user context into the search process.',
      'By understanding the ongoing dialogue, it can resolve references, maintain topic focus, and provide more relevant results.',
      'This approach is essential for multi-turn conversations where follow-up questions depend on previous context.',
    ],
    pros: [
      'Handles follow-up questions naturally',
      'Resolves pronouns and references',
      'Maintains conversation coherence',
      'Better user experience in dialogues',
      'Personalized to conversation flow',
    ],
    cons: [
      'Higher latency due to context processing',
      'Requires conversation state management',
      'Can be confused by topic switches',
      'More complex to implement and debug',
    ],
    useCases: [
      'Multi-turn rule clarifications',
      'Follow-up questions in game sessions',
      'Clarifying previous answers',
      'Progressive learning conversations',
    ],
    flowSteps: [
      {
        id: 'query',
        label: 'User Query',
        description: 'Current question',
        icon: '💬',
        color: 'blue',
      },
      {
        id: 'history',
        label: 'Load History',
        description: 'Previous turns',
        icon: '📜',
        color: 'purple',
      },
      {
        id: 'rewrite',
        label: 'Query Rewrite',
        description: 'Contextualize query',
        icon: '✏️',
        color: 'cyan',
      },
      {
        id: 'search',
        label: 'Enhanced Search',
        description: 'With context',
        icon: '🔍',
        color: 'green',
      },
      {
        id: 'filter',
        label: 'Context Filter',
        description: 'Topic relevance',
        icon: '🎯',
        color: 'orange',
      },
      {
        id: 'return',
        label: 'Return Results',
        description: 'Contextual matches',
        icon: '✅',
        color: 'red',
      },
    ],
    example: {
      query: 'What about the blue ones?',
      context: 'Previous question was about Catan resource cards',
      response:
        'The blue resource cards in Catan represent Ore. Ore is primarily used for building cities (3 Ore + 2 Wheat) and buying Development Cards (1 Ore + 1 Wheat + 1 Wool).',
      highlight:
        'Contextual search understood "blue ones" refers to Ore cards from conversation history',
    },
    technicalNotes: [
      'Uses Redis for conversation state storage',
      'LLM-based query rewriting for disambiguation',
      'Sliding window of last 5 turns for context',
    ],
  },
  MultiQuery: {
    id: 'MultiQuery',
    title: 'Multi-Query',
    icon: '🔄',
    description: [
      'Multi-Query expands a single user query into multiple reformulated queries to achieve comprehensive retrieval coverage.',
      'An LLM generates alternative phrasings, related questions, and different perspectives on the original query.',
      'Results from all generated queries are combined and deduplicated for thorough information retrieval.',
    ],
    pros: [
      'Comprehensive coverage of topic',
      'Overcomes vocabulary limitations',
      'Finds information the user might not have thought to ask',
      'Better recall for complex questions',
      'Reduces chance of missing relevant docs',
    ],
    cons: [
      'Higher latency from multiple searches',
      'Increased LLM costs for query expansion',
      'May retrieve tangentially related content',
      'Requires good deduplication logic',
    ],
    useCases: [
      'Complex strategy questions',
      'Comprehensive rule explanations',
      'Research-style queries',
      'When initial results are insufficient',
    ],
    flowSteps: [
      {
        id: 'query',
        label: 'User Query',
        description: 'Original question',
        icon: '💬',
        color: 'blue',
      },
      {
        id: 'expand',
        label: 'Query Expansion',
        description: 'LLM generates variants',
        icon: '🤖',
        color: 'purple',
      },
      {
        id: 'parallel',
        label: 'Parallel Search',
        description: 'All queries simultaneously',
        icon: '⚡',
        color: 'cyan',
      },
      {
        id: 'collect',
        label: 'Collect Results',
        description: 'From all queries',
        icon: '📥',
        color: 'green',
      },
      {
        id: 'dedup',
        label: 'Deduplicate',
        description: 'Remove duplicates',
        icon: '🔄',
        color: 'orange',
      },
      {
        id: 'rank',
        label: 'Final Ranking',
        description: 'Combined relevance',
        icon: '📊',
        color: 'red',
      },
    ],
    example: {
      query: 'How do alliances work in Risk?',
      context: 'Player asking about diplomacy mechanics',
      response:
        'In Risk, alliances are informal agreements between players. While there are no official alliance rules, players often form temporary pacts for mutual defense, coordinate attacks, or agree not to attack each other. These alliances can be broken at any time, making diplomacy a key strategic element.',
      highlight:
        'Multi-Query expanded to: "alliance rules", "diplomacy in Risk", "player agreements", "breaking alliances"',
    },
    technicalNotes: [
      'Generates 3-5 query variants using Claude Haiku',
      'Parallel execution with Promise.all',
      'Reciprocal Rank Fusion for result combination',
    ],
  },
  Agentic: {
    id: 'Agentic',
    title: 'Agentic RAG',
    icon: '🤖',
    description: [
      'Agentic RAG employs autonomous AI agents that can plan, reason, and execute multi-step retrieval operations.',
      'Unlike simple query-retrieve-respond patterns, agents can decide when to search, what to search for, and whether more information is needed.',
      'This approach handles complex questions requiring multiple information sources, verification, and reasoning chains.',
    ],
    pros: [
      'Handles complex, multi-part questions',
      'Can reason about what information is needed',
      'Self-correcting with verification steps',
      'Highest accuracy for difficult queries',
      'Can combine information from multiple sources',
    ],
    cons: [
      'Significantly higher latency (seconds to minutes)',
      'Highest cost per query',
      'Complex to implement and maintain',
      'Can be unpredictable in edge cases',
      'Requires careful prompt engineering',
    ],
    useCases: [
      'Complex strategy analysis',
      'Cross-game rule comparisons',
      'Contested rule interpretations',
      'Multi-step gameplay scenarios',
    ],
    flowSteps: [
      {
        id: 'query',
        label: 'User Query',
        description: 'Complex question',
        icon: '💬',
        color: 'blue',
      },
      {
        id: 'plan',
        label: 'Plan Steps',
        description: 'Agent decides approach',
        icon: '🎯',
        color: 'purple',
      },
      {
        id: 'retrieve',
        label: 'Retrieval Loop',
        description: 'Iterative searching',
        icon: '🔄',
        color: 'cyan',
      },
      {
        id: 'reason',
        label: 'Reasoning',
        description: 'Analyze findings',
        icon: '🧠',
        color: 'green',
      },
      {
        id: 'verify',
        label: 'Verification',
        description: 'Check consistency',
        icon: '✅',
        color: 'orange',
      },
      {
        id: 'synthesize',
        label: 'Synthesize',
        description: 'Final answer',
        icon: '📝',
        color: 'red',
      },
    ],
    example: {
      query:
        'In a 4-player Catan game, if two players tie for Longest Road but a third player has the actual longest road, who gets the 2 VP?',
      context: 'Complex edge case requiring multi-step reasoning',
      response:
        'The player with the actual longest road gets the 2 VP. Longest Road only considers the single longest continuous road each player has, not tied positions. If two players have roads of equal length, neither gets the bonus until one breaks the tie with a longer road. The third player with the longer road would hold the bonus.',
      highlight:
        'Agent performed: 1) Search Longest Road rules, 2) Search tie-breaking rules, 3) Verify with official FAQ, 4) Synthesize answer',
    },
    technicalNotes: [
      'Uses LangGraph for agent orchestration',
      'ReAct pattern for reasoning and acting',
      'Claude Sonnet for planning, Haiku for sub-tasks',
      'Max 5 retrieval iterations before fallback',
    ],
  },
};

/**
 * Get detail content for a specific strategy.
 */
export function getStrategyDetails(id: RetrievalStrategyType): StrategyDetailContent | undefined {
  return STRATEGY_DETAILS[id];
}
