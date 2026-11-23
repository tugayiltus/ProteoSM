import { SEARCH_PARAM } from '../../shared/list/useListParams';
import { ROUTES, SERVICE_FILTER_PARAM } from '../../shared/routes';
import type { TicketQueue } from '../tickets/api';

export const QUEUE_PARAM = 'queue';

export const queuePath = (queue: TicketQueue) => `${ROUTES.workspaceQueue}?${QUEUE_PARAM}=${queue}`;

/** Queue value for the staff ticket list without a queue filter: every ticket the viewer may see. */
export const ALL_QUEUE = 'all';

export const allTicketsPath = (search: string) => `${ROUTES.workspaceQueue}?${new URLSearchParams({ [QUEUE_PARAM]: ALL_QUEUE, [SEARCH_PARAM]: search }).toString()}`;

/** Every ticket of one service in the staff list ("Tümünü göster" on the service page). */
export const serviceTicketsPath = (serviceId: string) =>
  `${ROUTES.workspaceQueue}?${new URLSearchParams({ [QUEUE_PARAM]: ALL_QUEUE, [SERVICE_FILTER_PARAM]: serviceId }).toString()}`;
