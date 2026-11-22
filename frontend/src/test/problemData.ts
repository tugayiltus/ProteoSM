import type { ProblemDetail, ProblemListItem } from '../features/problems/api';

import { IT_ME } from './mockApi';
import { TEAMS } from './taskData';

export const PROBLEM: ProblemDetail = {
  id: 'prb-1',
  number: 'PRB-2026-000001',
  title: 'VPN bağlantısı her sabah kopuyor',
  description: 'Kullanıcılar 09:00 civarı VPN dışına düşüyor',
  impact: 'high',
  status: 'open',
  ownerTeamId: 'team-1',
  ownerTeamName: 'Servis Masası',
  ownerId: IT_ME.id,
  ownerName: 'Can Demir',
  rootCause: null,
  workaround: null,
  resolution: null,
  cancelReason: null,
  articleId: null,
  articleTitle: null,
  createdAt: '2026-11-20T08:00:00Z',
  knownErrorAt: null,
  resolvedAt: null,
  canWork: true,
  version: 5,
};

export const PROBLEM_ROW: ProblemListItem = {
  id: PROBLEM.id,
  number: PROBLEM.number,
  title: PROBLEM.title,
  status: 'open',
  impact: 'high',
  ownerTeamName: PROBLEM.ownerTeamName,
  ownerName: PROBLEM.ownerName,
  incidentCount: 3,
  updatedAt: '2026-11-20T09:00:00Z',
};

export function problemRoutes(problem: ProblemDetail = PROBLEM, extra: Record<string, unknown> = {}) {
  const base = `/api/v1/problems/${problem.number}`;
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    [`GET ${base}`]: { status: 200, body: problem },
    [`GET ${base}/timeline`]: { status: 200, body: [] },
    [`GET ${base}/tickets`]: { status: 200, body: [] },
    [`GET ${base}/changes`]: { status: 200, body: [] },
    'GET /api/v1/reference/teams': { status: 200, body: TEAMS },
    ...extra,
  };
}
