import { memo } from 'react';

import { type MeepleEntityType } from '../meeple-card-styles';

interface SymbolStripProps {
  entity: MeepleEntityType | string;
  // Identity chips
  identityChip1?: string;
  identityChip2?: string;
  // Game metrics
  playerCountDisplay?: string;
  playTimeDisplay?: string;
  // Player metrics
  gamesPlayed?: number;
  winRate?: number;
  // Session metrics
  winnerScore?: string;
  sessionDate?: string;
  // Agent metrics
  conversationCount?: number;
  agentAccuracy?: number;
  linkedKbCount?: number;
  // KnowledgeBase metrics
  pageCount?: number;
  chunkCount?: number;
}

function MetricPill({ icon, value }: { icon: string; value: string | number }) {
  return (
    <span
      title={icon}
      className="inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-mono bg-black/40 border border-white/10 text-white/80 whitespace-nowrap"
    >
      {String(value)}
    </span>
  );
}

function IdentityChip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium bg-white/10 border border-white/20 text-white/70 whitespace-nowrap">
      {label}
    </span>
  );
}

function GameMetrics({
  playerCountDisplay,
  playTimeDisplay,
}: Pick<SymbolStripProps, 'playerCountDisplay' | 'playTimeDisplay'>) {
  return (
    <>
      {playerCountDisplay && <MetricPill icon="👥" value={playerCountDisplay} />}
      {playTimeDisplay && <MetricPill icon="⏱" value={playTimeDisplay} />}
    </>
  );
}

function PlayerMetrics({
  gamesPlayed,
  winRate,
}: Pick<SymbolStripProps, 'gamesPlayed' | 'winRate'>) {
  return (
    <>
      {gamesPlayed !== undefined && <MetricPill icon="🎮" value={gamesPlayed} />}
      {winRate !== undefined && <MetricPill icon="🏆" value={`${winRate}%`} />}
    </>
  );
}

function SessionMetrics({
  winnerScore,
  sessionDate,
}: Pick<SymbolStripProps, 'winnerScore' | 'sessionDate'>) {
  return (
    <>
      {winnerScore && <MetricPill icon="🏆" value={winnerScore} />}
      {sessionDate && <MetricPill icon="📅" value={sessionDate} />}
    </>
  );
}

function AgentMetrics({
  conversationCount,
  agentAccuracy,
  linkedKbCount,
}: Pick<SymbolStripProps, 'conversationCount' | 'agentAccuracy' | 'linkedKbCount'>) {
  return (
    <>
      {conversationCount !== undefined && <MetricPill icon="💬" value={conversationCount} />}
      {agentAccuracy !== undefined && <MetricPill icon="🎯" value={`${agentAccuracy}%`} />}
      {linkedKbCount !== undefined && <MetricPill icon="📚" value={linkedKbCount} />}
    </>
  );
}

function KbMetrics({ pageCount, chunkCount }: Pick<SymbolStripProps, 'pageCount' | 'chunkCount'>) {
  return (
    <>
      {pageCount !== undefined && <MetricPill icon="📄" value={pageCount} />}
      {chunkCount !== undefined && <MetricPill icon="🔍" value={chunkCount} />}
    </>
  );
}

function EntityMetrics(props: SymbolStripProps) {
  switch (props.entity) {
    case 'game':
      return (
        <GameMetrics
          playerCountDisplay={props.playerCountDisplay}
          playTimeDisplay={props.playTimeDisplay}
        />
      );
    case 'player':
      return <PlayerMetrics gamesPlayed={props.gamesPlayed} winRate={props.winRate} />;
    case 'session':
      return <SessionMetrics winnerScore={props.winnerScore} sessionDate={props.sessionDate} />;
    case 'agent':
      return (
        <AgentMetrics
          conversationCount={props.conversationCount}
          agentAccuracy={props.agentAccuracy}
          linkedKbCount={props.linkedKbCount}
        />
      );
    case 'kb':
      return <KbMetrics pageCount={props.pageCount} chunkCount={props.chunkCount} />;
    default:
      return null;
  }
}

export const SymbolStrip = memo(function SymbolStrip(props: SymbolStripProps) {
  const { identityChip1, identityChip2 } = props;

  return (
    <div
      data-symbol-strip
      className="flex items-center justify-between gap-1 px-2 bg-black/50 border-t border-b border-white/5"
      style={{ height: '26px' }}
    >
      {/* Left: Identity Chips */}
      <div className="flex items-center gap-1 min-w-0 overflow-hidden">
        {identityChip1 && <IdentityChip label={identityChip1} />}
        {identityChip2 && <IdentityChip label={identityChip2} />}
      </div>

      {/* Right: Metric Pills */}
      <div className="flex items-center gap-1 shrink-0">
        <EntityMetrics {...props} />
      </div>
    </div>
  );
});
