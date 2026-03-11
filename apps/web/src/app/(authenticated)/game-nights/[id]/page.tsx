import { GameNightPlanningLayout } from '@/components/game-nights/GameNightPlanningLayout';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function GameNightDetailPage({ params }: Props) {
  const { id } = await params;

  // TODO: Fetch game night details from API once GET /game-nights/{id} endpoint is available
  const title = `Serata di gioco`;

  return (
    <div className="space-y-6">
      <GameNightPlanningLayout title={title} />
    </div>
  );
}
