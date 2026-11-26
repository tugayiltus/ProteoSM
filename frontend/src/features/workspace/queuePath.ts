import { SEARCH_PARAM } from '../../shared/list/useListParams';
import { OPEN_FILTER_ON, OPEN_FILTER_PARAM, ROUTES, SERVICE_FILTER_PARAM } from '../../shared/routes';
import type { WorkQueue } from '../tickets/api';

export const QUEUE_PARAM = 'queue';

export const queuePath = (queue: WorkQueue) => `${ROUTES.workspaceQueue}?${QUEUE_PARAM}=${queue}`;

/** Queue value for the staff ticket list without a queue filter: every ticket the viewer may see. */
export const ALL_QUEUE = 'all';

export const allTicketsPath = (search: string) => `${ROUTES.workspaceQueue}?${new URLSearchParams({ [QUEUE_PARAM]: ALL_QUEUE, [SEARCH_PARAM]: search }).toString()}`;

/** The open tickets of one service in the staff list ("Tümünü göster" on the service page). */
export const serviceTicketsPath = (serviceId: string) =>
  `${ROUTES.workspaceQueue}?${new URLSearchParams({ [QUEUE_PARAM]: ALL_QUEUE, [SERVICE_FILTER_PARAM]: serviceId, [OPEN_FILTER_PARAM]: OPEN_FILTER_ON }).toString()}`;
