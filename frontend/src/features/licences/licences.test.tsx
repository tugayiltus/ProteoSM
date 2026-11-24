import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';
import { TEAMS } from '../../test/taskData';

import type { LicenceDetail, LicenceListItem, SoftwareItem } from './api';

const SOFTWARE: SoftwareItem = { id: 'sw-1', name: 'Office', vendor: 'Microsoft', isActive: true, licenceCount: 1, version: 2 };
const ROW: LicenceListItem = {
  id: 'lic-1', name: 'Yıllık plan', softwareId: 'sw-1', softwareName: 'Office', vendor: 'Microsoft', kind: 'per_user', quantity: 5, used: 2,
  ownerTeamId: 'team-1', ownerTeamName: 'Servis Masası', expiresOn: '2027-03-01', isExpired: false,
};
const DETAIL: LicenceDetail = { ...ROW, notes: null, hasKey: true, version: 4 };
const SEAT = { id: 'seat-1', userId: 'u-9', userName: 'Ayşe Kaya', assetId: null, assetTag: null, targetAvailable: false, assignedAt: '2026-11-01T09:00:00Z', releasedAt: null };
const page = (items: unknown[]) => ({ status: 200, body: { items, page: 1, pageSize: 20, totalCount: items.length } });

const bodyOf = async (request: Request | undefined): Promise<unknown> => request?.clone().json();

function routes(me: unknown = IT_ME, extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: me },
    'GET /api/v1/reference/teams': { status: 200, body: TEAMS },
    'GET /api/v1/software': page([SOFTWARE]),
    'GET /api/v1/licences': page([ROW]),
    'GET /api/v1/licences/lic-1': { status: 200, body: DETAIL },
    'GET /api/v1/licences/lic-1/assignments': page([SEAT]),
    ...extra,
  } as never;
}

