/**
 * @output The loading state while the browser loads the other entry page at the same address.
 * @position Catch-all route of each entry for paths that the other entry serves (ADR-0038).
 */
import { useEffect } from 'react';
import { useLocation } from 'react-router';

import { LoadingState } from '../shared/feedback/States';
import { entryUrl } from '../shared/navigation/area';

export function LeaveArea() {
  const location = useLocation();
  const url = entryUrl(location);
  // Synchronizes the browser's document with the entry that serves this address.
  useEffect(() => {
    window.location.replace(url);
  }, [url]);
  return <LoadingState />;
}
