import { Card } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { FileText, Upload, CheckCircle, Loader2, AlertCircle } from "lucide-react"
import { cn } from "@/lib/utils"

type DocumentStatus = "ready" | "indexing" | "error"

interface Document {
  name: string
  status: DocumentStatus
  pages?: number
}

const documents: Document[] = [
  { name: "Regolamento_IT.pdf", status: "ready", pages: 24 },
  { name: "Espansione_Mercanti.pdf", status: "indexing", pages: 8 },
  { name: "Quick_Reference.pdf", status: "error" },
]

function StatusBadge({ status }: { status: DocumentStatus }) {
  const config = {
    ready: {
      label: "Pronto",
      icon: CheckCircle,
      className: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
    },
    indexing: {
      label: "Indicizzando",
      icon: Loader2,
      className: "bg-amber-500/15 text-amber-400 border-amber-500/30",
    },
    error: {
      label: "Errore",
      icon: AlertCircle,
      className: "bg-rose-500/15 text-rose-400 border-rose-500/30",
    },
  }

  const { label, icon: Icon, className } = config[status]

  return (
    <Badge variant="outline" className={cn("font-body font-medium text-[10px] px-2 py-0.5", className)}>
      <Icon className={cn("w-3 h-3 mr-1", status === "indexing" && "animate-spin")} />
      {label}
    </Badge>
  )
}

export function DocumentsSection() {
  return (
    <section>
      <div className="flex items-center justify-between mb-4">
        <h2 className="font-heading font-bold text-lg text-foreground">
          Documenti AI
        </h2>
        <Button
          size="sm"
          variant="outline"
          className="h-8 px-3 font-body font-semibold text-xs border-entity-game/30 text-entity-game hover:bg-entity-game/10 hover:text-entity-game"
        >
          <Upload className="w-3.5 h-3.5 mr-1.5" />
          Carica PDF
        </Button>
      </div>

      <div className="space-y-2">
        {documents.map((doc, index) => (
          <Card
            key={index}
            className={cn(
              "bg-card border-border/50 p-3",
              "shadow-warm hover:bg-card-hover transition-colors",
              "flex items-center gap-3"
            )}
          >
            <div className="p-2 rounded-lg bg-entity-document/15">
              <FileText className="w-4 h-4 text-entity-document" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="font-body font-semibold text-sm text-foreground truncate">
                {doc.name}
              </p>
              {doc.pages && (
                <p className="font-body text-xs text-muted-foreground">
                  {doc.pages} pagine
                </p>
              )}
            </div>
            <StatusBadge status={doc.status} />
          </Card>
        ))}
      </div>
    </section>
  )
}
