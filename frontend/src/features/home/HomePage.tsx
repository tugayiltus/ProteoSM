/**
 * @input Nothing; reads the session.
 * @output The role-based start page: report actions, IT queues, latest records and more links.
 * @position `/`, inside the app shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid } from '@astryxdesign/core/Grid';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { List, ListItem } from '@astryxdesign/core/List';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { ActionCard, type ActionCardProps } from '../../shared/page/ActionCard';
import { ROUTES, ticketPath, ticketRatePath } from '../../shared/routes';
import { isItStaffOrAdmin, useSession } from '../auth/session';
import { useTickets, type WorkQueue, type TicketStatus, type TicketListItem } from '../tickets/api';
import { TicketStatusBadge } from '../tickets/TicketBadges';
import { QueueCard } from '../workspace/QueueCard';

import { Announcements } from './Announcements';

const RECENT_COUNT = 5;
const RATE_COUNT = 5;
/** On-hold tickets fetched to find those that wait for the requester; older ones must not be missed. */
const ON_HOLD_SCAN_COUNT = 20;
const FIRST_PAGE = 1;
const HOME_QUEUES: readonly WorkQueue[] = ['assigned_to_me', 'team', 'sla_risk'];

/** Cards reflow from one column on phones to several on wide screens. */
const CARD_MIN_WIDTH = 260;
const CARD_COLUMNS = { minWidth: CARD_MIN_WIDTH } as const;

function ReportActions() {
  const { t } = useTranslation('tickets');
  const actions: ActionCardProps[] = [
    { to: ROUTES.newIncident, title: t('home.incidentCard'), description: t('home.incidentCardDescription'), icon: 'warning', iconColor: 'error' },
    { to: ROUTES.newRequest, title: t('home.requestCard'), description: t('home.requestCardDescription'), icon: 'plus' },
  ];
  return (
    <Grid columns={CARD_COLUMNS} gap={3}>
      {actions.map((action) => (
        <ActionCard key={action.to} {...action} />
      ))}
    </Grid>
  );
}

/** On hold with reason `user`: IT waits for the requester's answer. */
const isWaitingForRequester = (ticket: TicketListItem) => ticket.status === 'on_hold' && ticket.holdReason === 'user';

function RecentTicketRow({ number, subject, status, state, href = ticketPath(number), needsReply = false }: { number: string; subject: string; status: TicketStatus; state: ReturnType<typeof linkState>; href?: string; needsReply?: boolean }) {
  const { t } = useTranslation('tickets');
  const link = useRef<HTMLAnchorElement>(null);
  return (
    <ListItem
      label={
        <StateLink href={href} state={state} ref={link}>
          {subject}
        </StateLink>
      }
      description={number}
      interactiveRef={link}
      endContent={
        <HStack gap={2} vAlign="center">
          {needsReply && <Token size="sm" color="orange" label={t('home.needsReply')} />}
          <TicketStatusBadge status={status} />
        </HStack>
      }
    />
  );
}

function RecentTickets() {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  const tickets = useTickets({ queue: 'mine', status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: RECENT_COUNT });
  const onHold = useTickets({ queue: 'mine', status: 'on_hold', kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: ON_HOLD_SCAN_COUNT });
  const items = useMemo(() => {
    const newest = tickets.data?.items ?? [];
    const waiting = (onHold.data?.items ?? []).filter(isWaitingForRequester);
    const merged = [...waiting, ...newest.filter((ticket) => !waiting.some((w) => w.number === ticket.number))];
    return [...merged.filter(isWaitingForRequester), ...merged.filter((ticket) => !isWaitingForRequester(ticket))].slice(0, RECENT_COUNT);
  }, [tickets.data, onHold.data]);
  return (
    <Card padding={0}>
      <VStack gap={0}>
        <HStack gap={2} hAlign="between" vAlign="center" padding={4}>
          <Heading level={2}>{t('home.recentTitle')}</Heading>
          <Button label={t('home.allTickets')} variant="ghost" href={ROUTES.tickets} />
        </HStack>
        {tickets.isPending && <LoadingState rows={RECENT_COUNT} />}
        {tickets.isError && <ErrorState error={tickets.error} onRetry={() => void tickets.refetch()} />}
        {tickets.data?.items.length === 0 && <EmptyState message={t('home.recentEmpty')} />}
        {items.length > 0 && (
          <List hasDividers aria-label={t('home.recentTitle')}>
            {items.map((ticket) => (
              <RecentTicketRow key={ticket.number} number={ticket.number} subject={ticket.subject} status={ticket.status} state={linkState(location)} needsReply={isWaitingForRequester(ticket)} />
            ))}
          </List>
        )}
      </VStack>
    </Card>
  );
}

/** Shows only while the requester has completed tickets without a rating; stays absent on load, error and empty. */
function TicketsToRate() {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  const tickets = useTickets({ queue: 'to_rate', status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: RATE_COUNT });
  const items = tickets.data?.items ?? [];
  if (items.length === 0) {
    return null;
  }
  return (
    <Card padding={0}>
      <VStack gap={0}>
        <HStack padding={4}>
          <Heading level={2}>{t('home.rateTitle')}</Heading>
        </HStack>
        <List hasDividers aria-label={t('home.rateTitle')}>
          {items.map((ticket) => (
            <RecentTicketRow key={ticket.number} number={ticket.number} subject={ticket.subject} status={ticket.status} state={linkState(location)} href={ticketRatePath(ticket.number)} />
          ))}
        </List>
      </VStack>
    </Card>
  );
}

function WorkListLink() {
  const { t } = useTranslation('tickets');
  return <ActionCard to={ROUTES.workspace} title={t('home.workspaceCard')} description={t('home.workspaceCardDescription')} icon="inbox" />;
}

function MoreLinks() {
  const { t } = useTranslation('tickets');
  const { t: tk } = useTranslation('kb');
  const { t: ta } = useTranslation('assets');
  const { t: tl } = useTranslation('licences');
  const links: ActionCardProps[] = [
    { to: ROUTES.kb, title: tk('home.card'), description: tk('home.cardDescription'), icon: 'book' },
    { to: ROUTES.myAssets, title: ta('home.mineCard'), description: ta('home.mineCardDescription'), icon: 'laptop' },
    { to: ROUTES.mySoftware, title: tl('home.mineCard'), description: tl('home.mineCardDescription'), icon: 'licences' },
  ];
  return (
    <VStack gap={3}>
      <Heading level={2}>{t('home.moreTitle')}</Heading>
      <Grid columns={CARD_COLUMNS} gap={3}>
        {links.map((link) => (
          <ActionCard key={link.to} {...link} />
        ))}
      </Grid>
    </VStack>
  );
}

/** Employees get the report actions and their latest records; IT staff also their queues. */
export function HomePage() {
  const { t } = useTranslation('auth');
  const { t: tt } = useTranslation('tickets');
  const me = useSession();
  return (
    <VStack gap={6}>
      <PageHeader icon={ROUTE_META.home.icon} titleOverride={t('home.greeting', { name: me.firstName })} />
      <Announcements />
      <ReportActions />
      {isItStaffOrAdmin(me.role) && (
        <VStack gap={3}>
          <Heading level={2}>{tt('home.queuesTitle')}</Heading>
          <Grid columns={CARD_COLUMNS} gap={3}>
            <WorkListLink />
            {HOME_QUEUES.map((queue) => (
              <QueueCard key={queue} queue={queue} />
            ))}
          </Grid>
        </VStack>
      )}
      <TicketsToRate />
      <RecentTickets />
      <MoreLinks />
    </VStack>
  );
}
