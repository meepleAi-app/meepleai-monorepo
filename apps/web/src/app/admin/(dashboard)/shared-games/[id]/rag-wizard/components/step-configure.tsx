/**
 * Step 2: Configura — Per-file configuration
 *
 * For each file: document type Select + version Input.
 * Version validation: ^\d+\.\d+$
 * "Avvia Processing" button disabled until all configs are valid.
 */

'use client';

import { useCallback, useState } from 'react';

import { FileText } from 'lucide-react';

import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Button } from '@/components/ui/primitives/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

import type { FileConfig } from './rag-wizard';
import type { DocumentType } from '../lib/rag-api';

// ── Constants ───────────────────────────────────────────────────────────

const VERSION_REGEX = /^\d+\.\d+$/;

const DOCUMENT_TYPES: { value: DocumentType; label: string }[] = [
  { value: 'Rulebook', label: 'Regolamento' },
  { value: 'Errata', label: 'Errata' },
  { value: 'Homerule', label: 'Regola casalinga' },
];

// ── Props ───────────────────────────────────────────────────────────────

interface StepConfigureProps {
  configs: FileConfig[];
  onBack: () => void;
  onStartProcessing: (configs: FileConfig[]) => void;
}

// ── Component ───────────────────────────────────────────────────────────

export function StepConfigure({ configs: initialConfigs, onBack, onStartProcessing }: StepConfigureProps) {
  const [configs, setConfigs] = useState<FileConfig[]>(initialConfigs);

  const updateConfig = useCallback((index: number, updates: Partial<FileConfig>) => {
    setConfigs(prev =>
      prev.map((c, i) => (i === index ? { ...c, ...updates } : c))
    );
  }, []);

  const allValid = configs.every(c => VERSION_REGEX.test(c.version));

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">
        Configura tipo documento e versione per ogni file.
      </p>

      <div className="space-y-4">
        {configs.map((config, index) => {
          const versionValid = VERSION_REGEX.test(config.version);

          return (
            <div
              key={`${config.file.name}-${config.file.size}`}
              className="rounded-lg border bg-white/60 dark:bg-zinc-800/60 p-4 space-y-3"
            >
              {/* File name */}
              <div className="flex items-center gap-2">
                <FileText className="h-4 w-4 text-muted-foreground" />
                <span className="text-sm font-medium truncate">{config.file.name}</span>
              </div>

              {/* Config fields */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {/* Document Type */}
                <div className="space-y-1.5">
                  <Label htmlFor={`docType-${index}`} className="text-xs">
                    Tipo documento
                  </Label>
                  <Select
                    value={config.documentType}
                    onValueChange={(value: DocumentType) =>
                      updateConfig(index, { documentType: value })
                    }
                  >
                    <SelectTrigger id={`docType-${index}`} className="h-9 text-sm">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {DOCUMENT_TYPES.map(dt => (
                        <SelectItem key={dt.value} value={dt.value} className="text-sm">
                          {dt.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Version */}
                <div className="space-y-1.5">
                  <Label htmlFor={`version-${index}`} className="text-xs">
                    Versione
                  </Label>
                  <Input
                    id={`version-${index}`}
                    value={config.version}
                    onChange={e => updateConfig(index, { version: e.target.value })}
                    placeholder="1.0"
                    className={`h-9 text-sm ${!versionValid && config.version ? 'border-destructive' : ''}`}
                  />
                  {!versionValid && config.version && (
                    <p className="text-xs text-destructive">
                      Formato: X.Y (es. 1.0, 2.1)
                    </p>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Navigation */}
      <div className="flex justify-between pt-2">
        <Button variant="outline" onClick={onBack}>
          Indietro
        </Button>
        <Button onClick={() => onStartProcessing(configs)} disabled={!allValid}>
          Avvia Processing
        </Button>
      </div>
    </div>
  );
}
