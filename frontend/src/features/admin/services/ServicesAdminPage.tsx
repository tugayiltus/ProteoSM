/**
 * @input Nothing; search, status and page come from the URL query.
 * @output The service list with owner team, status and asset count; create, edit, assets and (de)activate.
 * @position `/admin/services`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { ActiveToken } from '../../../shared/page/ActiveToken';
import { RecordActions } from '../../../shared/page/RecordActions';
import { Icons } from '../../../shared/ui/icons';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { useAdminServices, useServiceDetail, useSetServiceActive, type ServiceListItem } from '../../services/serviceApi';
import { readStatusFilter, STATUS_FILTER_KEY } from '../shared/statusFilter';
import { StatusFilterSelector } from '../shared/StatusFilterSelector';

import { ServiceAssetsDialog } from './ServiceAssetsDialog';
import { ServiceFormDialog } from './ServiceFormDialog';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = [STATUS_FILTER_KEY] as const;

/** Confirms (de)activation with the service's current version, which the list row does not carry. */
function ToggleServiceDialog({ service, onClose }: { service: ServiceListItem; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const detail = useServiceDetail(service.id);
  const setActive = useSetServiceActive();
  return (
    <ConfirmDialog
      title={service.isActive ? t('services.deactivate') : t('services.activate')}
      message={service.isActive ? t('services.confirmDeactivate', { name: service.name }) : t('services.confirmActivate', { name: service.name })}
      confirmLabel={tc('actions.confirm')}
      variant={service.isActive ? 'danger' : 'primary'}
      isPending={setActive.isPending || detail.isPending}
      error={setActive.error ?? detail.error}
      onConfirm={() => {
        if (detail.data) {
          setActive.mutate({ id: service.id, version: detail.data.version, isActive: !service.isActive }, { onSuccess: onClose });
        }
      }}
      onCancel={onClose}
    />
  );
}

export function ServicesAdminPage() {
  const { t } = useTranslation('admin');
  const list = useListParams(FILTER_KEYS);
  const status = readStatusFilter(list.filters.status);
  const [form, setForm] = useState<{ serviceId: string | null } | null>(null);
  const [assetsOf, setAssetsOf] = useState<ServiceListItem | null>(null);
  const [toggling, setToggling] = useState<ServiceListItem | null>(null);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const services = useAdminServices({ search, active: status === undefined ? undefined : status === 'active', page: list.page, pageSize: PAGE_SIZE });

  const actions = (service: ServiceListItem) => (
    <RecordActions
      name={service.name}
      isActive={service.isActive}
      deactivateLabel={t('services.deactivate')}
      reactivateLabel={t('services.activate')}
      onEdit={() => setForm({ serviceId: service.id })}
      onToggle={() => setToggling(service)}
      extra={
        <Button
          label={t('services.manageAssets')}
          aria-label={recordActionLabel(t('services.manageAssets'), service.name)}
          size="sm"
          variant="secondary"
          onClick={() => setAssetsOf(service)}
        />
      }
    />
  );
  const columns: DataColumn<ServiceListItem>[] = [
    { key: 'name', header: t('services.columns.name'), renderCell: (service) => <Text type="label">{service.name}</Text> },
    { key: 'ownerTeam', header: t('services.columns.ownerTeam'), renderCell: (service) => service.ownerTeamName },
    { key: 'assets', header: t('services.columns.assets'), renderCell: (service) => String(service.assetCount) },
    { key: 'status', header: t('services.columns.status'), renderCell: (service) => <ActiveToken isActive={service.isActive} /> },
    { key: 'actions', header: t('services.columns.actions'), renderCell: actions },
  ];
  const renderCard = (service: ServiceListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="label" wordBreak="break-word">
              {service.name}
            </Text>
            <Text type="supporting">{t('services.cardMeta', { team: service.ownerTeamName, count: service.assetCount })}</Text>
          </VStack>
        </StackItem>
        <StackItem>
          <ActiveToken isActive={service.isActive} />
        </StackItem>
      </HStack>
      {actions(service)}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminServices.icon}
        titleOverride={t('services.title')}
        actions={<Button label={t('services.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ serviceId: null })} />}
      />
      <ListToolbar
        title={t('services.title')}
        search={list.search}
        searchLabel={t('services.searchLabel')}
        onSearchChange={list.setSearch}
        filters={<StatusFilterSelector value={status} onChange={(value) => list.setFilter(STATUS_FILTER_KEY, value)} />}
        activeCount={list.activeCount}
        onClear={list.clear}
      />
      <Card padding={0}>
        {services.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {services.isError && <ErrorState error={services.error} onRetry={() => void services.refetch()} />}
        {services.data && services.data.items.length === 0 && <EmptyState />}
        {services.data && services.data.items.length > 0 && (
          <DataList items={services.data.items} columns={columns} getKey={(service) => service.id} renderCard={renderCard} caption={t('services.listCaption')} />
        )}
      </Card>
      {services.data && services.data.items.length > 0 && (
        <Pagination page={services.data.page} pageSize={services.data.pageSize} totalItems={services.data.totalCount} onChange={list.setPage} />
      )}

      {form !== null && <ServiceFormDialog key={form.serviceId ?? 'new'} serviceId={form.serviceId} onClose={() => setForm(null)} />}
      {assetsOf !== null && <ServiceAssetsDialog key={assetsOf.id} serviceId={assetsOf.id} serviceName={assetsOf.name} onClose={() => setAssetsOf(null)} />}
      {toggling !== null && <ToggleServiceDialog key={toggling.id} service={toggling} onClose={() => setToggling(null)} />}
    </VStack>
  );
}
