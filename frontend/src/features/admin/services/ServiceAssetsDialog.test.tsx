import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { findAlert } from '../../../test/alerts';
import { LAPTOP } from '../../../test/assetData';
import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';
import { chooseOption } from '../../../test/select';

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });
const SERVICE_ID = '0198c1d2-0000-7000-8000-000000000001';
const SAP = { id: SERVICE_ID, name: 'SAP Finans', ownerTeamId: 't-1', ownerTeamName: 'Saha Destek', isActive: true, assetCount: 1, openTicketCount: 0 };
const LINKED = { id: 'a-9', tag: 'SRV-0009', name: 'Dell R740', status: 'assigned' };
const EMPTY_SECTION = { items: [], total: 0 };
const IMPACT = { assets: { items: [LINKED], total: 1 }, tickets: EMPTY_SECTION, changes: EMPTY_SECTION, problems: EMPTY_SECTION };
const RETIRED = { ...LAPTOP, id: 'a-2', assetTag: 'DT-0002', status: 'retired' };

function routes() {
  return {
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    'GET /api/v1/services': { status: 200, body: pageOf([SAP]) },
    [`GET /api/v1/services/${SERVICE_ID}/impact`]: { status: 200, body: IMPACT },
    'GET /api/v1/assets': { status: 200, body: pageOf([RETIRED]) },
  };
}

async function openDialog() {
  const rendered = renderRoute('/admin/services');
  await rendered.user.click(await screen.findByRole('button', { name: 'Varlıklar SAP Finans' }));
  return { ...rendered, dialog: await screen.findByRole('dialog') };
}

describe('ServiceAssetsDialog', () => {
  it('searches assets by tag and shows why an asset cannot be linked', async () => {
    let posted: unknown;
    const api = mockApi({
      ...routes(),
      [`POST /api/v1/admin/services/${SERVICE_ID}/assets`]: async (request) => {
        posted = await request.json();
        return { status: 409, body: { code: 'services.asset_not_linkable' } };
      },
    });
    const { user, dialog } = await openDialog();

    expect(await within(dialog).findByText('SRV-0009')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Etiket ara'), 'DT-0002');
    await waitFor(() =>
      expect(api.calls.some((call) => new URL(call.url).pathname === '/api/v1/assets' && new URL(call.url).searchParams.get('search') === 'DT-0002')).toBe(true),
    );
    await chooseOption(user, 'Bağlanacak varlık', /DT-0002/, dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Varlık bağla' }));

    expect(await findAlert('Kullanım dışı veya kayıp cihaz hizmete bağlanamaz.')).toBeInTheDocument();
    expect(posted).toEqual({ assetId: 'a-2' });
  });

  it('unlinks a linked asset by its id', async () => {
    let unlinked = false;
    mockApi({
      ...routes(),
      [`DELETE /api/v1/admin/services/${SERVICE_ID}/assets/a-9`]: () => {
        unlinked = true;
        return { status: 204 };
      },
    });
    const { user, dialog } = await openDialog();

    await user.click(await within(dialog).findByRole('button', { name: 'Bağı kaldır SRV-0009' }));

    await waitFor(() => expect(unlinked).toBe(true));
  });
});
