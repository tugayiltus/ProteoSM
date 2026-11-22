import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';
import { PROBLEM, problemRoutes } from '../../test/problemData';
import { renderRoute } from '../../test/renderRoute';

import type { ProblemDetail } from './api';

const BASE = `/api/v1/problems/${PROBLEM.number}`;
const DIAGNOSED: ProblemDetail = { ...PROBLEM, status: 'investigating', rootCause: 'DHCP kira süresi kısa', workaround: 'VPN istemcisini yeniden başlatın' };

const TICKETS = [
  { number: 'INC-2026-000010', title: 'VPN koptu', status: 'open', teamName: 'Servis Masası', linkedAt: '2026-11-20T09:00:00Z', canOpen: true },
  { number: 'INC-2026-000011', title: 'Saha VPN sorunu', status: 'in_progress', teamName: null, linkedAt: '2026-11-20T09:05:00Z', canOpen: false },
];
const CHANGES = [{ number: 'CHG-2026-000004', title: 'DHCP kira süresini uzat', status: 'scheduled', linkedAt: '2026-11-20T10:00:00Z', canOpen: false }];
const TIMELINE = [
  { id: 'e-1', kind: 'created', actorName: 'Can Demir', data: {}, occurredAt: '2026-11-20T08:00:00Z' },
  { id: 'e-2', kind: 'ticket_linked', actorName: 'Can Demir', data: { ticketNumber: 'INC-2026-000010', problemNumber: PROBLEM.number }, occurredAt: '2026-11-20T09:00:00Z' },
  { id: 'e-3', kind: 'commented', actorName: 'Can Demir', data: { text: 'Loglar toplandı' }, occurredAt: '2026-11-20T09:30:00Z' },
];

