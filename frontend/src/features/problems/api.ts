import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import type { components } from '../../shared/api/schema';
import { ROUTES } from '../../shared/routes';
import { changeKeys } from '../changes/api';
import { ticketKeys } from '../tickets/api';

type Schemas = components['schemas'];
export type ProblemDetail = Schemas['ProblemDetail'];
export type ProblemListItem = Schemas['ProblemListItem'];
export type ProblemStatus = Schemas['ProblemStatus'];
export type ProblemTimelineItem = Schemas['ProblemTimelineItem'];
export type ProblemTicketItem = Schemas['ProblemTicketItem'];
export type ProblemChangeItem = Schemas['ProblemChangeItem'];
export type TicketProblemItem = Schemas['TicketProblemItem'];
export type ProblemChangeSideItem = Schemas['ProblemChangeSideItem'];
export type Level = Schemas['Level'];
export type ProblemQueue = 'mine' | 'team' | 'team_open' | 'open' | 'known_errors';

/** Queues offered in the list filter. */
export const PROBLEM_QUEUES: readonly ProblemQueue[] = ['mine', 'team', 'open', 'known_errors'];
/** Open problems of my teams: only the İş listem card links to it, so the list accepts it from the URL but does not offer it. */
export const TEAM_OPEN_QUEUE: ProblemQueue = 'team_open';
export const URL_QUEUES: readonly ProblemQueue[] = [...PROBLEM_QUEUES, TEAM_OPEN_QUEUE];
export const PROBLEM_STATUSES: readonly ProblemStatus[] = ['open', 'investigating', 'known_error', 'resolved', 'cancelled'];
export const LEVELS: readonly Level[] = ['low', 'medium', 'high'];
/** A resolved or cancelled problem is read-only: no edits, state changes or new links. */
export const CLOSED_STATUSES: readonly ProblemStatus[] = ['resolved', 'cancelled'];
const QUEUE_PARAM = 'queue';
const HTTP_CONFLICT = 409;

export const isClosed = (status: ProblemStatus) => CLOSED_STATUSES.includes(status);

/** The problem list opened on one queue, as the İş listem card links to it. */
export const problemQueuePath = (queue: ProblemQueue) => `${ROUTES.problems}?${new URLSearchParams({ [QUEUE_PARAM]: queue }).toString()}`;

export const problemKeys = {
  all: ['problems'],
  list: (params: ProblemListParams) => ['problems', 'list', params],
  detail: (number: string) => ['problems', 'detail', number],
  timeline: (number: string) => ['problems', 'timeline', number],
  tickets: (number: string) => ['problems', 'tickets', number],
  changes: (number: string) => ['problems', 'changes', number],
  /** The problem of one incident, and the problems one change fixes: under `all`, so every problem write refreshes them. */
  ofTicket: (ticketNumber: string) => ['problems', 'of-ticket', ticketNumber],
  ofChange: (changeNumber: string) => ['problems', 'of-change', changeNumber],
} as const;

export interface ProblemListParams {
  queue: ProblemQueue | undefined;
  status: ProblemStatus | undefined;
  impact: Level | undefined;
  /** Only problems of this service; the service page links here. */
  serviceId?: string;
  search: string;
  page: number;
  pageSize: number;
}

