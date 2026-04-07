import { Card } from "@/components/ui/card"
import { Gamepad2, Clock, FileText, Trophy } from "lucide-react"
import { cn } from "@/lib/utils"

interface StatCardProps {
  icon: React.ReactNode
  value: string
  label: string
  iconColor?: string
}

function StatCard({ icon, value, label, iconColor = "text-entity-game" }: StatCardProps) {
  return (
    <Card className={cn(
      "bg-card border-border/50 p-4",
      "shadow-warm hover:bg-card-hover transition-colors"
    )}>
      <div className="flex items-start gap-3">
        <div className={cn("p-2 rounded-lg bg-muted/50", iconColor)}>
          {icon}
        </div>
        <div className="min-w-0 flex-1">
          <p className="font-heading font-bold text-lg text-foreground truncate">
            {value}
          </p>
          <p className="font-body text-xs text-muted-foreground truncate">
            {label}
          </p>
        </div>
      </div>
    </Card>
  )
}

export function StatsRow() {
  const stats = [
    {
      icon: <Gamepad2 className="w-4 h-4" />,
      value: "42",
      label: "sessioni totali",
      iconColor: "text-entity-session",
    },
    {
      icon: <Clock className="w-4 h-4" />,
      value: "6h 20m",
      label: "ultima sessione",
      iconColor: "text-entity-agent",
    },
    {
      icon: <FileText className="w-4 h-4" />,
      value: "3 PDF",
      label: "caricati",
      iconColor: "text-entity-document",
    },
    {
      icon: <Trophy className="w-4 h-4" />,
      value: "Mario",
      label: "più vittorie",
      iconColor: "text-entity-player",
    },
  ]

  return (
    <section className="grid grid-cols-2 lg:grid-cols-4 gap-3">
      {stats.map((stat, index) => (
        <StatCard key={index} {...stat} />
      ))}
    </section>
  )
}
