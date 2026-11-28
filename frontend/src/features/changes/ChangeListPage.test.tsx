import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CHANGE, CHANGE_ROW, changeRoutes } from '../../test/changeData';
import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption, findSelect } from '../../test/select';

const PAGE = { items: [CHANGE_ROW], page: 1, pageSize: 20, totalCount: 1 };

describe('ChangeListPage', () => {
  it('lists changes and keeps the queue, status, risk and search filters in the URL', async () => {
    const api = mockApi(changeRoutes(CHANGE, { 'GET /api/v1/changes': { status: 200, body: PAGE } }) as never);
    const { user, router } = renderRoute('/changes?queue=team');

    expect(await screen.findAllByText(CHANGE.title)).not.toHaveLength(0);
    await chooseOption(user, 'Durum', 'Planlandı');
    await chooseOption(user, 'Risk', 'Yüksek');
    await user.type(screen.getByRole('searchbox', { name: 'Numara veya başlıkta ara' }), 'anahtar');

    await waitFor(() => expect(router.state.location.search).toContain('status=scheduled'));
    expect(router.state.location.search).toContain('queue=team');
    expect(router.state.location.search).toContain('risk=high');
    await waitFor(() => expect(router.state.location.search).toContain('anahtar'));
    await waitFor(() =>
      expect(
        api.calls.some((c) => {
          const q = new URL(c.url).searchParams;
          return q.get('status') === 'scheduled' && q.get('queue') === 'team' && q.get('risk') === 'high' && q.get('search') === 'anahtar';
        }),
      ).toBe(true),
    );
  });

  it('offers and sends the this-week queue', async () => {
    const api = mockApi(changeRoutes(CHANGE, { 'GET /api/v1/changes': { status: 200, body: PAGE } }) as never);
    renderRoute('/changes?queue=this_week');

    expect(await screen.findAllByText(CHANGE.title)).not.toHaveLength(0);
    expect(await screen.findByRole('heading', { name: 'Bu hafta uygulanacak' })).toBeInTheDocument();
    expect(api.calls.some((c) => new URL(c.url).searchParams.get('queue') === 'this_week')).toBe(true);
  });

  it('shows the empty message when no change matches', async () => {
    mockApi(changeRoutes(CHANGE, { 'GET /api/v1/changes': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } } }) as never);
    renderRoute('/changes?queue=pending_approval');

    expect(await screen.findByText('Bu filtrelerle kayıt bulunamadı.')).toBeInTheDocument();
  });

  it('reads the service filter from the URL, sends it and offers the Hizmet filter', async () => {
    const api = mockApi(changeRoutes(CHANGE, { 'GET /api/v1/changes': { status: 200, body: PAGE }, 'GET /api/v1/services': { status: 200, body: { items: [{ id: 'svc-1', name: 'SAP', isActive: false, ownerTeamId: 'team-1', ownerTeamName: 'Servis Masası', assetCount: 0, openTicketCount: 0 }], page: 1, pageSize: 100, totalCount: 1 } } }) as never);
    const { user, router } = renderRoute('/changes?serviceId=svc-1');

    expect(await screen.findAllByText(CHANGE.title)).not.toHaveLength(0);
    expect(api.calls.some((c) => new URL(c.url).pathname === '/api/v1/changes' && new URL(c.url).searchParams.get('serviceId') === 'svc-1')).toBe(true);
    await waitFor(async () => expect(await findSelect('Hizmet')).toHaveTextContent('SAP (Pasif)'));
    await user.click(screen.getByRole('button', { name: 'Temizle' }));
    await waitFor(() => expect(router.state.location.search).not.toContain('serviceId'));
  });
});
