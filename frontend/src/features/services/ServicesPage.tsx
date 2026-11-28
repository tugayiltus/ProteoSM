/**
 * @input `team`, `active`, `q` and `page` in the URL query.
 * @output The service list with filters and paging; each row opens the service impact page.
 * @position `/services`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

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
import { ActiveToken } from '../../shared/page/ActiveToken';
import { ROUTES, servicePath } from '../../shared/routes';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { ADMIN_ONLY, useSession } from '../auth/session';
import { useReferenceTeams } from '../tickets/api';

import { useServices, type ServiceListItem } from './api';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['team', 'active'] as const;
const ACTIVE_VALUE = 'true';
const INACTIVE_VALUE = 'false';

const parseActive = (value: string): boolean | undefined => (value === ACTIVE_VALUE ? true : value === INACTIVE_VALUE ? false : undefined);

function OpenLink({ service }: { service: ServiceListItem }) {
  const { t } = useTranslation('services');
  const location = useLocation();
  return (
    <StateLink href={servicePath(service.id)} state={linkState(location)} label={recordActionLabel(t('list.open'), service.name)}>
      {t('list.open')}
    </StateLink>
  );
}

export function ServicesPage() {
  const { t } = useTranslation('services');
  const { t: tc } = useTranslation();
  const isAdmin = ADMIN_ONLY.includes(useSession().role);
  const list = useListParams(FILTER_KEYS);
  const teams = useReferenceTeams(true);
  const teamOptions = useMemo(() => (teams.data ?? []).map((team) => ({ value: team.id, label: team.name })), [teams.data]);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const teamId = list.filters.team === '' ? undefined : list.filters.team;
  const active = parseActive(list.filters.active);
  const services = useServices({ search, active, teamId, page: list.page, pageSize: PAGE_SIZE });
  const columns: DataColumn<ServiceListItem>[] = [
    { key: 'name', header: t('list.columns.name'), renderCell: (x) => <Text type="label">{x.name}</Text> },
    { key: 'team', header: t('list.columns.ownerTeam'), renderCell: (x) => x.ownerTeamName },
    { key: 'assets', header: t('list.columns.assets'), renderCell: (x) => x.assetCount },
    { key: 'tickets', header: t('list.columns.openTickets'), renderCell: (x) => x.openTicketCount },
    { key: 'status', header: t('list.columns.status'), renderCell: (x) => <ActiveToken isActive={x.isActive} /> },
    { key: 'open', header: '', renderCell: (x) => <OpenLink service={x} /> },
  ];
  const renderCard = (x: ServiceListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="label" wordBreak="break-word">
              {x.name}
            </Text>
            <Text type="supporting">{x.ownerTeamName}</Text>
          </VStack>
        </StackItem>
        <StackItem>
          <ActiveToken isActive={x.isActive} />
        </StackItem>
      </HStack>
      <Text type="supporting">{t('list.counts', { assets: x.assetCount, tickets: x.openTicketCount })}</Text>
      <OpenLink service={x} />
    </VStack>
  );
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.services.icon} />
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
              label={t('list.teamFilter')}
              placeholder={t('list.all')}
              options={teamOptions}
              value={teamId ?? ''}
              onChange={(value) => list.setFilter('team', value)}
            />
            <Selector
              label={t('list.statusFilter')}
              placeholder={t('list.all')}
              options={[
                { value: ACTIVE_VALUE, label: tc('status.active') },
                { value: INACTIVE_VALUE, label: tc('status.inactive') },
              ]}
              value={active === undefined ? '' : String(active)}
              onChange={(value) => list.setFilter('active', value)}
            />
          </>
        }
      />
      <Card padding={0}>
        {services.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {services.isError && <ErrorState error={services.error} onRetry={() => void services.refetch()} />}
        {services.data && services.data.items.length === 0 && <ListEmptyState
            activeCount={list.activeCount}
            onClear={list.clear}
            message={t('list.empty')}
            action={isAdmin ? <Button label={t('list.setup')} variant="secondary" href={ROUTES.adminServices} /> : undefined}
          />}
        {services.data && services.data.items.length > 0 && (
          <DataList items={services.data.items} columns={columns} getKey={(x) => x.id} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {services.data && services.data.items.length > 0 && (
        <Pagination page={services.data.page} pageSize={services.data.pageSize} totalItems={services.data.totalCount} onChange={list.setPage} />
      )}
    </VStack>
  );
}
