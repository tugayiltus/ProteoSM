/**
 * @input Software and licence filters, ids and form values.
 * @output Queries and mutations of software, licences, seats and licence keys.
 * @position Data layer of `/licences`, `/software` and `/software/mine`.
 */
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { useReloadOnConflict } from '../../shared/api/useReloadOnConflict';

export type SoftwareItem = components['schemas']['SoftwareItem'];
export type LicenceListItem = components['schemas']['LicenceListItem'];
export type LicenceDetail = components['schemas']['LicenceDetail'];
export type LicenceAssignment = components['schemas']['LicenceAssignmentItem'];
export type LicenceKind = components['schemas']['LicenceKind'];
export type LicenceInput = components['schemas']['CreateLicenceCommand'];
export type LicenceUpdate = components['schemas']['UpdateLicenceRequest'];
export type LicenceState = 'full' | 'expiring' | 'expired';

const OPTIONS_PAGE_SIZE = 100;

export const licenceKeys = {
  all: ['licences'] as const,
  software: ['licences', 'software'] as const,
  detail: (id: string) => ['licences', 'detail', id] as const,
};

const orUndefined = (value: string) => (value === '' ? undefined : value);

export interface SoftwareListParams {
  search: string;
  active: boolean | undefined;
  page: number;
  pageSize: number;
}

export function useSoftware({ search, ...query }: SoftwareListParams) {
  return useQuery({
    queryKey: [...licenceKeys.software, 'list', search, query],
    queryFn: () => unwrap(api.GET('/api/v1/software', { params: { query: { q: orUndefined(search), ...query } } })),
    placeholderData: keepPreviousData,
  });
}

export interface LicenceListParams {
  search: string;
  softwareId: string;
  teamId: string;
  kind: LicenceKind | undefined;
  state: LicenceState | undefined;
  page: number;
  pageSize: number;
}

export function useLicences({ search, softwareId, teamId, kind, state, page, pageSize }: LicenceListParams) {
  return useQuery({
    queryKey: [...licenceKeys.all, 'list', search, softwareId, teamId, kind, state, page, pageSize],
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/licences', {
          params: {
            query: {
              q: orUndefined(search),
              softwareId: orUndefined(softwareId),
              teamId: orUndefined(teamId),
              kind,
              state,
              page,
              pageSize,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useLicence(id: string) {
  return useQuery({ queryKey: licenceKeys.detail(id), queryFn: () => unwrap(api.GET('/api/v1/licences/{id}', { params: { path: { id } } })) });
}

export function useLicenceAssignments(id: string, released: boolean, page: number, pageSize: number) {
  return useQuery({
    queryKey: [...licenceKeys.detail(id), 'assignments', released, page, pageSize],
    queryFn: () => unwrap(api.GET('/api/v1/licences/{id}/assignments', { params: { path: { id }, query: { released, page, pageSize } } })),
    placeholderData: keepPreviousData,
  });
}

export function useAssetLicences(tag: string) {
  return useQuery({
    queryKey: [...licenceKeys.all, 'asset', tag],
    queryFn: () => unwrap(api.GET('/api/v1/assets/{tag}/licences', { params: { path: { tag } } })),
  });
}

export function useMySoftware() {
  return useQuery({ queryKey: [...licenceKeys.all, 'mine'], queryFn: () => unwrap(api.GET('/api/v1/me/software')) });
}

/** Active software whose vendor or name matches, for the licence form. */
export function useSoftwareSearch(search: string) {
  return useSoftware({ search, active: true, page: 1, pageSize: OPTIONS_PAGE_SIZE });
}

/** Every write refreshes lists and details; a conflict does too, because the page then shows an old state. */
function useLicenceWrite<TInput, TResult>(mutationFn: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  const reload = useReloadOnConflict(licenceKeys.all);
  return useMutation({ mutationFn, onSuccess: () => queryClient.invalidateQueries({ queryKey: licenceKeys.all }), onError: reload });
}

export const useCreateSoftware = () =>
  useLicenceWrite((body: components['schemas']['CreateSoftwareCommand']) => unwrap(api.POST('/api/v1/software', { body })));

export const useUpdateSoftware = () =>
  useLicenceWrite(({ id, ...body }: components['schemas']['UpdateSoftwareRequest'] & { id: string }) =>
    unwrap(api.PUT('/api/v1/software/{id}', { params: { path: { id } }, body })),
  );

export const useCreateLicence = () => useLicenceWrite((body: LicenceInput) => unwrap(api.POST('/api/v1/licences', { body })));

export const useUpdateLicence = () =>
  useLicenceWrite(({ id, ...body }: LicenceUpdate & { id: string }) => unwrap(api.PUT('/api/v1/licences/{id}', { params: { path: { id } }, body })));

export const useAssignLicence = () =>
  useLicenceWrite(({ id, ...body }: components['schemas']['AssignLicenceRequest'] & { id: string }) =>
    unwrap(api.POST('/api/v1/licences/{id}/assignments', { params: { path: { id } }, body })),
  );

export const useReleaseLicence = () =>
  useLicenceWrite(({ id, assignmentId }: { id: string; assignmentId: string }) =>
    unwrap(api.DELETE('/api/v1/licences/{id}/assignments/{assignmentId}', { params: { path: { id, assignmentId } } })),
  );

export const useSetLicenceKey = () =>
  useLicenceWrite(({ id, version, key }: { id: string; version: number; key: string | null }) =>
    unwrap(api.PUT('/api/v1/licences/{id}/key', { params: { path: { id } }, body: { version, key } })),
  );

/** Not a query on purpose: the key is never cached, and it is gone when the dialog closes (gcTime 0). */
export function useRevealLicenceKey() {
  return useMutation({
    mutationFn: (id: string) => unwrap(api.POST('/api/v1/licences/{id}/key/reveal', { params: { path: { id } } })),
    gcTime: 0,
  });
}
