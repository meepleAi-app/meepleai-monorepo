import { Card, CardHeader, CardTitle } from '@/components/ui/data-display/card';

const ISSUE_URL = 'https://github.com/meepleAi-app/meepleai-monorepo/issues/1874';

export function QueuePendingPanel() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>⏳ Queue pending re-sync</CardTitle>
      </CardHeader>
      <div className="px-4 py-6 text-center">
        <p className="text-sm text-muted-foreground">
          Queue pending: feature in arrivo (BE{' '}
          <a
            href={ISSUE_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="text-toolkit underline"
          >
            #1874
          </a>
          ).
        </p>
      </div>
    </Card>
  );
}
