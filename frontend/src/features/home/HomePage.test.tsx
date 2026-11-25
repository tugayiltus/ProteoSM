import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

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
});
