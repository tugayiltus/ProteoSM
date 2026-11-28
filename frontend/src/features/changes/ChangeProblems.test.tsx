import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { components } from '../../shared/api/schema';
import { CHANGE, changeRoutes } from '../../test/changeData';
import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

type ProblemChangeSideItem = components['schemas']['ProblemChangeSideItem'];

const PATH = `/changes/${CHANGE.number}`;
const PROBLEMS_KEY = `GET /api/v1/changes/${CHANGE.number}/problems`;
const ROW: ProblemChangeSideItem = { number: 'PRB-2026-000001', title: 'VPN bağlantısı her sabah kopuyor', status: 'investigating' };

const panel = () => screen.findByRole('region', { name: 'İlgili problemler' });

describe('Change İlgili problemler panel', () => {
  it('lists the problems this change fixes with their status and a link', async () => {
    mockApi(changeRoutes(CHANGE, { [PROBLEMS_KEY]: { status: 200, body: [ROW] } }) as never);
    renderRoute(PATH);

    const region = await panel();
    expect(await within(region).findByRole('link', { name: `${ROW.number} · ${ROW.title}` })).toHaveAttribute('href', `/problems/${ROW.number}`);
    expect(within(region).getByText('İnceleniyor')).toBeInTheDocument();
    expect(within(region).queryByRole('button')).not.toBeInTheDocument();
  });

  it('says so when no problem is linked', async () => {
    mockApi(changeRoutes(CHANGE, { [PROBLEMS_KEY]: { status: 200, body: [] } }) as never);
    renderRoute(PATH);

    const region = await panel();
    expect(await within(region).findByText('Bağlı problem yok.')).toBeInTheDocument();
  });

  it('shows an error with a retry when the list fails', async () => {
    mockApi(changeRoutes(CHANGE, { [PROBLEMS_KEY]: { status: 500, body: { title: 'x', status: 500 } } }) as never);
    renderRoute(PATH);

    const region = await panel();
    expect(await within(region).findByRole('button', { name: 'Yeniden dene' })).toBeInTheDocument();
  });
});
