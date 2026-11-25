/**
 * @input Announcement ids and form values.
 * @output Queries and mutations of announcements.
 * @position Data layer of `/admin/announcements`.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { useReloadOnConflict } from '../../../shared/api/useReloadOnConflict';

export type Announcement = components['schemas']['AnnouncementItem'];
export type AnnouncementInput = components['schemas']['CreateAnnouncementRequest'];

const announcementsKey = ['admin', 'announcements'] as const;

export function useAnnouncements() {
  return useQuery({ queryKey: announcementsKey, queryFn: () => unwrap(api.GET('/api/v1/admin/announcements')) });
}

function useAnnouncementWrite<TInput>(mutationFn: (input: TInput) => Promise<Announcement>) {
  const queryClient = useQueryClient();
  const reload = useReloadOnConflict(announcementsKey);
  return useMutation({ mutationFn, onSuccess: () => queryClient.invalidateQueries({ queryKey: announcementsKey }), onError: reload });
}

export const useCreateAnnouncement = () => useAnnouncementWrite((body: AnnouncementInput) => unwrap(api.POST('/api/v1/admin/announcements', { body })));

export const useUpdateAnnouncement = () =>
  useAnnouncementWrite(({ id, ...body }: AnnouncementInput & { id: string; version: number }) =>
    unwrap(api.PUT('/api/v1/admin/announcements/{id}', { params: { path: { id } }, body })),
  );