describe('ProblemDetailPage', () => {
  it('shows the problem sections, linked records and history', async () => {
    mockApi(
      problemRoutes(DIAGNOSED, {
        [`GET ${BASE}/tickets`]: { status: 200, body: TICKETS },
        [`GET ${BASE}/changes`]: { status: 200, body: CHANGES },
        [`GET ${BASE}/timeline`]: { status: 200, body: TIMELINE },
      }) as never,
    );
    renderRoute(`/problems/${PROBLEM.number}`);

    expect(await screen.findByRole('heading', { name: PROBLEM.title })).toBeInTheDocument();
    for (const label of ['Belirti', 'Kök neden', 'Geçici çözüm', 'Kalıcı çözüm']) {
      expect(screen.getByText(label)).toBeInTheDocument();
    }
    expect(screen.getByText(PROBLEM.description)).toBeInTheDocument();
    expect(screen.getByText(DIAGNOSED.workaround ?? '')).toBeInTheDocument();
    expect(screen.getAllByText('İnceleniyor')).not.toHaveLength(0);

    const incidents = await screen.findByRole('list', { name: 'İlgili arızalar' });
    expect(within(incidents).getByRole('link', { name: 'INC-2026-000010 · VPN koptu' })).toHaveAttribute('href', '/tickets/INC-2026-000010');
    // A row the viewer cannot open shows its number, title and status without a link.
    expect(within(incidents).getByText('INC-2026-000011 · Saha VPN sorunu')).toBeInTheDocument();
    expect(within(incidents).queryByRole('link', { name: /INC-2026-000011/ })).not.toBeInTheDocument();
    const changes = screen.getByRole('list', { name: 'Düzelten değişiklikler' });
    expect(within(changes).getByText('CHG-2026-000004 · DHCP kira süresini uzat')).toBeInTheDocument();
    expect(within(changes).queryByRole('link')).not.toBeInTheDocument();

    const history = await screen.findByRole('list', { name: 'Geçmiş ve notlar' });
    expect(within(history).getByText('Arıza bağlandı')).toBeInTheDocument();
    expect(within(history).getByText('INC-2026-000010')).toBeInTheDocument();
    expect(within(history).getByText('Loglar toplandı')).toBeInTheDocument();
  });

  it('starts an open problem with İncelemeye al as the primary step', async () => {
    let posted: unknown;
    let current: ProblemDetail = PROBLEM;
    mockApi(
      problemRoutes(PROBLEM, {
        [`GET ${BASE}`]: () => ({ status: 200, body: current }),
        [`POST ${BASE}/start`]: async (request: Request) => {
          posted = await request.json();
          current = { ...PROBLEM, status: 'investigating', version: 6 };
          return { status: 200, body: current };
        },
      }) as never,
    );
    const { user } = renderRoute(`/problems/${PROBLEM.number}`);

    await user.click(await screen.findByRole('button', { name: 'İncelemeye al' }));

    await waitFor(() => expect(posted).toEqual({ version: PROBLEM.version }));
    expect(await screen.findByRole('button', { name: 'Bilinen hata olarak işaretle' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'İncelemeye al' })).not.toBeInTheDocument();
  });

  it('marks an investigated problem as a known error once root cause and workaround are written', async () => {
    let posted: unknown;
    mockApi(
      problemRoutes(DIAGNOSED, {
        [`POST ${BASE}/known-error`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: { ...DIAGNOSED, status: 'known_error', version: 6 } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/problems/${PROBLEM.number}`);

    expect(await screen.findByRole('button', { name: 'Çözüldü olarak kapat' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'İncelemeye al' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Bilinen hata olarak işaretle' }));

    await waitFor(() => expect(posted).toEqual({ version: PROBLEM.version }));
  });

  it('keeps the known error step disabled until root cause and workaround are written', async () => {
    mockApi(problemRoutes({ ...PROBLEM, status: 'investigating' }) as never);
    renderRoute(`/problems/${PROBLEM.number}`);

    expect(await screen.findByRole('button', { name: 'Bilinen hata olarak işaretle' })).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByText(/önce kök nedeni ve geçici çözümü yazın/)).toBeInTheDocument();
  });

  it('resolves a known error with the resolution note', async () => {
    let posted: unknown;
    mockApi(
      problemRoutes({ ...DIAGNOSED, status: 'known_error' }, {
        [`POST ${BASE}/resolve`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: { ...DIAGNOSED, status: 'resolved', resolution: 'Kira süresi uzatıldı', version: 6 } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/problems/${PROBLEM.number}`);

    await user.click(await screen.findByRole('button', { name: 'Çözüldü olarak kapat' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByRole('button', { name: 'Bilinen hata olarak işaretle' })).not.toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Kalıcı çözüm'), '  Kira süresi uzatıldı ');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: PROBLEM.version, resolution: 'Kira süresi uzatıldı' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('shows the conflict message and closes the dialog on 409', async () => {
    mockApi(problemRoutes({ ...DIAGNOSED, status: 'known_error' }, { [`POST ${BASE}/resolve`]: { status: 409, body: { code: 'concurrency.conflict' } } }) as never);
    const { user } = renderRoute(`/problems/${PROBLEM.number}`);

    await user.click(await screen.findByRole('button', { name: 'Çözüldü olarak kapat' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Kalıcı çözüm'), 'Kira süresi uzatıldı');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    expect(await screen.findByText(/başka biri tarafından değiştirildi/)).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('shows the content but no write actions to someone outside the owner team', async () => {
    mockApi(
      problemRoutes({ ...DIAGNOSED, status: 'known_error', canWork: false }, {
        [`GET ${BASE}/tickets`]: { status: 200, body: TICKETS },
        [`GET ${BASE}/timeline`]: { status: 200, body: TIMELINE },
      }) as never,
    );
    renderRoute(`/problems/${PROBLEM.number}`);

    expect(await screen.findByText(DIAGNOSED.workaround ?? '')).toBeInTheDocument();
    expect(await screen.findByText('Loglar toplandı')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'INC-2026-000010 · VPN koptu' })).toBeInTheDocument();
    for (const name of ['Çözüldü olarak kapat', 'İptal et', 'Düzenle', 'Aktar', 'Sahip ata', 'Makale oluştur', 'Arıza bağla', 'Değişiklik bağla', /^Kaldır/]) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    }
    expect(screen.queryByRole('textbox', { name: 'Not' })).not.toBeInTheDocument();
  });

  it('creates the known error article and then shows its link', async () => {
    let created = false;
    let current: ProblemDetail = { ...DIAGNOSED, status: 'known_error' };
    mockApi(
      problemRoutes(current, {
        [`GET ${BASE}`]: () => ({ status: 200, body: current }),
        [`POST ${BASE}/article`]: () => {
          created = true;
          current = { ...current, articleId: 'art-1', articleTitle: `Bilinen hata: ${PROBLEM.title}`, version: 6 };
          return { status: 201, body: current };
        },
      }) as never,
    );
    const { user } = renderRoute(`/problems/${PROBLEM.number}`);

    await user.click(await screen.findByRole('button', { name: 'Makale oluştur' }));

    const link = await screen.findByRole('link', { name: `Bilinen hata: ${PROBLEM.title}` });
    expect(created).toBe(true);
    expect(link).toHaveAttribute('href', '/kb/art-1');
    expect(screen.queryByRole('button', { name: 'Makale oluştur' })).not.toBeInTheDocument();
  });

  it.each(['open', 'investigating', 'cancelled'] as const)('does not offer the article while the problem is %s', async (status) => {
    mockApi(problemRoutes({ ...DIAGNOSED, status }) as never);
    renderRoute(`/problems/${PROBLEM.number}`);

    expect(await screen.findByText('Bilinen hata makalesi')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Makale oluştur' })).not.toBeInTheDocument();
  });

  it('links an incident by its number', async () => {
    let posted: unknown;
    let linked: typeof TICKETS = [];
    mockApi(
      problemRoutes(DIAGNOSED, {
        [`GET ${BASE}/tickets`]: () => ({ status: 200, body: linked }),
        [`POST ${BASE}/tickets`]: async (request: Request) => {
          posted = await request.json();
          linked = TICKETS.slice(0, 1);
          return { status: 201, body: linked };
        },
      }) as never,
    );
    const { user } = renderRoute(`/problems/${PROBLEM.number}`);

    await user.click(await screen.findByRole('button', { name: 'Arıza bağla' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Arıza numarası'), ' INC-2026-000010 ');
    await user.click(within(dialog).getByRole('button', { name: 'Bağla' }));

    await waitFor(() => expect(posted).toEqual({ ticketNumber: 'INC-2026-000010' }));
    expect(await screen.findByRole('link', { name: 'INC-2026-000010 · VPN koptu' })).toBeInTheDocument();
  });
});
