/**
 * Breadcrumb Labels Configuration
 *
 * Maps route segments to human-readable labels for breadcrumb navigation.
 * Used to build breadcrumb trails from the current pathname.
 */

/** Route segment → display label */
export const BREADCRUMB_LABELS: Record<string, string> = {
  // Top-level sections
  dashboard: 'Dashboard',
  library: 'Libreria',
  games: 'Catalogo',
  chat: 'Chat',
  profile: 'Profilo',
  sessions: 'Sessioni',
  admin: 'Admin',
  settings: 'Impostazioni',

  // Library sub-sections
  private: 'Giochi Privati',
  wishlist: 'Wishlist',
  proposals: 'Proposte',
  history: 'Cronologia',

  // Admin sub-sections
  overview: 'Panoramica',
  users: 'Utenti',
  agents: 'Agenti',
  'knowledge-base': 'Knowledge Base',
  'shared-games': 'Giochi Condivisi',
  ai: 'AI',
  content: 'Contenuti',
  analytics: 'Analytics',
  config: 'Configurazione',
  monitor: 'Monitoraggio',
  pipeline: 'Pipeline',
  debug: 'Debug',
  strategy: 'Strategia',
  usage: 'Utilizzo',
  builder: 'Builder',
  documents: 'Documenti',

  // Common sub-pages
  new: 'Nuovo',
  edit: 'Modifica',
  details: 'Dettagli',
};

/**
 * Resolve a route segment to a display label.
 * Falls back to title-casing the segment if not found in config.
 */
export function getSegmentLabel(segment: string): string {
  // eslint-disable-next-line security/detect-object-injection
  const label = BREADCRUMB_LABELS[segment];
  if (label) return label;

  // Title-case fallback: "my-section" → "My Section"
  return segment
    .split('-')
    .map(word => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}
