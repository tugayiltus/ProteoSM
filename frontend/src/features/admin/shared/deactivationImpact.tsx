/**
 * @input The kind and id of a catalog entry about to be deactivated.
 * @output What still points at the entry, as counted by the server, and what deactivating does to it.
 * @position Inside the confirm dialog of the brand, model, category and request type pages.
 */
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { api, unwrap } from '../../../shared/api/client';
import { LoadingState } from '../../../shared/feedback/States';

const COUNT_TEXT = {
  models: 'impact.counts.models',
  active_models: 'impact.counts.active_models',
  assets: 'impact.counts.assets',
  active_children: 'impact.counts.active_children',
  open_tickets: 'impact.counts.open_tickets',
} as const;

const isCountKey = (key: string): key is keyof typeof COUNT_TEXT => key in COUNT_TEXT;

type DeactivationKind = 'brands' | 'models' | 'categories' | 'request-types';

function fetchImpact(kind: DeactivationKind, id: string) {
  const path = { params: { path: { id } } };
  switch (kind) {
    case 'brands':
      return unwrap(api.GET('/api/v1/admin/brands/{id}/deactivation-impact', path));
    case 'models':
      return unwrap(api.GET('/api/v1/admin/models/{id}/deactivation-impact', path));
    case 'categories':
      return unwrap(api.GET('/api/v1/admin/categories/{id}/deactivation-impact', path));
    case 'request-types':
      return unwrap(api.GET('/api/v1/admin/request-types/{id}/deactivation-impact', path));
  }
}

function useDeactivationImpact(kind: DeactivationKind, id: string) {
  return useQuery({ queryKey: ['admin', 'deactivation-impact', kind, id], queryFn: () => fetchImpact(kind, id) });
}

/** Counts come from the server; a count of zero is shown too, so "nothing is affected" is a stated fact. */
export function DeactivationImpact({ kind, id }: { kind: DeactivationKind; id: string }) {
  const { t } = useTranslation('admin');
  const impact = useDeactivationImpact(kind, id);
  if (impact.isPending) {
    return <LoadingState />;
  }
  if (impact.isError) {
    return <Text type="supporting">{t('impact.unavailable')}</Text>;
  }
  const entries = impact.data.counts.flatMap(({ key, count }) => (isCountKey(key) ? [{ key, count }] : []));
  return (
    <VStack gap={1}>
      <Text type="label">{t('impact.title')}</Text>
      {entries.map((entry) => (
        <Text key={entry.key} type="body">
          {t(COUNT_TEXT[entry.key], { count: entry.count })}
        </Text>
      ))}
      <Text type="supporting">{t('impact.note')}</Text>
    </VStack>
  );
}