describe('licences', () => {
  it('lists licences with their seats and sends the filters', async () => {
    const api = mockApi(routes());
    const { user } = renderRoute('/licences');
    const table = await screen.findByRole('table', { name: 'Lisans listesi' });
    expect(within(table).getByText('2 / 5')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Aç Office Yıllık plan' })).toHaveAttribute('href', '/licences/lic-1');
    await chooseOption(user, 'Tür', 'Cihaz başına');
    await chooseOption(user, 'Durum', 'Dolu');
    await waitFor(() =>
      expect(api.calls.some((c) => new URL(c.url).searchParams.get('kind') === 'per_device' && new URL(c.url).searchParams.get('state') === 'full')).toBe(true),
    );
  });

  it('creates a licence', async () => {
    const api = mockApi(routes(IT_ME, { 'POST /api/v1/licences': { status: 201, body: DETAIL } }));
    const { user } = renderRoute('/licences');
    await user.click(await screen.findByRole('button', { name: 'Yeni lisans' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yeni lisans' });
    await user.type(within(dialog).getByRole('textbox', { name: /^Lisans/ }), 'Yeni plan');
    await chooseOption(user, /^Yazılım/, 'Microsoft Office', dialog);
    await chooseOption(user, /^Sahip ekip/, 'Servis Masası', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));
    await waitFor(() => expect(api.calls.find((c) => c.method === 'POST')).toBeDefined());
    expect(await bodyOf(api.calls.find((c) => c.method === 'POST'))).toMatchObject({ softwareId: 'sw-1', name: 'Yeni plan', kind: 'per_user', quantity: 1, expiresOn: null });
  });

  it('assigns a user, marks an unavailable target and releases a seat', async () => {
    const api = mockApi(
      routes(IT_ME, {
        'GET /api/v1/reference/users': { status: 200, body: [{ id: 'u-1', fullName: 'Ali Can', email: 'ali@example.com' }] },
        'POST /api/v1/licences/lic-1/assignments': { status: 200, body: DETAIL },
        'DELETE /api/v1/licences/lic-1/assignments/seat-1': { status: 204 },
      }),
    );
    const { user } = renderRoute('/licences/lic-1');
    expect(await screen.findByText('Kullanılamıyor')).toBeInTheDocument();
    expect(screen.queryByText('Lisans anahtarı')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Atamayı kaldır Ayşe Kaya' }));
    await waitFor(() => expect(api.calls.some((c) => c.method === 'DELETE')).toBe(true));
    await user.click(screen.getByRole('button', { name: 'Ata' }));
    const dialog = await screen.findByRole('dialog', { name: 'Ata' });
    await user.type(within(dialog).getByRole('searchbox', { name: 'Kullanıcı ara' }), 'ali');
    await waitFor(() => expect(api.calls.some((c) => c.url.includes('/reference/users'))).toBe(true));
    await chooseOption(user, 'Kullanıcı', 'Ali Can', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Ata' }));
    await waitFor(() => expect(api.calls.find((c) => c.method === 'POST')).toBeDefined());
    expect(await bodyOf(api.calls.find((c) => c.method === 'POST'))).toEqual({ userId: 'u-1', assetId: null });
  });

  it('shows the key to an admin only after a confirmation', async () => {
    const api = mockApi(routes(ADMIN_ME, { 'POST /api/v1/licences/lic-1/key/reveal': { status: 200, body: { key: 'KEY-42' } } }));
    const { user } = renderRoute('/licences/lic-1');
    await user.click(await screen.findByRole('button', { name: 'Anahtarı göster' }));
    expect(api.calls.some((c) => c.url.includes('reveal'))).toBe(false);
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Anahtarı göster' }));
    expect(await screen.findByText('KEY-42')).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: 'Kapat' }).at(-1)!);
    await waitFor(() => expect(screen.queryByText('KEY-42')).not.toBeInTheDocument());
  });

  it('stores and clears a key', async () => {
    const api = mockApi(routes(ADMIN_ME, { 'PUT /api/v1/licences/lic-1/key': { status: 200, body: DETAIL } }));
    const { user } = renderRoute('/licences/lic-1');
    await user.click(await screen.findByRole('button', { name: 'Anahtarı değiştir' }));
    await user.type(within(await screen.findByRole('dialog')).getByRole('textbox', { name: 'Lisans anahtarı' }), 'NEW-1');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(api.calls.find((c) => c.method === 'PUT')).toBeDefined());
    expect(await bodyOf(api.calls.find((c) => c.method === 'PUT'))).toEqual({ version: 4, key: 'NEW-1' });
    await user.click(await screen.findByRole('button', { name: 'Anahtarı değiştir' }));
    await user.click(screen.getByRole('button', { name: 'Anahtarı sil' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Anahtarı sil' }));
    await waitFor(() => expect(api.calls.filter((c) => c.method === 'PUT')).toHaveLength(2));
    expect(await bodyOf(api.calls.filter((c) => c.method === 'PUT').at(-1))).toEqual({ version: 4, key: null });
  });

  it('never overwrites a change made while the edit dialog was open', async () => {
    let version = DETAIL.version;
    const api = mockApi(
      routes(IT_ME, {
        'GET /api/v1/licences/lic-1': () => ({ status: 200, body: { ...DETAIL, version } }),
        'PUT /api/v1/licences/lic-1': () => {
          version += 1;
          return { status: 409, body: { code: 'concurrency.conflict' } };
        },
      }),
    );
    const { user } = renderRoute('/licences/lic-1');
    await user.click(await screen.findByRole('button', { name: 'Düzenle' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(api.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
    expect(await bodyOf(api.calls.find((c) => c.method === 'PUT'))).toMatchObject({ version: 4 });
  });

  it('finds software of a long list by text', async () => {
    const api = mockApi(routes());
    const { user } = renderRoute('/licences');
    await user.click(await screen.findByRole('button', { name: 'Yeni lisans' }));
    await user.type(within(await screen.findByRole('dialog')).getByRole('searchbox', { name: 'Yazılım ara' }), 'zoom');
    await waitFor(() => expect(api.calls.some((c) => new URL(c.url).pathname === '/api/v1/software' && new URL(c.url).searchParams.get('q') === 'zoom')).toBe(true));
  });

  it('edits software and shows a taken name at the field', async () => {
    const api = mockApi(routes(IT_ME, { 'PUT /api/v1/software/sw-1': { status: 409, body: { code: 'licences.software_name_taken' } } }));
    const { user } = renderRoute('/software');
    await user.click(await screen.findByRole('button', { name: 'Düzenle Office' }));
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    expect(await screen.findByText('Bu üretici ve adla başka bir yazılım var.')).toBeInTheDocument();
    expect(await bodyOf(api.calls.find((c) => c.method === 'PUT'))).toMatchObject({ version: 2, isActive: true });
  });

  it('shows employees only their own software', async () => {
    mockApi(routes(EMPLOYEE_ME, { 'GET /api/v1/me/software': { status: 200, body: [{ name: 'Office', vendor: 'Microsoft' }] } }));
    renderRoute('/software/mine');
    expect(await screen.findByText('Office')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Lisanslar' })).not.toBeInTheDocument();
  });

  it('is not open to employees', async () => {
    mockApi(routes(EMPLOYEE_ME));
    const { router } = renderRoute('/licences');
    await waitFor(() => expect(router.state.location.pathname).not.toBe('/licences'));
  });
});
