import { redirect } from 'next/navigation';

type SearchParamValue = string | string[];

export default async function OAuthCallbackPage({
  searchParams,
}: {
  searchParams?: Promise<Record<string, SearchParamValue>>;
}) {
  const qs = new URLSearchParams();

  // Next.js 16: searchParams is now async
  const resolvedParams = await searchParams;

  if (resolvedParams) {
    Object.entries(resolvedParams).forEach(([key, value]) => {
      if (Array.isArray(value)) {
        value.forEach(v => qs.append(key, v));
      } else if (value !== undefined) {
        qs.append(key, value);
      }
    });
  }

  const suffix = qs.toString();
  redirect(suffix ? `/login?${suffix}` : '/login');
}
