/**
 * ShareChatModal Component (Issue #2052)
 *
 * Modal for creating shareable links for chat threads.
 * Allows users to generate public links with view or comment permissions.
 *
 * Features:
 * - WCAG 2.1 AA compliant accessibility
 * - Role selector (view/comment)
 * - Expiry duration selector (7/30 days)
 * - Optional label for organization
 * - One-time shareable URL with copy-to-clipboard
 * - Keyboard accessible (ESC to close, Tab navigation)
 * - Loading and error states
 * - Success state with URL display and revoke option
 *
 * @example
 * ```tsx
 * <ShareChatModal
 *   isOpen={isModalOpen}
 *   threadId={currentThreadId}
 *   onClose={() => setIsModalOpen(false)}
 *   onShareLinkCreated={(shareLink) => {
 *     toast.success('Share link created!');
 *   }}
 * />
 * ```
 */

import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { api, type CreateShareLinkResponse } from '@/lib/api';
import { Copy, Check, AlertCircle, Share2, Eye, MessageSquare, Calendar, Info } from 'lucide-react';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

export interface ShareChatModalProps {
  /** Whether the modal is open */
  isOpen: boolean;

  /** Chat thread ID to share */
  threadId: string;

  /** Callback when modal should close */
  onClose: () => void;

  /** Callback when share link is successfully created */
  onShareLinkCreated?: (shareLink: CreateShareLinkResponse) => void;
}

interface FormData {
  role: 'view' | 'comment';
  expiryDays: number;
  label: string;
}

/**
 * ShareChatModal component
 */
