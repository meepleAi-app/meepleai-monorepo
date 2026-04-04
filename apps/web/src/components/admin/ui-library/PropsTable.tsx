import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import type { MockVariant } from '@/config/component-registry';

interface PropsTableProps {
  mockProps?: Record<string, unknown>;
  mockVariants?: MockVariant[];
}

function inferType(value: unknown): string {
  if (value === null) return 'null';
  if (value === undefined) return 'undefined';
  if (Array.isArray(value)) return 'array';
  return typeof value;
}

function formatValue(value: unknown): string {
  if (value === null) return 'null';
  if (value === undefined) return 'undefined';
  if (typeof value === 'string') return `"${value}"`;
  if (typeof value === 'boolean' || typeof value === 'number') return String(value);
  if (Array.isArray(value)) {
    const preview = JSON.stringify(value);
    return preview.length > 40 ? preview.slice(0, 37) + '...' : preview;
  }
  if (typeof value === 'object') {
    const preview = JSON.stringify(value);
    return preview.length > 40 ? preview.slice(0, 37) + '...' : preview;
  }
  return String(value);
}

function PropRows({ props }: { props: Record<string, unknown> }) {
  return (
    <>
      {Object.entries(props).map(([key, value]) => (
        <TableRow key={key}>
          <TableCell className="font-mono text-xs font-medium text-foreground">{key}</TableCell>
          <TableCell>
            <span className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs text-muted-foreground">
              {inferType(value)}
            </span>
          </TableCell>
          <TableCell className="font-mono text-xs text-muted-foreground">
            {formatValue(value)}
          </TableCell>
        </TableRow>
      ))}
    </>
  );
}

export function PropsTable({ mockProps, mockVariants }: PropsTableProps) {
  const hasProps = mockProps && Object.keys(mockProps).length > 0;
  const hasVariants = mockVariants && mockVariants.length > 0;

  if (!hasProps && !hasVariants) {
    return (
      <p className="font-nunito text-sm text-muted-foreground italic">No mock props defined.</p>
    );
  }

  return (
    <div className="space-y-6">
      {hasProps && (
        <div>
          <h4 className="mb-2 font-quicksand text-sm font-semibold text-foreground">
            Default Props
          </h4>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-1/3">Prop</TableHead>
                <TableHead className="w-1/4">Type</TableHead>
                <TableHead>Value</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <PropRows props={mockProps} />
            </TableBody>
          </Table>
        </div>
      )}

      {hasVariants && (
        <div>
          <h4 className="mb-2 font-quicksand text-sm font-semibold text-foreground">Variants</h4>
          <div className="space-y-4">
            {mockVariants.map(variant => (
              <div key={variant.name}>
                <p className="mb-1 font-nunito text-xs font-medium text-muted-foreground uppercase tracking-wide">
                  {variant.name}
                </p>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-1/3">Prop</TableHead>
                      <TableHead className="w-1/4">Type</TableHead>
                      <TableHead>Value</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    <PropRows props={variant.props} />
                  </TableBody>
                </Table>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
