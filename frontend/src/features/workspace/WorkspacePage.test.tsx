import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { findSelect } from '../../test/select';
import { detailRoutes, INCIDENT, LIST_ITEM } from '../../test/ticketData';

const COUNTS: Record<string, number> = { assigned_to_me: 2, team: 5, unassigned: 3, on_hold: 1, sla_risk: 4 };
const OTHERS = 'Diğer kuyruklar';
const MINE_TOTAL = 7;
const DUE_AT = '2026-12-03T08:00:00Z';
const RISK_ITEM = {
  ...LIST_ITEM,
  subject: 'VPN bağlantısı kurulamıyor',
  status: 'in_progress' as const,
  sla: [{ metric: 'resolution' as const, dueAt: DUE_AT, remainingSeconds: 1800, isPaused: false, isWarned: true, isBreached: false, result: null }],
};
const WORK_ROW = { kind: 'incident', number: 'INC-2026-000001', title: 'VPN bağlantı sorunu', status: 'in_progress', priority: 'p3', dueDate: null, dueAt: DUE_AT, teamName: 'Ağ Ekibi', assigneeName: 'Can Demir', updatedAt: '2026-11-13T08:00:00Z' };

const countOf = (request: Request) => COUNTS[new URL(request.url).searchParams.get('queue') ?? ''] ?? 0;

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    'GET /api/v1/tickets': (request: Request) => ({ status: 200, body: { items: [RISK_ITEM], page: 1, pageSize: 20, totalCount: countOf(request) } }),
    'GET /api/v1/work-items': { status: 200, body: { items: [WORK_ROW], page: 1, pageSize: 20, totalCount: MINE_TOTAL } },
    ...extra,
  } as never;
}

