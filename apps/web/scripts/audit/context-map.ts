/**
 * Mappa esplicita prefisso rotta → bounded context.
 *
 * È scritta a mano di proposito: l'app router non conosce i bounded context del
 * backend, e dedurli con un'euristica produrrebbe attribuzioni plausibili e
 * sbagliate. Dove il prefisso non è noto la funzione risponde 'Unmapped', che è
 * un'informazione utile — significa "da classificare", non "irrilevante".
 *
 * Tre etichette non sono bounded context e sono dichiarate tali:
 *   - PublicPages: pagine istituzionali e legali (about, privacy, pricing, …)
 *   - DesignSystem: la libreria di componenti interna (/admin/ui-library)
 * Forzarle dentro un contesto avrebbe falsato la copertura per ondata.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import type { EndpointEntry } from './types';

/** Prefissi rotta → contesto. Il match più lungo vince (vedi contextForRoute). */
const ROUTE_CONTEXTS: Array<[string, string]> = [
  // Sezioni admin: distinte fra loro, non appiattite su Administration.
  //
  // Senza queste regole tutto ciò che sta sotto /admin cadeva sul fallback e
  // finiva in Administration, che così arrivava a 520 righe — il 30% del tracker
  // — mentre EntityRelationships e SecurityAudit restavano a zero pur avendo
  // endpoint propri. Un contesto che raccoglie gli scarti non è una unità di
  // lavoro: rende l'ondata ingestibile e nasconde gli altri contesti.
  ['/admin/agents', 'KnowledgeBase'],
  ['/admin/agent-definitions', 'KnowledgeBase'],
  ['/admin/agent-typologies', 'KnowledgeBase'],
  ['/admin/ai', 'KnowledgeBase'],
  ['/admin/ai-models', 'KnowledgeBase'],
  ['/admin/knowledge-base', 'KnowledgeBase'],
  ['/admin/kb', 'KnowledgeBase'],
  ['/admin/mechanic-analyses', 'KnowledgeBase'],
  ['/admin/mechanic-extractor', 'KnowledgeBase'],
  ['/admin/prompts', 'KnowledgeBase'],
  ['/admin/strategies', 'KnowledgeBase'],
  ['/admin/llm', 'KnowledgeBase'],
  ['/admin/embedding', 'KnowledgeBase'],
  ['/admin/rag-quality', 'KbQuality'],
  ['/admin/rag-executions', 'KbQuality'],
  ['/admin/rag-enhancements', 'KbQuality'],
  ['/admin/rag-dashboard', 'KbQuality'],
  ['/admin/quality', 'KbQuality'],
  ['/admin/ab-tests', 'KbQuality'],
  ['/admin/pdfs', 'DocumentProcessing'],
  ['/admin/pdf-analytics', 'DocumentProcessing'],
  ['/admin/seeding', 'DocumentProcessing'],
  ['/admin/bgg-queue', 'SharedGameCatalog'],
  ['/admin/wikidata', 'SharedGameCatalog'],
  ['/admin/categories', 'SharedGameCatalog'],
  ['/admin/entity-links', 'EntityRelationships'],
  ['/admin/audit-log', 'SecurityAudit'],
  ['/admin/feature-flags', 'SystemConfiguration'],
  ['/admin/configurations', 'SystemConfiguration'],
  ['/admin/settings', 'SystemConfiguration'],
  ['/admin/secrets', 'SystemConfiguration'],
  ['/admin/alert-rules', 'SystemConfiguration'],
  ['/admin/alert-channels', 'SystemConfiguration'],
  ['/admin/alerts', 'SystemConfiguration'],
  ['/admin/status-banner', 'SystemConfiguration'],
  ['/admin/financial-ledger', 'BusinessSimulations'],
  ['/admin/cost-calculator', 'BusinessSimulations'],
  ['/admin/budget', 'BusinessSimulations'],
  ['/admin/resource-forecast', 'BusinessSimulations'],
  ['/admin/tier-strategy', 'BusinessSimulations'],
  ['/admin/tier-routing', 'BusinessSimulations'],
  ['/admin/tiers', 'BusinessSimulations'],
  ['/admin/email-templates', 'UserNotifications'],
  ['/admin/emails', 'UserNotifications'],
  ['/admin/slack', 'UserNotifications'],
  ['/admin/test-results', 'Testing'],
  ['/admin/sandbox', 'Testing'],
  ['/admin/shared-games', 'SharedGameCatalog'],
  ['/admin/catalog', 'SharedGameCatalog'],
  ['/admin/catalog-ingestion', 'SharedGameCatalog'],
  ['/admin/games', 'GameManagement'],
  ['/admin/content', 'GameManagement'],
  ['/admin/config', 'SystemConfiguration'],
  ['/admin/providers', 'SystemConfiguration'],
  ['/admin/database-sync', 'DatabaseSync'],
  ['/admin/notifications', 'UserNotifications'],
  ['/admin/business', 'BusinessSimulations'],
  ['/admin/ui-library', 'DesignSystem'],
  ['/admin', 'Administration'],

  // Area utente.
  ['/library/[gameId]/kb', 'DocumentProcessing'],
  ['/library-public', 'UserLibrary'],
  ['/library', 'UserLibrary'],
  ['/upload', 'DocumentProcessing'],
  ['/gamebook', 'GameManagement'],
  ['/games', 'GameManagement'],
  ['/editor', 'GameManagement'],
  ['/hub', 'GameManagement'],
  ['/versions', 'SystemConfiguration'],
  ['/discover', 'SharedGameCatalog'],
  ['/shared-games', 'SharedGameCatalog'],
  ['/chat', 'KnowledgeBase'],
  ['/agents', 'KnowledgeBase'],
  ['/knowledge-base', 'KnowledgeBase'],
  ['/kb', 'KnowledgeBase'],
  ['/pipeline-builder', 'DocumentProcessing'],
  ['/toolkit', 'GameToolkit'],
  ['/toolkits', 'GameToolkit'],
  ['/sessions', 'SessionTracking'],
  ['/game-nights', 'SessionTracking'],
  ['/play-records', 'SessionTracking'],
  ['/players', 'SessionTracking'],
  ['/join', 'SessionTracking'],
  ['/dashboard', 'UserLibrary'],
  ['/notifications', 'UserNotifications'],
  ['/dev', 'Testing'],

  // Autenticazione e ciclo di vita dell'account.
  ['/login', 'Authentication'],
  ['/register', 'Authentication'],
  ['/profile', 'Authentication'],
  ['/reset-password', 'Authentication'],
  ['/verify-email', 'Authentication'],
  ['/verification-pending', 'Authentication'],
  ['/verification-success', 'Authentication'],
  ['/oauth-callback', 'Authentication'],
  ['/accept-invite', 'Authentication'],
  ['/invites', 'Authentication'],
  ['/invitation-expired', 'Authentication'],
  ['/setup-account', 'Authentication'],
  ['/setup', 'Authentication'],
  ['/onboarding', 'Authentication'],
  ['/welcome', 'Authentication'],

  // Pagine istituzionali: fuori dai bounded context, ma parte della superficie.
  ['/', 'PublicPages'],
  ['/about', 'PublicPages'],
  ['/contact', 'PublicPages'],
  ['/cookies', 'PublicPages'],
  ['/cookie-settings', 'PublicPages'],
  ['/faq', 'PublicPages'],
  ['/how-it-works', 'PublicPages'],
  ['/legal', 'PublicPages'],
  ['/offline', 'PublicPages'],
  ['/pricing', 'PublicPages'],
  ['/privacy', 'PublicPages'],
  ['/terms', 'PublicPages'],
];

