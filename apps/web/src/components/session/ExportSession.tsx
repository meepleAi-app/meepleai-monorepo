'use client';

/**
 * Export Session Component (Issue #3347)
 *
 * Allows users to export session data as PDF and share on social media.
 */

import { useState, useCallback } from 'react';
import {
  Check,
  ChevronDown,
  Copy,
  Download,
  Facebook,
  FileText,
  Link2,
  Loader2,
  Share2,
  Twitter,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ExportSessionProps {
  /** Session ID */
  sessionId: string;

  /** Session code for display */
  sessionCode: string;

  /** Optional game name */
  gameName?: string;

  /** Whether the session is finalized */
  isFinalized?: boolean;

  /** Custom class name */
  className?: string;
}

export interface ExportOptions {
  includeScoreChart: boolean;
  includeDiceHistory: boolean;
  includeCardHistory: boolean;
}

export interface ShareLinkResponse {
  shareUrl: string;
  ogMetadata: {
    title: string;
    description: string;
    imageUrl?: string;
    url: string;
    type: string;
  };
}

// ============================================================================
// Component
// ============================================================================

export function ExportSession({
  sessionId,
  sessionCode,
  gameName,
  isFinalized: _isFinalized = false,
  className,
}: ExportSessionProps) {
  const [isExportOpen, setIsExportOpen] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [isGeneratingLink, setIsGeneratingLink] = useState(false);
  const [copied, setCopied] = useState(false);
  const [shareLink, setShareLink] = useState<string | null>(null);
  const [exportOptions, setExportOptions] = useState<ExportOptions>({
    includeScoreChart: true,
    includeDiceHistory: false,
    includeCardHistory: false,
  });

  // ==========================================================================
  // Handlers
  // ==========================================================================

  const handleExportPdf = useCallback(async () => {
    setIsExporting(true);

    try {
      const params = new URLSearchParams({
        includeScoreChart: exportOptions.includeScoreChart.toString(),
        includeDiceHistory: exportOptions.includeDiceHistory.toString(),
        includeCardHistory: exportOptions.includeCardHistory.toString(),
      });

      const response = await fetch(
        `/api/v1/game-sessions/${sessionId}/export/pdf?${params}`,
        { credentials: 'include' }
      );

      if (!response.ok) {
        throw new Error('Failed to export PDF');
      }

      // Get filename from Content-Disposition header
      const disposition = response.headers.get('Content-Disposition');
      const filenameMatch = disposition?.match(/filename="(.+)"/);
      const filename = filenameMatch ? filenameMatch[1] : `session_${sessionCode}.pdf`;

      // Download the file
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      setIsExportOpen(false);
    } catch (error) {
      console.error('Export failed:', error);
    } finally {
      setIsExporting(false);
    }
  }, [sessionId, sessionCode, exportOptions]);

  const handleGenerateShareLink = useCallback(async () => {
    setIsGeneratingLink(true);

    try {
      const response = await fetch(`/api/v1/game-sessions/${sessionId}/share-link`, {
        method: 'POST',
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error('Failed to generate share link');
      }

      const data: ShareLinkResponse = await response.json();
      setShareLink(data.shareUrl);
    } catch (error) {
      console.error('Failed to generate share link:', error);
    } finally {
      setIsGeneratingLink(false);
    }
  }, [sessionId]);

  const handleCopyLink = useCallback(async () => {
    if (!shareLink) return;

    try {
      await navigator.clipboard.writeText(shareLink);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (error) {
      console.error('Failed to copy:', error);
    }
  }, [shareLink]);

  const handleShareTwitter = useCallback(() => {
    if (!shareLink) return;

    const text = gameName
      ? `Check out our ${gameName} session on MeepleAI!`
      : `Check out our game session on MeepleAI!`;

    const url = `https://twitter.com/intent/tweet?text=${encodeURIComponent(text)}&url=${encodeURIComponent(shareLink)}`;
    window.open(url, '_blank', 'noopener,noreferrer');
  }, [shareLink, gameName]);

  const handleShareFacebook = useCallback(() => {
    if (!shareLink) return;

    const url = `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(shareLink)}`;
    window.open(url, '_blank', 'noopener,noreferrer');
  }, [shareLink]);

  const handleNativeShare = useCallback(async () => {
    if (!shareLink || !navigator.share) return;

    try {
      await navigator.share({
        title: gameName ? `${gameName} Session` : 'MeepleAI Session',
        text: 'Check out our game session!',
        url: shareLink,
      });
    } catch (error) {
      // User cancelled or share failed
      console.error('Share failed:', error);
    }
  }, [shareLink, gameName]);

  // ==========================================================================
  // Render
  // ==========================================================================

  return (
    <div className={cn('flex items-center gap-2', className)}>
      {/* Export PDF Button */}
      <Dialog open={isExportOpen} onOpenChange={setIsExportOpen}>
        <DialogTrigger asChild>
          <Button variant="outline" size="sm" className="gap-2">
            <FileText className="h-4 w-4" />
            <span className="hidden sm:inline">Export</span>
          </Button>
        </DialogTrigger>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Download className="h-5 w-5 text-primary" />
              Export Session
            </DialogTitle>
            <DialogDescription>
              Download a PDF report of this session
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            {/* Export Options */}
            <div className="space-y-3">
              <div className="flex items-center space-x-2">
                <Checkbox
                  id="include-chart"
                  checked={exportOptions.includeScoreChart}
                  onCheckedChange={(checked) =>
                    setExportOptions((prev) => ({
                      ...prev,
                      includeScoreChart: checked === true,
                    }))
                  }
                />
                <Label htmlFor="include-chart" className="text-sm">
                  Include score progression chart
                </Label>
              </div>

              <div className="flex items-center space-x-2">
                <Checkbox
                  id="include-dice"
                  checked={exportOptions.includeDiceHistory}
                  onCheckedChange={(checked) =>
                    setExportOptions((prev) => ({
                      ...prev,
                      includeDiceHistory: checked === true,
                    }))
                  }
                />
                <Label htmlFor="include-dice" className="text-sm">
                  Include dice roll history
                </Label>
              </div>

              <div className="flex items-center space-x-2">
                <Checkbox
                  id="include-cards"
                  checked={exportOptions.includeCardHistory}
                  onCheckedChange={(checked) =>
                    setExportOptions((prev) => ({
                      ...prev,
                      includeCardHistory: checked === true,
                    }))
                  }
                />
                <Label htmlFor="include-cards" className="text-sm">
                  Include card game history
                </Label>
              </div>
            </div>

            {/* Preview hint */}
            <p className="text-xs text-muted-foreground">
              The PDF will include session details, final standings, and any selected history.
            </p>
          </div>

          {/* Actions */}
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setIsExportOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleExportPdf} disabled={isExporting} className="gap-2">
              {isExporting ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Generating...
                </>
              ) : (
                <>
                  <Download className="h-4 w-4" />
                  Download PDF
                </>
              )}
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      {/* Share Button */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" size="sm" className="gap-2">
            <Share2 className="h-4 w-4" />
            <span className="hidden sm:inline">Share</span>
            <ChevronDown className="h-3 w-3" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          {/* Generate/Copy Link */}
          {!shareLink ? (
            <DropdownMenuItem
              onClick={handleGenerateShareLink}
              disabled={isGeneratingLink}
              className="gap-2"
            >
              {isGeneratingLink ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Link2 className="h-4 w-4" />
              )}
              Generate Share Link
            </DropdownMenuItem>
          ) : (
            <DropdownMenuItem onClick={handleCopyLink} className="gap-2">
              {copied ? (
                <Check className="h-4 w-4 text-green-500" />
              ) : (
                <Copy className="h-4 w-4" />
              )}
              {copied ? 'Copied!' : 'Copy Link'}
            </DropdownMenuItem>
          )}

          <DropdownMenuSeparator />

          {/* Social Share */}
          <DropdownMenuItem
            onClick={handleShareTwitter}
            disabled={!shareLink}
            className="gap-2"
          >
            <Twitter className="h-4 w-4" />
            Share on Twitter
          </DropdownMenuItem>

          <DropdownMenuItem
            onClick={handleShareFacebook}
            disabled={!shareLink}
            className="gap-2"
          >
            <Facebook className="h-4 w-4" />
            Share on Facebook
          </DropdownMenuItem>

          {/* Native Share (Mobile) */}
          {typeof navigator !== 'undefined' && 'share' in navigator && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                onClick={handleNativeShare}
                disabled={!shareLink}
                className="gap-2"
              >
                <Share2 className="h-4 w-4" />
                More Options...
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

export default ExportSession;