describe('WorkspacePage', () => {
  it('puts the counted views under the title, before the list, and opens an unassigned view', async () => {
    mockApi(routes());
    const { user, router } = renderRoute('/workspace');

    const heading = await screen.findByRole('heading', { level: 1, name: 'İş listem' });
    const views = screen.getByRole('group', { name: 'İş görünümleri' });
    const list = await screen.findByRole('region', { name: 'Bana atanan işler' });
    expect(heading.compareDocumentPosition(views) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(views.compareDocumentPosition(list) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(await within(views).findByRole('link', { name: /^Bana atanan/ })).toHaveAttribute('href', '/workspace');
    expect(await within(views).findByText(String(MINE_TOTAL))).toBeInTheDocument();
    expect(await within(views).findByRole('link', { name: /^Atanmamış/ })).toHaveAttribute('href', '/workspace/queue?queue=unassigned');
    expect(await within(views).findByRole('link', { name: /^SLA riski/ })).toHaveAttribute('href', '/workspace/queue?queue=sla_risk');
    expect(within(views).getByRole('button', { name: OTHERS })).toHaveAttribute('aria-expanded', 'false');

    await user.click(within(views).getByRole('link', { name: /Atanmamış/ }));
    await waitFor(() => expect(router.state.location.search).toBe('?queue=unassigned'));
    expect(await screen.findByRole('heading', { level: 1, name: 'İş listem' })).toBeInTheDocument();
    expect((await screen.findAllByRole('heading', { name: 'Atanmamış arıza ve talepler' })).length).toBeGreaterThan(0);
  });

  it('shows subject, type, status, team and the due time in each combined-list row', async () => {
    mockApi(routes());
    renderRoute('/workspace');

    const list = await screen.findByRole('region', { name: 'Bana atanan işler' });
    expect((await within(list).findAllByText('VPN bağlantı sorunu')).length).toBeGreaterThan(0);
    expect(within(list).getAllByText('Arıza').length).toBeGreaterThan(0);
    expect(within(list).getAllByText('Ağ Ekibi').length).toBeGreaterThan(0);
    expect(within(list).getAllByText(/\d{2}:\d{2}/).length).toBeGreaterThan(0);
  });

  it('keeps the page of the combined list in the URL', async () => {
    const api = mockApi(routes({ 'GET /api/v1/work-items': { status: 200, body: { items: [WORK_ROW], page: 2, pageSize: 20, totalCount: 45 } } }));
    renderRoute('/workspace?page=2');

    await waitFor(() => expect(api.calls.some((c) => new URL(c.url).pathname === '/api/v1/work-items' && new URL(c.url).searchParams.get('page') === '2')).toBe(true));
  });

  it('opens the other queues from the "Diğer kuyruklar" toggle', async () => {
    mockApi(routes());
    const { user, router } = renderRoute('/workspace');

    const toggle = await screen.findByRole('button', { name: OTHERS });
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(await screen.findByText('5 kayıt')).toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: /Ekiplerim/ }));

    await waitFor(() => expect(router.state.location.search).toBe('?queue=team'));
    expect((await screen.findAllByRole('heading', { name: 'Ekibimin arıza ve talepleri' })).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: OTHERS })).toHaveAttribute('aria-expanded', 'true');
  });

  it('shows the SLA risk view with the due time of the timer to watch and marks it as the current view', async () => {
    const api = mockApi(routes());
    renderRoute('/workspace/queue?queue=sla_risk');

    expect((await screen.findAllByRole('heading', { name: 'SLA riski olan arıza ve talepler' })).length).toBeGreaterThan(0);
    expect(await screen.findByText('VPN bağlantısı kurulamıyor')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Son tarih' })).toBeInTheDocument();
    expect(document.querySelector('[data-sla-state="warned"]')).not.toBeNull();
    expect(screen.getByRole('link', { name: /SLA riski/ })).toHaveAttribute('aria-current', 'page');
    expect(api.calls.some((c) => new URL(c.url).searchParams.get('queue') === 'sla_risk' && new URL(c.url).searchParams.get('pageSize') === '20')).toBe(true);
  });

  it('keeps old queue links working', async () => {
    mockApi(routes());
    renderRoute('/workspace/queue');

    expect((await screen.findAllByRole('heading', { name: 'Ekibimin arıza ve talepleri' })).length).toBeGreaterThan(0);
  });

  it('returns from a ticket to the same search, filter and page', async () => {
    mockApi(routes({ ...detailRoutes() }));
    const { user, router } = renderRoute('/workspace/queue?queue=sla_risk&status=in_progress&q=vpn&page=2');

    await user.click((await screen.findAllByRole('link', { name: new RegExp(`Aç.*${RISK_ITEM.number}`) }))[0] as HTMLElement);
    await waitFor(() => expect(router.state.location.pathname).toBe(`/tickets/${INCIDENT.number}`));

    const trail = within(await screen.findByRole('navigation', { name: 'Sayfa yolu' }));
    expect(await trail.findByRole('link', { name: 'Kuyruk' })).toHaveAttribute('href', '/workspace/queue?queue=sla_risk&status=in_progress&q=vpn&page=2');
  });

  it('shows the this-week change card with its total and links to the queue', async () => {
    mockApi(
      routes({
        'GET /api/v1/changes': (request: Request) => ({ status: 200, body: { items: [], page: 1, pageSize: 1, totalCount: new URL(request.url).searchParams.get('queue') === 'this_week' ? 6 : 2 } }),
      }),
    );
    const { user } = renderRoute('/workspace');

    await user.click(await screen.findByRole('button', { name: OTHERS }));
    const card = await screen.findByRole('link', { name: 'Bu hafta uygulanacak 6 değişiklik' });
    expect(card).toHaveAttribute('href', '/changes?queue=this_week');
  });

  it('shows the team problems card with its total and links to the queue', async () => {
    const api = mockApi(routes({ 'GET /api/v1/problems': { status: 200, body: { items: [], page: 1, pageSize: 1, totalCount: 4 } } }));
    const { user } = renderRoute('/workspace');

    await user.click(await screen.findByRole('button', { name: OTHERS }));
    const card = await screen.findByRole('link', { name: 'Ekibimin problemleri 4 problem' });
    expect(card).toHaveAttribute('href', '/problems?queue=team_open');
    expect(api.calls.some((c) => c.url.includes('/api/v1/problems') && new URL(c.url).searchParams.get('queue') === 'team_open')).toBe(true);
  });

  it('filters the staff ticket list by the service in the URL', async () => {
    const api = mockApi(
      routes({
        'GET /api/v1/tickets': { status: 200, body: { items: [LIST_ITEM], page: 1, pageSize: 20, totalCount: 1 } },
        'GET /api/v1/services': { status: 200, body: { items: [{ id: 'svc-1', name: 'SAP', isActive: true, ownerTeamId: 'team-1', ownerTeamName: 'Servis Masası', assetCount: 0, openTicketCount: 0 }], page: 1, pageSize: 100, totalCount: 1 } },
      }),
    );
    const { user } = renderRoute('/workspace/queue?queue=all&serviceId=svc-1');

    await waitFor(async () => expect(await findSelect('Hizmet')).toHaveTextContent('SAP'));
    await waitFor(() => expect(api.calls.some((c) => new URL(c.url).pathname === '/api/v1/tickets' && new URL(c.url).searchParams.get('serviceId') === 'svc-1')).toBe(true));
    await user.click(screen.getByRole('button', { name: 'Temizle' }));
    await waitFor(() => expect(api.calls.some((c) => new URL(c.url).pathname === '/api/v1/tickets' && !new URL(c.url).searchParams.has('serviceId'))).toBe(true));
  });
});
