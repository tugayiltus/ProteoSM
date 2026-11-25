/**
 * @input The edited values and the version they were read with.
 * @output Query and mutation of the company-wide settings.
 * @position Data layer of `/admin/settings`.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { useReloadOnConflict } from '../../../shared/api/useReloadOnConflict';

export type SystemSettings = components['schemas']['SystemSettingsDto'];

const settingsKey = ['admin', 'settings'] as const;

export function useSystemSettings() {
  return useQuery({ queryKey: settingsKey, queryFn: () => unwrap(api.GET('/api/v1/admin/system-settings')) });
}

export function useUpdateSystemSettings() {
  const queryClient = useQueryClient();
  const reload = useReloadOnConflict(settingsKey);
  return useMutation({
    mutationFn: (body: SystemSettings) => unwrap(api.PUT('/api/v1/admin/system-settings', { body })),
    onSuccess: (saved) => queryClient.setQueryData(settingsKey, saved),
    onError: reload,
  });
}
