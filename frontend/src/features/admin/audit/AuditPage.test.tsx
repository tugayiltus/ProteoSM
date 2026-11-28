import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';
import { chooseOption } from '../../../test/select';

const EVENT = {
  id: 'e-1',
  occurredAt: '2026-09-26T09:05:00Z',
  actorUserId: 'a-1',
  actorName: 'Tugay İltuş',
  action: 'users.role_changed',
  entityType: 'user',
  entityId: 'u-1',
  reason: null,
  oldValues: '{"role":"employee"}',
  newValues: '{"role":"it_staff"}',
  correlationId: 'corr-1',
};

describe('AuditPage', () => {
  it('shows Turkish labels, local time and change details, and filters by entity type', async () => {
    const mock = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/audit-events': { status: 200, body: { items: [EVENT], page: 1, pageSize: 20, totalCount: 1 } },
    });
    const { user } = renderRoute('/admin/audit');

    expect((await screen.findAllByText(/12:05/)).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Rol değiştirildi').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Tugay İltuş').length).toBeGreaterThan(0);
    await user.click(screen.getAllByRole('button', { name: 'Ayrıntılar' })[0]!);
    expect((await screen.findAllByText(/it_staff/)).length).toBeGreaterThan(0);

    await chooseOption(user, 'Kayıt türü', 'Ekip');

    await waitFor(() => expect(mock.calls.some((r) => new URL(r.url).searchParams.get('entityType') === 'team')).toBe(true));
  });

  it('shows the user, action, entity and period filters and sends them to the server', async () => {
    const mock = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/users': { status: 200, body: { items: [{ id: 'a-1', firstName: 'Tugay', lastName: 'İltuş', email: 't@example.com', role: 'admin', isActive: true }], page: 1, pageSize: 100, totalCount: 1 } },
      'GET /api/v1/admin/audit-events': { status: 200, body: { items: [EVENT], page: 1, pageSize: 20, totalCount: 1 } },
    });
    const { user } = renderRoute('/admin/audit?from=2026-09-01&to=2026-09-30');

    expect(await screen.findByText('Yalnızca yöneticilere açıktır', { exact: false })).toBeInTheDocument();
    for (const label of ['Kullanıcı', 'Eylem', 'Başlangıç günü', 'Bitiş günü', 'Kayıt türü']) {
      expect(screen.getAllByLabelText(label, { exact: false }).length).toBeGreaterThan(0);
    }
    await chooseOption(user, 'Kullanıcı', 'Tugay İltuş');
    await chooseOption(user, 'Eylem', 'Rol değiştirildi');

    await waitFor(() =>
      expect(
        mock.calls.some((r) => {
          const query = new URL(r.url).searchParams;
          return query.get('actorUserId') === 'a-1' && query.get('action') === 'users.role_changed' && query.get('from') === '2026-08-31T21:00:00.000Z' && query.get('to') === '2026-09-30T20:59:59.999Z';
        }),
      ).toBe(true),
    );
  });

  it('offers inactive users in the actor filter and keeps an actor from the link selectable', async () => {
    const users = [
      { id: 'a-1', firstName: 'Tugay', lastName: 'İltuş', email: 't@example.com', role: 'admin', isActive: true },
      { id: 'i-1', firstName: 'Eski', lastName: 'Çalışan', email: 'e@example.com', role: 'employee', isActive: false },
    ];
    const mock = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/users': { status: 200, body: { items: users, page: 1, pageSize: 100, totalCount: 2 } },
      'GET /api/v1/admin/audit-events': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } },
    });
    renderRoute('/admin/audit?actor=i-1');

    expect((await screen.findAllByText('Eski Çalışan')).length).toBeGreaterThan(0);
    expect(mock.calls.filter((r) => new URL(r.url).pathname === '/api/v1/admin/users').every((r) => !new URL(r.url).searchParams.has('isActive'))).toBe(true);
    await waitFor(() => expect(mock.calls.some((r) => new URL(r.url).searchParams.get('actorUserId') === 'i-1')).toBe(true));
  });

  it('keeps an actor that is not among the loaded users visible as an option', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/users': { status: 200, body: { items: [], page: 1, pageSize: 100, totalCount: 0 } },
      'GET /api/v1/admin/audit-events': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } },
    });
    renderRoute('/admin/audit?actor=gone-1');

    expect((await screen.findAllByText('Seçili kullanıcı')).length).toBeGreaterThan(0);
  });

  it('is closed to employees', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    const { router } = renderRoute('/admin/audit');
    await waitFor(() => expect(router.state.location.pathname).not.toBe('/admin/audit'));
  });
});
