import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const MINUTES: Record<string, [number, number]> = { p1: [30, 240], p2: [60, 480], p3: [240, 1620], p4: [480, 2700] };
const OVERRIDE = { kind: 'request', priority: 'p3', requestTypeId: 'rt-1', metric: 'resolution', targetMinutes: 100, warningPercent: 50 };

function targets() {
  return ['incident', 'request'].flatMap((kind) =>
    Object.entries(MINUTES).flatMap(([priority, [first, resolution]]) => [
      { kind, priority, requestTypeId: null, metric: 'first_response', targetMinutes: first, warningPercent: 75 },
      { kind, priority, requestTypeId: null, metric: 'resolution', targetMinutes: resolution, warningPercent: 75 },
    ]),
  );
}

const policy = (number: number, extra: unknown[] = []) => ({
  versionId: `v-${number}`,
  number,
  createdAt: '2026-09-27T09:00:00Z',
  createdBy: 'u-1',
  targets: [...targets(), ...extra],
  pauseRules: [
    { holdReason: 'user', pausesFirstResponse: false, pausesResolution: true },
    { holdReason: 'manager_approval', pausesFirstResponse: false, pausesResolution: true },
  ],
});

describe('SlaPolicyPage', () => {
  it('shows the current version with its targets and pause rules', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/admin/sla/policy': { status: 200, body: policy(1) } });
    renderRoute('/admin/sla/policy');

    expect(await screen.findByText('Geçerli sürüm: 1')).toBeInTheDocument();
    expect(screen.getByLabelText('Arıza P1 · Kritik İlk yanıt Hedef (dk)')).toHaveValue('30');
    expect(screen.getByRole('checkbox', { name: 'Kullanıcıdan yanıt bekleniyor: Çözüm durur' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Kullanıcıdan yanıt bekleniyor: İlk yanıt durur' })).not.toBeChecked();
  });

  it('saves a new version, keeps request-type overrides and shows the new version', async () => {
    let posted: { targets: unknown[]; pauseRules: unknown[] } | undefined;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/sla/policy': { status: 200, body: policy(1, [OVERRIDE]) },
      'POST /api/v1/admin/sla/policy': async (request) => {
        posted = (await request.json()) as { targets: unknown[]; pauseRules: unknown[] };
        return { status: 201, body: policy(2, [OVERRIDE]) };
      },
    });
    const { user } = renderRoute('/admin/sla/policy');

    const field = await screen.findByLabelText('Arıza P1 · Kritik İlk yanıt Hedef (dk)');
    await user.clear(field);
    await user.type(field, '45');
    await user.click(screen.getByRole('button', { name: 'Yeni sürüm olarak kaydet' }));

    expect(await screen.findByText('Geçerli sürüm: 2')).toBeInTheDocument();
    expect((await screen.findAllByText('Yeni sürüm oluşturuldu.')).length).toBeGreaterThan(0);
    expect(posted?.targets).toHaveLength(17);
    expect(posted?.targets).toContainEqual({ kind: 'incident', priority: 'p1', requestTypeId: null, metric: 'first_response', targetMinutes: 45, warningPercent: 75 });
    expect(posted?.targets).toContainEqual(OVERRIDE);
    expect(posted?.pauseRules).toHaveLength(2);
  });

  it('refuses an empty target without calling the API', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/admin/sla/policy': { status: 200, body: policy(1) } });
    const { user } = renderRoute('/admin/sla/policy');

    await user.clear(await screen.findByLabelText('Arıza P1 · Kritik İlk yanıt Hedef (dk)'));
    await user.click(screen.getByRole('button', { name: 'Yeni sürüm olarak kaydet' }));

    expect(await screen.findByText(/Hedefler 1 veya daha büyük/)).toBeInTheDocument();
    expect(api.calls.some((c) => c.method === 'POST')).toBe(false);
  });

  it('saves_problem_targets_with_version', async () => {
    const PROBLEM_TARGETS = {
      version: 42,
      targets: [
        { impact: 'high', targetMinutes: 960, warningPercent: 80 },
        { impact: 'medium', targetMinutes: 2400, warningPercent: 80 },
        { impact: 'low', targetMinutes: 4800, warningPercent: 80 },
      ],
    };
    let put: { targets: unknown[]; version: number } | undefined;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/sla/policy': { status: 200, body: policy(1) },
      'GET /api/v1/admin/sla/problem-targets': { status: 200, body: PROBLEM_TARGETS },
      'PUT /api/v1/admin/sla/problem-targets': async (request) => {
        put = (await request.json()) as { targets: unknown[]; version: number };
        return { status: 200, body: { ...PROBLEM_TARGETS, version: 43 } };
      },
    });
    const { user } = renderRoute('/admin/sla/policy');
    const field = await screen.findByLabelText('Yüksek Hedef (dk)');
    expect(field).toHaveValue('960');
    await user.clear(field);
    await user.type(field, '600');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    expect((await screen.findAllByText('Problem hedefleri kaydedildi.')).length).toBeGreaterThan(0);
    expect(put?.version).toBe(42);
    expect(put?.targets).toContainEqual({ impact: 'high', targetMinutes: 600, warningPercent: 80 });
    expect(put?.targets).toHaveLength(3);
  });
});
