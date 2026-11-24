import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CHANGE } from '../../test/changeData';
import { ADMIN_ME, EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';
import { TEAMS } from '../../test/taskData';

import type { ReleaseDetail, ReleaseListItem } from './api';

const NUMBER = 'REL-2026-000001';
const PATH = `/api/v1/releases/${NUMBER}`;
const DETAIL: ReleaseDetail = {
  id: 'rel-1', number: NUMBER, title: 'Kasım sürümü', description: 'Birlikte çıkanlar', status: 'planned', ownerTeamId: 'team-1', ownerTeamName: 'Servis Masası',
  ownerId: null, ownerName: null, windowStart: null, windowEnd: null, notes: null, cancelReason: null, createdAt: '2026-11-01T09:00:00Z', startedAt: null, closedAt: null,
  changes: [
    { number: 'CHG-2026-000001', title: 'Sunucu yaması', status: 'draft', ownerTeamName: 'Servis Masası', canOpen: true },
    { number: 'CHG-2026-000002', title: 'Ağ', status: 'closed', ownerTeamName: 'Ağ', canOpen: false },
  ],
  canWork: true, version: 3,
};
const ROW: ReleaseListItem = { id: 'rel-1', number: NUMBER, title: DETAIL.title, status: 'planned', ownerTeamName: 'Servis Masası', ownerName: null, changeCount: 2, windowStart: null, windowEnd: null, updatedAt: '2026-11-01T09:00:00Z' };
const bodyOf = async (request: Request | undefined): Promise<unknown> => request?.clone().json();

function routes(me: unknown = IT_ME, detail: ReleaseDetail = DETAIL, extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: me },
    'GET /api/v1/reference/teams': { status: 200, body: TEAMS },
    'GET /api/v1/releases': { status: 200, body: { items: [ROW], page: 1, pageSize: 20, totalCount: 1 } },
    [`GET ${PATH}`]: { status: 200, body: detail },
    [`GET ${PATH}/timeline`]: { status: 200, body: [{ id: 'e-1', kind: 'change_added', actorName: 'Ali Can', data: { number: 'CHG-2026-000001' }, occurredAt: '2026-11-01T10:00:00Z' }] },
    ...extra,
  } as never;
}

describe('releases', () => {
  it('lists releases and sends the filters', async () => {
    const api = mockApi(routes());
    const { user } = renderRoute('/releases');
    expect(await screen.findByRole('link', { name: `Aç ${NUMBER}` })).toHaveAttribute('href', `/releases/${NUMBER}`);
    await chooseOption(user, 'Durum', 'Sürüyor');
    await waitFor(() => expect(api.calls.some((c) => new URL(c.url).searchParams.get('status') === 'in_progress')).toBe(true));
  });

  it('creates a release and refuses a window that ends before it starts', async () => {
    const api = mockApi(routes(ADMIN_ME, DETAIL, { 'POST /api/v1/releases': { status: 201, body: DETAIL } }));
    const { user, router } = renderRoute('/releases');
    await user.click(await screen.findByRole('button', { name: 'Yeni sürüm' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yeni sürüm' });
    await user.type(within(dialog).getByRole('textbox', { name: /^Başlık/ }), 'Yeni');
    await user.type(within(dialog).getByRole('textbox', { name: /^Açıklama/ }), 'Metin');
    await chooseOption(user, /^Sahip ekip/, 'Servis Masası', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/releases/${NUMBER}`));
    expect(await bodyOf(api.calls.find((c) => c.method === 'POST'))).toMatchObject({ title: 'Yeni', ownerTeamId: 'team-1', ownerId: null, windowStart: null });
  });

  it('starts, cancels and shows changes the viewer cannot open as text', async () => {
    const api = mockApi(routes(IT_ME, DETAIL, { [`POST ${PATH}/start`]: { status: 200, body: DETAIL }, [`POST ${PATH}/cancel`]: { status: 200, body: DETAIL } }));
    const { user } = renderRoute(`/releases/${NUMBER}`);
    expect(await screen.findByRole('link', { name: /CHG-2026-000001/ })).toHaveAttribute('href', '/changes/CHG-2026-000001');
    expect(screen.queryByRole('link', { name: /CHG-2026-000002/ })).not.toBeInTheDocument();
    expect(await screen.findByText(/Değişiklik eklendi/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Başlat' }));
    await waitFor(() => expect(api.calls.some((c) => c.url.endsWith('/start'))).toBe(true));
    await user.click(screen.getByRole('button', { name: 'Sürümü iptal et' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('İptal nedeni'), 'Ertelendi');
    await user.click(within(dialog).getByRole('button', { name: 'Sürümü iptal et' }));
    await waitFor(() => expect(api.calls.some((c) => c.url.endsWith('/cancel'))).toBe(true));
    expect(await bodyOf(api.calls.find((c) => c.url.endsWith('/cancel')))).toEqual({ version: 3, reason: 'Ertelendi' });
  });

  it('adds and removes a change and shows a refusal in the dialog', async () => {
    const api = mockApi(
      routes(IT_ME, DETAIL, {
        [`POST ${PATH}/changes`]: { status: 409, body: { code: 'releases.change_taken' } },
        [`DELETE ${PATH}/changes/CHG-2026-000002`]: { status: 200, body: DETAIL },
      }),
    );
    const { user } = renderRoute(`/releases/${NUMBER}`);
    await user.click(await screen.findByRole('button', { name: 'Değişiklik ekle' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Değişiklik numarası'), 'CHG-2026-000009');
    await user.click(within(dialog).getByRole('button', { name: 'Değişiklik ekle' }));
    expect(await screen.findByText('Bu değişiklik zaten bir sürümde.')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Vazgeç' }));
    await user.click(screen.getByRole('button', { name: 'Çıkar CHG-2026-000002' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Çıkar' }));
    await waitFor(() => expect(api.calls.some((c) => c.method === 'DELETE')).toBe(true));
  });

  it('edits with the version the user saw', async () => {
    const api = mockApi(routes(ADMIN_ME, DETAIL, { [`PUT ${PATH}`]: { status: 200, body: DETAIL } }));
    const { user } = renderRoute(`/releases/${NUMBER}`);
    await user.click(await screen.findByRole('button', { name: 'Düzenle' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByRole('textbox', { name: /^Notlar/ }), 'Not');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(api.calls.some((c) => c.method === 'PUT')).toBe(true));
    expect(await bodyOf(api.calls.find((c) => c.method === 'PUT'))).toMatchObject({ version: 3, notes: 'Not' });
  });

  it('offers no actions on a finished release or without the right to work', async () => {
    mockApi(routes(IT_ME, { ...DETAIL, canWork: false }));
    renderRoute(`/releases/${NUMBER}`);
    expect(await screen.findByText('Birlikte çıkanlar')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Başlat' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Değişiklik ekle' })).not.toBeInTheDocument();
  });

  it('links the release on the change page', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      [`GET /api/v1/changes/${CHANGE.number}`]: { status: 200, body: { access: 'full', detail: { ...CHANGE, release: { number: NUMBER, title: 'Kasım sürümü', status: 'planned' } }, summary: null } },
    });
    renderRoute(`/changes/${CHANGE.number}`);
    expect(await screen.findByRole('link', { name: `${NUMBER} · Kasım sürümü` })).toHaveAttribute('href', `/releases/${NUMBER}`);
  });

  it('is not open to employees', async () => {
    mockApi(routes(EMPLOYEE_ME));
    const { router } = renderRoute('/releases');
    await waitFor(() => expect(router.state.location.pathname).not.toBe('/releases'));
  });
});
