import { Card, CardHeader, CardTitle } from '@/components/ui/data-display/card';

const ISSUE_URL = 'https://github.com/meepleAi-app/meepleai-monorepo/issues/1874';

export function FailedItemsPanel() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>✕ Failed items (last 30gg)</CardTitle>
      </CardHeader>
      <div className="px-4 py-6 text-center">
        <p className="text-sm text-muted-foreground">
          Failed items: feature in arrivo (BE{' '}
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
