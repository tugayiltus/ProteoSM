import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { pageOf } from '../../test/assetData';
import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { LIST_ITEM } from '../../test/ticketData';

const RATE_TITLE = 'Puan bekleyen kayıtlar';
const ROW_COUNT = 5;
const QUEUE_TO_RATE = 'to_rate';

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
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': ticketsFor(items) });
    renderRoute('/');
    expect(await screen.findByRole('heading', { name: RATE_TITLE })).toBeInTheDocument();
    const list = screen.getByRole('list', { name: RATE_TITLE });
    const links = await within(list).findAllByRole('link');
    expect(links).toHaveLength(ROW_COUNT);
    expect(links[0]).toHaveAttribute('href', '/tickets/INC-2026-000001?rate=1');
  });

  it('card_is_hidden_when_queue_is_empty', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': ticketsFor([]) });
    renderRoute('/');
    expect(await screen.findByRole('heading', { name: 'Son kayıtlarım' })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('heading', { name: RATE_TITLE })).not.toBeInTheDocument());
  });
});
