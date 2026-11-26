import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { formatDateTime } from '../../shared/lib/format';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

import type { TicketDetail } from './api';

const PATH = `/tickets/${INCIDENT.number}`;
const RATING_KEY = `PUT /api/v1/tickets/${INCIDENT.number}/rating`;
const SECTION = 'Memnuniyet';
const QUESTION = 'Bu kayıt nasıl çözüldü?';
const RATE_UNTIL = '2026-10-10T09:00:00Z';

const COMPLETED: TicketDetail = {
  ...INCIDENT,
  status: 'completed',
  outcome: 'resolved',
  resolution: 'Toner değiştirildi',
  completedAt: '2026-09-26T09:00:00Z',
};
const RATEABLE: TicketDetail = { ...COMPLETED, canRate: true, rateUntil: RATE_UNTIL };
const RATED: TicketDetail = { ...RATEABLE, rating: { score: 4, comment: 'Hızlıydı', ratedAt: '2026-09-27T08:00:00Z' } };

function routes(ticket: TicketDetail, me: object, extra: Record<string, unknown> = {}) {
  return { 'GET /api/v1/auth/me': { status: 200, body: me }, ...detailRoutes(ticket), ...extra };
}

const section = () => screen.findByRole('region', { name: SECTION });

describe('Ticket rating section', () => {
  it('requester_sees_five_choices_and_sends_score_and_comment', async () => {
    let sent: unknown;
    mockApi(
      routes(RATEABLE, EMPLOYEE_ME, {
        [RATING_KEY]: async (request: Request) => {
          sent = await request.json();
          return { status: 200, body: RATED };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    const region = await section();
    const group = within(region).getByRole('radiogroup', { name: QUESTION });
    expect(within(group).getAllByRole('radio')).toHaveLength(5);
    await user.click(within(group).getByRole('radio', { name: 'İyi' }));
    await user.type(within(region).getByLabelText(/Yorum/), 'Hızlıydı');
    await user.click(within(region).getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(sent).toEqual({ score: 4, comment: 'Hızlıydı' }));
  });

  it('sends_null_comment_when_only_spaces', async () => {
    let sent: unknown;
    mockApi(
      routes(RATEABLE, EMPLOYEE_ME, {
        [RATING_KEY]: async (request: Request) => {
          sent = await request.json();
          return { status: 200, body: RATED };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    const region = await section();
    await user.click(within(region).getByRole('radio', { name: 'Çok iyi' }));
    await user.type(within(region).getByLabelText(/Yorum/), '   ');
    await user.click(within(region).getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(sent).toEqual({ score: 5, comment: null }));
  });

  it('after_rating_shows_score_and_change_until_date', async () => {
    mockApi(routes(RATED, EMPLOYEE_ME) as never);
    const { user } = renderRoute(PATH);

    const region = await section();
    expect(within(region).getByText('4 / 5 · İyi')).toBeInTheDocument();
    expect(within(region).getByText(new RegExp(formatDateTime(RATE_UNTIL)))).toBeInTheDocument();
    expect(within(region).queryByRole('radiogroup')).not.toBeInTheDocument();

    await user.click(within(region).getByRole('button', { name: 'Değiştir' }));
    expect(within(region).getByRole('radio', { name: 'İyi' })).toBeChecked();
    expect(within(region).getByLabelText(/Yorum/)).toHaveValue('Hızlıydı');
  });

  it('desk_view_shows_score_and_comment_without_form', async () => {
    mockApi(routes({ ...RATED, canRate: false, canWork: true }, IT_ME) as never);
    renderRoute(PATH);

    const region = await section();
    expect(within(region).getByText('4 / 5 · İyi')).toBeInTheDocument();
    expect(within(region).getByText('Hızlıydı')).toBeInTheDocument();
    expect(within(region).queryByRole('radiogroup')).not.toBeInTheDocument();
    expect(within(region).queryByRole('button', { name: 'Değiştir' })).not.toBeInTheDocument();
  });

  it('desk_view_shows_puan_yok_when_null', async () => {
    mockApi(routes({ ...COMPLETED, canWork: true }, IT_ME) as never);
    renderRoute(PATH);

    const region = await section();
    expect(within(region).getByText('Puan yok')).toBeInTheDocument();
  });

  it('desk_view_shows_nothing_on_an_open_ticket', async () => {
    mockApi(routes({ ...INCIDENT, canWork: true }, IT_ME) as never);
    renderRoute(PATH);

    await screen.findByText(INCIDENT.description);
    expect(screen.queryByRole('region', { name: SECTION })).not.toBeInTheDocument();
  });

  it('desk_view_shows_nothing_when_the_outcome_is_not_rateable', async () => {
    mockApi(routes({ ...COMPLETED, outcome: 'service_request_required', canWork: true }, IT_ME) as never);
    renderRoute(PATH);

    await screen.findByText(INCIDENT.description);
    expect(screen.queryByRole('region', { name: SECTION })).not.toBeInTheDocument();
  });

  it('rate_query_param_focuses_first_choice', async () => {
    mockApi(routes(RATEABLE, EMPLOYEE_ME) as never);
    renderRoute(`${PATH}?rate=1`);

    const region = await section();
    await waitFor(() => expect(within(region).getByRole('radio', { name: 'Çok kötü' })).toHaveFocus());
  });

  it('server_window_closed_error_shows_in_the_form_alert', async () => {
    mockApi(routes(RATEABLE, EMPLOYEE_ME, { [RATING_KEY]: { status: 409, body: { code: 'ratings.window_closed' } } }) as never);
    const { user } = renderRoute(PATH);

    const region = await section();
    await user.click(within(region).getByRole('radio', { name: 'Orta' }));
    await user.click(within(region).getByRole('button', { name: 'Gönder' }));

    expect(await within(region).findByText('Puan verme süresi doldu.')).toBeInTheDocument();
  });

  it('field_error_on_comment_shows_its_text', async () => {
    mockApi(
      routes(RATEABLE, EMPLOYEE_ME, {
        [RATING_KEY]: { status: 400, body: { code: 'validation.failed', errors: { comment: ['validation.too_long'] } } },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    const region = await section();
    await user.click(within(region).getByRole('radio', { name: 'Orta' }));
    await user.click(within(region).getByRole('button', { name: 'Gönder' }));

    expect(await within(region).findByText('Bu alan çok uzun.')).toBeInTheDocument();
  });
});
