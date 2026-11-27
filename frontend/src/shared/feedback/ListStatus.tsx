/**
 * @input A list query state, the item count, the active filter count and the clear and retry callbacks.
 * @output Skeleton rows, an error with retry, a first-use empty state, or a no-result state with a clear action.
 * @position Inside the card of a list page, before the `DataList`; renders nothing once items exist.
 */
import { Button } from '@astryxdesign/core/Button';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from './States';

const DEFAULT_ROWS = 5;

export interface ListStatusProps {
  isPending: boolean;
  isError: boolean;
  error: unknown;
  /** Items on the current page; `undefined` while no data has arrived. */
  count: number | undefined;
  /** Search and filters in use. Above zero, an empty list means "no result", not "first use". */
  activeCount: number;
  onClear: () => void;
  onRetry: () => void;
  /** Text for a list that has no record yet. */
  firstUseMessage?: string;
  rows?: number;
}

/** Tells the list outcomes apart so each one gets its own message and action. */
export function ListStatus({ isPending, isError, error, count, activeCount, onClear, onRetry, firstUseMessage, rows = DEFAULT_ROWS }: ListStatusProps) {
  const { t } = useTranslation();
  if (isError) {
    return <ErrorState error={error} onRetry={onRetry} />;
  }
  if (isPending) {
    return <LoadingState rows={rows} />;
  }
  if (count !== 0) {
    return null;
  }
  if (activeCount > 0) {
    return <EmptyState message={t('states.noResults')} action={<Button label={t('filters.clearAll')} variant="secondary" onClick={onClear} />} />;
  }
  return <EmptyState message={firstUseMessage} />;
}
