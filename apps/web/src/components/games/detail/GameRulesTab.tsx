/**
 * Game Rules Tab Component
 *
 * Displays PDF rulebooks, RuleSpecs, and document management
 */

import React from 'react';
import { Game, PdfDocumentDto } from '@/lib/api';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { FileText, Download, Eye, CheckCircle, Clock, XCircle, AlertCircle } from 'lucide-react';

interface GameRulesTabProps {
  game: Game;
  documents: PdfDocumentDto[];
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function getStatusIcon(status: string) {
  switch (status.toLowerCase()) {
    case 'completed':
      return <CheckCircle className="h-4 w-4 text-green-500" />;
    case 'processing':
      return <Clock className="h-4 w-4 text-blue-500" />;
    case 'failed':
      return <XCircle className="h-4 w-4 text-red-500" />;
    case 'pending':
      return <Clock className="h-4 w-4 text-yellow-500" />;
    default:
      return <AlertCircle className="h-4 w-4 text-gray-500" />;
  }
}

function getStatusBadge(status: string) {
  const statusLower = status.toLowerCase();
  switch (statusLower) {
    case 'completed':
      return <Badge variant="default" className="bg-green-600">Completed</Badge>;
    case 'processing':
      return <Badge variant="default" className="bg-blue-600">Processing</Badge>;
    case 'failed':
      return <Badge variant="destructive">Failed</Badge>;
    case 'pending':
      return <Badge variant="secondary">Pending</Badge>;
    default:
      return <Badge variant="outline">{status}</Badge>;
  }
}

export function GameRulesTab({ game, documents }: GameRulesTabProps) {
  const completedDocuments = documents.filter(doc => doc.processingStatus.toLowerCase() === 'completed');
  const processingDocuments = documents.filter(doc =>
    ['processing', 'pending'].includes(doc.processingStatus.toLowerCase())
  );
  const failedDocuments = documents.filter(doc => doc.processingStatus.toLowerCase() === 'failed');

  return (
    <div className="space-y-6">
      {/* PDF Documents Card */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>Rulebooks & Documents</span>
            <Badge variant="secondary">{documents.length} total</Badge>
          </CardTitle>
          <CardDescription>
            Uploaded PDF rulebooks and documentation
          </CardDescription>
        </CardHeader>
        <CardContent>
          {documents.length === 0 ? (
            <Alert>
              <FileText className="h-4 w-4" />
              <AlertDescription>
                No rulebooks have been uploaded for this game yet.
              </AlertDescription>
            </Alert>
          ) : (
            <div className="space-y-4">
              {/* Completed Documents */}
              {completedDocuments.length > 0 && (
                <div className="space-y-3">
                  <h4 className="font-semibold text-sm text-muted-foreground">Ready to Use</h4>
                  <div className="space-y-2">
                    {completedDocuments.map((doc) => (
                      <div
                        key={doc.id}
                        className="flex items-center justify-between p-4 border border-slate-200 dark:border-slate-700 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors"
                      >
                        <div className="flex items-center gap-3 flex-1 min-w-0">
                          {getStatusIcon(doc.processingStatus)}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <p className="font-medium truncate">{doc.fileName}</p>
                              {getStatusBadge(doc.processingStatus)}
                            </div>
                            <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
                              <span>{formatFileSize(doc.fileSizeBytes)}</span>
                              {doc.pageCount && <span>•</span>}
                              {doc.pageCount && <span>{doc.pageCount} pages</span>}
                              <span>•</span>
                              <span>Uploaded {new Date(doc.uploadedAt).toLocaleDateString()}</span>
                            </div>
                          </div>
                        </div>
                        <div className="flex items-center gap-2 ml-4">
                          <Button size="sm" variant="outline" disabled>
                            <Eye className="h-4 w-4 mr-2" />
                            View
                          </Button>
                          <Button size="sm" variant="outline" disabled>
                            <Download className="h-4 w-4 mr-2" />
                            Download
                          </Button>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Processing Documents */}
              {processingDocuments.length > 0 && (
                <div className="space-y-3">
                  <h4 className="font-semibold text-sm text-muted-foreground">Processing</h4>
                  <div className="space-y-2">
                    {processingDocuments.map((doc) => (
                      <div
                        key={doc.id}
                        className="flex items-center justify-between p-4 border border-slate-200 dark:border-slate-700 rounded-lg bg-blue-50 dark:bg-blue-900/10"
                      >
                        <div className="flex items-center gap-3 flex-1 min-w-0">
                          {getStatusIcon(doc.processingStatus)}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <p className="font-medium truncate">{doc.fileName}</p>
                              {getStatusBadge(doc.processingStatus)}
                            </div>
                            <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
                              <span>{formatFileSize(doc.fileSizeBytes)}</span>
                              <span>•</span>
                              <span>Uploaded {new Date(doc.uploadedAt).toLocaleDateString()}</span>
                            </div>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Failed Documents */}
              {failedDocuments.length > 0 && (
                <div className="space-y-3">
                  <h4 className="font-semibold text-sm text-muted-foreground">Failed</h4>
                  <div className="space-y-2">
                    {failedDocuments.map((doc) => (
                      <div
                        key={doc.id}
                        className="flex items-center justify-between p-4 border border-red-200 dark:border-red-700 rounded-lg bg-red-50 dark:bg-red-900/10"
                      >
                        <div className="flex items-center gap-3 flex-1 min-w-0">
                          {getStatusIcon(doc.processingStatus)}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <p className="font-medium truncate">{doc.fileName}</p>
                              {getStatusBadge(doc.processingStatus)}
                            </div>
                            <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
                              <span>{formatFileSize(doc.fileSizeBytes)}</span>
                              <span>•</span>
                              <span>Uploaded {new Date(doc.uploadedAt).toLocaleDateString()}</span>
                            </div>
                          </div>
                        </div>
                        <Button size="sm" variant="outline" disabled>
                          Retry
                        </Button>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* RuleSpec Section (Placeholder) */}
      <Card>
        <CardHeader>
          <CardTitle>RuleSpec Versions</CardTitle>
          <CardDescription>
            Structured rule specifications and version history
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              RuleSpec integration coming soon. This will allow you to view and manage structured rule versions.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>

      {/* Upload Section (Placeholder) */}
      <Card>
        <CardHeader>
          <CardTitle>Upload New Rulebook</CardTitle>
          <CardDescription>
            Add PDF rulebooks to this game
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Document upload functionality coming soon.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    </div>
  );
}
