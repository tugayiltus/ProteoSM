import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';
import { PROBLEM, PROBLEM_ROW, problemRoutes } from '../../test/problemData';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption, findSelect } from '../../test/select';
import { stubPhoneViewport } from '../../test/viewport';

const PAGE = { items: [PROBLEM_ROW], page: 1, pageSize: 20, totalCount: 1 };

describe('ProblemListPage', () => {
  it('phone_card_shows_sla_line', async () => {
    stubPhoneViewport();
    const sla = { metric: 'known_error', dueAt: '2026-11-24T09:00:00Z', remainingSeconds: 9000, isPaused: false, isWarned: false, isBreached: false, result: null };
    mockApi(problemRoutes(PROBLEM, { 'GET /api/v1/problems': { status: 200, body: { ...PAGE, items: [{ ...PROBLEM_ROW, sla }] } } }) as never);
    renderRoute('/problems');

    expect(await screen.findByText('Bilinen hataya kadar:')).toBeInTheDocument();
    expect(screen.getByText('Kalan 2 sa 30 dk')).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Problem listesi' })).not.toBeInTheDocument();
  });

  it('list_shows_sla_state', async () => {
    const sla = { metric: 'known_error', dueAt: '2026-11-24T09:00:00Z', remainingSeconds: -3660, isPaused: false, isWarned: true, isBreached: true, result: null };
    const withSla = { ...PAGE, items: [{ ...PROBLEM_ROW, sla }, { ...PROBLEM_ROW, id: 'prb-2', number: 'PRB-2026-000002', sla: null }] };
    mockApi(problemRoutes(PROBLEM, { 'GET /api/v1/problems': { status: 200, body: withSla } }) as never);
    renderRoute('/problems');

    const table = await screen.findByRole('table', { name: 'Problem listesi' });
    expect(within(table).getAllByText('İhlal · 1 sa 1 dk gecikme')).toHaveLength(1);
    expect(within(table).getAllByText('Bilinen hataya kadar:')).toHaveLength(1);
  });

  it('lists problems and keeps the queue, status, impact and search filters in the URL', async () => {
    const api = mockApi(problemRoutes(PROBLEM, { 'GET /api/v1/problems': { status: 200, body: PAGE } }) as never);
    const { user, router } = renderRoute('/problems?queue=team');

    expect(await screen.findAllByText(PROBLEM.title)).not.toHaveLength(0);
    expect(screen.getByRole('heading', { name: 'Ekibimin problemleri' })).toBeInTheDocument();
    await chooseOption(user, 'Durum', 'Bilinen hata');
    await chooseOption(user, 'Etki', 'Yüksek');
    await user.type(screen.getByRole('searchbox', { name: 'Numara veya başlıkta ara' }), 'vpn');

    await waitFor(() => expect(router.state.location.search).toContain('status=known_error'));
    expect(router.state.location.search).toContain('queue=team');
    expect(router.state.location.search).toContain('impact=high');
    await waitFor(() => expect(router.state.location.search).toContain('vpn'));
    await waitFor(() =>
      expect(
        api.calls.some((c) => {
          const q = new URL(c.url).searchParams;
          return q.get('status') === 'known_error' && q.get('queue') === 'team' && q.get('impact') === 'high' && q.get('search') === 'vpn';
        }),
      ).toBe(true),
    );
  });

  it('opens the open team problems queue the workspace card links to', async () => {
    const api = mockApi(problemRoutes(PROBLEM, { 'GET /api/v1/problems': { status: 200, body: PAGE } }) as never);
    renderRoute('/problems?queue=team_open');

    expect(await screen.findByRole('heading', { name: 'Ekibimin açık problemleri' })).toBeInTheDocument();
    expect(api.calls.some((c) => new URL(c.url).searchParams.get('queue') === 'team_open')).toBe(true);
  });

  it('opens a problem from its row', async () => {
    mockApi(problemRoutes(PROBLEM, { 'GET /api/v1/problems': { status: 200, body: PAGE } }) as never);
    renderRoute('/problems');

    const link = await screen.findByRole('link', { name: `Aç ${PROBLEM.number}` });
    expect(link).toHaveAttribute('href', `/problems/${PROBLEM.number}`);
    expect(within(screen.getByRole('table', { name: 'Problem listesi' })).getByRole('cell', { name: '3' })).toBeInTheDocument();
  });

  it('shows the empty message when no problem matches', async () => {
    mockApi(problemRoutes(PROBLEM, { 'GET /api/v1/problems': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } } }) as never);
    renderRoute('/problems?queue=known_errors');

    expect(await screen.findByText('Bu filtreyle problem yok.')).toBeInTheDocument();
  });

  it('reads the service filter from the URL and sends it', async () => {
    const api = mockApi(problemRoutes(PROBLEM, { 'GET /api/v1/problems': { status: 200, body: PAGE }, 'GET /api/v1/services': { status: 200, body: { items: [{ id: 'svc-1', name: 'SAP', isActive: true, ownerTeamId: 'team-1', ownerTeamName: 'Servis Masası', assetCount: 0, openTicketCount: 0 }], page: 1, pageSize: 100, totalCount: 1 } } }) as never);
    renderRoute('/problems?serviceId=svc-1&open=true');

    expect(await screen.findAllByText(PROBLEM.title)).not.toHaveLength(0);
    expect(api.calls.some((c) => new URL(c.url).pathname === '/api/v1/problems' && new URL(c.url).searchParams.get('serviceId') === 'svc-1' && new URL(c.url).searchParams.get('open') === 'true')).toBe(true);
    expect(screen.getByRole('checkbox', { name: 'Yalnız açık kayıtlar' })).toBeChecked();
    await waitFor(async () => expect(await findSelect('Hizmet')).toHaveTextContent('SAP'));
  });
});
