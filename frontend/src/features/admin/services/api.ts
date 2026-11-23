import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo } from 'react';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import type { SelectOption } from '../../../shared/form/formFields';
import { assetKeys } from '../../assets/api';
import { adminKeys } from '../shared/queryKeys';
import { REFERENCE_PAGE_SIZE } from '../shared/referenceOptions';

export type ServiceListItem = components['schemas']['ServiceListItem'];
export type ServiceDetail = components['schemas']['ServiceDetail'];
export type ServiceRef = components['schemas']['ServiceRef'];
export type ImpactAsset = components['schemas']['ImpactAsset'];
export type ServiceInput = components['schemas']['CreateServiceCommand'];

export interface ServiceListParams {
  search: string;
  active: boolean | undefined;
  /** Owner team; the IT services page filters by it, the admin page does not. */
  teamId?: string;
  page: number;
  pageSize: number;
}

/** The admin and IT services pages both read through the IT read API; the admin API only writes. */
export function useAdminServices(params: ServiceListParams) {
  return useQuery({
    queryKey: [...adminKeys.services, 'list', params],
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/services', {
          params: { query: { q: params.search === '' ? undefined : params.search, active: params.active, teamId: params.teamId, page: params.page, pageSize: params.pageSize } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useServiceDetail(id: string, enabled = true) {
  return useQuery({
    queryKey: [...adminKeys.services, 'detail', id],
    queryFn: () => unwrap(api.GET('/api/v1/services/{id}', { params: { path: { id } } })),
    enabled,
  });
}

export function useServiceImpact(id: string) {
  return useQuery({
    queryKey: [...adminKeys.services, 'impact', id],
    queryFn: () => unwrap(api.GET('/api/v1/services/{id}/impact', { params: { path: { id } } })),
  });
}

/** Active services, for the asset page picker and the record service picker. Only an active service can be set. */
export function useActiveServiceOptions(enabled: boolean): SelectOption[] {
  const services = useQuery({
    queryKey: [...adminKeys.services, 'options'],
    queryFn: () => unwrap(api.GET('/api/v1/services', { params: { query: { active: true, pageSize: REFERENCE_PAGE_SIZE } } })),
    enabled,
  });
  return useMemo(() => (services.data?.items ?? []).filter((s) => s.isActive).map((s) => ({ value: s.id, label: s.name })), [services.data]);
}

/** Every service, also inactive ones, for the list filters: old records keep an inactive service. */
export function useServiceFilterOptions(): SelectOption[] {
  const services = useQuery({
    queryKey: [...adminKeys.services, 'filterOptions'],
    queryFn: () => unwrap(api.GET('/api/v1/services', { params: { query: { pageSize: REFERENCE_PAGE_SIZE } } })),
  });
  return useMemo(() => (services.data?.items ?? []).map((s) => ({ value: s.id, label: s.name })), [services.data]);
}

/** Active teams; only these can own a service. */
export function useOwnerTeamOptions(): SelectOption[] {
  const teams = useQuery({
    queryKey: [...adminKeys.teams, 'options', 'active'],
    queryFn: () => unwrap(api.GET('/api/v1/admin/teams', { params: { query: { isActive: true, pageSize: REFERENCE_PAGE_SIZE } } })),
  });
  return useMemo(() => (teams.data?.items ?? []).map((team) => ({ value: team.id, label: team.name })), [teams.data]);
}

/** Service writes change the admin list, the impact view and the services shown on asset pages. */
function useInvalidateServices() {
  const queryClient = useQueryClient();
  return () => Promise.all([queryClient.invalidateQueries({ queryKey: adminKeys.services }), queryClient.invalidateQueries({ queryKey: assetKeys.all })]);
}

export function useCreateService() {
  const invalidate = useInvalidateServices();
  return useMutation({ mutationFn: (body: ServiceInput) => unwrap(api.POST('/api/v1/admin/services', { body })), onSuccess: invalidate });
}

export function useUpdateService() {
  const invalidate = useInvalidateServices();
  return useMutation({
    mutationFn: ({ id, ...body }: ServiceInput & { id: string; version: number }) =>
      unwrap(api.PUT('/api/v1/admin/services/{id}', { params: { path: { id } }, body })),
    onSuccess: invalidate,
  });
}

export function useSetServiceActive() {
  const invalidate = useInvalidateServices();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/services/{id}/activate', options))
        : unwrap(api.POST('/api/v1/admin/services/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}

export function useLinkServiceAsset() {
  const invalidate = useInvalidateServices();
  return useMutation({
    mutationFn: ({ serviceId, assetId }: { serviceId: string; assetId: string }) =>
      unwrap(api.POST('/api/v1/admin/services/{id}/assets', { params: { path: { id: serviceId } }, body: { assetId } })),
    onSuccess: invalidate,
  });
}

export function useUnlinkServiceAsset() {
  const invalidate = useInvalidateServices();
  return useMutation({
    mutationFn: ({ serviceId, assetId }: { serviceId: string; assetId: string }) =>
      unwrap(api.DELETE('/api/v1/admin/services/{id}/assets/{assetId}', { params: { path: { id: serviceId, assetId } } })),
    onSuccess: invalidate,
  });
}
