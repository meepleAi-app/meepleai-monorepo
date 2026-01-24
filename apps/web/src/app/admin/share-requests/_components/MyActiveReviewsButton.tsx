'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMyReviews } from '@/hooks/queries';
import { Button } from '@/components/ui/primitives/button';
import { Badge } from '@/components/ui/data-display/badge';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from '@/components/ui/navigation/sheet';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { FileText, Loader2 } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

/**
 * My Active Reviews Button Component
 *
 * Displays a button with badge count showing number of active reviews.
 * Opens a sheet/popover with list of currently locked reviews.
 * Click on a review to navigate to its detail page.
 *
 * Features:
 * - Badge count indicator
 * - Auto-refresh every 30 seconds
 * - Quick navigation to review detail
 * - Expiration time display
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 */

export function MyActiveReviewsButton(): JSX.Element {
  const router = useRouter();
  const { data: activeReviews, isLoading } = useMyReviews();
  const [open, setOpen] = useState(false);

  const count = activeReviews?.length ?? 0;

  const handleReviewClick = (shareRequestId: string) => {
    setOpen(false);
    router.push(`/admin/share-requests/${shareRequestId}`);
  };

  return (
    <>
      <Button
        variant="outline"
        size="sm"
        onClick={() => setOpen(true)}
        className="relative"
        data-testid="my-active-reviews-button"
      >
        <FileText className="h-4 w-4 mr-2" />
        My Reviews
        {count > 0 && (
          <Badge
            variant="default"
            className="ml-2 bg-blue-600 text-white px-1.5 py-0 text-xs h-5 min-w-[1.25rem] flex items-center justify-center"
          >
            {count}
          </Badge>
        )}
      </Button>

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent side="right" className="w-96">
          <SheetHeader>
            <SheetTitle>Active Reviews</SheetTitle>
            <SheetDescription>
              Share requests you are currently reviewing
            </SheetDescription>
          </SheetHeader>

          <div className="mt-6">
            {isLoading && (
              <div className="flex items-center justify-center py-8">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            )}

            {!isLoading && count === 0 && (
              <div className="py-8 text-center">
                <FileText className="h-12 w-12 mx-auto text-muted-foreground/50 mb-3" />
                <p className="text-sm text-muted-foreground">No active reviews</p>
                <p className="text-xs text-muted-foreground mt-1">
                  Start reviewing a share request to see it here
                </p>
              </div>
            )}

            {!isLoading && count > 0 && (
              <ScrollArea className="h-[calc(100vh-12rem)]">
                <div className="space-y-3">
                  {activeReviews?.map((review) => (
                    <ActiveReviewItem
                      key={review.shareRequestId}
                      review={review}
                      onClick={() => handleReviewClick(review.shareRequestId)}
                    />
                  ))}
                </div>
              </ScrollArea>
            )}
          </div>
        </SheetContent>
      </Sheet>
    </>
  );
}

interface ActiveReviewItemProps {
  review: {
    shareRequestId: string;
    gameTitle: string;
    contributorName: string;
    reviewLockExpiresAt: string;
    status: string;
  };
  onClick: () => void;
}

function ActiveReviewItem({ review, onClick }: ActiveReviewItemProps): JSX.Element {
  const timeRemaining = new Date(review.reviewLockExpiresAt).getTime() - Date.now();
  const isExpiringSoon = timeRemaining < 5 * 60 * 1000; // 5 minutes

  return (
    <button
      onClick={onClick}
      className="w-full p-4 rounded-lg border border-border bg-card hover:bg-muted/50 transition-colors text-left"
      data-testid={`active-review-${review.shareRequestId}`}
    >
      <div className="space-y-2">
        <div className="flex items-start justify-between gap-2">
          <h4 className="font-medium text-sm line-clamp-2">{review.gameTitle}</h4>
          {isExpiringSoon && (
            <Badge variant="outline" className="border-yellow-300 bg-yellow-50 text-yellow-700 text-xs shrink-0">
              Expiring
            </Badge>
          )}
        </div>

        <p className="text-xs text-muted-foreground">
          by {review.contributorName}
        </p>

        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <FileText className="h-3 w-3" />
          <span>
            Expires {formatDistanceToNow(new Date(review.reviewLockExpiresAt), { addSuffix: true })}
          </span>
        </div>
      </div>
    </button>
  );
}