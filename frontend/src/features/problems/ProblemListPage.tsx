/**
 * @input `queue`, `status`, `impact`, `serviceId`, `search` and `page` in the URL query.
 * @output The problem list with filters, paging and a button that opens a new problem.
 * @position `/problems`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { OPEN_FILTER_ON, OPEN_FILTER_PARAM, problemPath, SERVICE_FILTER_PARAM } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { OpenOnlyFilter } from '../services/OpenOnlyFilter';
import { ServiceFilter } from '../services/ServiceFilter';
import { SlaSummary } from '../tickets/SlaBadge';

import { LEVELS, PROBLEM_QUEUES, PROBLEM_STATUSES, toSlaEntry, URL_QUEUES, useProblems, type ProblemListItem } from './api';
import { ImpactBadge, ProblemStatusBadge } from './ProblemBadges';
import { ProblemFormDialog } from './ProblemFormDialog';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['queue', 'status', 'impact', SERVICE_FILTER_PARAM, OPEN_FILTER_PARAM] as const;

const pick = <T extends string>(allowed: readonly T[], value: string): T | undefined => allowed.find((item) => item === value);

function OpenLink({ problem }: { problem: ProblemListItem }) {
  const { t } = useTranslation('problems');
  const location = useLocation();
  return (
    <StateLink href={problemPath(problem.number)} state={linkState(location)} label={recordActionLabel(t('list.open'), problem.number)}>
      {t('list.open')}
    </StateLink>
  );
}

export function ProblemListPage() {
  const { t } = useTranslation('problems');
  const { t: tc } = useTranslation();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const list = useListParams(FILTER_KEYS);
  const queue = pick(URL_QUEUES, list.filters.queue);
  // A queue opened from a link but not offered in the filter still shows as the selected value.
  const queueOptions = queue === undefined || PROBLEM_QUEUES.includes(queue) ? PROBLEM_QUEUES : [...PROBLEM_QUEUES, queue];
  const status = pick(PROBLEM_STATUSES, list.filters.status);
  const impact = pick(LEVELS, list.filters.impact);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const serviceId = list.filters[SERVICE_FILTER_PARAM] === '' ? undefined : list.filters[SERVICE_FILTER_PARAM];
  const open = list.filters[OPEN_FILTER_PARAM] === OPEN_FILTER_ON ? true : undefined;
  const problems = useProblems({ queue, status, impact, serviceId, open, search, page: list.page, pageSize: PAGE_SIZE });
  const columns: DataColumn<ProblemListItem>[] = [
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <Text type="label">{x.number}</Text> },
    { key: 'title', header: t('list.columns.title'), renderCell: (x) => x.title },
    { key: 'status', header: t('list.columns.status'), renderCell: (x) => <ProblemStatusBadge status={x.status} /> },
    { key: 'impact', header: t('list.columns.impact'), renderCell: (x) => <ImpactBadge impact={x.impact} /> },
    { key: 'sla', header: t('list.columns.sla'), renderCell: (x) => (x.sla ? <SlaSummary sla={[toSlaEntry(x.sla)]} /> : tc('none')) },
    { key: 'incidents', header: t('list.columns.incidents'), renderCell: (x) => x.incidentCount },
    { key: 'team', header: t('list.columns.team'), renderCell: (x) => x.ownerTeamName },
    { key: 'owner', header: t('list.columns.owner'), renderCell: (x) => x.ownerName ?? tc('none') },
    { key: 'updated', header: t('list.columns.updated'), renderCell: (x) => formatDateTime(x.updatedAt) },
    { key: 'open', header: '', renderCell: (x) => <OpenLink problem={x} /> },
  ];
  const renderCard = (x: ProblemListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="supporting">{x.number}</Text>
            <Text type="label" wordBreak="break-word">
              {x.title}
            </Text>
          </VStack>
        </StackItem>
        <StackItem>
          <ProblemStatusBadge status={x.status} />
        </StackItem>
      </HStack>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <ImpactBadge impact={x.impact} />
        <Text type="supporting">{t('list.incidentCount', { count: x.incidentCount })}</Text>
      </HStack>
      {x.sla && <SlaSummary sla={[toSlaEntry(x.sla)]} />}
      <OpenLink problem={x} />
    </VStack>
  );
  const option = (value: string | undefined) => value ?? '';
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.problems.icon}
        titleOverride={queue ? t(`queues.${queue}`) : undefined}
        actions={<Button label={t('list.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setCreating(true)} />}
      />
      <ListToolbar
        title={t('list.title')}
        search={list.search}
        searchLabel={t('list.search')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('list.queueFilter')}
              placeholder={t('list.all')}
              options={queueOptions.map((value) => ({ value, label: t(`queues.${value}`) }))}
              value={option(queue)}
              onChange={(value) => list.setFilter('queue', value)}
            />
            <Selector
              label={t('list.statusFilter')}
              placeholder={t('list.all')}
              options={PROBLEM_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
              value={option(status)}
              onChange={(value) => list.setFilter('status', value)}
            />
            <Selector
              label={t('list.impactFilter')}
              placeholder={t('list.all')}
              options={LEVELS.map((value) => ({ value, label: t(`levels.${value}`) }))}
              value={option(impact)}
              onChange={(value) => list.setFilter('impact', value)}
            />
            <ServiceFilter value={list.filters[SERVICE_FILTER_PARAM]} onChange={(value) => list.setFilter(SERVICE_FILTER_PARAM, value)} />
            <OpenOnlyFilter value={list.filters[OPEN_FILTER_PARAM]} onChange={(value) => list.setFilter(OPEN_FILTER_PARAM, value)} />
          </>
        }
      />
      <Card padding={0}>
        {problems.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {problems.isError && <ErrorState error={problems.error} onRetry={() => void problems.refetch()} />}
        {problems.data && problems.data.items.length === 0 && <EmptyState message={t('list.empty')} />}
        {problems.data && problems.data.items.length > 0 && (
          <DataList items={problems.data.items} columns={columns} getKey={(x) => x.number} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {problems.data && problems.data.items.length > 0 && (
        <Pagination page={problems.data.page} pageSize={problems.data.pageSize} totalItems={problems.data.totalCount} onChange={list.setPage} />
      )}
      {creating && <ProblemFormDialog onClose={() => setCreating(false)} onSaved={(problem) => void navigate(problemPath(problem.number))} />}
    </VStack>
  );
}
