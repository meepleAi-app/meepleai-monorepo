/**
 * Admin Hub - Client Component
 *
 * Consolidates 50+ admin pages into a single tabbed dashboard.
 * Uses MeepleCard v2 design tokens (entity colors, glassmorphism,
 * warm shadows) to create a visually cohesive admin experience.
 *
 * Layout: Renders inside AuthenticatedLayout (sidebar + breadcrumb provided).
 * Each tab groups related admin pages as interactive cards with KPIs,
 * sparkline charts, and quick-action buttons.
 */

'use client';

import { type ReactNode, useCallback, useMemo } from 'react';

import {
  type LucideIcon,
  LayoutDashboardIcon,
  UsersIcon,
  GamepadIcon,
  BotIcon,
  SettingsIcon,
  BarChartIcon,
  BellIcon,
  ServerIcon,
  ActivityIcon,
  KeyIcon,
  CheckSquareIcon,
  DownloadIcon,
  HelpCircleIcon,
  ShareIcon,
  CpuIcon,
  FileTextIcon,
  SearchIcon,
  DollarSignIcon,
  ShieldIcon,
  BoxIcon,
  DatabaseIcon,
  PackageIcon,
  ClipboardListIcon,
  BellRingIcon,
  SettingsIcon as AlertConfigIcon,
  FileSearchIcon,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

// ─── Entity Color Map (matches MeepleCard v2 --e-* tokens) ───
const ENTITY_COLORS = {
  overview: { hsl: '38 92% 50%', label: 'Amber' },
  users: { hsl: '262 83% 58%', label: 'Purple' },
  games: { hsl: '25 95% 45%', label: 'Orange' },
  ai: { hsl: '38 92% 50%', label: 'Amber' },
  config: { hsl: '210 40% 55%', label: 'Slate' },
  analytics: { hsl: '220 80% 55%', label: 'Blue' },
  alerts: { hsl: '350 89% 60%', label: 'Rose' },
  system: { hsl: '240 60% 55%', label: 'Indigo' },
} as const;

type EntitySection = keyof typeof ENTITY_COLORS;

// ─── Types ───
interface HubCardData {
  title: string;
  subtitle: string;
  icon: LucideIcon;
  href: string;
  kpis: Array<{ value: string; label: string; change?: { direction: 'up' | 'down'; pct: string } }>;
  sparkline?: number[];
  statusDot?: 'green' | 'amber' | 'red';
  alertChips?: Array<{ label: string; severity: 'critical' | 'warning' | 'info' }>;
  actions: Array<{ label: string; href: string; primary?: boolean }>;
}

// ─── Sparkline SVG Component ───
function Sparkline({ data, color, id }: { data: number[]; color: string; id: string }) {
  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  const w = 200;
  const h = 40;
  const points = data.map((v, i) => ({
    x: (i / (data.length - 1)) * w,
    y: h - ((v - min) / range) * (h - 4) - 2,
  }));

  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x},${p.y}`).join(' ');
  const areaPath = `${linePath} L${w},${h} L0,${h} Z`;
  const gradId = `spark-${id}`;

  return (
    <div className="h-10 w-full opacity-90">
      <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" className="h-full w-full">
        <defs>
          <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={`hsl(${color})`} stopOpacity="0.25" />
            <stop offset="100%" stopColor={`hsl(${color})`} stopOpacity="0" />
          </linearGradient>
        </defs>
        <path d={areaPath} fill={`url(#${gradId})`} />
        <path d={linePath} fill="none" stroke={`hsl(${color})`} strokeWidth="2" />
      </svg>
    </div>
  );
}

// ─── Status Dot ───
function StatusDot({ status }: { status: 'green' | 'amber' | 'red' }) {
  return (
    <span
      className={cn(
        'inline-block h-2 w-2 rounded-full shrink-0',
        status === 'green' && 'bg-green-500 shadow-[0_0_8px_hsl(142_76%_50%/0.5)]',
        status === 'amber' && 'bg-amber-400 shadow-[0_0_8px_hsl(45_93%_56%/0.5)]',
        status === 'red' && 'bg-red-500 shadow-[0_0_8px_hsl(0_72%_51%/0.5)]'
      )}
    />
  );
}

