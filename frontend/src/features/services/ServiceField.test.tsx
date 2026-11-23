import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { components } from '../../shared/api/schema';
import { CHANGE, changeRoutes } from '../../test/changeData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { PROBLEM, problemRoutes } from '../../test/problemData';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption, findSelect } from '../../test/select';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

type ServiceRef = components['schemas']['ServiceRef'];
type TicketAssetItem = components['schemas']['TicketAssetItem'];

const SAP: ServiceRef = { id: 'svc-sap', name: 'SAP', isActive: true };
const MAIL: ServiceRef = { id: 'svc-mail', name: 'E-posta', isActive: true };
const OLD: ServiceRef = { id: 'svc-old', name: 'Eski sistem', isActive: false };

const TICKET_PATH = `/tickets/${INCIDENT.number}`;
const TICKET_SERVICE_ROUTE = `PUT /api/v1/tickets/${INCIDENT.number}/service`;
const STAFF_TICKET = { ...INCIDENT, service: null };

const listOf = (items: ServiceRef[]) => ({
  status: 200,
  body: { items: items.map((s) => ({ ...s, ownerTeamId: 'team-1', ownerTeamName: 'Servis Masası', assetCount: 0, openTicketCount: 0 })), page: 1, pageSize: 100, totalCount: items.length },
});

const asset = (tag: string): TicketAssetItem => ({ assetId: `id-${tag}`, assetTag: tag, type: 'laptop', brand: 'Dell', model: 'Latitude', linkedAt: '2026-11-01T08:00:00Z' });

function ticketRoutes(assets: Record<string, ServiceRef[]>, extra: Record<string, unknown> = {}) {
  const services = Object.fromEntries(Object.entries(assets).map(([tag, list]) => [`GET /api/v1/assets/${tag}/services`, { status: 200, body: list }]));
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    ...detailRoutes(STAFF_TICKET),
    [`GET /api/v1/tickets/${INCIDENT.number}/assets`]: { status: 200, body: Object.keys(assets).map(asset) },
    'GET /api/v1/services': listOf([SAP, MAIL, OLD]),
    ...services,
    ...extra,
  };
}

const serviceRow = () => screen.findByRole('button', { name: 'Hizmet seç' });

