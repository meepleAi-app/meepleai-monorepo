import { GameNightList } from '@/components/game-nights/GameNightList';

export default function GameNightsPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold font-quicksand">Serate di Gioco</h1>
      </div>
      <GameNightList nights={[]} isLoading={false} />
    </div>
  );
}
