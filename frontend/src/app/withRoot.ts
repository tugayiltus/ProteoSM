import type { RouteObject } from 'react-router';

import { LoadingState } from '../shared/feedback/States';

/**
 * A pathless root gives every route a hydration fallback: the first load of a lazy page renders the loading state
 * instead of nothing (and React Router's HydrateFallback warning, UX-16).
 */
export const withRoot = (pages: RouteObject[]): RouteObject[] => [{ id: 'root', HydrateFallback: LoadingState, children: pages }];
