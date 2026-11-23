import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';
import { chooseOption } from '../../../test/select';

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });
const SERVICE_ID = '0198c1d2-0000-7000-8000-000000000001';
const TEAM = { id: 't-1', name: 'Saha Destek', isActive: true, isDefaultIntake: false, memberCount: 2, version: 1 };
const SAP = { id: SERVICE_ID, name: 'SAP Finans', ownerTeamId: 't-1', ownerTeamName: 'Saha Destek', isActive: true, assetCount: 3, openTicketCount: 1 };
const MAIL = { ...SAP, id: '0198c1d2-0000-7000-8000-000000000002', name: 'E-posta', isActive: false, assetCount: 0 };
const SAP_DETAIL = { id: SERVICE_ID, name: 'SAP Finans', description: null, ownerTeamId: 't-1', ownerTeamName: 'Saha Destek', isActive: true, version: 4 };

function routes() {
  return {
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    'GET /api/v1/services': { status: 200, body: pageOf([SAP, MAIL]) },
    'GET /api/v1/admin/teams': { status: 200, body: pageOf([TEAM]) },
    [`GET /api/v1/services/${SERVICE_ID}`]: { status: 200, body: SAP_DETAIL },
  };
}

describe('ServicesAdminPage', () => {
  it('lists the name, owner team, status and asset count', async () => {
    mockApi(routes());
    renderRoute('/admin/services');

    const table = await screen.findByRole('table', { name: 'Hizmet listesi' });
    const sapRow = within(table).getByText('SAP Finans').closest('tr')!;
    expect(within(sapRow).getByText('Saha Destek')).toBeInTheDocument();
    expect(within(sapRow).getByText('Aktif')).toBeInTheDocument();
    expect(within(sapRow).getByText('3')).toBeInTheDocument();
    const mailRow = within(table).getByText('E-posta').closest('tr')!;
    expect(within(mailRow).getByText('Pasif')).toBeInTheDocument();
    expect(within(mailRow).getByRole('button', { name: 'Aktif et E-posta' })).toBeInTheDocument();
  });

  it('shows the name-taken error on the name field when creating', async () => {
    let posted: unknown;
    mockApi({
      ...routes(),
      'POST /api/v1/admin/services': async (request) => {
        posted = await request.json();
        return { status: 409, body: { code: 'services.name_taken' } };
      },
    });
    const { user } = renderRoute('/admin/services');

    await user.click(await screen.findByRole('button', { name: 'Yeni hizmet' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/^Hizmet/), 'SAP Finans');
    await chooseOption(user, /^Sahip ekip/, 'Saha Destek', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await within(dialog).findByText('Bu adla başka bir hizmet var.')).toBeInTheDocument();
    expect(posted).toEqual({ name: 'SAP Finans', description: null, ownerTeamId: 't-1' });
  });

  it('deactivates a service with its current version', async () => {
    let posted: unknown;
    mockApi({
      ...routes(),
      [`POST /api/v1/admin/services/${SERVICE_ID}/deactivate`]: async (request) => {
        posted = await request.json();
        return { status: 200, body: { ...SAP_DETAIL, isActive: false, version: 5 } };
      },
    });
    const { user } = renderRoute('/admin/services');

    await user.click(await screen.findByRole('button', { name: 'Pasife al SAP Finans' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: 4 }));
  });

  it('sends the active filter from the URL to the list', async () => {
    const api = mockApi(routes());
    renderRoute('/admin/services?status=inactive');

    await screen.findByRole('table', { name: 'Hizmet listesi' });
    const list = api.calls.find((call) => new URL(call.url).pathname === '/api/v1/services');
    expect(new URL(list!.url).searchParams.get('active')).toBe('false');
  });
});