/**
 * Prefissi degli endpoint API → contesto.
 *
 * Separata da ROUTE_CONTEXTS perché la superficie API non ricalca quella delle
 * pagine: esistono famiglie di endpoint senza alcuna pagina corrispondente
 * (agent-memory, achievements, emails), e quel disallineamento è materiale
 * d'audit, non rumore da appianare.
 */
const API_CONTEXTS: Array<[string, string]> = [
  ['/live-sessions', 'SessionTracking'],
  ['/game-sessions', 'SessionTracking'],
  ['/game-night', 'SessionTracking'],
  ['/playlists', 'SessionTracking'],
  ['/activity', 'SessionTracking'],
  ['/interactions', 'SessionTracking'],
  ['/comments', 'SessionTracking'],
  ['/users', 'Administration'],
  ['/alerts', 'Administration'],
  ['/logs', 'Administration'],
  ['/metrics', 'Administration'],
  ['/status', 'Administration'],
  ['/status-banner', 'Administration'],
  ['/auth', 'Authentication'],
  ['/user', 'Authentication'],
  ['/waitlist', 'Authentication'],
  ['/connect', 'Authentication'],
  ['/callback', 'Authentication'],
  ['/disconnect', 'Authentication'],
  ['/permissions', 'Authentication'],
  ['/game-toolkits', 'GameToolkit'],
  ['/toolboxes', 'GameToolbox'],
  ['/toolbox-templates', 'GameToolbox'],
  ['/agent-memory', 'AgentMemory'],
  ['/chat-threads', 'KnowledgeBase'],
  ['/rag', 'KnowledgeBase'],
  ['/rag-dashboard', 'KnowledgeBase'],
  ['/strategies', 'KnowledgeBase'],
  ['/prompts', 'KnowledgeBase'],
  ['/context-engineering', 'KnowledgeBase'],
  ['/models', 'KnowledgeBase'],
  ['/llm', 'KnowledgeBase'],
  ['/query', 'KnowledgeBase'],
  ['/agent-typologies', 'KnowledgeBase'],
  ['/llm-costs', 'KbQuality'],
  ['/budget', 'KbQuality'],
  ['/pdfs', 'DocumentProcessing'],
  ['/pdf', 'DocumentProcessing'],
  ['/ingest', 'DocumentProcessing'],
  ['/documents', 'DocumentProcessing'],
  ['/document-collections', 'DocumentProcessing'],
  ['/kb-docs', 'DocumentProcessing'],
  ['/photo-batches', 'DocumentProcessing'],
  ['/resources', 'DocumentProcessing'],
  ['/private-games', 'GameManagement'],
  ['/gamebooks', 'GameManagement'],
  ['/rulespecs', 'GameManagement'],
  ['/wizard', 'GameManagement'],
  ['/wishlist', 'UserLibrary'],
  ['/achievements', 'Gamification'],
  ['/bgg', 'SharedGameCatalog'],
  ['/extract-bgg-games', 'SharedGameCatalog'],
  ['/shared', 'SharedGameCatalog'],
  ['/share-links', 'SharedGameCatalog'],
  ['/compare', 'SharedGameCatalog'],
  ['/emails', 'UserNotifications'],
  ['/test', 'Testing'],
  ['/testing', 'Testing'],
  ['/seed-e2e-users', 'Testing'],
];

