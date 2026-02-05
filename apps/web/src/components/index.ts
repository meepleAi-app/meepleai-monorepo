/**
 * Component exports for easy importing
 */

// AI-14: Search mode toggle for hybrid search feature
export { default as SearchModeToggle, SearchMode } from './search/SearchModeToggle';

// Issue #2051: Document source selector
export { DocumentSourceSelector } from './chat/DocumentSourceSelector';
export type { DocumentSource } from './chat/DocumentSourceSelector';

// Issue #859: BGAI-074 - Citation components
export { CitationCard } from './citations/CitationCard';
export { CitationList } from './citations/CitationList';

// Issue #1833: UI-006 - CitationLink component
export { CitationLink } from './ui/data-display/citation-link';

// Issue #898: MetricsChart component (Chart.js/Recharts)
export { MetricsChart } from './metrics/MetricsChart';
export type { MetricsChartProps, ChartType, DataSeries, DataPoint } from './metrics/MetricsChart';
