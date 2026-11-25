/**
 * @input The edited values and the version they were read with.
 * @output Query and mutation of the company-wide settings.
 * @position Data layer of `/admin/settings`.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';

export type SystemSettings = components['schemas']['SystemSettingsDto'];

const settingsKey = ['admin', 'settings'] as const;

/** Only a conflict reloads the open form's version (query client); any other refetch would skip the conflict check. */
export function useSystemSettings() {
  return useQuery({
    queryKey: settingsKey,
    queryFn: () => unwrap(api.GET('/api/v1/admin/system-settings')),
    gcTime: 0,
    refetchOnReconnect: false,
  });
}

export function useUpdateSystemSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SystemSettings) => unwrap(api.PUT('/api/v1/admin/system-settings', { body })),
    onSuccess: (saved) => queryClient.setQueryData(settingsKey, saved),
  });
}