export function useProblems(params: ProblemListParams) {
  return useQuery({
    queryKey: problemKeys.list(params),
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/problems', {
          params: {
            query: {
              queue: params.queue,
              status: params.status,
              impact: params.impact,
              serviceId: params.serviceId,
              search: params.search === '' ? undefined : params.search,
              page: params.page,
              pageSize: params.pageSize,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useProblem(number: string) {
  return useQuery({ queryKey: problemKeys.detail(number), queryFn: () => unwrap(api.GET('/api/v1/problems/{number}', { params: { path: { number } } })) });
}

export function useProblemTimeline(number: string) {
  return useQuery({
    queryKey: problemKeys.timeline(number),
    queryFn: () => unwrap(api.GET('/api/v1/problems/{number}/timeline', { params: { path: { number } } })),
  });
}

export function useProblemTickets(number: string) {
  return useQuery({
    queryKey: problemKeys.tickets(number),
    queryFn: () => unwrap(api.GET('/api/v1/problems/{number}/tickets', { params: { path: { number } } })),
  });
}

export function useProblemChanges(number: string) {
  return useQuery({
    queryKey: problemKeys.changes(number),
    queryFn: () => unwrap(api.GET('/api/v1/problems/{number}/changes', { params: { path: { number } } })),
  });
}

/** The incident's problem, or null when it has none (204). Employees get 404, so the caller gates the query by role. */
export function useTicketProblem(ticketNumber: string, enabled: boolean) {
  return useQuery({
    enabled,
    queryKey: problemKeys.ofTicket(ticketNumber),
    queryFn: async (): Promise<TicketProblemItem | null> =>
      (await unwrap(api.GET('/api/v1/tickets/{number}/problem', { params: { path: { number: ticketNumber } } }))) ?? null,
  });
}

export function useChangeProblems(changeNumber: string) {
  return useQuery({
    queryKey: problemKeys.ofChange(changeNumber),
    queryFn: () => unwrap(api.GET('/api/v1/changes/{number}/problems', { params: { path: { number: changeNumber } } })),
  });
}

/** Problem writes show on ticket and change pages too (problem card, history), so all three caches refresh. */
function useInvalidateProblems() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: problemKeys.all });
    await queryClient.invalidateQueries({ queryKey: ticketKeys.all });
    await queryClient.invalidateQueries({ queryKey: changeKeys.all });
  };
}

/** Stores the answer at once, so the next action sends the new version instead of the old one and gets 409. */
function useStoreProblem() {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateProblems();
  return async (updated: ProblemDetail) => {
    queryClient.setQueryData(problemKeys.detail(updated.number), updated);
    await invalidate();
  };
}

/**
 * A 409 means the page shows an old state: a stale version, or a state another person already changed.
 * Reload the problem, so the next try sends the current version instead of the same 409 again.
 */
function useReloadOnConflict() {
  const queryClient = useQueryClient();
  return (error: Error) => {
    if (error instanceof ApiError && error.status === HTTP_CONFLICT) {
      void queryClient.invalidateQueries({ queryKey: problemKeys.all });
    }
  };
}

export interface NewProblemInput {
  title: string;
  description: string;
  impact: Level;
  ownerTeamId: string;
  ownerId: string | null;
  incidentNumber: string | null;
}

/** A 409 from a create with an incident can mean the incident got a problem meanwhile, so the ticket card reloads. */
export function useCreateProblem() {
  const invalidate = useInvalidateProblems();
  const reload = useReloadOnConflict();
  return useMutation({ mutationFn: (body: NewProblemInput) => unwrap(api.POST('/api/v1/problems', { body })), onSuccess: invalidate, onError: reload });
}

export interface ProblemEdit {
  title: string;
  description: string;
  impact: Level;
  rootCause: string | null;
  workaround: string | null;
}

/** `version` is the one the dialog opened with, so a background refetch does not skip the conflict check. */
export function useEditProblem(number: string, version: number) {
  const store = useStoreProblem();
  const reload = useReloadOnConflict();
  return useMutation({
    mutationFn: (edit: ProblemEdit) => unwrap(api.PUT('/api/v1/problems/{number}', { params: { path: { number } }, body: { version, ...edit } })),
    onSuccess: store,
    onError: reload,
  });
}

/** Sets or clears (`null`) the problem's service, in every problem state. */
export function useSetProblemService(problem: Pick<ProblemDetail, 'number' | 'version'>) {
  const store = useStoreProblem();
  const reload = useReloadOnConflict();
  return useMutation({
    mutationFn: (serviceId: string | null) =>
      unwrap(api.PUT('/api/v1/problems/{number}/service', { params: { path: { number: problem.number } }, body: { serviceId, version: problem.version } })),
    onSuccess: store,
    onError: reload,
  });
}

/** One problem action. Every body carries the problem's version (optimistic concurrency). */
export type ProblemAction =
  | { kind: 'start' }
  | { kind: 'known-error' }
  | { kind: 'resolve'; resolution: string }
  | { kind: 'cancel'; reason: string }
  | { kind: 'transfer'; teamId: string; reason: string }
  | { kind: 'owner'; ownerId: string | null }
  | { kind: 'article' };
export type ProblemActionKind = ProblemAction['kind'];

function runAction(problem: Pick<ProblemDetail, 'number' | 'version'>, action: ProblemAction) {
  const path = { params: { path: { number: problem.number } } };
  const version = problem.version;
  switch (action.kind) {
    case 'start':
      return api.POST('/api/v1/problems/{number}/start', { ...path, body: { version } });
    case 'known-error':
      return api.POST('/api/v1/problems/{number}/known-error', { ...path, body: { version } });
    case 'resolve':
      return api.POST('/api/v1/problems/{number}/resolve', { ...path, body: { version, resolution: action.resolution } });
    case 'cancel':
      return api.POST('/api/v1/problems/{number}/cancel', { ...path, body: { version, reason: action.reason } });
    case 'transfer':
      return api.POST('/api/v1/problems/{number}/transfer', { ...path, body: { version, teamId: action.teamId, reason: action.reason } });
    case 'owner':
      return api.POST('/api/v1/problems/{number}/owner', { ...path, body: { version, ownerId: action.ownerId } });
    case 'article':
      return api.POST('/api/v1/problems/{number}/article', { ...path, body: { version } });
  }
}

export function useProblemAction(problem: Pick<ProblemDetail, 'number' | 'version'>) {
  const store = useStoreProblem();
  const reload = useReloadOnConflict();
  return useMutation({ mutationFn: (action: ProblemAction) => unwrap(runAction(problem, action)), onSuccess: store, onError: reload });
}

export function useAddProblemNote(number: string) {
  const invalidate = useInvalidateProblems();
  return useMutation({
    mutationFn: (text: string) => unwrap(api.POST('/api/v1/problems/{number}/notes', { params: { path: { number } }, body: { text } })),
    onSuccess: invalidate,
  });
}

export function useLinkProblemTicket(number: string) {
  const invalidate = useInvalidateProblems();
  return useMutation({
    mutationFn: (ticketNumber: string) => unwrap(api.POST('/api/v1/problems/{number}/tickets', { params: { path: { number } }, body: { ticketNumber } })),
    onSuccess: invalidate,
  });
}

/** Links one incident to the problem whose number the user types, from the incident's side. */
export function useLinkIncidentToProblem(ticketNumber: string) {
  const invalidate = useInvalidateProblems();
  const reload = useReloadOnConflict();
  return useMutation({
    mutationFn: (problemNumber: string) =>
      unwrap(api.POST('/api/v1/problems/{number}/tickets', { params: { path: { number: problemNumber } }, body: { ticketNumber } })),
    onSuccess: invalidate,
    onError: reload,
  });
}

export function useUnlinkProblemTicket(number: string) {
  const invalidate = useInvalidateProblems();
  return useMutation({
    mutationFn: (ticketNumber: string) => unwrap(api.DELETE('/api/v1/problems/{number}/tickets/{ticketNumber}', { params: { path: { number, ticketNumber } } })),
    onSuccess: invalidate,
  });
}

export function useLinkProblemChange(number: string) {
  const invalidate = useInvalidateProblems();
  return useMutation({
    mutationFn: (changeNumber: string) => unwrap(api.POST('/api/v1/problems/{number}/changes', { params: { path: { number } }, body: { changeNumber } })),
    onSuccess: invalidate,
  });
}

export function useUnlinkProblemChange(number: string) {
  const invalidate = useInvalidateProblems();
  return useMutation({
    mutationFn: (changeNumber: string) => unwrap(api.DELETE('/api/v1/problems/{number}/changes/{changeNumber}', { params: { path: { number, changeNumber } } })),
    onSuccess: invalidate,
  });
}
