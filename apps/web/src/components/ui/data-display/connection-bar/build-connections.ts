import { Bot, Dices, FileText, Gamepad2, MessageCircle, Star, Users, Wrench } from 'lucide-react';

import type { ConnectionPip } from './types';

function pip(
  entityType: ConnectionPip['entityType'],
  count: number,
  label: string,
  icon: ConnectionPip['icon']
): ConnectionPip {
  return { entityType, count, label, icon, isEmpty: count === 0 };
}

export function buildGameConnections(counts: {
  agentCount: number;
  kbCount: number;
  chatCount: number;
  sessionCount: number;
}): ConnectionPip[] {
  return [
    pip('agent', counts.agentCount, 'Agent', Bot),
    pip('kb', counts.kbCount, 'KB', FileText),
    pip('chat', counts.chatCount, 'Chat', MessageCircle),
    pip('session', counts.sessionCount, 'Sessioni', Dices),
  ];
}

export function buildPlayerConnections(counts: {
  sessionCount: number;
  favoriteGameCount: number;
}): ConnectionPip[] {
  return [
    pip('session', counts.sessionCount, 'Sessioni', Dices),
    pip('game', counts.favoriteGameCount, 'Preferiti', Star),
  ];
}

export function buildSessionConnections(counts: {
  gameCount: number;
  playerCount: number;
  toolCount: number;
  agentCount: number;
}): ConnectionPip[] {
  return [
    pip('game', counts.gameCount, 'Gioco', Gamepad2),
    pip('player', counts.playerCount, 'Giocatori', Users),
    pip('tool', counts.toolCount, 'Tools', Wrench),
    pip('agent', counts.agentCount, 'Agente', Bot),
  ];
}

export function buildAgentConnections(counts: {
  gameCount: number;
  kbCount: number;
  chatCount: number;
}): ConnectionPip[] {
  return [
    pip('game', counts.gameCount, 'Gioco', Gamepad2),
    pip('kb', counts.kbCount, 'KB', FileText),
    pip('chat', counts.chatCount, 'Chat', MessageCircle),
  ];
}

export function buildKbConnections(counts: {
  gameCount: number;
  agentCount: number;
}): ConnectionPip[] {
  return [
    pip('game', counts.gameCount, 'Gioco', Gamepad2),
    pip('agent', counts.agentCount, 'Agente', Bot),
  ];
}

export function buildChatConnections(counts: {
  agentCount: number;
  gameCount: number;
}): ConnectionPip[] {
  return [
    pip('agent', counts.agentCount, 'Agente', Bot),
    pip('game', counts.gameCount, 'Gioco', Gamepad2),
  ];
}

export function buildEventConnections(counts: {
  participantCount: number;
  gameCount: number;
  sessionCount: number;
}): ConnectionPip[] {
  return [
    pip('player', counts.participantCount, 'Partecipanti', Users),
    pip('game', counts.gameCount, 'Giochi', Gamepad2),
    pip('session', counts.sessionCount, 'Sessioni', Dices),
  ];
}

export function buildToolkitConnections(counts: {
  gameCount: number;
  toolCount: number;
  sessionCount: number;
}): ConnectionPip[] {
  return [
    pip('game', counts.gameCount, 'Gioco', Gamepad2),
    pip('tool', counts.toolCount, 'Tools', Wrench),
    pip('session', counts.sessionCount, 'Sessioni', Dices),
  ];
}

export function buildToolConnections(counts: { toolkitCount: number }): ConnectionPip[] {
  return [pip('toolkit', counts.toolkitCount, 'Toolkit', Wrench)];
}
