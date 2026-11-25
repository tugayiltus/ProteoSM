import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

import type { Announcement } from './api';

const PATH = '/api/v1/admin/announcements';
const ITEM: Announcement = { id: 'a-1', title: 'VPN bakımı', body: 'Kesinti', level: 'warning', startsAt: '2020-01-01T08:00:00Z', endsAt: null, version: 4 };

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    [`GET ${PATH}`]: { status: 200, body: [ITEM, { ...ITEM, id: 'a-2', title: 'Eski', endsAt: '2020-02-01T08:00:00Z' }, { ...ITEM, id: 'a-3', title: 'Gelecek', startsAt: '2999-01-01T08:00:00Z' }] },
    ...extra,
  } as never;
}

describe('AnnouncementsPage', () => {
  it('shows which announcements are scheduled, live and ended', async () => {
    mockApi(routes());
    renderRoute('/admin/announcements');
    const table = await screen.findByRole('table');
    for (const state of ['Yayında', 'Bitti', 'Planlandı']) {
      expect(within(table).getByText(state)).toBeInTheDocument();
    }
  });

  it('creates one that starts now without an end', async () => {
    const api = mockApi(routes({ [`POST ${PATH}`]: { status: 201, body: ITEM } }));
    const { user } = renderRoute('/admin/announcements');
    await user.click(await screen.findByRole('button', { name: 'Yeni duyuru' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yeni duyuru' });
    await user.type(within(dialog).getByRole('textbox', { name: /^Başlık/ }), 'VPN bakımı');
    await user.type(within(dialog).getByRole('textbox', { name: /^Mesaj/ }), 'Kesinti');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));
    await expect.poll(() => api.calls.some((c) => c.method === 'POST')).toBe(true);
    const body: unknown = await api.calls.find((c) => c.method === 'POST')?.clone().json();
    expect(body).toMatchObject({ title: 'VPN bakımı', body: 'Kesinti', level: 'info', endsAt: null });
    const { startsAt } = body as { startsAt: string };
    expect(Math.abs(Date.parse(startsAt) - Date.now())).toBeLessThan(120_000);
  });

  it('shows a refused period at the end field and sends the version', async () => {
    const api = mockApi(routes({ [`PUT ${PATH}/a-1`]: { status: 409, body: { code: 'announcements.invalid_period' } } }));
    const { user } = renderRoute('/admin/announcements');
    await user.click(await screen.findByRole('button', { name: 'Düzenle VPN bakımı' }));
    await user.click(await screen.findByRole('button', { name: 'Kaydet' }));
    expect(await screen.findByText('Bitiş, başlangıçtan sonra olmalı.')).toBeInTheDocument();
    const body: unknown = await api.calls.find((c) => c.method === 'PUT')?.clone().json();
    expect(body).toMatchObject({ version: 4 });
  });
});
