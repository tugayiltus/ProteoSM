import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const PATH = '/api/v1/admin/system-settings';
const SETTINGS = { licenceNoticeDays: 30, warrantyWindowDays: 90, version: 7 };

describe('SettingsPage', () => {
  it('saves the values with the version they were read with', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      [`GET ${PATH}`]: { status: 200, body: SETTINGS },
      [`PUT ${PATH}`]: { status: 200, body: { ...SETTINGS, licenceNoticeDays: 45, version: 8 } },
    });
    const { user } = renderRoute('/admin/settings');
    const notice = await screen.findByLabelText(/Lisans bitiş uyarısı/);
    await user.clear(notice);
    await user.type(notice, '45');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    expect(await screen.findByText('Ayarlar kaydedildi.')).toBeInTheDocument();
    const body: unknown = await api.calls.find((c) => c.method === 'PUT')?.clone().json();
    expect(body).toEqual({ licenceNoticeDays: 45, warrantyWindowDays: 90, version: 7 });
  });

  it('refuses a value out of range before it reaches the server', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, [`GET ${PATH}`]: { status: 200, body: SETTINGS } });
    const { user } = renderRoute('/admin/settings');
    const notice = await screen.findByLabelText(/Lisans bitiş uyarısı/);
    await user.clear(notice);
    await user.type(notice, '400');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    expect(await screen.findByText('Geçersiz değer.')).toBeInTheDocument();
    expect(api.calls.some((c) => c.method === 'PUT')).toBe(false);
  });

  it('keeps the input and the warning after a conflict, and saves over the reloaded version', async () => {
    let current = SETTINGS;
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, [`PUT ${PATH}`]: { status: 409, body: { code: 'concurrency.conflict' } } });
    api.set(`GET ${PATH}`, () => ({ status: 200, body: current }));
    const { user } = renderRoute('/admin/settings');
    const notice = await screen.findByLabelText(/Lisans bitiş uyarısı/);
    await user.clear(notice);
    await user.type(notice, '45');
    current = { ...SETTINGS, warrantyWindowDays: 60, version: 9 };
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(api.calls.filter((c) => c.method === 'GET' && c.url.includes(PATH)).length).toBeGreaterThan(1));
    expect(await screen.findByText(/başka biri tarafından değiştirildi/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Lisans bitiş uyarısı/)).toHaveValue('45');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(api.calls.filter((c) => c.method === 'PUT')).toHaveLength(2));
    const body: unknown = await api.calls.filter((c) => c.method === 'PUT').at(-1)?.clone().json();
    expect(body).toMatchObject({ licenceNoticeDays: 45, version: 9 });
  });
});
