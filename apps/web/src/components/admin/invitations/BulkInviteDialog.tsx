/**
 * BulkInviteDialog Component (Issue #132)
 *
 * Dialog for sending bulk invitations via CSV content.
 * Supports pasting CSV text, previewing parsed rows, and viewing results.
 */

'use client';

import { useState } from 'react';

import { AlertCircleIcon, CheckCircleIcon, UploadIcon } from 'lucide-react';
import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import type { BulkInviteResponse } from '@/lib/api/schemas/invitation.schemas';

interface ParsedRow {
  email: string;
  role: string;
  valid: boolean;
  error?: string;
}

function parseCsvContent(text: string): ParsedRow[] {
  const lines = text
    .split('\n')
    .map(l => l.trim())
    .filter(l => l.length > 0);

  return lines.map(line => {
    const parts = line.split(',').map(p => p.trim());
    const email = parts[0] || '';
    const role = parts[1] || 'User';

    if (!email) {
      return { email, role, valid: false, error: 'Email is empty' };
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      return { email, role, valid: false, error: 'Invalid email format' };
    }
    if (!['User', 'Editor', 'Admin'].includes(role)) {
      return { email, role, valid: false, error: `Invalid role: ${role}` };
    }

    return { email, role, valid: true };
  });
}

export interface BulkInviteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
}

type Step = 'input' | 'preview' | 'results';

export function BulkInviteDialog({ open, onOpenChange, onSuccess }: BulkInviteDialogProps) {
  const [csvText, setCsvText] = useState('');
  const [parsedRows, setParsedRows] = useState<ParsedRow[]>([]);
  const [step, setStep] = useState<Step>('input');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [results, setResults] = useState<BulkInviteResponse | null>(null);

  function resetForm() {
    setCsvText('');
    setParsedRows([]);
    setStep('input');
    setResults(null);
  }

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen) {
      resetForm();
    }
    onOpenChange(nextOpen);
  }

  function handlePreview() {
    const rows = parseCsvContent(csvText);
    setParsedRows(rows);
    setStep('preview');
  }

  async function handleSubmit() {
    setIsSubmitting(true);
    try {
      const validRows = parsedRows.filter(r => r.valid);
      const csv = validRows.map(r => `${r.email},${r.role}`).join('\n');
      const response = await api.invitations.bulkSendInvitations(csv);
      setResults(response);
      setStep('results');

      const successCount = response.successful.length;
      const failCount = response.failed.length;

      if (failCount === 0) {
        toast.success(`All ${successCount} invitations sent successfully`);
      } else {
        toast.success(`${successCount} sent, ${failCount} failed`);
      }

      onSuccess?.();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Bulk invite failed';
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  }

  const validCount = parsedRows.filter(r => r.valid).length;
  const invalidCount = parsedRows.filter(r => !r.valid).length;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Bulk Invite Users</DialogTitle>
          <DialogDescription>
            Paste CSV content with one entry per line: email,role (role defaults to User).
          </DialogDescription>
        </DialogHeader>

        {/* Step 1: Input */}
        {step === 'input' && (
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="csv-content">CSV Content</Label>
              <textarea
                id="csv-content"
                className="flex min-h-[120px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                placeholder={`alice@example.com,User\nbob@example.com,Editor\ncarol@example.com,Admin`}
                value={csvText}
                onChange={e => setCsvText(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Format: email,role (one per line). Valid roles: User, Editor, Admin.
              </p>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
                Cancel
              </Button>
              <Button type="button" onClick={handlePreview} disabled={!csvText.trim()}>
                <UploadIcon className="mr-2 h-4 w-4" />
                Preview
              </Button>
            </DialogFooter>
          </div>
        )}

        {/* Step 2: Preview */}
        {step === 'preview' && (
          <div className="space-y-4">
            <div className="text-sm">
              <span className="font-medium">{validCount}</span> valid,{' '}
              <span className="font-medium text-red-600">{invalidCount}</span> invalid
            </div>

            <div className="max-h-[300px] overflow-auto rounded-md border">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-3 py-2 text-left">Email</th>
                    <th className="px-3 py-2 text-left">Role</th>
                    <th className="px-3 py-2 text-left">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {parsedRows.map((row, i) => (
                    <tr key={i} className={`border-b ${!row.valid ? 'bg-red-50' : ''}`}>
                      <td className="px-3 py-2">{row.email || '(empty)'}</td>
                      <td className="px-3 py-2">{row.role}</td>
                      <td className="px-3 py-2">
                        {row.valid ? (
                          <span className="text-green-600">Valid</span>
                        ) : (
                          <span className="text-red-600">{row.error}</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setStep('input')}>
                Back
              </Button>
              <Button
                type="button"
                onClick={handleSubmit}
                disabled={validCount === 0 || isSubmitting}
              >
                {isSubmitting
                  ? 'Sending...'
                  : `Send ${validCount} Invitation${validCount !== 1 ? 's' : ''}`}
              </Button>
            </DialogFooter>
          </div>
        )}

        {/* Step 3: Results */}
        {step === 'results' && results && (
          <div className="space-y-4">
            {results.successful.length > 0 && (
              <div className="rounded-md border border-green-200 bg-green-50 p-3">
                <div className="flex items-center gap-2 font-medium text-green-800">
                  <CheckCircleIcon className="h-4 w-4" />
                  {results.successful.length} invitation{results.successful.length !== 1 ? 's' : ''}{' '}
                  sent
                </div>
              </div>
            )}

            {results.failed.length > 0 && (
              <div className="rounded-md border border-red-200 bg-red-50 p-3">
                <div className="flex items-center gap-2 font-medium text-red-800 mb-2">
                  <AlertCircleIcon className="h-4 w-4" />
                  {results.failed.length} failed
                </div>
                <ul className="space-y-1 text-sm text-red-700">
                  {results.failed.map((f, i) => (
                    <li key={i}>
                      {f.email}: {f.error}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <DialogFooter>
              <Button type="button" onClick={() => handleOpenChange(false)}>
                Done
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
