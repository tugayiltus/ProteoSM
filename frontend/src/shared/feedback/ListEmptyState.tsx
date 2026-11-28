/**
 * @input Active filter count, the clear callback, the first-use message and an optional first-use action.
 * @output An empty list state: with filters a "no result" message and "Filtreleri temizle", without them the first-use message and action.
 * @position The empty branch of every list page, under its `ListToolbar`.
 */
import { Button } from '@astryxdesign/core/Button';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { EmptyState } from './States';

export interface ListEmptyStateProps {
  /** Number of active search and filter values. */
  activeCount: number;
  onClear: () => void;
  /** Shown when no filter is active: the list holds no record yet. */
  message: string;
  /** Shown only when no filter is active, e.g. a create button the user may use. */
  action?: ReactNode;
}

export function ListEmptyState({ activeCount, onClear, message, action }: ListEmptyStateProps) {
  const { t } = useTranslation();
  if (activeCount > 0) {
    return <EmptyState message={t('states.noResults')} action={<Button label={t('states.clearFilters')} variant="secondary" onClick={onClear} />} />;
  }
  return <EmptyState message={message} action={action} />;
}
