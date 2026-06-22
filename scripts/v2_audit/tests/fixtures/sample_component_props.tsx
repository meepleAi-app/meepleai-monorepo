interface PlayerCardProps {
  name: string;
  avatar: string;
  wins: number;
}

export function PlayerCard({ name, avatar, wins }: PlayerCardProps) {
  return <div>{name}</div>;
}
