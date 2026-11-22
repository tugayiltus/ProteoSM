import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { components } from '../../shared/api/schema';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';
import { TEAMS } from '../../test/taskData';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

type TicketProblemItem = components['schemas']['TicketProblemItem'];

const PATH = `/tickets/${INCIDENT.number}`;
const PROBLEM_KEY = `GET /api/v1/tickets/${INCIDENT.number}/problem`;
const PROBLEM_NUMBER = 'PRB-2026-000001';
const LINKED: TicketProblemItem = {
  number: PROBLEM_NUMBER,
  title: 'VPN bağlantısı her sabah kopuyor',
  status: 'known_error',
  workaround: 'VPN istemcisini yeniden başlatın',
  ownerTeamName: 'Ağ Ekibi',
};
const NOT_LINKED = { status: 204 };

const card = () => screen.findByRole('region', { name: 'Problem' });
const problemLoads = (api: ReturnType<typeof mockApi>) =>
  api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === `/api/v1/tickets/${INCIDENT.number}/problem`).length;

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    'GET /api/v1/reference/teams': { status: 200, body: TEAMS },
    ...detailRoutes(INCIDENT),
    ...extra,
  };
}

describe('Ticket Problem card', () => {
  it('shows the linked problem with its status, workaround and a link', async () => {
    mockApi(routes({ [PROBLEM_KEY]: { status: 200, body: LINKED } }) as never);
    renderRoute(PATH);

    const region = await card();
    expect(await within(region).findByRole('link', { name: `${PROBLEM_NUMBER} · ${LINKED.title}` })).toHaveAttribute('href', `/problems/${PROBLEM_NUMBER}`);
    expect(within(region).getByText('Bilinen hata')).toBeInTheDocument();
    expect(within(region).getByText(LINKED.workaround ?? '')).toBeInTheDocument();
    expect(within(region).queryByRole('button', { name: 'Probleme bağla' })).not.toBeInTheDocument();
  });

  it('links the incident to a problem by its number', async () => {
    let posted: unknown;
    let current: { status: number; body?: unknown } = NOT_LINKED;
    mockApi(
      routes({
        [PROBLEM_KEY]: () => current,
        [`POST /api/v1/problems/${PROBLEM_NUMBER}/tickets`]: async (request: Request) => {
          posted = await request.json();
          current = { status: 200, body: LINKED };
          return { status: 201, body: [] };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    const region = await card();
    await user.click(await within(region).findByRole('button', { name: 'Probleme bağla' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Problem numarası'), ` ${PROBLEM_NUMBER} `);
    await user.click(within(dialog).getByRole('button', { name: 'Bağla' }));

    await waitFor(() => expect(posted).toEqual({ ticketNumber: INCIDENT.number }));
    expect(await within(region).findByRole('link', { name: `${PROBLEM_NUMBER} · ${LINKED.title}` })).toBeInTheDocument();
  });

  it('shows the Turkish error and reloads the card when the link answers 409', async () => {
    const api = mockApi(
      routes({
        [PROBLEM_KEY]: NOT_LINKED,
        [`POST /api/v1/problems/${PROBLEM_NUMBER}/tickets`]: { status: 409, body: { code: 'problems.ticket_already_linked' } },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    const region = await card();
    await user.click(await within(region).findByRole('button', { name: 'Probleme bağla' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Problem numarası'), PROBLEM_NUMBER);
    await user.click(within(dialog).getByRole('button', { name: 'Bağla' }));

    expect(await within(dialog).findByText('Bu arıza zaten bir probleme bağlı.')).toBeInTheDocument();
    await waitFor(() => expect(problemLoads(api)).toBeGreaterThan(1));
  });

  it('creates a problem from the incident with its subject and description and links it in the same save', async () => {
    let posted: Record<string, unknown> | undefined;
    mockApi(
      routes({
        [PROBLEM_KEY]: NOT_LINKED,
        'POST /api/v1/problems': async (request: Request) => {
          posted = (await request.json()) as Record<string, unknown>;
          return { status: 201, body: { number: PROBLEM_NUMBER } };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    const region = await card();
    await user.click(await within(region).findByRole('button', { name: 'Problem oluştur' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Başlık')).toHaveValue(INCIDENT.subject);
    expect(within(dialog).getByLabelText('Belirti')).toHaveValue(INCIDENT.description);
    await chooseOption(user, 'Sahip ekip', 'Servis Masası', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    await waitFor(() =>
      expect(posted).toMatchObject({ title: INCIDENT.subject, description: INCIDENT.description, ownerTeamId: 'team-1', incidentNumber: INCIDENT.number }),
    );
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('reloads the card when the create answers 409 and keeps the dialog open with the error', async () => {
    const api = mockApi(
      routes({
        [PROBLEM_KEY]: NOT_LINKED,
        'POST /api/v1/problems': { status: 409, body: { code: 'problems.owner_team_not_allowed' } },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    const region = await card();
    await user.click(await within(region).findByRole('button', { name: 'Problem oluştur' }));
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Sahip ekip', 'Servis Masası', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await within(dialog).findByText('Problem yalnızca üyesi olduğunuz bir ekibe verilebilir.')).toBeInTheDocument();
    await waitFor(() => expect(problemLoads(api)).toBeGreaterThan(1));
  });

  it('shows no card on a request and makes no request', async () => {
    const request = { ...INCIDENT, number: 'REQ-2026-000001', kind: 'request' as const };
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...detailRoutes(request) } as never);
    renderRoute(`/tickets/${request.number}`);

    expect(await screen.findByRole('heading', { name: request.subject })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Problem' })).not.toBeInTheDocument();
    expect(api.calls.some((call) => call.url.endsWith('/problem'))).toBe(false);
  });

  it('shows no card to an employee and makes no request', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...detailRoutes(INCIDENT), [PROBLEM_KEY]: { status: 200, body: LINKED } } as never);
    renderRoute(PATH);

    expect(await screen.findByRole('heading', { name: INCIDENT.subject })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Problem' })).not.toBeInTheDocument();
    expect(api.calls.some((call) => call.url.endsWith('/problem'))).toBe(false);
  });
});
