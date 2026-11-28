/**
 * @input `status`, `team`, `q` and `page` in the URL query.
 * @output The release list with filters, paging and the new release dialog.
 * @position `/releases`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';

import { ListEmptyState } from '../../shared/feedback/ListEmptyState';
import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { releasePath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { WindowText } from '../changes/WindowText';
import { useReferenceTeams } from '../tickets/api';

import { RELEASE_STATUSES, useReleases, type ReleaseListItem } from './api';
import { ReleaseFormDialog } from './ReleaseFormDialog';
import { ReleaseStatusBadge } from './ReleaseStatusBadge';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['status', 'team'] as const;

function OpenLink({ release }: { release: ReleaseListItem }) {
  const { t } = useTranslation('releases');
  const location = useLocation();
  return (
    <StateLink href={releasePath(release.number)} state={linkState(location)} label={recordActionLabel(t('list.open'), release.number)}>
      {t('list.open')}
    </StateLink>
  );
}

export function ReleasesPage() {
  const { t } = useTranslation('releases');
  const navigate = useNavigate();
  const list = useListParams(FILTER_KEYS);
  const [isCreating, setIsCreating] = useState(false);
  const teams = useReferenceTeams(true);
  const teamOptions = useMemo(() => (teams.data ?? []).map((team) => ({ value: team.id, label: team.name })), [teams.data]);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const status = RELEASE_STATUSES.find((value) => value === list.filters.status);
  const releases = useReleases({ search, status, teamId: list.filters.team, page: list.page, pageSize: PAGE_SIZE });
  const columns: DataColumn<ReleaseListItem>[] = [
    { key: 'number', header: t('columns.number'), renderCell: (x) => x.number },
    { key: 'title', header: t('columns.title'), renderCell: (x) => <Text type="label">{x.title}</Text> },
    { key: 'status', header: t('columns.status'), renderCell: (x) => <ReleaseStatusBadge status={x.status} /> },
    { key: 'team', header: t('columns.team'), renderCell: (x) => x.ownerTeamName },
    { key: 'window', header: t('columns.window'), renderCell: (x) => <WindowText start={x.windowStart} end={x.windowEnd} /> },
    { key: 'changes', header: t('columns.changes'), renderCell: (x) => x.changeCount },
    { key: 'open', header: '', renderCell: (x) => <OpenLink release={x} /> },
  ];
  const renderCard = (x: ReleaseListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <Text type="supporting">{x.number}</Text>
        <ReleaseStatusBadge status={x.status} />
      </HStack>
      <Text type="label" wordBreak="break-word">
        {x.title}
      </Text>
      <Text type="supporting">{t('list.cardMeta', { team: x.ownerTeamName, count: x.changeCount })}</Text>
      <OpenLink release={x} />
    </VStack>
  );
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.releases.icon}
        actions={<Button label={t('form.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setIsCreating(true)} />}
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
              label={t('columns.status')}
              placeholder={t('list.all')}
              options={RELEASE_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
              value={status ?? ''}
              onChange={(value) => list.setFilter('status', value)}
            />
            <Selector label={t('columns.team')} placeholder={t('list.all')} options={teamOptions} value={list.filters.team} onChange={(value) => list.setFilter('team', value)} />
          </>
        }
      />
      <Card padding={0}>
        {releases.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {releases.isError && <ErrorState error={releases.error} onRetry={() => void releases.refetch()} />}
        {releases.data && releases.data.items.length === 0 && <ListEmptyState activeCount={list.activeCount} onClear={list.clear} message={t('list.empty')} action={<Button label={t('form.new')} variant="secondary" icon={<Icon icon={Icons.plus} />} onClick={() => setIsCreating(true)} />} />}
        {releases.data && releases.data.items.length > 0 && (
          <DataList items={releases.data.items} columns={columns} getKey={(x) => x.id} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {releases.data && releases.data.items.length > 0 && (
        <Pagination page={releases.data.page} pageSize={releases.data.pageSize} totalItems={releases.data.totalCount} onChange={list.setPage} />
      )}
      {isCreating && <ReleaseFormDialog release={null} onSaved={(x) => void navigate(releasePath(x.number))} onClose={() => setIsCreating(false)} />}
    </VStack>
  );
}
