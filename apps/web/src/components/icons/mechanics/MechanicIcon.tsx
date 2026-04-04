import { ComponentType } from 'react';

import { AreaControlIcon } from './icons/AreaControl';
import { CompetitiveIcon } from './icons/Competitive';
import { CooperativeIcon } from './icons/Cooperative';
import { DeckBuildingIcon } from './icons/DeckBuilding';
import { DefaultMechanicIcon } from './icons/DefaultMechanic';
import { DiceRollingIcon } from './icons/DiceRolling';
import { DungeonCrawlerIcon } from './icons/DungeonCrawler';
import { EngineBuildingIcon } from './icons/EngineBuilding';
import { NarrativeRpgIcon } from './icons/NarrativeRpg';
import { PuzzleAbstractIcon } from './icons/PuzzleAbstract';
import { RouteBuildingIcon } from './icons/RouteBuilding';
import { SetCollectionIcon } from './icons/SetCollection';
import { SocialDeductionIcon } from './icons/SocialDeduction';
import { TilePlacementIcon } from './icons/TilePlacement';
import { TradingIcon } from './icons/Trading';
import { WorkerPlacementIcon } from './icons/WorkerPlacement';

export interface MechanicIconProps {
  mechanic: string;
  size?: number;
  className?: string;
}

const MECHANIC_ICONS: Record<string, ComponentType<{ size?: number; className?: string }>> = {
  'engine-building': EngineBuildingIcon,
  'area-control': AreaControlIcon,
  'deck-building': DeckBuildingIcon,
  'worker-placement': WorkerPlacementIcon,
  cooperative: CooperativeIcon,
  competitive: CompetitiveIcon,
  'dice-rolling': DiceRollingIcon,
  'puzzle-abstract': PuzzleAbstractIcon,
  'narrative-rpg': NarrativeRpgIcon,
  'tile-placement': TilePlacementIcon,
  trading: TradingIcon,
  'set-collection': SetCollectionIcon,
  'dungeon-crawler': DungeonCrawlerIcon,
  'route-building': RouteBuildingIcon,
  'social-deduction': SocialDeductionIcon,
};

export function MechanicIcon({ mechanic, size = 24, className = '' }: MechanicIconProps) {
  const Icon = MECHANIC_ICONS[mechanic] ?? DefaultMechanicIcon;
  return <Icon size={size} className={className} />;
}
