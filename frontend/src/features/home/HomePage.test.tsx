import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { pageOf } from '../../test/assetData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { LIST_ITEM } from '../../test/ticketData';

const RATE_TITLE = 'Puan bekleyen kayıtlar';
const ROW_COUNT = 5;
const QUEUE_TO_RATE = 'to_rate';

const rateCalls = (api: ReturnType<typeof mockApi>) => api.calls.filter((c) => new URL(c.url).searchParams.get('queue') === QUEUE_TO_RATE);

function ticketsFor(rateItems: (typeof LIST_ITEM)[]) {
  return (request: Request) => ({
    status: 200,
    body: pageOf(new URL(request.url).searchParams.get('queue') === QUEUE_TO_RATE ? rateItems : []),
  });
}

describe('HomePage', () => {
  it('has a "Nasıl Yapılır?" card that opens the knowledge base', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/kb/articles': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } } });
    const { user, router } = renderRoute('/');

    await user.click(await screen.findByRole('link', { name: /Nasıl Yapılır\?/ }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/kb'));
  });

  it('shows the announcements and the link to the own software', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      'GET /api/v1/announcements': { status: 200, body: [{ id: 'a-1', title: 'VPN bakımı', body: 'Bu akşam kesinti var.', level: 'warning', startsAt: '2026-11-25T08:00:00Z', endsAt: null }] },
    });
    renderRoute('/');
    expect(await screen.findByText('VPN bakımı')).toBeInTheDocument();
    expect(screen.getByText('Bu akşam kesinti var.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Yazılımlarım Size/ })).toHaveAttribute('href', '/software/mine');
  });

  it('employee_sees_puan_bekleyen_kayitlar_with_rows_linking_to_rate', async () => {
    const items = Array.from({ length: ROW_COUNT }, (_, i) => ({ ...LIST_ITEM, number: `INC-2026-00000${i + 1}`, subject: `Konu ${i + 1}` }));
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': ticketsFor(items) });
    renderRoute('/');
    expect(await screen.findByRole('heading', { name: RATE_TITLE })).toBeInTheDocument();
    const list = screen.getByRole('list', { name: RATE_TITLE });
    const links = await within(list).findAllByRole('link');
    expect(links).toHaveLength(ROW_COUNT);
    expect(rateCalls(api).map((c) => new URL(c.url).searchParams.get('pageSize'))).toContain(String(ROW_COUNT));
    expect(links[0]).toHaveAttribute('href', '/tickets/INC-2026-000001?rate=1');
  });

  it('card_is_hidden_when_queue_is_empty', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': ticketsFor([]) });
    renderRoute('/');
    await waitFor(() => expect(rateCalls(api)).toHaveLength(1));
    expect(await screen.findByRole('heading', { name: 'Son kayıtlarım' })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('heading', { name: RATE_TITLE })).not.toBeInTheDocument());
  });
  it('gives IT staff an explicit link to the work list, and employees none', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, 'GET /api/v1/tickets': ticketsFor([]) });
    renderRoute('/');
    expect(await within(await screen.findByRole('main')).findByRole('link', { name: /İş listem/ })).toHaveAttribute('href', '/workspace');
  });

  it('hides the work list link from employees', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': ticketsFor([]) });
    renderRoute('/');
    await screen.findByRole('heading', { name: 'Son kayıtlarım' });
    expect(within(screen.getByRole('main')).queryByRole('link', { name: /İş listem/ })).not.toBeInTheDocument();
  });

  it('puts tickets waiting for the requester first in recent records and marks them', async () => {
    const waiting = { ...LIST_ITEM, number: 'INC-2026-000009', subject: 'Yanıt bekleyen', status: 'on_hold' as const, holdReason: 'user' as const };
    const plain = { ...LIST_ITEM, number: 'INC-2026-000008', subject: 'Normal kayıt' };
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      'GET /api/v1/tickets': (request: Request) => ({ status: 200, body: pageOf(new URL(request.url).searchParams.get('queue') === 'mine' ? [plain, waiting] : []) }),
    });
    renderRoute('/');
    const list = await screen.findByRole('list', { name: 'Son kayıtlarım' });
    const links = within(list).getAllByRole('link');
    expect(links[0]).toHaveTextContent('Yanıt bekleyen');
    expect(within(list).getAllByText('Yanıtınız bekleniyor')).toHaveLength(1);
  });
  it('shows a waiting ticket even when it is older than the five newest', async () => {
    const newest = Array.from({ length: ROW_COUNT }, (_, i) => ({ ...LIST_ITEM, number: `INC-2026-00001${i}`, subject: `Yeni ${i}` }));
    const old = { ...LIST_ITEM, number: 'INC-2025-000001', subject: 'Eski bekleyen', status: 'on_hold' as const, holdReason: 'user' as const };
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      'GET /api/v1/tickets': (request: Request) => {
        const params = new URL(request.url).searchParams;
        if (params.get('queue') !== 'mine') {
          return { status: 200, body: pageOf([]) };
        }
        return { status: 200, body: pageOf(params.get('status') === 'on_hold' ? [old] : newest) };
      },
    });
    renderRoute('/');
    const list = await screen.findByRole('list', { name: 'Son kayıtlarım' });
    await waitFor(() => expect(within(list).getAllByRole('link')[0]).toHaveTextContent('Eski bekleyen'));
    expect(within(list).getAllByRole('link')).toHaveLength(ROW_COUNT);
  });
});
