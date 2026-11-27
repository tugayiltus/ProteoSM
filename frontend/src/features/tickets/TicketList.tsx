/**
 * @input The queue that fixes the scope (mine, team …) and whether IT work columns (and the service filter) show.
 * @output The ticket list with search, status and priority filters in the URL, and paging. Staff rows show subject, type, status, team and the due time.
 * @position "Kayıtlarım" and the IT queue pages.
 */
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { OPEN_FILTER_ON, OPEN_FILTER_PARAM, SERVICE_FILTER_PARAM, ticketPath } from '../../shared/routes';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { OpenOnlyFilter } from '../services/OpenOnlyFilter';
import { ServiceFilter } from '../services/ServiceFilter';

import { PRIORITIES, TICKET_STATUSES, useTickets, type TicketListItem, type TicketQueue } from './api';
import { TicketDueCell } from './SlaDue';
import { PriorityBadge, TicketStatusBadge } from './TicketBadges';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['status', 'priority'] as const;
/** The staff list also filters by service. "Kayıtlarım" does not: employees never see services. */
const STAFF_FILTER_KEYS = [...FILTER_KEYS, SERVICE_FILTER_PARAM, OPEN_FILTER_PARAM] as const;
type StaffFilterKey = (typeof STAFF_FILTER_KEYS)[number];

const pick = <T extends string>(allowed: readonly T[], value: string): T | undefined => allowed.find((item) => item === value);

function OpenLink({ ticket }: { ticket: TicketListItem }) {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  return (
    <StateLink href={ticketPath(ticket.number)} state={linkState(location)} label={recordActionLabel(t('list.open'), ticket.number)}>
      {t('list.open')}
    </StateLink>
  );
}

/** `title` names the list in its toolbar; the default is the employee's own list. */
export function TicketList({ queue, showWorkColumns, title }: { queue: TicketQueue | undefined; showWorkColumns: boolean; title?: string }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const list = useListParams<StaffFilterKey>(showWorkColumns ? STAFF_FILTER_KEYS : FILTER_KEYS);
  const status = pick(TICKET_STATUSES, list.filters.status);
  const priority = pick(PRIORITIES, list.filters.priority);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const serviceId = showWorkColumns && list.filters[SERVICE_FILTER_PARAM] !== '' ? list.filters[SERVICE_FILTER_PARAM] : undefined;
  const open = showWorkColumns && list.filters[OPEN_FILTER_PARAM] === OPEN_FILTER_ON ? true : undefined;
  const tickets = useTickets({ queue, status, kind: undefined, priority, serviceId, open, search, page: list.page, pageSize: PAGE_SIZE });

  const columns: DataColumn<TicketListItem>[] = [
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <Text type="label">{x.number}</Text> },
    { key: 'subject', header: t('list.columns.subject'), renderCell: (x) => x.subject },
    ...(showWorkColumns ? [{ key: 'kind', header: t('list.columns.kind'), renderCell: (x: TicketListItem) => t(`kinds.${x.kind}`) }] : []),
    { key: 'status', header: t('list.columns.status'), renderCell: (x) => <TicketStatusBadge status={x.status} /> },
    ...(showWorkColumns
      ? [
          { key: 'priority', header: t('list.columns.priority'), renderCell: (x: TicketListItem) => <PriorityBadge priority={x.priority} /> },
          { key: 'team', header: t('list.columns.team'), renderCell: (x: TicketListItem) => x.teamName },
          { key: 'assignee', header: t('list.columns.assignee'), renderCell: (x: TicketListItem) => x.assigneeName ?? tc('none') },
          { key: 'due', header: t('list.columns.due'), renderCell: (x: TicketListItem) => <TicketDueCell sla={x.sla} /> },
        ]
      : [{ key: 'updated', header: t('list.columns.updated'), renderCell: (x: TicketListItem) => formatDateTime(x.updatedAt) }]),
    { key: 'open', header: '', renderCell: (x) => <OpenLink ticket={x} /> },
  ];

  const renderCard = (x: TicketListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="supporting">{x.number}</Text>
            <Text type="label" wordBreak="break-word">
              {x.subject}
            </Text>
          </VStack>
        </StackItem>
        <StackItem><TicketStatusBadge status={x.status} /></StackItem>
      </HStack>
      {showWorkColumns && (
        <HStack gap={2} vAlign="center" wrap="wrap">
          <Text type="supporting">{t(`kinds.${x.kind}`)}</Text>
          <PriorityBadge priority={x.priority} />
          <Text type="supporting">{x.teamName}</Text>
          <TicketDueCell sla={x.sla} />
        </HStack>
      )}
      <OpenLink ticket={x} />
    </VStack>
  );

  const option = (value: string | undefined) => value ?? '';
  return (
    <VStack gap={3}>
      <ListToolbar
        title={title ?? t('list.title')}
        search={list.search}
        searchLabel={t('list.search')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('list.statusFilter')}
              placeholder={t('list.all')}
              options={TICKET_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
              value={option(status)}
              onChange={(value) => list.setFilter('status', value)}
            />
            {showWorkColumns && (
              <Selector
                label={t('list.priorityFilter')}
                placeholder={t('list.all')}
                options={PRIORITIES.map((value) => ({ value, label: t(`priorities.${value}`) }))}
                value={option(priority)}
                onChange={(value) => list.setFilter('priority', value)}
              />
            )}
            {showWorkColumns && <ServiceFilter value={list.filters[SERVICE_FILTER_PARAM]} onChange={(value) => list.setFilter(SERVICE_FILTER_PARAM, value)} />}
            {showWorkColumns && <OpenOnlyFilter value={list.filters[OPEN_FILTER_PARAM]} onChange={(value) => list.setFilter(OPEN_FILTER_PARAM, value)} />}
          </>
        }
      />
      <Card padding={0}>
        {tickets.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {tickets.isError && <ErrorState error={tickets.error} onRetry={() => void tickets.refetch()} />}
        {tickets.data && tickets.data.items.length === 0 && <EmptyState message={t('list.empty')} />}
        {tickets.data && tickets.data.items.length > 0 && (
          <DataList items={tickets.data.items} columns={columns} getKey={(x) => x.number} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {tickets.data && tickets.data.items.length > 0 && <Pagination page={tickets.data.page} pageSize={tickets.data.pageSize} totalItems={tickets.data.totalCount} onChange={list.setPage} />}
    </VStack>
  );
}
