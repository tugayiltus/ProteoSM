import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, Outlet, RouterProvider, type RouteObject } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AstryxProvider } from '../ui/AstryxProvider';

import { RouteErrorInShell, RouteErrorPage } from './RouteErrorPage';
import { isStaleChunkError } from './staleChunk';

const TITLE = 'Sayfa açılamadı';
const STALE_TITLE = 'Uygulama güncellenmiş olabilir';
const RETRY = 'Yeniden dene';
const BACK = 'Uygulamaya dön';
const SHELL_NAV = 'Kabuk menüsü';
const CHUNK_ERROR = 'Failed to fetch dynamically imported module: /assets/Page-abc.js';

function Shell() {
  return (
    <div>
      <nav aria-label={SHELL_NAV} />
      <Outlet />
    </div>
  );
}

function Throwing(): never {
  throw new Error('render failed');
}

/** The same shape as the entries: a root with the full-page screen, a layout, and an inner route with the in-shell screen. */
const routesWith = (page: RouteObject): RouteObject[] => [
  {
    errorElement: <RouteErrorPage />,
    children: [{ element: <Shell />, errorElement: <RouteErrorPage />, children: [{ errorElement: <RouteErrorInShell />, children: [page] }] }],
  },
];

function renderRoutes(routes: RouteObject[]) {
  const router = createMemoryRouter(routes, { initialEntries: ['/page'] });
  render(
    <AstryxProvider>
      <RouterProvider router={router} />
    </AstryxProvider>,
  );
}

describe('route error screen', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });
  afterEach(() => vi.restoreAllMocks());

  it('shows the screen with both actions when a lazy page chunk fails', async () => {
    renderRoutes(routesWith({ path: 'page', lazy: () => Promise.reject(new Error(CHUNK_ERROR)) }));
    expect(await screen.findByRole('heading', { level: 1, name: STALE_TITLE })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: RETRY })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: BACK })).toHaveAttribute('href', '/');
  });

  it('keeps the shell when a page throws while rendering, and logs the error', async () => {
    renderRoutes(routesWith({ path: 'page', Component: Throwing }));
    expect(await screen.findByRole('heading', { level: 1, name: TITLE })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: SHELL_NAV })).toBeInTheDocument();
    expect(screen.queryByText('render failed')).not.toBeInTheDocument();
    expect(console.error).toHaveBeenCalledWith('Route error', expect.objectContaining({ message: 'render failed' }));
  });

  it('falls back to the full page when the layout itself fails to load', async () => {
    const routes: RouteObject[] = [
      { errorElement: <RouteErrorPage />, children: [{ path: 'page', lazy: () => Promise.reject(new Error(CHUNK_ERROR)), children: [] }] },
    ];
    renderRoutes(routes);
    expect(await screen.findByRole('heading', { level: 1, name: STALE_TITLE })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: SHELL_NAV })).not.toBeInTheDocument();
  });

  it('reloads the page on "Yeniden dene"', async () => {
    const reload = vi.fn();
    vi.stubGlobal('location', { ...window.location, reload });
    renderRoutes(routesWith({ path: 'page', Component: Throwing }));
    await userEvent.setup().click(await screen.findByRole('button', { name: RETRY }));
    expect(reload).toHaveBeenCalledOnce();
  });

  it('recognises the stale-chunk messages of the browsers', () => {
    expect(isStaleChunkError(new Error(CHUNK_ERROR))).toBe(true);
    expect(isStaleChunkError(new TypeError('error loading dynamically imported module'))).toBe(true);
    expect(isStaleChunkError(new Error('render failed'))).toBe(false);
    expect(isStaleChunkError('Failed to fetch dynamically imported module')).toBe(false);
  });
});
