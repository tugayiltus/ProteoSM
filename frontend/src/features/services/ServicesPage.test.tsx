import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';
import { SERVICE, serviceRoutes } from '../../test/serviceData';

describe('ServicesPage', () => {
  it('lists services and keeps the text, team and active filters in the URL', async () => {
    const api = mockApi(serviceRoutes() as never);
    const { user, router } = renderRoute('/services');

    expect(await screen.findAllByText(SERVICE.name)).not.toHaveLength(0);
    await chooseOption(user, 'Sahip ekip', 'Saha Destek');
    await chooseOption(user, 'Durum', 'Pasif');
    await user.type(screen.getByRole('searchbox', { name: 'Hizmet ara' }), 'posta');

    await waitFor(() => expect(router.state.location.search).toContain('team=team-2'));
    expect(router.state.location.search).toContain('active=false');
    await waitFor(() => expect(router.state.location.search).toContain('q=posta'));
    await waitFor(() =>
      expect(
        api.calls.some((c) => {
          const q = new URL(c.url).searchParams;
          return c.method === 'GET' && q.get('teamId') === 'team-2' && q.get('active') === 'false' && q.get('q') === 'posta';
        }),
      ).toBe(true),
    );
  });

  it('reads the filters from a shared link', async () => {
    const api = mockApi(serviceRoutes() as never);
    renderRoute('/services?team=team-1&active=true&q=e-posta');

    await waitFor(() =>
      expect(
        api.calls.some((c) => {
          const q = new URL(c.url).searchParams;
          return new URL(c.url).pathname === '/api/v1/services' && q.get('teamId') === 'team-1' && q.get('active') === 'true' && q.get('q') === 'e-posta';
        }),
      ).toBe(true),
    );
  });

  it('opens a service from its row and shows its counts', async () => {
    mockApi(serviceRoutes() as never);
    renderRoute('/services');

    const link = await screen.findByRole('link', { name: `Aç ${SERVICE.name}` });
    expect(link).toHaveAttribute('href', `/services/${SERVICE.id}`);
    const table = screen.getByRole('table', { name: 'Hizmet listesi' });
    expect(within(table).getByRole('cell', { name: '4' })).toBeInTheDocument();
    expect(within(table).getByRole('cell', { name: '7' })).toBeInTheDocument();
  });

  it('shows the empty message when no service matches', async () => {
    mockApi(serviceRoutes({ 'GET /api/v1/services': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } } }) as never);
    renderRoute('/services?q=yok');

    expect(await screen.findByText('Bu filtrelerle eşleşen kayıt yok.')).toBeInTheDocument();
  });

  it('is not open to employees', async () => {
    mockApi(serviceRoutes({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } }) as never);
    const { router } = renderRoute('/services');

    await waitFor(() => expect(router.state.location.pathname).not.toBe('/services'));
  });
});
