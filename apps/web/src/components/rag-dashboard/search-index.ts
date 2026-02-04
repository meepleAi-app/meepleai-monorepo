/**
 * Search Index for RAG Dashboard Global Search
 *
 * Defines all searchable sections with metadata for fuzzy matching.
 */

export type SearchGroup = 'understand' | 'explore' | 'compare' | 'build' | 'optimize';

export interface SearchableSection {
  /** Unique section ID (matches DOM id) */
  id: string;
  /** Display title */
  title: string;
  /** Description shown in search results */
  description: string;
  /** Additional keywords for better matching */
  keywords: string[];
  /** Navigation group */
  group: SearchGroup;
  /** Emoji icon for the section */
  icon: string;
  /** Whether section requires technical view mode */
  technicalOnly?: boolean;
  /** Whether section requires business view mode */
  businessOnly?: boolean;
}

/**
 * All searchable sections in the RAG Dashboard
 */
export const SEARCH_INDEX: SearchableSection[] = [
  // UNDERSTAND Group
  {
    id: 'overview',
    title: 'System Overview',
    description: 'Key performance metrics for the TOMAC-RAG system',
    keywords: ['stats', 'metrics', 'performance', 'KPI', 'dashboard', 'summary'],
    group: 'understand',
    icon: '📊',
  },
  {
    id: 'architecture',
    title: 'Architecture Explorer',
    description: 'Interactive system architecture diagram',
    keywords: ['diagram', 'layers', 'components', 'system', 'design', 'structure'],
    group: 'understand',
    icon: '🏗️',
    technicalOnly: true,
  },
  {
    id: 'layers',
    title: 'Layer Documentation',
    description: 'Detailed technical docs with code examples and decision trees',
    keywords: ['docs', 'documentation', 'code', 'examples', 'API', 'reference'],
    group: 'understand',
    icon: '📚',
    technicalOnly: true,
  },

  // EXPLORE Group
  {
    id: 'query-sim',
    title: 'Query Simulator',
    description: 'Test the routing system with sample queries',
    keywords: ['test', 'simulate', 'query', 'routing', 'demo', 'try'],
    group: 'explore',
    icon: '🔍',
  },
  {
    id: 'token-flow',
    title: 'Token Flow',
    description: 'Visualize token consumption through each layer',
    keywords: ['tokens', 'consumption', 'visualization', 'flow', 'usage', 'cost'],
    group: 'explore',
    icon: '🌊',
  },
  {
    id: 'walkthrough',
    title: 'Decision Walkthrough',
    description: 'Step-by-step visualization of RAG decision process',
    keywords: ['steps', 'process', 'decision', 'flow', 'walkthrough', 'guide'],
    group: 'explore',
    icon: '🚶',
  },

  // COMPARE Group
  {
    id: 'variants',
    title: '31 RAG Variants',
    description: 'Interactive comparison of all strategy × template × tier combinations',
    keywords: ['variants', 'comparison', 'strategies', 'templates', 'tiers', '31'],
    group: 'compare',
    icon: '⚖️',
    technicalOnly: true,
  },
  {
    id: 'performance',
    title: 'Performance Metrics',
    description: 'Real-time performance data across strategies and query types',
    keywords: ['performance', 'metrics', 'latency', 'accuracy', 'speed', 'benchmarks'],
    group: 'compare',
    icon: '📈',
    technicalOnly: true,
  },

  // BUILD Group
  {
    id: 'prompts',
    title: 'Prompt Builder',
    description: 'Interactive tool to assemble and preview RAG prompts',
    keywords: ['prompts', 'builder', 'templates', 'assembly', 'preview', 'create'],
    group: 'build',
    icon: '✏️',
    technicalOnly: true,
  },
  {
    id: 'roles',
    title: 'Agent Roles',
    description: 'Pre-built agent configurations with system prompts',
    keywords: ['agents', 'roles', 'configuration', 'presets', 'personas', 'characters'],
    group: 'build',
    icon: '🎭',
    technicalOnly: true,
  },
  {
    id: 'agent-integration',
    title: 'Agent-RAG Integration',
    description: 'How MeepleAI agents assemble prompts with RAG context',
    keywords: ['integration', 'agents', 'RAG', 'context', 'assembly', 'prompts'],
    group: 'build',
    icon: '🔗',
    technicalOnly: true,
  },

  // OPTIMIZE Group
  {
    id: 'cost',
    title: 'Cost Calculator',
    description: 'Estimate monthly costs based on usage patterns',
    keywords: ['cost', 'calculator', 'pricing', 'estimate', 'budget', 'ROI', 'savings'],
    group: 'optimize',
    icon: '💰',
  },
  {
    id: 'model-optimizer',
    title: 'Model Selection',
    description: 'Optimize model choices based on cost, speed, and quality requirements',
    keywords: ['model', 'selection', 'optimizer', 'LLM', 'AI', 'GPT', 'Claude'],
    group: 'optimize',
    icon: '🤖',
    technicalOnly: true,
  },

  // BUSINESS View
  {
    id: 'executive-summary',
    title: 'Executive Summary',
    description: 'Key takeaways for stakeholders',
    keywords: ['executive', 'summary', 'business', 'stakeholders', 'ROI', 'highlights'],
    group: 'optimize',
    icon: '📋',
    businessOnly: true,
  },
];

