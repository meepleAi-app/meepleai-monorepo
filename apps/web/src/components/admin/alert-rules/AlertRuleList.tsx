import { Edit, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Switch } from '@/components/ui/forms/switch';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';

interface AlertRuleListProps {
  rules: AlertRule[];
  onEdit: (rule: AlertRule) => void;
  onDelete: (id: string) => void;
  onToggle: (id: string) => void;
}

export function AlertRuleList({ rules, onEdit, onDelete, onToggle }: AlertRuleListProps) {
  const getSeverityColor = (severity: string) => {
    switch (severity) {
      case 'Critical':
        return 'destructive';
      case 'Error':
        return 'destructive';
      case 'Warning':
        return 'default';
      case 'Info':
        return 'secondary';
      default:
        return 'outline';
    }
  };

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Type</TableHead>
            <TableHead>Severity</TableHead>
            <TableHead>Threshold</TableHead>
            <TableHead>Duration</TableHead>
            <TableHead>Enabled</TableHead>
            <TableHead>Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rules.length === 0 ? (
            <TableRow>
              <TableCell colSpan={7} className="text-center text-muted-foreground">
                No alert rules configured
              </TableCell>
            </TableRow>
          ) : (
            rules.map(rule => (
              <TableRow key={rule.id}>
                <TableCell className="font-medium">{rule.name}</TableCell>
                <TableCell>{rule.alertType}</TableCell>
                <TableCell>
                  <Badge variant={getSeverityColor(rule.severity)}>{rule.severity}</Badge>
                </TableCell>
                <TableCell>
                  {rule.thresholdValue} {rule.thresholdUnit}
                </TableCell>
                <TableCell>{rule.durationMinutes} min</TableCell>
                <TableCell>
                  <Switch checked={rule.isEnabled} onCheckedChange={() => onToggle(rule.id)} />
                </TableCell>
                <TableCell>
                  <div className="flex gap-2">
                    <Button variant="ghost" size="sm" onClick={() => onEdit(rule)}>
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => onDelete(rule.id)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}
