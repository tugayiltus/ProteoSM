/**
 * @input A ticket.
 * @output True while the ticket is refetched or a reply is sent.
 * @position Guards the work actions of the ticket page.
 */
import { useIsFetching, useIsMutating } from '@tanstack/react-query';

import { ticketKeys, type TicketDetail } from './api';

/**
 * Every action sends the version shown here. A reply being sent changes it, and a refetch then brings the new one:
 * wait for both, or the action (and a dialog, which keeps the version it opened with) would fail as a conflict.
 */
export function useTicketBusy(ticket: TicketDetail): boolean {
  const replying = useIsMutating({ mutationKey: ticketKeys.addComment }) > 0;
  return useIsFetching({ queryKey: ticketKeys.detail(ticket.number) }) > 0 || replying;
}

export type MainAction = 'claim' | 'reply' | null;

/**
 * "Üzerime al" while the viewer can work on the ticket and nobody has it; otherwise "Yanıtla" for anyone who sees the reply box.
 * A closed ticket has no main action. The server still decides each action.
 */
export function mainActionOf(ticket: TicketDetail): MainAction {
  if (ticket.status === 'completed' || ticket.status === 'cancelled') {
    return null;
  }
  return ticket.canWork && ticket.assigneeId === null ? 'claim' : 'reply';
}
