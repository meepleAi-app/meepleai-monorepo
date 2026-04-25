import type { ReactElement } from 'react';

import {
  Archive,
  Award,
  BookOpen,
  Bot,
  Brain,
  Calendar,
  Camera,
  Clock,
  Copy,
  Dices,
  Download,
  Edit,
  Eye,
  FileText,
  History,
  Layers,
  Layout,
  MapPin,
  MessageCircle,
  MessageSquare,
  Play,
  RefreshCw,
  Settings,
  Star,
  Trophy,
  Users,
  Wrench,
} from 'lucide-react';

const ICON_SIZE = 14;

/**
 * Lucide icon registry for MeepleCard connection chip icons.
 *
 * Each entry is a pre-instantiated React element so consumers can pass
 * `navIcons.kb` directly to a ConnectionChipProps.iconOverride prop without
 * re-rendering the icon component.
 */
export const navIcons = {
  // Game
  kb: <BookOpen size={ICON_SIZE} strokeWidth={1.75} />,
  agent: <Bot size={ICON_SIZE} strokeWidth={1.75} />,
  chat: <MessageCircle size={ICON_SIZE} strokeWidth={1.75} />,
  session: <Dices size={ICON_SIZE} strokeWidth={1.75} />,
  // Player
  trophy: <Trophy size={ICON_SIZE} strokeWidth={1.75} />,
  partite: <Dices size={ICON_SIZE} strokeWidth={1.75} />,
  favorites: <Star size={ICON_SIZE} strokeWidth={1.75} />,
  achievement: <Award size={ICON_SIZE} strokeWidth={1.75} />,
  // Session
  players: <Users size={ICON_SIZE} strokeWidth={1.75} />,
  notes: <FileText size={ICON_SIZE} strokeWidth={1.75} />,
  tools: <Wrench size={ICON_SIZE} strokeWidth={1.75} />,
  photos: <Camera size={ICON_SIZE} strokeWidth={1.75} />,
  // Agent
  memory: <Brain size={ICON_SIZE} strokeWidth={1.75} />,
  config: <Settings size={ICON_SIZE} strokeWidth={1.75} />,
  // KB
  chunks: <Layers size={ICON_SIZE} strokeWidth={1.75} />,
  reindex: <RefreshCw size={ICON_SIZE} strokeWidth={1.75} />,
  preview: <Eye size={ICON_SIZE} strokeWidth={1.75} />,
  download: <Download size={ICON_SIZE} strokeWidth={1.75} />,
  // Chat
  messages: <MessageSquare size={ICON_SIZE} strokeWidth={1.75} />,
  archive: <Archive size={ICON_SIZE} strokeWidth={1.75} />,
  // Event
  location: <MapPin size={ICON_SIZE} strokeWidth={1.75} />,
  games: <Dices size={ICON_SIZE} strokeWidth={1.75} />,
  date: <Calendar size={ICON_SIZE} strokeWidth={1.75} />,
  // Toolkit/Tool
  decks: <Layout size={ICON_SIZE} strokeWidth={1.75} />,
  phases: <Clock size={ICON_SIZE} strokeWidth={1.75} />,
  history: <History size={ICON_SIZE} strokeWidth={1.75} />,
  use: <Play size={ICON_SIZE} strokeWidth={1.75} />,
  edit: <Edit size={ICON_SIZE} strokeWidth={1.75} />,
  copy: <Copy size={ICON_SIZE} strokeWidth={1.75} />,
} as const satisfies Record<string, ReactElement>;

export type NavIconKey = keyof typeof navIcons;
