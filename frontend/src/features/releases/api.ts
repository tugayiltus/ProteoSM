/**
 * @input Release filters, numbers and form values.
 * @output Queries and mutations of releases.
 * @position Data layer of `/releases` and `/releases/:number`. Writes also refresh changes, which show their release.
 */
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { useReloadOnConflict } from '../../shared/api/useReloadOnConflict';
import { changeKeys } from '../changes/api';

type Schemas = components['schemas'];
export type ReleaseDetail = Schemas['ReleaseDetail'];
export type ReleaseListItem = Schemas['ReleaseListItem'];
export type ReleaseChangeItem = Schemas['ReleaseChangeItem'];
export type ReleaseStatus = Schemas['ReleaseStatus'];
export type ReleaseTimelineItem = Schemas['ReleaseTimelineItem'];
export type ReleaseInput = Schemas['CreateReleaseRequest'];

export const RELEASE_STATUSES = ['planned', 'in_progress', 'completed', 'cancelled'] as const satisfies readonly ReleaseStatus[];
export const isOpenRelease = (status: ReleaseStatus) => status === 'planned' || status === 'in_progress';

export const releaseKeys = {
  all: ['releases'] as const,
  detail: (number: string) => ['releases', 'detail', number] as const,
};

export interface ReleaseListParams {
  search: string;
  status: ReleaseStatus | undefined;
  teamId: string;
  page: number;
  pageSize: number;
}

const orUndefined = (value: string) => (value === '' ? undefined : value);

export function useReleases({ search, status, teamId, page, pageSize }: ReleaseListParams) {
  return useQuery({
    queryKey: [...releaseKeys.all, 'list', search, status, teamId, page, pageSize],
    queryFn: () => unwrap(api.GET('/api/v1/releases', { params: { query: { q: orUndefined(search), status, teamId: orUndefined(teamId), page, pageSize } } })),
    placeholderData: keepPreviousData,
  });
}

export function useRelease(number: string) {
  return useQuery({ queryKey: releaseKeys.detail(number), queryFn: () => unwrap(api.GET('/api/v1/releases/{number}', { params: { path: { number } } })) });
}

export function useReleaseTimeline(number: string) {
  return useQuery({
    queryKey: [...releaseKeys.detail(number), 'timeline'],
    queryFn: () => unwrap(api.GET('/api/v1/releases/{number}/timeline', { params: { path: { number } } })),
  });
}

function useReleaseWrite<TInput>(mutationFn: (input: TInput) => Promise<ReleaseDetail>) {
  const queryClient = useQueryClient();
  const reload = useReloadOnConflict(releaseKeys.all);
  return useMutation({
    mutationFn,
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: releaseKeys.all }), queryClient.invalidateQueries({ queryKey: changeKeys.all })]),
    onError: reload,
  });
}

export const useCreateRelease = () => useReleaseWrite((body: ReleaseInput) => unwrap(api.POST('/api/v1/releases', { body })));

export const useUpdateRelease = () =>
  useReleaseWrite(({ number, ...body }: ReleaseInput & { number: string; version: number }) =>
    unwrap(api.PUT('/api/v1/releases/{number}', { params: { path: { number } }, body })),
  );

export type ReleaseStep = 'start' | 'complete';

export const useReleaseStep = () =>
  useReleaseWrite(({ number, version, step }: { number: string; version: number; step: ReleaseStep }) => {
    const options = { params: { path: { number } }, body: { version } };
    return step === 'start' ? unwrap(api.POST('/api/v1/releases/{number}/start', options)) : unwrap(api.POST('/api/v1/releases/{number}/complete', options));
  });

export const useCancelRelease = () =>
  useReleaseWrite(({ number, ...body }: { number: string; version: number; reason: string }) =>
    unwrap(api.POST('/api/v1/releases/{number}/cancel', { params: { path: { number } }, body })),
  );

export const useAddReleaseChange = () =>
  useReleaseWrite(({ number, changeNumber }: { number: string; changeNumber: string }) =>
    unwrap(api.POST('/api/v1/releases/{number}/changes', { params: { path: { number } }, body: { changeNumber } })),
  );

export const useRemoveReleaseChange = () =>
  useReleaseWrite(({ number, changeNumber }: { number: string; changeNumber: string }) =>
    unwrap(api.DELETE('/api/v1/releases/{number}/changes/{changeNumber}', { params: { path: { number, changeNumber } } })),
  );
