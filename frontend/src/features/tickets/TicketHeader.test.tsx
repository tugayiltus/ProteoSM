import { render, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { dayRelation } from '../../shared/lib/format';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

import { TicketSlaLine } from './SlaDue';
import { currentSla } from './slaState';

const DUE_AT = '2099-12-03T08:00:00Z';
const LATER_AT = '2099-12-04T08:00:00Z';
const TIMER = { metric: 'resolution' as const, dueAt: DUE_AT, remainingSeconds: 1800, isPaused: false, isWarned: true, isBreached: false, result: null };
const WORKABLE = { ...INCIDENT, canWork: true, sla: [TIMER] };

describe('ticket header', () => {
  it('shows subject, number, status, team, the SLA state with its due date and "Üzerime al" for unassigned work', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...detailRoutes(WORKABLE) } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    expect(await screen.findByRole('heading', { level: 1, name: INCIDENT.subject })).toBeInTheDocument();
    expect(screen.getByText(new RegExp(`${INCIDENT.number} · Arıza · ${INCIDENT.teamName}`))).toBeInTheDocument();
    expect(screen.getByText(/Çözüm SLA'sı risk altında · Son tarih /)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Üzerime al' })).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Yanıtla' })).not.toBeInTheDocument();
  });

  it('offers "Yanıtla" once the ticket has an assignee and moves the focus to the public reply field', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...detailRoutes({ ...WORKABLE, assigneeId: IT_ME.id, assigneeName: 'Can Demir' }) } as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.type(await screen.findByRole('textbox', { name: 'Mesajınız' }), 'Genel taslak');
    await user.click(screen.getByRole('button', { name: 'Yanıtla' }));

    expect(screen.getByRole('tab', { name: 'Yanıt' })).toHaveAttribute('aria-selected', 'true');
    const focused = screen.getByRole('textbox', { name: 'Mesajınız' });
    expect(focused).toHaveFocus();
    expect(focused).toHaveValue('Genel taslak');
  });

  it('never turns an internal note draft into a public reply draft', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...detailRoutes({ ...WORKABLE, assigneeId: IT_ME.id, assigneeName: 'Can Demir' }) } as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await screen.findByRole('textbox', { name: 'Mesajınız' });
    await user.click(screen.getByRole('tab', { name: 'İç not' }));
    await user.type(screen.getByRole('textbox', { name: 'Mesajınız' }), 'Gizli not');
    await user.click(screen.getByRole('button', { name: 'Yanıtla' }));

    expect(screen.getByRole('tab', { name: 'Yanıt' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('textbox', { name: 'Mesajınız' })).toHaveValue('');
    expect(screen.getByRole('textbox', { name: 'Mesajınız' })).toHaveFocus();
    await user.click(screen.getByRole('tab', { name: 'İç not' }));
    expect(screen.getByRole('textbox', { name: 'Mesajınız' })).toHaveValue('Gizli not');
  });

  it('moves the keyboard focus to "Yanıtla" after a claim', async () => {
    const base = `/api/v1/tickets/${INCIDENT.number}`;
    const claimed = { ...WORKABLE, assigneeId: IT_ME.id, assigneeName: 'Can Demir', version: WORKABLE.version + 1 };
    let isClaimed = false;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      ...detailRoutes(WORKABLE),
      [`GET ${base}`]: () => ({ status: 200, body: isClaimed ? claimed : WORKABLE }),
      [`POST ${base}/claim`]: () => {
        isClaimed = true;
        return { status: 200, body: claimed };
      },
    } as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    const claim = await screen.findByRole('button', { name: 'Üzerime al' });
    claim.focus();
    await user.keyboard('{Enter}');

    await waitFor(() => expect(screen.getByRole('button', { name: 'Yanıtla' })).toHaveFocus());
  });

  it('gives the requester "Yanıtla" and no work action, and none on a closed ticket', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...detailRoutes() } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);
    expect(await screen.findByRole('button', { name: 'Yanıtla' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Üzerime al' })).not.toBeInTheDocument();
  });

  it('shows no main action on a closed ticket', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...detailRoutes({ ...WORKABLE, status: 'completed' }) } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);
    await screen.findByRole('heading', { level: 1, name: INCIDENT.subject });
    expect(screen.queryByRole('button', { name: 'Yanıtla' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Üzerime al' })).not.toBeInTheDocument();
  });

  it('keeps the related sections to one row when they are empty', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...detailRoutes(WORKABLE) } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    const assets = await screen.findByRole('region', { name: 'Bağlı cihazlar' });
    expect(await within(assets).findByText('Bağlı cihaz yok.')).toBeInTheDocument();
    expect(within(assets).getByRole('button', { name: 'Cihaz bağla' })).toBeInTheDocument();
  });
});

describe('SLA line states', () => {
  it('tells loading, error and "no SLA" apart', () => {
    const { rerender } = render(<TicketSlaLine load={{ status: 'loading' }} />);
    expect(screen.getByText('SLA yükleniyor')).toBeInTheDocument();
    rerender(<TicketSlaLine load={{ status: 'error' }} />);
    expect(screen.getByText('SLA bilgisi alınamadı')).toBeInTheDocument();
    rerender(<TicketSlaLine load={{ status: 'ready', sla: [] }} />);
    expect(screen.getByText('SLA yok')).toBeInTheDocument();
  });

  it('names a paused timer without inventing a due date', () => {
    render(<TicketSlaLine load={{ status: 'ready', sla: [{ ...TIMER, dueAt: null, isPaused: true, isWarned: false }] }} />);
    expect(screen.getByText("Çözüm SLA'sı duraklatıldı")).toBeInTheDocument();
  });
});

describe('currentSla', () => {
  it('picks the running timer due first, else the first running one, else the first', () => {
    const early = { ...TIMER, metric: 'first_response' as const, dueAt: DUE_AT };
    const late = { ...TIMER, dueAt: LATER_AT };
    const stopped = { ...TIMER, result: 'met' as const };
    expect(currentSla([late, early])).toBe(early);
    expect(currentSla([stopped, { ...late, dueAt: null }])?.dueAt).toBeNull();
    expect(currentSla([stopped])).toBe(stopped);
    expect(currentSla([])).toBeNull();
  });
});

describe('dayRelation', () => {
  it('tells today and tomorrow in the display time zone, also across a month end', () => {
    const now = new Date('2026-12-31T10:00:00Z');
    expect(dayRelation('2026-12-31T20:00:00Z', now)).toBe('today');
    expect(dayRelation('2027-01-01T08:00:00Z', now)).toBe('tomorrow');
    expect(dayRelation('2027-01-02T08:00:00Z', now)).toBe('other');
  });
});