export function ShareChatModal({
  isOpen,
  threadId,
  onClose,
  onShareLinkCreated,
}: ShareChatModalProps) {
  // Form state
  const [formData, setFormData] = useState<FormData>({
    role: 'view',
    expiryDays: 7,
    label: '',
  });

  // UI state
  const [isCreating, setIsCreating] = useState(false);
  const [createdShareLink, setCreatedShareLink] = useState<CreateShareLinkResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  // Reset state when modal opens/closes
  useEffect(() => {
    if (!isOpen) {
      setFormData({ role: 'view', expiryDays: 7, label: '' });
      setCreatedShareLink(null);
      setError(null);
      setCopied(false);
      setIsCreating(false);
    }
  }, [isOpen]);

  const handleCreateShareLink = async () => {
    setIsCreating(true);
    setError(null);

    try {
      const result = await api.shareLinks.createShareLink({
        threadId,
        role: formData.role,
        expiryDays: formData.expiryDays,
        label: formData.label.trim() || undefined,
      });

      setCreatedShareLink(result);
      onShareLinkCreated?.(result);

      logger.info(`Share link created: ${result.shareLinkId} with role ${result.role}`);
    } catch (err: unknown) {
      const context = createErrorContext('ShareChatModal', 'createShareLink', { threadId });
      const error = err instanceof Error ? err : new Error(String(err));
      logger.error('Failed to create share link', error, context);
      setError(err instanceof Error ? err.message : 'Failed to create share link');
    } finally {
      setIsCreating(false);
    }
  };

  const handleCopyLink = async () => {
    if (!createdShareLink) return;

    try {
      await navigator.clipboard.writeText(createdShareLink.shareableUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err: unknown) {
      const error = err instanceof Error ? err : new Error(String(err));
      logger.error('Failed to copy link to clipboard', error);
    }
  };

  const handleClose = () => {
    onClose();
  };

  // Show success view if link was created
  if (createdShareLink) {
    return (
      <Dialog open={isOpen} onOpenChange={handleClose}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Share2 className="h-5 w-5 text-green-600" />
              Share Link Created
            </DialogTitle>
            <DialogDescription>
              Your shareable link is ready. Copy and share it with others.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            {/* Role badge */}
            <div>
              <Label className="text-sm font-medium">Access Level</Label>
              <div className="mt-1.5">
                <Badge variant={createdShareLink.role === 'comment' ? 'default' : 'secondary'}>
                  {createdShareLink.role === 'view' && (
                    <>
                      <Eye className="mr-1 h-3 w-3" />
                      View Only
                    </>
                  )}
                  {createdShareLink.role === 'comment' && (
                    <>
                      <MessageSquare className="mr-1 h-3 w-3" />
                      View + Comment
                    </>
                  )}
                </Badge>
              </div>
            </div>

            {/* Expiry date */}
            <div>
              <Label className="text-sm font-medium">Expires</Label>
              <div className="mt-1.5 flex items-center gap-2 text-sm text-muted-foreground">
                <Calendar className="h-4 w-4" />
                {new Date(createdShareLink.expiresAt).toLocaleDateString('en-US', {
                  year: 'numeric',
                  month: 'long',
                  day: 'numeric',
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </div>
            </div>

            {/* Shareable URL */}
            <div>
              <Label htmlFor="shareable-url" className="text-sm font-medium">
                Shareable Link
              </Label>
              <div className="mt-1.5 flex gap-2">
                <Input
                  id="shareable-url"
                  value={createdShareLink.shareableUrl}
                  readOnly
                  className="font-mono text-sm"
                  onClick={e => (e.target as HTMLInputElement).select()}
                />
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  onClick={handleCopyLink}
                  aria-label={copied ? 'Copied!' : 'Copy link to clipboard'}
                >
                  {copied ? (
                    <Check className="h-4 w-4 text-green-600" />
                  ) : (
                    <Copy className="h-4 w-4" />
                  )}
                </Button>
              </div>
              <p className="mt-1.5 text-xs text-muted-foreground">
                Anyone with this link can access the chat thread.
              </p>
            </div>

            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription className="text-sm">
                You can revoke this link anytime from the chat thread menu.
              </AlertDescription>
            </Alert>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={handleClose}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    );
  }

  // Show form view for creating link
  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Share Chat Thread</DialogTitle>
          <DialogDescription>
            Create a public link to share this conversation with others.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {/* Role selector */}
          <div className="space-y-2">
            <Label htmlFor="role">Access Level</Label>
            <Select
              value={formData.role}
              onValueChange={value =>
                setFormData({ ...formData, role: value as 'view' | 'comment' })
              }
            >
              <SelectTrigger id="role">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="view">
                  <div className="flex items-center gap-2">
                    <Eye className="h-4 w-4" />
                    <div>
                      <div className="font-medium">View Only</div>
                      <div className="text-xs text-muted-foreground">
                        Recipients can read messages and citations
                      </div>
                    </div>
                  </div>
                </SelectItem>
                <SelectItem value="comment">
                  <div className="flex items-center gap-2">
                    <MessageSquare className="h-4 w-4" />
                    <div>
                      <div className="font-medium">View + Comment</div>
                      <div className="text-xs text-muted-foreground">
                        Recipients can add new messages (rate-limited)
                      </div>
                    </div>
                  </div>
                </SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Expiry duration */}
          <div className="space-y-2">
            <Label htmlFor="expiry">Link Expires After</Label>
            <Select
              value={formData.expiryDays.toString()}
              onValueChange={value => setFormData({ ...formData, expiryDays: parseInt(value) })}
            >
              <SelectTrigger id="expiry">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="7">7 days</SelectItem>
                <SelectItem value="30">30 days</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Optional label */}
          <div className="space-y-2">
            <Label htmlFor="label">
              Label <span className="text-xs text-muted-foreground">(optional)</span>
            </Label>
            <Input
              id="label"
              placeholder="e.g., Shared with Design Team"
              value={formData.label}
              onChange={e => setFormData({ ...formData, label: e.target.value })}
              maxLength={200}
            />
            <p className="text-xs text-muted-foreground">Help you identify this link later</p>
          </div>

          {/* Error alert */}
          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={handleClose} disabled={isCreating}>
            Cancel
          </Button>
          <LoadingButton
            type="button"
            onClick={handleCreateShareLink}
            isLoading={isCreating}
            loadingText="Creating..."
          >
            <Share2 className="mr-2 h-4 w-4" />
            Create Share Link
          </LoadingButton>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
