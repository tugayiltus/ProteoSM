import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, IT_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';
import { chooseOption } from '../../../test/select';

import type { MonitoringSource } from './api';

const PATH = '/api/v1/admin/monitoring-sources';
const SOURCE: MonitoringSource = {
  id: 'src-1', name: 'Zabbix', teamId: 't-1', teamName: 'Saha Destek', requesterId: 'u-1', requesterName: 'Ali Can', serviceId: null, serviceName: null,
  categoryId: null, categoryName: null, impact: 'high', urgency: 'high', isActive: true, secretSetAt: '2026-11-01T09:00:00Z', lastEventAt: null, failedCount: 3, lastFailedAt: null, version: 5,
};
const pageOf = (items: unknown[]) => ({ status: 200, body: { items, page: 1, pageSize: 100, totalCount: items.length } });
const bodyOf = async (request: Request | undefined): Promise<unknown> => request?.clone().json();

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    [`GET ${PATH}`]: { status: 200, body: [SOURCE] },
    'GET /api/v1/admin/teams': pageOf([{ id: 't-1', name: 'Saha Destek', isActive: true, isDefaultIntake: false, memberCount: 1, version: 1 }]),
    'GET /api/v1/admin/users': pageOf([{ id: 'u-1', firstName: 'Ali', lastName: 'Can', email: 'ali@example.com', role: 'it_staff', isActive: true, departmentId: null, departmentName: null, locationId: null, locationName: null, version: 1 }]),
    'GET /api/v1/admin/categories': { status: 200, body: [] },
    'GET /api/v1/services': pageOf([]),
    ...extra,
  } as never;
}

describe('MonitoringPage', () => {
  it('lists the address, last event and refused requests', async () => {
    mockApi(routes());
    renderRoute('/admin/monitoring');
    const table = await screen.findByRole('table');
    expect(within(table).getByText('/api/v1/monitoring/src-1/events')).toBeInTheDocument();
    expect(within(table).getByText('Olay gelmedi')).toBeInTheDocument();
    expect(within(table).getByText('3')).toBeInTheDocument();
  });

  it('shows the secret of a new source once', async () => {
    const api = mockApi(routes({ [`POST ${PATH}`]: { status: 201, body: { source: SOURCE, secret: 'abc123' } } }));
    const { user } = renderRoute('/admin/monitoring');
    await user.click(await screen.findByRole('button', { name: 'Yeni kaynak' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yeni kaynak' });
    await user.type(within(dialog).getByRole('textbox', { name: /^Ad/ }), 'Zabbix');
    await chooseOption(user, /^Ekip/, 'Saha Destek', dialog);
    await chooseOption(user, /^Talep sahibi/, /Ali Can/, dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));
    expect(await screen.findByText('abc123')).toBeInTheDocument();
    expect(await bodyOf(api.calls.find((c) => c.method === 'POST'))).toMatchObject({ name: 'Zabbix', teamId: 't-1', requesterId: 'u-1', serviceId: null, impact: 'medium', isActive: true });
    await user.click(screen.getAllByRole('button', { name: 'Kapat' }).at(-1)!);
    await waitFor(() => expect(screen.queryByText('abc123')).not.toBeInTheDocument());
  });

  it('replaces the secret only after a confirmation', async () => {
    const api = mockApi(routes({ [`POST ${PATH}/src-1/secret`]: { status: 200, body: { source: SOURCE, secret: 'new-secret' } } }));
    const { user } = renderRoute('/admin/monitoring');
    await user.click(await screen.findByRole('button', { name: 'Anahtarı yenile Zabbix' }));
    expect(api.calls.some((c) => c.method === 'POST')).toBe(false);
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Anahtarı yenile' }));
    expect(await screen.findByText('new-secret')).toBeInTheDocument();
    expect(await bodyOf(api.calls.find((c) => c.method === 'POST'))).toEqual({ version: 5 });
  });

  it('edits a source and shows a taken name at the field', async () => {
    const api = mockApi(routes({ [`PUT ${PATH}/src-1`]: { status: 409, body: { code: 'monitoring.name_taken' } } }));
    const { user } = renderRoute('/admin/monitoring');
    await user.click(await screen.findByRole('button', { name: 'Düzenle Zabbix' }));
    await user.click(await screen.findByRole('button', { name: 'Kaydet' }));
    expect(await screen.findByText('Bu adla başka bir izleme kaynağı var.')).toBeInTheDocument();
    expect(await bodyOf(api.calls.find((c) => c.method === 'PUT'))).toMatchObject({ version: 5, name: 'Zabbix' });
  });

  it('is for admins only', async () => {
    const api = mockApi(routes({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME } }));
    renderRoute('/admin/monitoring');
    await waitFor(() => expect(api.calls.some((c) => c.url.includes('/auth/me'))).toBe(true));
    expect(api.calls.some((c) => c.url.includes('monitoring-sources'))).toBe(false);
  });

  it('says before the confirmation that the old key stops working at once', async () => {
    const api = mockApi(routes());
    const { user } = renderRoute('/admin/monitoring');

    await user.click(await screen.findByRole('button', { name: 'Anahtarı yenile Zabbix' }));
    const dialog = await screen.findByRole('dialog');

    expect(within(dialog).getByText('Zabbix kaynağının eski anahtarı hemen geçersiz olur.')).toBeInTheDocument();
    expect(api.calls.some((call) => call.method === 'POST')).toBe(false);
  });
});
