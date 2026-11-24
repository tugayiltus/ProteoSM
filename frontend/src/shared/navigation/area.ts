import type { Location } from 'react-router';

import { ROUTES } from '../routes';

/** Query parameter that carries the page to return to after sign-in across a full page load. */
export const RETURN_PARAM = 'from';

/** `/admin` and everything below it is the admin entry; the rest is the user application (ADR-0038). */
export const isAdminPath = (pathname: string) => pathname === ROUTES.admin || pathname.startsWith(`${ROUTES.admin}/`);

/** A path on this origin. "//host" and "/\\host" are protocol-relative: never follow them. */
export const isLocalPath = (path: unknown): path is string =>
  typeof path === 'string' && path.startsWith('/') && !path.startsWith('//') && !path.startsWith('/\\');

/** The address to load; a full page load drops the router state, so the page to return to moves into the query. */
export function entryUrl(location: Pick<Location, 'pathname' | 'search' | 'hash' | 'state'>): string {
  const params = new URLSearchParams(location.search);
  const state: unknown = location.state;
  if (typeof state === 'object' && state !== null && 'from' in state && location.pathname === ROUTES.login && isLocalPath(state.from)) {
    params.set(RETURN_PARAM, state.from);
  }
  const search = params.toString();
  return `${location.pathname}${search === '' ? '' : `?${search}`}${location.hash}`;
}