/**
 * Group metadata for display
 */
export const GROUP_LABELS: Record<SearchGroup, { label: string; icon: string }> = {
  understand: { label: 'Understand', icon: '🎓' },
  explore: { label: 'Explore', icon: '🔍' },
  compare: { label: 'Compare', icon: '⚖️' },
  build: { label: 'Build', icon: '🔨' },
  optimize: { label: 'Optimize', icon: '💰' },
};

/**
 * Simple fuzzy match scoring
 * Returns a score based on how well the query matches the text
 */
export function fuzzyMatch(query: string, text: string): number {
  const lowerQuery = query.toLowerCase();
  const lowerText = text.toLowerCase();

  // Exact match = highest score
  if (lowerText === lowerQuery) return 100;

  // Contains exact query = high score
  if (lowerText.includes(lowerQuery)) return 80;

  // Word start match = good score
  const words = lowerText.split(/\s+/);
  if (words.some((word) => word.startsWith(lowerQuery))) return 70;

  // Partial match on words
  const queryWords = lowerQuery.split(/\s+/);
  const matchedWords = queryWords.filter((qw) => words.some((w) => w.includes(qw)));
  if (matchedWords.length > 0) {
    return 50 + (matchedWords.length / queryWords.length) * 30;
  }

  // Fuzzy character match (typo tolerance)
  let matches = 0;
  let lastIndex = -1;
  for (const char of lowerQuery) {
    const index = lowerText.indexOf(char, lastIndex + 1);
    if (index > lastIndex) {
      matches++;
      lastIndex = index;
    }
  }

  const fuzzyScore = (matches / lowerQuery.length) * 40;
  return fuzzyScore > 20 ? fuzzyScore : 0;
}

/**
 * Search sections with fuzzy matching
 */
export function searchSections(
  query: string,
  options?: {
    viewMode?: 'technical' | 'business';
    limit?: number;
  }
): SearchableSection[] {
  const { viewMode = 'technical', limit = 10 } = options || {};

  if (!query.trim()) {
    // Return all sections for empty query
    return SEARCH_INDEX.filter((section) => {
      if (viewMode === 'business' && section.technicalOnly) return false;
      if (viewMode === 'technical' && section.businessOnly) return false;
      return true;
    }).slice(0, limit);
  }

  // Score each section
  const scored = SEARCH_INDEX.filter((section) => {
    // Filter by view mode
    if (viewMode === 'business' && section.technicalOnly) return false;
    if (viewMode === 'technical' && section.businessOnly) return false;
    return true;
  }).map((section) => {
    // Calculate match score across all searchable fields
    const titleScore = fuzzyMatch(query, section.title) * 1.5;
    const descScore = fuzzyMatch(query, section.description);
    const keywordScores = section.keywords.map((kw) => fuzzyMatch(query, kw));
    const maxKeywordScore = Math.max(...keywordScores, 0);

    const totalScore = Math.max(titleScore, descScore, maxKeywordScore);

    return { section, score: totalScore };
  });

  // Sort by score and filter out low scores
  return scored
    .filter(({ score }) => score > 20)
    .sort((a, b) => b.score - a.score)
    .slice(0, limit)
    .map(({ section }) => section);
}

/**
 * Get sections grouped by navigation group
 */
export function getSectionsGrouped(
  sections: SearchableSection[]
): Record<SearchGroup, SearchableSection[]> {
  const groups: Record<SearchGroup, SearchableSection[]> = {
    understand: [],
    explore: [],
    compare: [],
    build: [],
    optimize: [],
  };

  for (const section of sections) {
    groups[section.group].push(section);
  }

  return groups;
}
