import { matchPath, useLocation } from 'react-router';

import { ROUTE_IDS, ROUTE_META, type RouteId } from './routeMeta';

/**
 * Pages a detail page may return to with their filters (spec §4.5); anything else falls back to the default parent.
 * The service page is a record hub: its impact rows return to it.
 */
export const RETURNABLE_LISTS: readonly RouteId[] = ['myTickets', 'workspace', 'workspaceQueue', 'tasks', 'changes', 'changeCalendar', 'approvals', 'assets', 'myAssets', 'kbList', 'report', 'reportList', 'serviceDetail'];

export interface ReturnTarget {
  routeId: RouteId;
  href: string;
  params: Record<string, string>;
  /** Breadcrumb text of a return page whose title comes from loaded data (a service name). */
  label?: string;
}

/** The route id whose pattern matches a pathname; static patterns win over parameterised ones. */
export function matchRouteId(pathname: string): { routeId: RouteId; params: Record<string, string> } | null {
  const ordered = [...ROUTE_IDS].sort((a, b) => Number(ROUTE_META[a].path.includes(':')) - Number(ROUTE_META[b].path.includes(':')));
  for (const routeId of ordered) {
    const match = matchPath({ path: ROUTE_META[routeId].path, end: true }, pathname);
    if (match !== null) {
      return { routeId, params: Object.fromEntries(Object.entries(match.params).filter((entry): entry is [string, string] => entry[1] !== undefined)) };
    }
  }
  return null;
}

/** Reads `state.from` left by a list link; only same-app paths of a returnable list are accepted. */
export function parseReturn(state: unknown): ReturnTarget | null {
  if (typeof state !== 'object' || state === null || !('from' in state)) {
    return null;
  }
  const from = (state as { from: unknown }).from;
  if (typeof from !== 'string' || !from.startsWith('/') || from.startsWith('//') || from.startsWith('/\\')) {
    return null;
  }
  const url = new URL(from, window.location.origin);
  if (url.origin !== window.location.origin) {
    return null;
  }
  const match = matchRouteId(url.pathname);
  if (match === null || !RETURNABLE_LISTS.includes(match.routeId)) {
    return null;
  }
  const label = (state as { label?: unknown }).label;
  return { routeId: match.routeId, href: `${url.pathname}${url.search}`, params: match.params, ...(typeof label === 'string' && label !== '' ? { label } : {}) };
}

/**
 * State for a link from a list to a detail, so the detail can return to the same filtered list.
 * `label` names a return page with a data title; its breadcrumb then shows that name, not "loading".
 */
export const linkState = (location: { pathname: string; search: string }, label?: string): { from: string; label?: string } =>
  label === undefined ? { from: `${location.pathname}${location.search}` } : { from: `${location.pathname}${location.search}`, label };

/** Where a detail page's "back" goes: the list it came from, or `defaultTo`. */
export function useReturnTo(defaultTo: string): string {
  const location = useLocation();
  return parseReturn(location.state)?.href ?? defaultTo;
}
