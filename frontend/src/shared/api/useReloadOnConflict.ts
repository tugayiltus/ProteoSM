import { useQueryClient, type QueryKey } from '@tanstack/react-query';

import { ApiError } from './errors';

const HTTP_CONFLICT = 409;

/**
 * A 409 means the page shows an old state: a stale version, or a state another person already changed.
 * Reload the records, so the next try sends the current version instead of the same 409 again.
 */
export function useReloadOnConflict(queryKey: QueryKey) {
  const queryClient = useQueryClient();
  return (error: Error) => {
    if (error instanceof ApiError && error.status === HTTP_CONFLICT) {
      void queryClient.invalidateQueries({ queryKey });
    }
  };
}
