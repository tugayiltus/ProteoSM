/**
 * @input Nothing; talks to `/api/v1/admin/email-settings`.
 * @output Hooks to read and save the outgoing mail settings, send a test mail and switch the sender.
 * @position The admin "Giden posta ayarları" page.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';

export type EmailSettings = components['schemas']['EmailSettingsDto'];
export type EmailSettingsInput = components['schemas']['UpdateEmailSettingsRequest'];
export type EmailProvider = components['schemas']['EmailProvider'];
export type EmailSecurity = components['schemas']['EmailSecurity'];
export type EmailTestResult = components['schemas']['EmailTestResult'];

const SETTINGS_KEY = ['admin', 'email-settings'] as const;

/**
 * Reads the outgoing mail settings. Only a conflict reloads them while the form is open (query client), so the form
 * keeps the version it was read with.
 */
export function useEmailSettings() {
  return useQuery({
    queryKey: SETTINGS_KEY,
    queryFn: () => unwrap(api.GET('/api/v1/admin/email-settings')),
    gcTime: 0,
    refetchOnReconnect: false,
  });
}

function useSettingsMutation<TInput>(send: (input: TInput) => Promise<EmailSettings>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: send, onSuccess: (settings) => queryClient.setQueryData(SETTINGS_KEY, settings) });
}

/** Saves the settings. Active settings must pass a test send on the server first. */
export const useSaveEmailSettings = () =>
  useSettingsMutation((input: EmailSettingsInput) => unwrap(api.PUT('/api/v1/admin/email-settings', { body: input })));

/** Puts the tested panel settings in use. */
export const useActivateEmailSettings = () =>
  useSettingsMutation((input: { version: number }) => unwrap(api.POST('/api/v1/admin/email-settings/activate', { body: input })));

/** Sends with the `.env` values again; the panel values stay stored. */
export const useUseEnvironmentEmail = () =>
  useSettingsMutation((input: { version: number }) => unwrap(api.POST('/api/v1/admin/email-settings/use-environment', { body: input })));

/** A test send records its result on the server, so the settings are read again. */
export function useTestEmailSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/admin/email-settings/test')),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SETTINGS_KEY }),
  });
}
