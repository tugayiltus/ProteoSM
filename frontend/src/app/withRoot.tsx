import type { RouteObject } from 'react-router';

import { RouteErrorPage } from '../shared/feedback/RouteErrorPage';
import { LoadingState } from '../shared/feedback/States';

/**
 * A pathless root gives every route a hydration fallback: the first load of a lazy page renders the loading state
 * instead of nothing (and React Router's HydrateFallback warning, UX-16). It is also the last error boundary: a failure
 * outside every layout shows the full-page error screen.
 */
export const withRoot = (pages: RouteObject[]): RouteObject[] => [{ id: 'root', HydrateFallback: LoadingState, errorElement: <RouteErrorPage />, children: pages }];
