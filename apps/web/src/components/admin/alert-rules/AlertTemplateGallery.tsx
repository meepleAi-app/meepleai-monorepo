import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import type { AlertTemplate } from '@/lib/api/schemas/alert-rules.schemas';

interface AlertTemplateGalleryProps {
  templates: AlertTemplate[];
  onSelect: (template: AlertTemplate) => void;
}

export function AlertTemplateGallery({ templates, onSelect }: AlertTemplateGalleryProps) {
  return (
    <div>
      <h2 className="text-xl font-semibold mb-4">Quick Start Templates</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {templates.map(template => (
          <Card key={template.alertType}>
            <CardHeader>
              <div className="flex justify-between items-start">
                <CardTitle className="text-lg">{template.name}</CardTitle>
                <Badge variant="outline">{template.category}</Badge>
              </div>
              <CardDescription>{template.description}</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="text-sm text-muted-foreground mb-4">
                <p>
                  Threshold: {template.thresholdValue} {template.thresholdUnit}
                </p>
                <p>Duration: {template.durationMinutes} minutes</p>
                <p>Severity: {template.severity}</p>
              </div>
              <Button
                size="sm"
                variant="outline"
                className="w-full"
                onClick={() => onSelect(template)}
              >
                Use Template
              </Button>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
