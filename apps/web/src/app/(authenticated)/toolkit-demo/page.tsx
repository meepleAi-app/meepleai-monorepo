'use client';

/**
 * Toolkit v2 Demo — Epic #4968
 *
 * Standalone demo page that mounts the new Toolkit v2 UI with mock data.
 * No API or auth required beyond the standard Next.js authenticated layout.
 *
 * Route: /toolkit-demo
 *
 * NOTE: Intentionally placed outside /toolkit/ to avoid matching the
 * /toolkit/[sessionId] dynamic route and triggering real API/SSE connections.
 */

import React, { useState, useCallback } from 'react';

import { Hash } from 'lucide-react';

import {
  SessionHeader,
  TurnIndicatorBar,
  TurnOrderTool,
  WhiteboardTool,
  CounterTool,
  DiceRoller,
  Scoreboard,
  ToolRail,
} from '@/components/session';

import type {
  Session,
  TurnOrderData,
  WhiteboardState,
  CounterState,
  ScoreboardData,
  ScoreEntry,
  Participant,
  DiceRoll,
  ToolItem,
  CounterToolConfig,
} from '@/components/session';

// ── Mock data ─────────────────────────────────────────────────────────────────

const MOCK_SESSION: Session = {
  id: 'demo-session-1',
  sessionCode: 'DEMO42',
  sessionType: 'GameSpecific',
  gameName: 'Catan',
  gameIcon: '🌾',
  sessionDate: new Date(),
  status: 'Active',
  participantCount: 3,
};

const MOCK_PARTICIPANTS: Participant[] = [
  { id: 'p1', displayName: 'Mario (io)', isOwner: true,  isCurrentUser: true,  avatarColor: '#D97706', totalScore: 8,  rank: 1 },
  { id: 'p2', displayName: 'Giulia',     isOwner: false, isCurrentUser: false, avatarColor: '#2563EB', totalScore: 7,  rank: 2 },
  { id: 'p3', displayName: 'Luca',       isOwner: false, isCurrentUser: false, avatarColor: '#DC2626', totalScore: 5,  rank: 3 },
];

const MOCK_TURN_ORDER_INIT: TurnOrderData = {
  id: 'to-1',
  sessionId: 'demo-session-1',
  playerOrder: ['Mario (io)', 'Giulia', 'Luca'],
  currentIndex: 0,
  currentPlayer: 'Mario (io)',
  nextPlayer: 'Giulia',
  roundNumber: 2,
};

const MOCK_WHITEBOARD_STATE: WhiteboardState = {
  strokes: [],
  tokens: [],
  gridSize: '6x6',
  showGrid: true,
  mode: 'both',
};

const MOCK_COUNTER_CONFIG: CounterToolConfig = {
  name: 'Risorse',
  minValue: 0,
  maxValue: 20,
  defaultValue: 0,
  isPerPlayer: true,
  icon: null,
  color: '#D97706',
};

const MOCK_COUNTER_STATE_INIT: CounterState = {
  minValue: 0,
  maxValue: 20,
  defaultValue: 0,
  isPerPlayer: true,
  currentValue: 0,
  playerValues: { p1: 3, p2: 5, p3: 2 },
};

const MOCK_SCORES: ScoreEntry[] = [
  { id: 's1', participantId: 'p1', roundNumber: 1, category: 'Risorse', scoreValue: 3, timestamp: new Date(), createdBy: 'p1' },
  { id: 's2', participantId: 'p2', roundNumber: 1, category: 'Risorse', scoreValue: 2, timestamp: new Date(), createdBy: 'p2' },
  { id: 's3', participantId: 'p3', roundNumber: 1, category: 'Risorse', scoreValue: 1, timestamp: new Date(), createdBy: 'p3' },
  { id: 's4', participantId: 'p1', roundNumber: 2, category: 'Città',   scoreValue: 5, timestamp: new Date(), createdBy: 'p1' },
  { id: 's5', participantId: 'p2', roundNumber: 2, category: 'Città',   scoreValue: 5, timestamp: new Date(), createdBy: 'p2' },
  { id: 's6', participantId: 'p3', roundNumber: 2, category: 'Città',   scoreValue: 4, timestamp: new Date(), createdBy: 'p3' },
];

