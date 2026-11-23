import type { ServiceDetail, ServiceImpact, ServiceListItem } from '../features/services/api';

import { IT_ME } from './mockApi';
import { PROBLEM } from './problemData';
import { TEAMS } from './taskData';

export const SERVICE: ServiceDetail = {
  id: 'svc-1',
  name: 'E-posta',
  description: 'Kurumsal e-posta ve takvim',
  ownerTeamId: 'team-1',
  ownerTeamName: 'Servis Masası',
  isActive: true,
  version: 3,
};

export const SERVICE_ROW: ServiceListItem = {
  id: SERVICE.id,
  name: SERVICE.name,
  ownerTeamId: SERVICE.ownerTeamId,
  ownerTeamName: SERVICE.ownerTeamName,
  isActive: true,
  assetCount: 4,
  openTicketCount: 7,
};

/** Assets, tickets and problems hold more than they list (the per-section cap); changes list all. */
export const IMPACT: ServiceImpact = {
  assets: { items: [{ id: 'asset-1', tag: 'ASSET-2026-0001', name: 'Posta sunucusu', status: 'assigned' }], total: 30 },
  tickets: {
    items: [
      { number: 'INC-2026-000001', title: 'Posta gitmiyor', status: 'in_progress', canOpen: true },
      { number: 'INC-2026-000002', title: 'Başka ekibin kaydı', status: 'open', canOpen: false },
    ],
    total: 25,
  },
  changes: { items: [{ number: 'CHG-2026-000001', title: 'Posta sunucusu yaması', status: 'scheduled', canOpen: true }], total: 1 },
  problems: { items: [{ number: PROBLEM.number, title: PROBLEM.title, status: 'open', canOpen: true }], total: 21 },
};

export function serviceRoutes(extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    'GET /api/v1/reference/teams': { status: 200, body: TEAMS },
    'GET /api/v1/services': { status: 200, body: { items: [SERVICE_ROW], page: 1, pageSize: 20, totalCount: 1 } },
    [`GET /api/v1/services/${SERVICE.id}`]: { status: 200, body: SERVICE },
    [`GET /api/v1/services/${SERVICE.id}/impact`]: { status: 200, body: IMPACT },
    ...extra,
  };
}
