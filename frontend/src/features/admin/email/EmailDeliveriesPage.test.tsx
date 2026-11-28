import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { formatDateTime } from '../../../shared/lib/format';
import { findAlert } from '../../../test/alerts';
import { ADMIN_ME, mockApi, type MockRoutes } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';
import { chooseOption } from '../../../test/select';

const delivery = (id: string, status: string, recipientEmail: string) => ({
  id,
  outboxId: `o-${id}`,
  eventType: 'ticket_created',
  recipientUserId: 'u-1',
  recipientEmail,
  subject: `Konu ${id}`,
  body: null,
  status,
  attempts: status === 'failed' ? 5 : 1,
  nextAttemptAt: '2026-09-27T09:00:00Z',
  lastError: status === 'failed' ? 'SMTP reddetti' : null,
  sentAt: null,
  lastAttemptAt: null,
  eventCreatedAt: '2026-09-27T09:00:00Z',
});

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });

function setup(extra: MockRoutes = {}) {
  return mockApi({
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    'GET /api/v1/admin/email-deliveries': { status: 200, body: pageOf([delivery('d-1', 'sent', 'sent@example.com'), delivery('d-2', 'failed', 'failed@example.com')]) },
    ...extra,
  });
}

const lastQuery = (mock: ReturnType<typeof mockApi>) =>
  new URL(mock.calls.filter((r) => r.method === 'GET' && new URL(r.url).pathname === '/api/v1/admin/email-deliveries').at(-1)!.url).searchParams;

describe('EmailDeliveriesPage', () => {
  it('offers retry on failed deliveries only', async () => {
    setup();
    renderRoute('/admin/email-deliveries');

    expect((await screen.findAllByRole('button', { name: 'Yeniden dene failed@example.com' })).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Yeniden dene sent@example.com' })).not.toBeInTheDocument();
  });

  it('retries a failed delivery and reloads the list', async () => {
    const mock = setup({ 'POST /api/v1/admin/email-deliveries/d-2/retry': { status: 200, body: delivery('d-2', 'pending', 'failed@example.com') } });
    const { user } = renderRoute('/admin/email-deliveries');
    const listCalls = () => mock.calls.filter((r) => r.method === 'GET' && new URL(r.url).pathname === '/api/v1/admin/email-deliveries').length;

    const button = (await screen.findAllByRole('button', { name: 'Yeniden dene failed@example.com' }))[0]!;
    const before = listCalls();
    await user.click(button);

    await waitFor(() => expect(mock.calls.some((r) => r.method === 'POST' && r.url.endsWith('/d-2/retry'))).toBe(true));
    await waitFor(() => expect(listCalls()).toBeGreaterThan(before));
  });

  it('shows the retry error when the delivery is no longer failed', async () => {
    setup({ 'POST /api/v1/admin/email-deliveries/d-2/retry': { status: 409, body: { code: 'email_deliveries.not_failed' } } });
    const { user } = renderRoute('/admin/email-deliveries');

    await user.click((await screen.findAllByRole('button', { name: 'Yeniden dene failed@example.com' }))[0]!);

    expect(await findAlert('Yalnızca başarısız olan e-postalar yeniden gönderilebilir.')).toBeInTheDocument();
  });

  it('sends the status, event and recipient filters to the server', async () => {
    const mock = setup();
    const { user } = renderRoute('/admin/email-deliveries');

    await screen.findByRole('table');
    await chooseOption(user, 'Durum', 'Başarısız');
    await chooseOption(user, 'Bildirim', 'Kayıt oluşturuldu');
    await user.type(screen.getByLabelText('Alıcı'), 'failed@example.com');

    await waitFor(() => {
      const query = lastQuery(mock);
      expect(query.get('status')).toBe('failed');
      expect(query.get('eventType')).toBe('ticket_created');
      expect(query.get('recipient')).toBe('failed@example.com');
    });
  });

  it('shows the empty state', async () => {
    setup({ 'GET /api/v1/admin/email-deliveries': { status: 200, body: pageOf([]) } });
    renderRoute('/admin/email-deliveries');

    expect(await screen.findByText('Henüz e-posta teslimatı yok.')).toBeInTheDocument();
  });

  it('shows the last attempt time of every attempted row, the next run of a queued retry and the stored safe error', async () => {
    const sent = { ...delivery('d-1', 'sent', 'sent@example.com'), sentAt: '2026-09-27T09:05:00Z', lastAttemptAt: '2026-09-27T09:05:00Z' };
    const queued = { ...delivery('d-3', 'pending', 'queued@example.com'), attempts: 2, nextAttemptAt: '2026-09-27T10:00:00Z', lastAttemptAt: '2026-09-27T09:30:00Z' };
    const failed = { ...delivery('d-2', 'failed', 'failed@example.com'), lastAttemptAt: '2026-09-27T09:45:00Z' };
    const fresh = delivery('d-4', 'pending', 'fresh@example.com');
    setup({ 'GET /api/v1/admin/email-deliveries': { status: 200, body: pageOf([sent, queued, failed, fresh]) } });
    renderRoute('/admin/email-deliveries');

    const sentRow = (await screen.findAllByText('sent@example.com'))[0]!.closest('tr') as HTMLElement;
    expect(within(sentRow).getByText(formatDateTime('2026-09-27T09:05:00Z'))).toBeInTheDocument();
    const failedRow = screen.getAllByText('failed@example.com')[0]!.closest('tr') as HTMLElement;
    expect(within(failedRow).getByText(formatDateTime('2026-09-27T09:45:00Z'))).toBeInTheDocument();
    expect(within(failedRow).getByText('Başarısız')).toBeInTheDocument();
    expect(within(failedRow).getByText('SMTP reddetti')).toBeInTheDocument();
    const queuedRow = screen.getAllByText('queued@example.com')[0]!.closest('tr') as HTMLElement;
    expect(within(queuedRow).getByText(/Sonraki deneme: /)).toBeInTheDocument();
    const freshRow = screen.getAllByText('fresh@example.com')[0]!.closest('tr') as HTMLElement;
    expect(within(freshRow).queryByText(/Sonraki deneme/)).not.toBeInTheDocument();
  });

  it('opens already filtered to failed deliveries when the link carries the status', async () => {
    const api = setup();
    renderRoute('/admin/email-deliveries?status=failed');

    await waitFor(() => expect(api.calls.some((r) => new URL(r.url).searchParams.get('status') === 'failed')).toBe(true));
  });
});
