import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { adminKeys } from '../../../shared/api/adminKeys';
import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';

export type AuditEventItem = components['schemas']['AuditEventItem'];

export interface AuditListParams {
  entityType: string | undefined;
  action?: string;
  actorUserId?: string;
  /** ISO instants; the page turns the chosen days into the start and the end of the day. */
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}

export function useAuditEvents(params: AuditListParams) {
  return useQuery({
    queryKey: [...adminKeys.audit, params],
    queryFn: () =>
      unwrap(api.GET('/api/v1/admin/audit-events', { params: { query: { entityType: params.entityType, action: params.action, actorUserId: params.actorUserId, from: params.from, to: params.to, page: params.page, pageSize: params.pageSize } } })),
    placeholderData: keepPreviousData,
  });
}
