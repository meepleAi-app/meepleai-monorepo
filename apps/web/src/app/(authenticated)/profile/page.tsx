import { Suspense } from 'react';

import { ProfilePageContent } from './_components/ProfilePageContent';

export default function ProfilePage(): React.JSX.Element {
  return (
    <Suspense fallback={<div className="min-h-screen bg-background" />}>
      <ProfilePageContent />
    </Suspense>
  );
}
