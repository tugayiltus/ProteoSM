import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi, type MockRoutes } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const ID = '0198c1d2-0000-7000-8000-000000000001';
const SECTION = (total: number) => ({ items: [], total });

interface ImpactCase {
  name: string;
  route: string;
  list: string;
  items: unknown[];
  button: RegExp;
  impactPath: string;
  counts: { key: string; count: number }[];
  expected: string[];
}

const CASES: ImpactCase[] = [
  {
    name: 'brand',
    route: '/admin/brands',
    list: 'GET /api/v1/admin/brands',
    items: [{ id: ID, name: 'Lenovo', isActive: true, version: 1 }],
    button: /Pasifleştir.*Lenovo/,
    impactPath: `GET /api/v1/admin/brands/${ID}/deactivation-impact`,
    counts: [
      { key: 'models', count: 4 },
      { key: 'active_models', count: 3 },
      { key: 'assets', count: 12 },
    ],
    expected: ['4 model', '3 aktif model', '12 varlık'],
  },
  {
    name: 'model',
    route: '/admin/models',
    list: 'GET /api/v1/admin/models',
    items: [{ id: ID, brandId: 'b-1', brandName: 'Lenovo', type: 'laptop', name: 'T14', isActive: true, version: 1 }],
    button: /Pasifleştir.*T14/,
    impactPath: `GET /api/v1/admin/models/${ID}/deactivation-impact`,
    counts: [{ key: 'assets', count: 7 }],
    expected: ['7 varlık'],
  },
  {
    name: 'category',
    route: '/admin/categories',
    list: 'GET /api/v1/admin/categories',
    items: [{ id: ID, name: 'Donanım', parentId: null, scope: 'both', isActive: true, version: 1 }],
    button: /Pasifleştir.*Donanım/,
    impactPath: `GET /api/v1/admin/categories/${ID}/deactivation-impact`,
    counts: [
      { key: 'active_children', count: 2 },
      { key: 'open_tickets', count: 5 },
    ],
    expected: ['2 aktif alt kategori', '5 açık kayıt'],
  },
  {
    name: 'request type',
    route: '/admin/request-types',
    list: 'GET /api/v1/admin/request-types',
    items: [{ id: ID, name: 'Yeni dizüstü', requiresApproval: true, isActive: true, version: 1 }],
    button: /Pasifleştir.*Yeni dizüstü/,
    impactPath: `GET /api/v1/admin/request-types/${ID}/deactivation-impact`,
    counts: [{ key: 'open_tickets', count: 0 }],
    expected: ['0 açık kayıt'],
  },
];

function routes(extra: MockRoutes): MockRoutes {
  return { 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/admin/brands': { status: 200, body: [] }, ...extra };
}

describe.each(CASES)('deactivating a $name', ({ route, list, items, button, impactPath, counts, expected }) => {
  it('shows the counted records and that they stay unchanged', async () => {
    mockApi(routes({ [list]: { status: 200, body: items }, [impactPath]: { status: 200, body: { counts } } }));
    const { user } = renderRoute(route);

    await user.click(await screen.findByRole('button', { name: button }));
    const dialog = await screen.findByRole('dialog');

    for (const text of expected) {
      expect(await within(dialog).findByText(text)).toBeInTheDocument();
    }
    expect(within(dialog).getByText(/bu kayıtları değiştirmez/)).toBeInTheDocument();
  });

  it('still lets the admin confirm when the count fails', async () => {
    mockApi(routes({ [list]: { status: 200, body: items }, [impactPath]: { status: 500, body: { code: 'server.error' } } }));
    const { user } = renderRoute(route);

    await user.click(await screen.findByRole('button', { name: button }));
    const dialog = await screen.findByRole('dialog');

    expect(await within(dialog).findByText(/Etkilenen kayıtlar sayılamadı/)).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Onayla' })).toBeEnabled();
  });
});

describe('deactivating a service', () => {
  const SERVICE = { id: ID, name: 'SAP Finans', ownerTeamId: 't-1', ownerTeamName: 'Saha Destek', isActive: true, assetCount: 3, openTicketCount: 1 };

  it('shows the assets and open records the service touches', async () => {
    mockApi(
      routes({
        'GET /api/v1/services': { status: 200, body: { items: [SERVICE], page: 1, pageSize: 20, totalCount: 1 } },
        'GET /api/v1/admin/teams': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } },
        [`GET /api/v1/services/${ID}`]: { status: 200, body: { ...SERVICE, description: null, version: 2 } },
        [`GET /api/v1/services/${ID}/impact`]: { status: 200, body: { assets: SECTION(3), tickets: SECTION(2), changes: SECTION(1), problems: SECTION(0) } },
      }),
    );
    const { user } = renderRoute('/admin/services');

    await user.click(await screen.findByRole('button', { name: 'Pasife al SAP Finans' }));
    const dialog = await screen.findByRole('dialog');

    expect(await within(dialog).findByText('3 bağlı varlık')).toBeInTheDocument();
    expect(within(dialog).getByText('2 açık olay ve talep')).toBeInTheDocument();
    expect(within(dialog).getByText('1 açık değişiklik')).toBeInTheDocument();
    expect(within(dialog).getByText('0 açık problem')).toBeInTheDocument();
  });
});
