import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';
import { PROBLEM, problemRoutes } from '../../test/problemData';
import { renderRoute } from '../../test/renderRoute';
import { IMPACT, SERVICE, serviceRoutes } from '../../test/serviceData';

const DETAIL_PATH = `/services/${SERVICE.id}`;

const section = (name: string) => within(screen.getByRole('region', { name }));

describe('ServiceDetailPage', () => {
  it('shows the service fields and the four impact sections with their totals', async () => {
    mockApi(serviceRoutes() as never);
    renderRoute(DETAIL_PATH);

    expect(await screen.findByRole('heading', { level: 1, name: SERVICE.name })).toBeInTheDocument();
    expect(screen.getByText(SERVICE.ownerTeamName)).toBeInTheDocument();
    expect(screen.getByText(SERVICE.description ?? '')).toBeInTheDocument();
    expect(screen.getByText('Aktif')).toBeInTheDocument();

    expect(await screen.findByRole('heading', { name: 'Varlıklar' })).toBeInTheDocument();
    expect(section('Varlıklar').getByText('Toplam 30')).toBeInTheDocument();
    expect(section('Açık kayıtlar').getByText('Toplam 25')).toBeInTheDocument();
    expect(section('Değişiklikler').getByText('Toplam 1')).toBeInTheDocument();
    expect(section('Açık problemler').getByText('Toplam 21')).toBeInTheDocument();
    expect(section('Varlıklar').getByRole('link', { name: /ASSET-2026-0001/ })).toHaveAttribute('href', '/assets/asset-1');
    expect(section('Değişiklikler').getByRole('link', { name: /CHG-2026-000001/ })).toHaveAttribute('href', '/changes/CHG-2026-000001');
  });

  it('links "Tümünü göster" to the filtered list only when a section holds more than it lists', async () => {
    mockApi(serviceRoutes() as never);
    renderRoute(DETAIL_PATH);

    await screen.findByRole('heading', { name: 'Açık kayıtlar' });
    expect(section('Açık kayıtlar').getByRole('link', { name: 'Tümünü göster' })).toHaveAttribute('href', `/workspace/queue?queue=all&serviceId=${SERVICE.id}&open=true`);
    expect(section('Açık problemler').getByRole('link', { name: 'Tümünü göster' })).toHaveAttribute('href', `/problems?serviceId=${SERVICE.id}&open=true`);
    expect(section('Değişiklikler').queryByRole('link', { name: 'Tümünü göster' })).not.toBeInTheDocument();
    expect(section('Varlıklar').getByRole('link', { name: 'Tümünü göster' })).toHaveAttribute('href', `/assets?serviceId=${SERVICE.id}`);
  });

  it('links the change list when changes hold more than they list', async () => {
    mockApi(serviceRoutes({ [`GET /api/v1/services/${SERVICE.id}/impact`]: { status: 200, body: { ...IMPACT, changes: { ...IMPACT.changes, total: 40 } } } }) as never);
    renderRoute(DETAIL_PATH);

    await screen.findByRole('heading', { name: 'Değişiklikler' });
    expect(section('Değişiklikler').getByRole('link', { name: 'Tümünü göster' })).toHaveAttribute('href', `/changes?serviceId=${SERVICE.id}&open=true`);
  });

  it('shows a row the viewer cannot open without a link', async () => {
    mockApi(serviceRoutes() as never);
    renderRoute(DETAIL_PATH);

    await screen.findByRole('heading', { name: 'Açık kayıtlar' });
    const tickets = section('Açık kayıtlar');
    expect(tickets.getByRole('link', { name: /INC-2026-000001/ })).toHaveAttribute('href', '/tickets/INC-2026-000001');
    expect(tickets.getByText(/INC-2026-000002/)).toBeInTheDocument();
    expect(tickets.queryByRole('link', { name: /INC-2026-000002/ })).not.toBeInTheDocument();
    expect(tickets.getByText('Açma yetkiniz yok')).toBeInTheDocument();
  });

  it('shows empty sections', async () => {
    const empty = { items: [], total: 0 };
    mockApi(serviceRoutes({ [`GET /api/v1/services/${SERVICE.id}/impact`]: { status: 200, body: { assets: empty, tickets: empty, changes: empty, problems: empty } } }) as never);
    renderRoute(DETAIL_PATH);

    await screen.findByRole('heading', { name: 'Açık problemler' });
    expect(section('Açık problemler').getByText('Kayıt yok.')).toBeInTheDocument();
  });

  it('returns to the service from the breadcrumb of a record opened here', async () => {
    mockApi({ ...problemRoutes(), ...serviceRoutes() } as never);
    const { user } = renderRoute(DETAIL_PATH);

    await user.click(await screen.findByRole('link', { name: new RegExp(PROBLEM.number) }));

    const trail = within(await screen.findByRole('navigation', { name: 'Sayfa yolu' }));
    expect(await trail.findByText(PROBLEM.number)).toBeInTheDocument();
    expect(trail.getByRole('link', { name: SERVICE.name })).toHaveAttribute('href', DETAIL_PATH);
    expect(trail.getByRole('link', { name: 'Hizmetler' })).toHaveAttribute('href', '/services');
  });
});