// ─── Alert Chip ───
function AlertChip({ label, severity }: { label: string; severity: 'critical' | 'warning' | 'info' }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-semibold',
        severity === 'critical' && 'bg-red-500/15 text-red-400',
        severity === 'warning' && 'bg-amber-400/15 text-amber-400',
        severity === 'info' && 'bg-blue-500/15 text-blue-400'
      )}
    >
      {label}
    </span>
  );
}

// ─── Hub Card Component ───
function HubCard({ card, section }: { card: HubCardData; section: EntitySection }) {
  const router = useRouter();
  const color = ENTITY_COLORS[section].hsl;
  const Icon = card.icon;

  const handleClick = useCallback(() => {
    router.push(card.href);
  }, [router, card.href]);

  const handleActionClick = useCallback(
    (e: React.MouseEvent, href: string) => {
      e.stopPropagation();
      router.push(href);
    },
    [router]
  );

  return (
    <div
      onClick={handleClick}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          handleClick();
        }
      }}
      role="button"
      tabIndex={0}
      aria-label={`${card.title} — ${card.subtitle}`}
      className={cn(
        'group relative cursor-pointer overflow-hidden rounded-2xl',
        'border border-border/50 dark:border-border/30',
        'transition-all duration-350 ease-[cubic-bezier(0.4,0,0.2,1)]',
        'outline-2 outline-transparent outline-offset-2',
        'hover:-translate-y-1.5 hover:shadow-[0_20px_50px_rgb(0_0_0/0.5)]',
        // Glassmorphism
        'bg-card/70 dark:bg-[hsl(0_0%_16%/0.7)] backdrop-blur-xl backdrop-saturate-[180%]'
      )}
      style={{
        // @ts-expect-error CSS custom property
        '--card-hsl': color,
      }}
    >
      {/* Left accent strip */}
      <div
        className="absolute left-0 top-0 bottom-0 w-1 rounded-l-2xl transition-[width] duration-250 group-hover:w-1.5"
        style={{ background: `hsl(${color})` }}
      />
      {/* Gradient wash */}
      <div
        className="pointer-events-none absolute inset-y-0 right-0 w-2/5 -z-10"
        style={{ background: `linear-gradient(135deg, hsl(${color} / 0.06), transparent 70%)` }}
      />
      {/* Hover glow ring */}
      <div
        className="absolute inset-0 rounded-2xl opacity-0 transition-opacity duration-250 group-hover:opacity-100 pointer-events-none"
        style={{ boxShadow: `inset 0 0 0 2px hsl(${color} / 0.25)` }}
      />

      <div className="relative p-5">
        {/* Header */}
        <div className="flex items-start justify-between mb-4">
          <div className="flex items-center gap-3">
            <div
              className="flex h-9 w-9 items-center justify-center rounded-[10px] shrink-0"
              style={{ background: `hsl(${color} / 0.15)` }}
            >
              <Icon className="h-[18px] w-[18px]" style={{ color: `hsl(${color})` }} />
            </div>
            <div>
              <h3 className="font-quicksand text-[15px] font-bold leading-tight text-foreground">
                {card.title}
              </h3>
              <p className="text-xs text-muted-foreground mt-0.5">{card.subtitle}</p>
            </div>
          </div>
          {card.statusDot && <StatusDot status={card.statusDot} />}
        </div>

        {/* Alert chips (if present) */}
        {card.alertChips && (
          <div className="flex flex-wrap gap-2 mb-3.5">
            {card.alertChips.map((chip) => (
              <AlertChip key={chip.label} label={chip.label} severity={chip.severity} />
            ))}
          </div>
        )}

        {/* KPIs */}
        <div className="flex gap-4 mb-3.5">
          {card.kpis.map((kpi) => (
            <div key={kpi.label} className="flex-1 min-w-0">
              <div className="font-quicksand text-xl font-bold text-foreground leading-tight">
                {kpi.value}
              </div>
              <div className="text-[11px] text-muted-foreground font-medium mt-0.5">{kpi.label}</div>
              {kpi.change && (
                <div
                  className={cn(
                    'text-[11px] font-semibold mt-0.5',
                    kpi.change.direction === 'up' ? 'text-green-500' : 'text-red-500'
                  )}
                >
                  {kpi.change.direction === 'up' ? '▲' : '▼'} {kpi.change.pct}
                </div>
              )}
            </div>
          ))}
        </div>

        {/* Sparkline */}
        {card.sparkline && (
          <Sparkline data={card.sparkline} color={color} id={`${section}-${card.title.replace(/\s+/g, '-').toLowerCase()}`} />
        )}

        {/* Actions */}
        <div className="flex gap-2 pt-3 mt-1 border-t border-border/30">
          {card.actions.map((action) => (
            <button
              key={action.label}
              onClick={(e) => handleActionClick(e, action.href)}
              className={cn(
                'rounded-lg border px-3.5 py-1.5 text-xs font-semibold',
                'transition-all duration-150 whitespace-nowrap',
                action.primary
                  ? 'border-transparent text-foreground'
                  : 'border-border/50 bg-muted/30 text-foreground hover:bg-muted/60'
              )}
              style={
                action.primary
                  ? { background: `hsl(${color} / 0.15)`, borderColor: `hsl(${color} / 0.25)` }
                  : undefined
              }
            >
              {action.label}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

// ─── Tab Data Configuration ───
const TAB_CONFIG: Array<{
  id: EntitySection;
  label: string;
  icon: LucideIcon;
  badge?: number;
  cards: HubCardData[];
}> = [
  {
    id: 'overview',
    label: 'Overview',
    icon: LayoutDashboardIcon,
    cards: [
      {
        title: 'System Health',
        subtitle: 'Stato dei servizi',
        icon: ActivityIcon,
        href: '/admin/infrastructure',
        statusDot: 'green',
        kpis: [
          { value: '99.7%', label: 'Uptime (30d)' },
          { value: '7/8', label: 'Servizi OK' },
          { value: '42ms', label: 'Avg Latency' },
        ],
        actions: [
          { label: 'Infrastruttura', href: '/admin/infrastructure', primary: true },
          { label: 'Servizi', href: '/admin/services' },
        ],
      },
      {
        title: 'Utenti Attivi',
        subtitle: 'Online e DAU',
        icon: UsersIcon,
        href: '/admin/users',
        kpis: [
          { value: '47', label: 'Online ora' },
          { value: '1,234', label: 'Utenti totali', change: { direction: 'up', pct: '12%' } },
          { value: '312', label: 'DAU' },
        ],
        sparkline: [18, 22, 20, 28, 32, 30, 35, 38, 42, 47],
        actions: [
          { label: 'Lista Utenti', href: '/admin/users', primary: true },
          { label: 'Sessioni', href: '/admin/sessions' },
        ],
      },
      {
        title: 'Azioni in Sospeso',
        subtitle: 'Da gestire',
        icon: BellIcon,
        href: '/admin/shared-games/pending-approvals',
        kpis: [
          { value: '5', label: 'Approvazioni giochi' },
          { value: '3', label: 'Alert attivi' },
          { value: '2', label: 'Tipologie pending' },
        ],
        sparkline: [8, 6, 7, 5, 9, 7, 10, 8, 6, 10],
        actions: [
          { label: 'Coda Approvazioni', href: '/admin/shared-games/pending-approvals', primary: true },
          { label: 'Vedi Alert', href: '/admin/alerts' },
        ],
      },
      {
        title: 'AI Usage',
        subtitle: 'Richieste e costi oggi',
        icon: CpuIcon,
        href: '/admin/ai-usage',
        kpis: [
          { value: '2,847', label: 'Richieste oggi', change: { direction: 'up', pct: '8%' } },
          { value: '$14.20', label: 'Costo oggi' },
          { value: '324ms', label: 'Avg Response' },
        ],
        sparkline: [120, 180, 220, 200, 280, 340, 310, 380, 420, 460],
        actions: [
          { label: 'AI Monitor', href: '/admin/ai-requests', primary: true },
          { label: 'AI Usage', href: '/admin/ai-usage' },
        ],
      },
    ],
  },
  {
    id: 'users',
    label: 'Users',
    icon: UsersIcon,
    cards: [
      {
        title: 'Lista Utenti',
        subtitle: 'Gestione e ruoli',
        icon: UsersIcon,
        href: '/admin/users',
        kpis: [
          { value: '1,234', label: 'Totale utenti' },
          { value: '18', label: 'Nuovi oggi', change: { direction: 'up', pct: '23%' } },
          { value: '94%', label: 'Verificati' },
        ],
        sparkline: [980, 1020, 1050, 1080, 1120, 1150, 1180, 1200, 1220, 1234],
        actions: [
          { label: 'Gestisci Utenti', href: '/admin/users', primary: true },
          { label: 'Bulk Actions', href: '/admin/users' },
        ],
      },
      {
        title: 'Sessioni Attive',
        subtitle: 'Sessioni live',
        icon: ActivityIcon,
        href: '/admin/sessions',
        statusDot: 'green',
        kpis: [
          { value: '47', label: 'Sessioni live' },
          { value: '12m', label: 'Durata media' },
        ],
        actions: [{ label: 'Sessioni', href: '/admin/sessions', primary: true }],
      },
      {
        title: 'API Keys',
        subtitle: 'Chiavi attive',
        icon: KeyIcon,
        href: '/admin/api-keys',
        kpis: [
          { value: '86', label: 'Chiavi attive' },
          { value: '3.2K', label: 'Chiamate oggi', change: { direction: 'up', pct: '5%' } },
        ],
        sparkline: [2800, 2900, 3000, 2950, 3100, 3050, 3200, 3150, 3180, 3200],
        actions: [
          { label: 'Gestisci Keys', href: '/admin/api-keys', primary: true },
          { label: 'Crea Nuova', href: '/admin/api-keys' },
        ],
      },
    ],
  },
  {
    id: 'games',
    label: 'Games',
    icon: GamepadIcon,
    badge: 5,
    cards: [
      {
        title: 'Catalogo Giochi',
        subtitle: 'Giochi condivisi',
        icon: GamepadIcon,
        href: '/admin/shared-games',
        kpis: [
          { value: '892', label: 'Totale giochi' },
          { value: '14', label: 'Nuovi questa sett.', change: { direction: 'up', pct: '32%' } },
        ],
        sparkline: [720, 740, 760, 790, 810, 830, 850, 870, 885, 892],
        actions: [
          { label: 'Catalogo', href: '/admin/shared-games', primary: true },
          { label: 'Aggiungi', href: '/admin/shared-games/new' },
        ],
      },
      {
        title: 'Approvazioni',
        subtitle: 'In attesa di review',
        icon: CheckSquareIcon,
        href: '/admin/shared-games/pending-approvals',
        statusDot: 'amber',
        kpis: [
          { value: '5', label: 'In coda' },
          { value: '2.4h', label: 'Tempo medio attesa' },
          { value: '89%', label: 'Tasso approvazione' },
        ],
        actions: [
          { label: 'Coda Review', href: '/admin/shared-games/pending-approvals', primary: true },
          { label: 'Pending Deletes', href: '/admin/shared-games/pending-deletes' },
        ],
      },
      {
        title: 'Import BGG',
        subtitle: 'BoardGameGeek sync',
        icon: DownloadIcon,
        href: '/admin/shared-games/add-from-bgg',
        kpis: [
          { value: '12 feb', label: 'Ultimo import' },
          { value: '97%', label: 'Success rate' },
        ],
        actions: [
          { label: 'Importa da BGG', href: '/admin/shared-games/add-from-bgg', primary: true },
          { label: 'Bulk Import', href: '/admin/shared-games/import' },
        ],
      },
      {
        title: 'FAQs',
        subtitle: 'Domande frequenti',
        icon: HelpCircleIcon,
        href: '/admin/faqs',
        kpis: [
          { value: '247', label: 'Totale FAQ' },
          { value: '8', label: 'Senza risposta' },
        ],
        actions: [{ label: 'Gestisci FAQ', href: '/admin/faqs', primary: true }],
      },
      {
        title: 'Share Requests',
        subtitle: 'Richieste condivisione',
        icon: ShareIcon,
        href: '/admin/share-requests',
        kpis: [
          { value: '3', label: 'Pendenti' },
          { value: '12', label: 'Approvate oggi' },
        ],
        actions: [{ label: 'Richieste', href: '/admin/share-requests', primary: true }],
      },
    ],
  },
  {
    id: 'ai',
    label: 'AI & Agents',
    icon: BotIcon,
    badge: 2,
    cards: [
      {
        title: 'Agent Definitions',
        subtitle: 'Agenti AI configurati',
        icon: BotIcon,
        href: '/admin/agent-definitions',
        statusDot: 'green',
        kpis: [
          { value: '12', label: 'Totale agenti' },
          { value: '10', label: 'Attivi' },
          { value: '1', label: 'In errore' },
        ],
        actions: [
          { label: 'Gestisci Agenti', href: '/admin/agent-definitions', primary: true },
          { label: 'Playground', href: '/admin/agent-definitions/playground' },
        ],
      },
      {
        title: 'Agent Typologies',
        subtitle: 'Classificazioni',
        icon: CpuIcon,
        href: '/admin/agent-typologies',
        kpis: [
          { value: '8', label: 'Tipi definiti' },
          { value: '2', label: 'Pending' },
        ],
        actions: [
          { label: 'Tipologie', href: '/admin/agent-typologies', primary: true },
          { label: 'Crea Nuova', href: '/admin/agent-typologies/create' },
        ],
      },
      {
        title: 'AI Models',
        subtitle: 'Configurazione modelli',
        icon: ServerIcon,
        href: '/admin/ai-models',
        kpis: [
          { value: '6', label: 'Modelli config.' },
          { value: 'GPT-4o', label: 'Modello primario' },
        ],
        actions: [{ label: 'Configura', href: '/admin/ai-models', primary: true }],
      },
      {
        title: 'Prompts',
        subtitle: 'Gestione prompt system',
        icon: FileTextIcon,
        href: '/admin/prompts',
        kpis: [
          { value: '34', label: 'Totale prompt' },
          { value: '128', label: 'Versioni' },
        ],
        actions: [{ label: 'Editor Prompt', href: '/admin/prompts', primary: true }],
      },
      {
        title: 'PDF Documents',
        subtitle: 'Processing pipeline',
        icon: FileSearchIcon,
        href: '/admin/pdfs',
        kpis: [
          { value: '156', label: 'Totale PDF' },
          { value: '3', label: 'In coda processing' },
        ],
        actions: [
          { label: 'Documenti', href: '/admin/pdfs', primary: true },
          { label: 'Upload', href: '/admin/pdfs' },
        ],
      },
      {
        title: 'RAG Dashboard',
        subtitle: 'Retrieval & search',
        icon: SearchIcon,
        href: '/admin/rag-executions',
        kpis: [
          { value: '1.2K', label: 'Query oggi' },
          { value: '180ms', label: 'Avg latency' },
        ],
        sparkline: [80, 120, 160, 140, 200, 180, 220, 240, 260, 280],
        actions: [
          { label: 'RAG Monitor', href: '/admin/rag-executions', primary: true },
          { label: 'Tier Config', href: '/admin/rag/tier-strategy-config' },
        ],
      },
      {
        title: 'AI Usage & Costi',
        subtitle: 'Monitoraggio spesa',
        icon: DollarSignIcon,
        href: '/admin/ai-usage',
        kpis: [
          { value: '$14.20', label: 'Costo oggi' },
          { value: '$342', label: 'Questo mese', change: { direction: 'down', pct: '3%' } },
        ],
        sparkline: [380, 360, 350, 340, 360, 350, 342, 338, 345, 342],
        actions: [{ label: 'Report Costi', href: '/admin/ai-usage', primary: true }],
      },
    ],
  },
  {
    id: 'config',
    label: 'Config',
    icon: SettingsIcon,
    cards: [
      {
        title: 'Impostazioni Generali',
        subtitle: 'Configurazione globale',
        icon: SettingsIcon,
        href: '/admin/configuration',
        kpis: [{ value: '14 feb', label: 'Ultima modifica' }],
        actions: [{ label: 'Configura', href: '/admin/configuration', primary: true }],
      },
      {
        title: 'Rate Limits',
        subtitle: 'Limiti per tier e utente',
        icon: ShieldIcon,
        href: '/admin/config/rate-limits',
        kpis: [
          { value: '4', label: 'Tier configurati' },
          { value: '28', label: 'Hits limite oggi' },
        ],
        actions: [
          { label: 'Rate Limits', href: '/admin/config/rate-limits', primary: true },
          { label: 'Override Utenti', href: '/admin/config/rate-limits' },
        ],
      },
      {
        title: 'Game Library Limits',
        subtitle: 'Limiti collezione per tier',
        icon: BoxIcon,
        href: '/admin/configuration/game-library-limits',
        kpis: [{ value: 'Free: 50', label: 'Basic: 200 | Pro: Illim.' }],
        actions: [
          { label: 'Configura Limiti', href: '/admin/configuration/game-library-limits', primary: true },
        ],
      },
      {
        title: 'PDF Tier Limits',
        subtitle: 'Limiti upload per tier',
        icon: FileTextIcon,
        href: '/admin/configuration/pdf-tier-limits',
        kpis: [{ value: 'Free: 5', label: 'Basic: 25 | Pro: 100' }],
        actions: [
          { label: 'Configura', href: '/admin/configuration/pdf-tier-limits', primary: true },
        ],
      },
    ],
  },
  {
    id: 'analytics',
    label: 'Analytics',
    icon: BarChartIcon,
    cards: [
      {
        title: 'Analytics Dashboard',
        subtitle: 'Metriche e trend',
        icon: BarChartIcon,
        href: '/admin/analytics',
        kpis: [
          { value: '8.4K', label: 'Page views oggi', change: { direction: 'up', pct: '15%' } },
          { value: '312', label: 'Visitatori unici' },
        ],
        sparkline: [4200, 5100, 6300, 5800, 7200, 6900, 7800, 8100, 8200, 8400],
        actions: [
          { label: 'Dashboard', href: '/admin/analytics', primary: true },
          { label: 'Export', href: '/admin/bulk-export' },
        ],
      },
      {
        title: 'Reports',
        subtitle: 'Report personalizzati',
        icon: ClipboardListIcon,
        href: '/admin/reports',
        kpis: [
          { value: '24', label: 'Report generati' },
          { value: '3', label: 'Schedulati' },
        ],
        actions: [{ label: 'Genera Report', href: '/admin/reports', primary: true }],
      },
      {
        title: 'Management',
        subtitle: 'Metriche di sistema',
        icon: ActivityIcon,
        href: '/admin/management',
        kpis: [
          { value: '99.7%', label: 'Uptime' },
          { value: '42ms', label: 'Latenza media' },
        ],
        actions: [{ label: 'Gestione', href: '/admin/management', primary: true }],
      },
    ],
  },
  {
    id: 'alerts',
    label: 'Alerts',
    icon: BellIcon,
    badge: 3,
    cards: [
      {
        title: 'Alert Attivi',
        subtitle: 'Richiede attenzione',
        icon: BellRingIcon,
        href: '/admin/alerts',
        statusDot: 'red',
        alertChips: [
          { label: '2 Critical', severity: 'critical' },
          { label: '1 Warning', severity: 'warning' },
          { label: '4 Info', severity: 'info' },
        ],
        kpis: [],
        sparkline: [3, 5, 4, 7, 3, 6, 8, 5, 4, 7],
        actions: [
          { label: 'Vedi Alert', href: '/admin/alerts', primary: true },
          { label: 'Muta Tutti', href: '/admin/alerts' },
        ],
      },
      {
        title: 'Alert Rules',
        subtitle: 'Regole configurate',
        icon: ShieldIcon,
        href: '/admin/alert-rules',
        kpis: [
          { value: '18', label: 'Regole totali' },
          { value: '15', label: 'Attive' },
        ],
        actions: [
          { label: 'Gestisci Regole', href: '/admin/alert-rules', primary: true },
          { label: 'Crea Regola', href: '/admin/alert-rules' },
        ],
      },
      {
        title: 'Alert Config',
        subtitle: 'Canali notifica',
        icon: AlertConfigIcon,
        href: '/admin/alerts/config',
        kpis: [
          { value: 'Email', label: 'Canale primario' },
          { value: 'Slack', label: 'Canale secondario' },
        ],
        actions: [{ label: 'Configura', href: '/admin/alerts/config', primary: true }],
      },
    ],
  },
  {
    id: 'system',
    label: 'System',
    icon: ServerIcon,
    cards: [
      {
        title: 'Infrastruttura',
        subtitle: 'Health e status DB',
        icon: ServerIcon,
        href: '/admin/infrastructure',
        statusDot: 'green',
        kpis: [
          { value: '5/5', label: 'Core services' },
          { value: 'PG 16', label: 'DB Version' },
        ],
        actions: [{ label: 'Dettagli', href: '/admin/infrastructure', primary: true }],
      },
      {
        title: 'Cache Redis',
        subtitle: 'Performance e memoria',
        icon: DatabaseIcon,
        href: '/admin/cache',
        kpis: [
          { value: '94.2%', label: 'Hit rate' },
          { value: '128MB', label: 'Memoria usata' },
        ],
        sparkline: [88, 89, 90, 91, 92, 93, 92, 93, 94, 94],
        actions: [
          { label: 'Cache Manager', href: '/admin/cache', primary: true },
          { label: 'Invalida', href: '/admin/cache' },
        ],
      },
      {
        title: 'Services',
        subtitle: 'Microservizi status',
        icon: BoxIcon,
        href: '/admin/services',
        kpis: [
          { value: '7', label: 'Running' },
          { value: '1', label: 'Warning' },
          { value: '0', label: 'Stopped' },
        ],
        actions: [{ label: 'Servizi', href: '/admin/services', primary: true }],
      },
      {
        title: 'n8n Templates',
        subtitle: 'Workflow automation',
        icon: PackageIcon,
        href: '/admin/n8n-templates',
        kpis: [
          { value: '12', label: 'Workflow totali' },
          { value: '8', label: 'Attivi' },
        ],
        actions: [{ label: 'Templates', href: '/admin/n8n-templates', primary: true }],
      },
      {
        title: 'Bulk Export',
        subtitle: 'Esportazione dati',
        icon: DownloadIcon,
        href: '/admin/bulk-export',
        kpis: [{ value: '18 feb', label: 'Ultimo export' }],
        actions: [{ label: 'Esporta', href: '/admin/bulk-export', primary: true }],
      },
      {
        title: 'Audit Log',
        subtitle: 'Trail completo',
        icon: ClipboardListIcon,
        href: '/admin/audit-log',
        kpis: [
          { value: '342', label: 'Eventi oggi', change: { direction: 'up', pct: '6%' } },
        ],
        sparkline: [280, 300, 310, 290, 320, 330, 310, 340, 335, 342],
        actions: [{ label: 'Audit Log', href: '/admin/audit-log', primary: true }],
      },
    ],
  },
];

// ─── Main Component ───
export function AdminHubClient() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Admin Hub
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Panoramica completa del sistema &mdash; tutte le sezioni in un&apos;unica vista
        </p>
      </div>

      {/* Mock data notice */}
      <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 px-4 py-2.5 text-xs text-amber-600 dark:text-amber-400">
        I dati KPI mostrati sono illustrativi &mdash; il collegamento ai dati live è in fase di sviluppo.
      </div>

      {/* Tabs */}
      <Tabs defaultValue="overview" className="w-full">
        <TabsList className="h-auto flex-wrap gap-1 bg-muted/40 backdrop-blur-sm p-1.5 rounded-xl border border-border/30 w-full justify-start">
          {TAB_CONFIG.map((tab) => {
            const Icon = tab.icon;
            return (
              <TabsTrigger
                key={tab.id}
                value={tab.id}
                className={cn(
                  'gap-1.5 px-3.5 py-2 text-xs font-semibold font-quicksand',
                  'data-[state=active]:shadow-md',
                  'rounded-lg transition-all'
                )}
              >
                <Icon className="h-3.5 w-3.5" />
                {tab.label}
                {tab.badge && (
                  <Badge
                    variant="destructive"
                    className="ml-1 h-4 min-w-4 px-1 text-[10px] font-bold"
                  >
                    {tab.badge}
                  </Badge>
                )}
              </TabsTrigger>
            );
          })}
        </TabsList>

        {TAB_CONFIG.map((tab) => (
          <TabsContent key={tab.id} value={tab.id} className="mt-6">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
              {tab.cards.map((card) => (
                <HubCard key={card.href + card.title} card={card} section={tab.id} />
              ))}
            </div>
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}
