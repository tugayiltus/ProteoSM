/**
 * @input `software`, `team`, `kind`, `state`, `q` and `page` in the URL query.
 * @output The licence list with filters and paging, and the new licence dialog.
 * @position `/licences`, inside the app shell (IT staff and admins).
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
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate } from '../../shared/lib/format';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { licencePath, ROUTES, SOFTWARE_FILTER_PARAM } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { useReferenceTeams } from '../tickets/api';

import { useLicences, useSoftware, type LicenceKind, type LicenceListItem, type LicenceState } from './api';
import { LicenceFormDialog } from './LicenceFormDialog';
import { LicenceSeats } from './LicenceTokens';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SOFTWARE_OPTIONS = 100;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = [SOFTWARE_FILTER_PARAM, 'team', 'kind', 'state'] as const;
const KINDS = ['per_user', 'per_device'] as const satisfies readonly LicenceKind[];
const STATES = ['full', 'expiring', 'expired'] as const satisfies readonly LicenceState[];

function OpenLink({ licence }: { licence: LicenceListItem }) {
  const { t } = useTranslation('licences');
  const location = useLocation();
  return (
    <StateLink href={licencePath(licence.id)} state={linkState(location)} label={recordActionLabel(t('list.open'), `${licence.softwareName} ${licence.name}`)}>
      {t('list.open')}
    </StateLink>
  );
}

export function LicencesPage() {
  const { t } = useTranslation('licences');
  const list = useListParams(FILTER_KEYS);
  const [isCreating, setIsCreating] = useState(false);
  const teams = useReferenceTeams(true);
  const software = useSoftware({ search: '', active: undefined, page: FIRST_PAGE, pageSize: SOFTWARE_OPTIONS });
  const teamOptions = useMemo(() => (teams.data ?? []).map((team) => ({ value: team.id, label: team.name })), [teams.data]);
  const softwareOptions = useMemo(() => (software.data?.items ?? []).map((x) => ({ value: x.id, label: `${x.vendor} ${x.name}` })), [software.data]);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const licences = useLicences({
    search,
    softwareId: list.filters.software,
    teamId: list.filters.team,
    kind: KINDS.find((kind) => kind === list.filters.kind),
    state: STATES.find((state) => state === list.filters.state),
    page: list.page,
    pageSize: PAGE_SIZE,
  });
  const end = (x: LicenceListItem) => (x.expiresOn === null ? t('noEnd') : formatDate(x.expiresOn));
  const columns: DataColumn<LicenceListItem>[] = [
    { key: 'software', header: t('columns.software'), renderCell: (x) => <Text type="label">{`${x.vendor} ${x.softwareName}`}</Text> },
    { key: 'name', header: t('columns.name'), renderCell: (x) => x.name },
    { key: 'kind', header: t('columns.kind'), renderCell: (x) => t(`kinds.${x.kind}`) },
    { key: 'seats', header: t('columns.seats'), renderCell: (x) => <LicenceSeats licence={x} /> },
    { key: 'team', header: t('columns.team'), renderCell: (x) => x.ownerTeamName },
    { key: 'end', header: t('columns.end'), renderCell: end },
    { key: 'open', header: '', renderCell: (x) => <OpenLink licence={x} /> },
  ];
  const renderCard = (x: LicenceListItem) => (
    <VStack gap={2}>
      <Text type="label" wordBreak="break-word">{`${x.vendor} ${x.softwareName}`}</Text>
      <Text wordBreak="break-word">{x.name}</Text>
      <LicenceSeats licence={x} />
      <Text type="supporting">{t('list.cardMeta', { kind: t(`kinds.${x.kind}`), team: x.ownerTeamName, end: end(x) })}</Text>
      <OpenLink licence={x} />
    </VStack>
  );
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.licences.icon}
        actions={
          <HStack gap={2} wrap="wrap">
            <Button label={t('software.title')} variant="secondary" href={ROUTES.software} />
            <Button label={t('form.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setIsCreating(true)} />
          </HStack>
        }
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
            <Selector label={t('columns.software')} placeholder={t('list.all')} options={softwareOptions} value={list.filters.software} onChange={(value) => list.setFilter(SOFTWARE_FILTER_PARAM, value)} />
            <Selector label={t('columns.team')} placeholder={t('list.all')} options={teamOptions} value={list.filters.team} onChange={(value) => list.setFilter('team', value)} />
            <Selector label={t('columns.kind')} placeholder={t('list.all')} options={KINDS.map((kind) => ({ value: kind, label: t(`kinds.${kind}`) }))} value={list.filters.kind} onChange={(value) => list.setFilter('kind', value)} />
            <Selector label={t('list.stateFilter')} placeholder={t('list.all')} options={STATES.map((state) => ({ value: state, label: t(`states.${state}`) }))} value={list.filters.state} onChange={(value) => list.setFilter('state', value)} />
          </>
        }
      />
      <Card padding={0}>
        {licences.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {licences.isError && <ErrorState error={licences.error} onRetry={() => void licences.refetch()} />}
        {licences.data && licences.data.items.length === 0 && <EmptyState message={t('list.empty')} />}
        {licences.data && licences.data.items.length > 0 && (
          <DataList items={licences.data.items} columns={columns} getKey={(x) => x.id} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {licences.data && licences.data.items.length > 0 && (
        <Pagination page={licences.data.page} pageSize={licences.data.pageSize} totalItems={licences.data.totalCount} onChange={list.setPage} />
      )}
      {isCreating && <LicenceFormDialog licence={null} onClose={() => setIsCreating(false)} />}
    </VStack>
  );
}
