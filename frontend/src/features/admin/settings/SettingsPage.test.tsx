import { screen } from '@testing-library/react';
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
});
