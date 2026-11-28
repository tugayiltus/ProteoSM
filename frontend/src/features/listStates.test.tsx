import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { IT_ME, mockApi } from '../test/mockApi';
import { renderRoute } from '../test/renderRoute';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 20, totalCount: 0 };

interface ListCase {
  name: string;
  path: string;
  api: string;
  /** A route with one active filter. */
  filteredPath: string;
  firstUse: string;
  createLabel: string;
}

const CASES: readonly ListCase[] = [
  { name: 'tasks', path: '/tasks', api: 'GET /api/v1/tasks', filteredPath: '/tasks?status=open', firstUse: 'Görev yok.', createLabel: 'Yeni görev' },
  { name: 'problems', path: '/problems', api: 'GET /api/v1/problems', filteredPath: '/problems?status=open', firstUse: 'Henüz problem yok.', createLabel: 'Yeni problem' },
  { name: 'changes', path: '/changes', api: 'GET /api/v1/changes', filteredPath: '/changes?status=draft', firstUse: 'Henüz değişiklik yok.', createLabel: 'Yeni değişiklik' },
  { name: 'releases', path: '/releases', api: 'GET /api/v1/releases', filteredPath: '/releases?status=planned', firstUse: 'Henüz sürüm yok.', createLabel: 'Yeni sürüm' },
];

describe.each(CASES)('$name list states', ({ path, api, filteredPath, firstUse, createLabel }) => {
  it('shows the first-use message and a create action when nothing exists and no filter is set', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, [api]: { status: 200, body: EMPTY_PAGE } });
    renderRoute(path);
    expect(await screen.findByText(firstUse)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Filtreleri temizle' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: createLabel }).length).toBeGreaterThan(1);
  });

  it('shows a no-result message with "Filtreleri temizle" when a filter hides everything, and clearing drops the filter', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, [api]: { status: 200, body: EMPTY_PAGE } });
    const { user, router } = renderRoute(filteredPath);
    expect(await screen.findByText('Bu filtrelerle kayıt bulunamadı.')).toBeInTheDocument();
    expect(screen.queryByText(firstUse)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Filtreleri temizle' }));
    await waitFor(() => expect(router.state.location.search).toBe(''));
    expect(await screen.findByText(firstUse)).toBeInTheDocument();
  });

  it('shows the load error with a retry button, not an empty message', async () => {
    let calls = 0;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      [api]: () => {
        calls += 1;
        return calls === 1 ? { status: 500, body: { code: 'server_error' } } : { status: 200, body: EMPTY_PAGE };
      },
    });
    const { user } = renderRoute(path);
    const retry = await screen.findByRole('button', { name: 'Tekrar dene' });
    expect(screen.queryByText(firstUse)).not.toBeInTheDocument();
    await user.click(retry);
    expect(await screen.findByText(firstUse)).toBeInTheDocument();
  });
});