describe('ServiceField', () => {
  it('offers only active services in the picker', async () => {
    const api = mockApi(ticketRoutes({}) as never);
    const { user } = renderRoute(TICKET_PATH);

    await user.click(await serviceRow());
    const dialog = await screen.findByRole('dialog');
    await user.click(await findSelect('Hizmet', dialog));

    expect(await screen.findByRole('option', { name: 'SAP' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'E-posta' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Eski sistem' })).not.toBeInTheDocument();
    expect(api.calls.some((c) => new URL(c.url).pathname === '/api/v1/services' && new URL(c.url).searchParams.get('active') === 'true')).toBe(true);
  });

  it('pre-selects the one active service of the linked assets and saves only on confirm', async () => {
    let body: unknown;
    const api = mockApi(
      ticketRoutes(
        { 'ASSET-2026-0001': [SAP], 'ASSET-2026-0002': [SAP, OLD] },
        {
          [TICKET_SERVICE_ROUTE]: async (request: Request) => {
            body = await request.json();
            return { status: 200, body: { ...STAFF_TICKET, service: SAP } };
          },
        },
      ) as never,
    );
    const { user } = renderRoute(TICKET_PATH);

    await user.click(await serviceRow());
    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText('Önerilen: SAP')).toBeInTheDocument();
    await waitFor(async () => expect(await findSelect('Hizmet', dialog)).toHaveTextContent('SAP'));
    expect(api.calls.some((c) => c.method === 'PUT')).toBe(false);

    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(body).toEqual({ serviceId: SAP.id, version: INCIDENT.version }));
  });

  it('does not pre-select when the linked assets carry two services', async () => {
    const api = mockApi(ticketRoutes({ 'ASSET-2026-0001': [SAP], 'ASSET-2026-0002': [MAIL] }) as never);
    const { user } = renderRoute(TICKET_PATH);

    await user.click(await serviceRow());
    const dialog = await screen.findByRole('dialog');
    const select = await findSelect('Hizmet', dialog);
    await waitFor(() => expect(api.calls.filter((c) => /^\/api\/v1\/assets\/.+\/services$/.test(new URL(c.url).pathname))).toHaveLength(2));
    expect(within(dialog).getByRole('button', { name: 'Kaydet' })).toHaveAttribute('aria-disabled', 'true');
    expect(within(dialog).queryByText(/Önerilen/)).not.toBeInTheDocument();
    expect(select).not.toHaveTextContent('SAP');
    expect(select).not.toHaveTextContent('E-posta');
  });

  it('shows an inactive service with Pasif and clears it with serviceId null', async () => {
    let body: unknown;
    mockApi(
      ticketRoutes(
        {},
        {
          ...detailRoutes({ ...INCIDENT, service: OLD }),
          [`GET /api/v1/tickets/${INCIDENT.number}/assets`]: { status: 200, body: [] },
          [TICKET_SERVICE_ROUTE]: async (request: Request) => {
            body = await request.json();
            return { status: 200, body: STAFF_TICKET };
          },
        },
      ) as never,
    );
    const { user } = renderRoute(TICKET_PATH);

    expect(await screen.findByRole('link', { name: OLD.name })).toHaveAttribute('href', `/services/${OLD.id}`);
    expect(screen.getByText('Pasif')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Hizmeti kaldır' }));
    await waitFor(() => expect(body).toEqual({ serviceId: null, version: INCIDENT.version }));
  });

  it('shows the Turkish text when the service became inactive (409)', async () => {
    mockApi(ticketRoutes({}, { [TICKET_SERVICE_ROUTE]: { status: 409, body: { code: 'services.inactive' } } }) as never);
    const { user } = renderRoute(TICKET_PATH);

    await user.click(await serviceRow());
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Hizmet', 'SAP', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));
    expect(await within(dialog).findByText('Pasif hizmet seçilemez.')).toBeInTheDocument();
  });

  it('has no Hizmet field on the employee ticket view', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...detailRoutes(INCIDENT) } as never);
    renderRoute(TICKET_PATH);

    expect(await screen.findByText(INCIDENT.description)).toBeInTheDocument();
    expect(screen.queryByText('Hizmet')).not.toBeInTheDocument();
    expect(screen.queryByText('Hizmet yok')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hizmet seç' })).not.toBeInTheDocument();
  });

  it('sets the service on a change with its version', async () => {
    let body: unknown;
    mockApi(
      changeRoutes(CHANGE, {
        'GET /api/v1/services': listOf([SAP]),
        [`PUT /api/v1/changes/${CHANGE.number}/service`]: async (request: Request) => {
          body = await request.json();
          return { status: 200, body: { ...CHANGE, service: SAP } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/changes/${CHANGE.number}`);

    expect(await screen.findByText('Hizmet yok')).toBeInTheDocument();
    await user.click(await serviceRow());
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Hizmet', 'SAP', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(body).toEqual({ serviceId: SAP.id, version: CHANGE.version }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('sets the service on a problem with its version', async () => {
    let body: unknown;
    mockApi(
      problemRoutes(PROBLEM, {
        'GET /api/v1/services': listOf([SAP]),
        [`PUT /api/v1/problems/${PROBLEM.number}/service`]: async (request: Request) => {
          body = await request.json();
          return { status: 200, body: { ...PROBLEM, service: SAP } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/problems/${PROBLEM.number}`);

    await user.click(await serviceRow());
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Hizmet', 'SAP', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(body).toEqual({ serviceId: SAP.id, version: PROBLEM.version }));
  });

  it('hides the picker when the viewer cannot work the problem', async () => {
    mockApi(problemRoutes({ ...PROBLEM, canWork: false, service: SAP }) as never);
    renderRoute(`/problems/${PROBLEM.number}`);

    expect(await screen.findByRole('link', { name: SAP.name })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hizmet seç' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hizmeti değiştir' })).not.toBeInTheDocument();
  });
});