/** Cartelle sotto Routing/ che nominano già il proprio contesto. */
const FILE_CONTEXTS = new Set([
  'Administration',
  'AgentMemory',
  'Authentication',
  'BusinessSimulations',
  'DatabaseSync',
  'DocumentProcessing',
  'EntityRelationships',
  'GameManagement',
  'GameToolbox',
  'GameToolkit',
  'Gamification',
  'KbQuality',
  'KnowledgeBase',
  'SecurityAudit',
  'SessionTracking',
  'SharedGameCatalog',
  'SystemConfiguration',
  'Testing',
  'UserLibrary',
  'UserNotifications',
]);

/** Contesto di una rotta. Il prefisso più specifico vince. */
export function contextForRoute(route: string): string {
  const match = ROUTE_CONTEXTS.filter(([prefix]) =>
    // La root è un match esatto: trattarla come prefisso farebbe combaciare
    // qualunque rotta e nasconderebbe tutti gli 'Unmapped'.
    prefix === '/' ? route === '/' : route === prefix || route.startsWith(`${prefix}/`)
  ).sort((a, b) => b[0].length - a[0].length)[0];
  return match?.[1] ?? 'Unmapped';
}

const matchPrefix = (table: Array<[string, string]>, value: string): string | undefined =>
  table
    .filter(([prefix]) => value === prefix || value.startsWith(`${prefix}/`))
    .sort((a, b) => b[0].length - a[0].length)[0]?.[1];

/**
 * Contesto di un endpoint, in tre passaggi: la cartella sotto Routing/ lo
 * dichiara; altrimenti lo deduce il prefisso API; altrimenti la mappa delle
 * rotte, per le famiglie che condividono il nome con una pagina (/games, /library).
 */
export function contextForEndpoint(e: EndpointEntry): string {
  const folder = e.file.split('/')[1];
  if (folder && FILE_CONTEXTS.has(folder)) return folder;

  const apiPath = e.path.replace(/^\/api\/v1/, '') || '/';
  return matchPrefix(API_CONTEXTS, apiPath) ?? contextForRoute(apiPath);
}
