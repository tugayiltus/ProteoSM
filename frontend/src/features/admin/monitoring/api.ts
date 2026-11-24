/**
 * @input Monitoring source ids and form values.
 * @output Queries and mutations of monitoring sources.
 * @position Data layer of `/admin/monitoring`. A new secret is in the mutation result only, never in the query cache.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { useReloadOnConflict } from '../../../shared/api/useReloadOnConflict';

export type MonitoringSource = components['schemas']['MonitoringSourceItem'];
export type MonitoringSourceInput = components['schemas']['CreateMonitoringSourceRequest'];

const monitoringKey = ['admin', 'monitoring'] as const;

export function useMonitoringSources() {
  return useQuery({ queryKey: monitoringKey, queryFn: () => unwrap(api.GET('/api/v1/admin/monitoring-sources')) });
}

function useMonitoringWrite<TInput, TResult>(mutationFn: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  const reload = useReloadOnConflict(monitoringKey);
  // gcTime 0: a result may hold the secret and must not stay in memory after the dialog closes.
  return useMutation({ mutationFn, gcTime: 0, onSuccess: () => queryClient.invalidateQueries({ queryKey: monitoringKey }), onError: reload });
}

export const useCreateMonitoringSource = () => useMonitoringWrite((body: MonitoringSourceInput) => unwrap(api.POST('/api/v1/admin/monitoring-sources', { body })));

export const useUpdateMonitoringSource = () =>
  useMonitoringWrite(({ id, ...body }: MonitoringSourceInput & { id: string; version: number }) =>
    unwrap(api.PUT('/api/v1/admin/monitoring-sources/{id}', { params: { path: { id } }, body })),
  );

export const useReplaceMonitoringSecret = () =>
  useMonitoringWrite(({ id, version }: { id: string; version: number }) =>
    unwrap(api.POST('/api/v1/admin/monitoring-sources/{id}/secret', { params: { path: { id } }, body: { version } })),
  );