const MOCK_SCOREBOARD: ScoreboardData = {
  participants: MOCK_PARTICIPANTS,
  scores: MOCK_SCORES,
  rounds: [1, 2],
  categories: ['Risorse', 'Città'],
};

/** Custom counter tool that appears below the "Custom" divider in the rail. */
const CUSTOM_TOOLS: ToolItem[] = [
  {
    id: 'counter-risorse',
    label: 'Risorse',
    shortLabel: 'Ris.',
    icon: <Hash className="w-5 h-5" aria-hidden="true" />,
    type: 'custom',
  },
];

// ── Demo Page ─────────────────────────────────────────────────────────────────

export default function ToolkitDemoPage() {
  // Active tool (matches IDs in BASE_TOOLS + CUSTOM_TOOLS)
  const [activeTool, setActiveTool] = useState<string>('turn-order');

  // TurnOrder state (simulated locally — no API)
  const [turnOrder, setTurnOrder] = useState<TurnOrderData>(MOCK_TURN_ORDER_INIT);

  // Whiteboard state (local only)
  const [whiteboardState, setWhiteboardState] = useState<WhiteboardState>(MOCK_WHITEBOARD_STATE);

  // Counter state (local only)
  const [counterState, setCounterState] = useState<CounterState>(MOCK_COUNTER_STATE_INIT);

  // Dice roll history (local only)
  const [diceHistory, setDiceHistory] = useState<DiceRoll[]>([]);

  // ── Turn order actions ───────────────────────────────────────────────────

  const handleAdvanceTurn = useCallback(async () => {
    setTurnOrder((prev: TurnOrderData) => {
      const nextIndex = (prev.currentIndex + 1) % prev.playerOrder.length;
      const isNewRound = nextIndex === 0;
      return {
        ...prev,
        currentIndex: nextIndex,
        currentPlayer: prev.playerOrder[nextIndex],
        nextPlayer: prev.playerOrder[(nextIndex + 1) % prev.playerOrder.length],
        roundNumber: isNewRound ? prev.roundNumber + 1 : prev.roundNumber,
      };
    });
  }, []);

  const handleResetTurn = useCallback(async () => {
    setTurnOrder((prev: TurnOrderData) => ({
      ...prev,
      currentIndex: 0,
      currentPlayer: prev.playerOrder[0],
      nextPlayer: prev.playerOrder[1],
      roundNumber: 1,
    }));
  }, []);

  // ── Whiteboard actions ───────────────────────────────────────────────────

  const handleStrokesChange = useCallback((strokes: WhiteboardState['strokes']) => {
    setWhiteboardState((prev: WhiteboardState) => ({ ...prev, strokes }));
  }, []);

  const handleStructuredChange = useCallback(
    (
      tokens: WhiteboardState['tokens'],
      gridSize: WhiteboardState['gridSize'],
      showGrid: boolean,
      mode: WhiteboardState['mode'],
    ) => {
      setWhiteboardState((prev: WhiteboardState) => ({ ...prev, tokens, gridSize, showGrid, mode }));
    },
    [],
  );

  const handleWhiteboardClear = useCallback(() => {
    setWhiteboardState(MOCK_WHITEBOARD_STATE);
  }, []);

  // ── Counter actions ──────────────────────────────────────────────────────

  const handleCounterChange = useCallback(async (playerId: string, change: number) => {
    setCounterState((prev: CounterState) => {
      const current = prev.playerValues[playerId] ?? prev.defaultValue;
      const next = Math.max(prev.minValue, Math.min(prev.maxValue, current + change));
      return {
        ...prev,
        playerValues: { ...prev.playerValues, [playerId]: next },
      };
    });
  }, []);

  // ── Dice actions ─────────────────────────────────────────────────────────

  const handleDiceRoll = useCallback(async (formula: string, label?: string): Promise<DiceRoll> => {
    // Parse "NdX" formula (e.g. "2d6", "1d20")
    const match = formula.match(/^(\d+)d(\d+)$/i);
    const count = match ? parseInt(match[1], 10) : 1;
    const sides = match ? parseInt(match[2], 10) : 6;

    const rolls = Array.from({ length: count }, () => Math.ceil(Math.random() * sides));
    const total = rolls.reduce((a, b) => a + b, 0);

    const roll: DiceRoll = {
      id: `roll-${Date.now()}`,
      participantId: 'p1',
      participantName: 'Mario (io)',
      formula,
      label,
      rolls,
      modifier: 0,
      total,
      timestamp: new Date(),
    };

    setDiceHistory(prev => [roll, ...prev.slice(0, 19)]);
    return roll;
  }, []);

  // ── Rendered tool area ───────────────────────────────────────────────────

  function renderActiveTool() {
    switch (activeTool) {
      case 'scoreboard':
        return (
          <Scoreboard
            data={MOCK_SCOREBOARD}
            isRealTime={false}
            variant="full"
          />
        );

      case 'turn-order':
        return (
          <TurnOrderTool
            turnOrder={turnOrder}
            isHost
            onAdvanceTurn={handleAdvanceTurn}
            onResetTurnOrder={handleResetTurn}
          />
        );

      case 'dice':
        return (
          <DiceRoller
            sessionId="demo-session-1"
            participantId="p1"
            participantName="Mario (io)"
            onRoll={handleDiceRoll}
            rollHistory={diceHistory}
          />
        );

      case 'whiteboard':
        return (
          <WhiteboardTool
            whiteboardState={whiteboardState}
            onStrokesChange={handleStrokesChange}
            onStructuredChange={handleStructuredChange}
            onClear={handleWhiteboardClear}
          />
        );

      case 'counter-risorse':
        return (
          <CounterTool
            config={MOCK_COUNTER_CONFIG}
            counterState={counterState}
            participants={MOCK_PARTICIPANTS}
            currentUserId="p1"
            onApplyChange={handleCounterChange}
          />
        );

      default:
        return (
          <p className="text-sm text-stone-500 dark:text-stone-400 italic">
            Tool &quot;{activeTool}&quot; non ancora mappato in questa demo.
          </p>
        );
    }
  }

  // ── Render ───────────────────────────────────────────────────────────────
  //
  // `fixed inset-0 z-[200]` escapes AuthenticatedLayout's sidebar + container
  // so the toolkit renders as a true full-screen experience.

  return (
    <div className="fixed inset-0 z-[200] flex flex-col bg-slate-50 dark:bg-slate-950 overflow-hidden">
      {/* Header */}
      <div className="shrink-0">
        <SessionHeader
          session={MOCK_SESSION}
          onPause={() => alert('Pausa sessione (demo)')}
          onFinalize={() => alert('Finalizza sessione (demo)')}
          onShare={() => alert('Condividi risultati (demo)')}
        />
        <TurnIndicatorBar
          activePlayerName={turnOrder.currentPlayer}
          roundNumber={turnOrder.roundNumber}
          canEndTurn={activeTool === 'turn-order'}
          onEndTurn={() => void handleAdvanceTurn()}
        />
      </div>

      {/* Body: ToolRail + scrollable content */}
      <div className="flex flex-1 overflow-hidden">
        <ToolRail
          activeTool={activeTool}
          onToolSelect={setActiveTool}
          customTools={CUSTOM_TOOLS}
        />
        <main className="flex-1 overflow-auto p-4 md:p-6">
          {renderActiveTool()}
        </main>
      </div>
    </div>
  );
}
