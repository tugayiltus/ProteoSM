import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const PAGED_EMPTY = { items: [], page: 1, pageSize: 20, totalCount: 0 };
const NO_FILTER_MESSAGE = 'Bu filtrelerle kayıt bulunamadı.';
const CLEAR_LABEL = 'Filtreleri temizle';
const RETRY_LABEL = 'Tekrar dene';

interface ListCase {
  name: string;
  route: string;
  endpoint: string;
  empty: unknown;
  firstUse: string;
}

const CASES: ListCase[] = [
  { name: 'users', route: '/admin/users', endpoint: 'GET /api/v1/admin/users', empty: PAGED_EMPTY, firstUse: 'Henüz kullanıcı yok. İlk kullanıcıyı ekleyin.' },
  { name: 'departments', route: '/admin/departments', endpoint: 'GET /api/v1/admin/departments', empty: PAGED_EMPTY, firstUse: 'Henüz departman yok. İlk departmanı ekleyin.' },
  { name: 'teams', route: '/admin/teams', endpoint: 'GET /api/v1/admin/teams', empty: PAGED_EMPTY, firstUse: 'Henüz ekip yok. İlk ekibi ekleyin.' },
  { name: 'services', route: '/admin/services', endpoint: 'GET /api/v1/services', empty: PAGED_EMPTY, firstUse: 'Henüz hizmet yok. İlk hizmeti ekleyin.' },
  { name: 'audit', route: '/admin/audit', endpoint: 'GET /api/v1/admin/audit-events', empty: PAGED_EMPTY, firstUse: 'Henüz denetim kaydı yok.' },
  { name: 'email deliveries', route: '/admin/email-deliveries', endpoint: 'GET /api/v1/admin/email-deliveries', empty: PAGED_EMPTY, firstUse: 'Henüz e-posta teslimatı yok.' },
  { name: 'categories', route: '/admin/categories', endpoint: 'GET /api/v1/admin/categories', empty: [], firstUse: 'Henüz kategori yok. İlk kategoriyi ekleyin.' },
  { name: 'request types', route: '/admin/request-types', endpoint: 'GET /api/v1/admin/request-types', empty: [], firstUse: 'Henüz talep tipi yok. İlk talep tipini ekleyin.' },
  { name: 'brands', route: '/admin/brands', endpoint: 'GET /api/v1/admin/brands', empty: [], firstUse: 'Henüz marka yok. İlk markayı ekleyin.' },
  { name: 'models', route: '/admin/models', endpoint: 'GET /api/v1/admin/models', empty: [], firstUse: 'Henüz model yok. İlk modeli ekleyin.' },
  { name: 'email templates', route: '/admin/email-templates', endpoint: 'GET /api/v1/admin/email-templates', empty: [], firstUse: 'Henüz e-posta şablonu yok.' },
];

describe.each(CASES)('$name list states', ({ route, endpoint, empty, firstUse }) => {
  it('shows the first-use text without a clear action when nothing is filtered', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, [endpoint]: { status: 200, body: empty } });
    renderRoute(route);

    expect(await screen.findByText(firstUse)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: CLEAR_LABEL })).not.toBeInTheDocument();
  });

  it('shows a no-result text with a clear action when a filter is on', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, [endpoint]: { status: 200, body: empty } });
    renderRoute(`${route}?q=zzz`);

    expect(await screen.findByText(NO_FILTER_MESSAGE)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: CLEAR_LABEL })).toBeInTheDocument();
    expect(screen.queryByText(firstUse)).not.toBeInTheDocument();
  });

  it('shows the load error with a retry action', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, [endpoint]: { status: 500, body: { code: 'server.error' } } });
    renderRoute(route);

    expect(await screen.findByRole('button', { name: RETRY_LABEL })).toBeInTheDocument();
    expect(screen.queryByText(firstUse)).not.toBeInTheDocument();
    expect(screen.queryByText(NO_FILTER_MESSAGE)).not.toBeInTheDocument();
  });
});
