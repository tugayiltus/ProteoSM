/**
 * @input `active`, `q` and `page` in the URL query.
 * @output The software list with create and edit dialogs; a row links to its licences.
 * @position `/software`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Link } from '@astryxdesign/core/Link';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { ListEmptyState } from '../../shared/feedback/ListEmptyState';
import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { FormSwitch, FormTextInput } from '../../shared/form/formFields';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ActiveToken } from '../../shared/page/ActiveToken';
import { softwareLicencesPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { useCloseOnConflict } from '../changes/useCloseOnConflict';

import { useCreateSoftware, useSoftware, useUpdateSoftware, type SoftwareItem } from './api';

/** Mirrors backend LicenceLimits. */
const NAME_MAX_LENGTH = 150;
const VENDOR_MAX_LENGTH = 100;
const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['active'] as const;
const ACTIVE_VALUE = 'true';
const INACTIVE_VALUE = 'false';

const schema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  vendor: z.string().trim().min(1, ERROR_CODES.required).max(VENDOR_MAX_LENGTH, ERROR_CODES.tooLong),
  isActive: z.boolean(),
});
type FormValues = z.infer<typeof schema>;
const FORM_FIELDS = ['name', 'vendor'] as const;
const CODE_FIELDS = { [ERROR_CODES.softwareNameTaken]: 'name' } as const;

function SoftwareFormDialog({ software, onClose }: { software: SoftwareItem | null; onClose: () => void }) {
  const { t } = useTranslation('licences');
  const { t: tc } = useTranslation();
  const create = useCreateSoftware();
  const update = useUpdateSoftware();
  const mutation = software === null ? create : update;
  const closeOnConflict = useCloseOnConflict(onClose);
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: software?.name ?? '', vendor: software?.vendor ?? '', isActive: software?.isActive ?? true },
  });
  const onSubmit = handleSubmit(({ name, vendor, isActive }) => {
    const onError = (error: unknown) => {
      closeOnConflict(error);
      applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS);
    };
    const callbacks = { onSuccess: onClose, onError };
    if (software === null) {
      create.mutate({ name, vendor }, callbacks);
    } else {
      update.mutate({ id: software.id, version: software.version, name, vendor, isActive }, callbacks);
    }
  });
  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;
  return (
    <FormDialog
      isDirty={isDirty}
      title={software === null ? t('software.new') : t('software.edit')}
      submitLabel={software === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="vendor" label={t('software.vendor')} autoComplete="off" isRequired />
      <FormTextInput control={control} name="name" label={t('software.name')} autoComplete="off" isRequired />
      {software !== null && <FormSwitch control={control} name="isActive" label={t('software.active')} />}
    </FormDialog>
  );
}

export function SoftwarePage() {
  const { t } = useTranslation('licences');
  const { t: tc } = useTranslation();
  const list = useListParams(FILTER_KEYS);
  const [form, setForm] = useState<{ software: SoftwareItem | null } | null>(null);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const active = list.filters.active === ACTIVE_VALUE ? true : list.filters.active === INACTIVE_VALUE ? false : undefined;
  const software = useSoftware({ search, active, page: list.page, pageSize: PAGE_SIZE });
  const licenceLink = (x: SoftwareItem) => (
    <Link href={softwareLicencesPath(x.id)} aria-label={recordActionLabel(t('software.licences', { count: x.licenceCount }), x.name)}>
      {t('software.licences', { count: x.licenceCount })}
    </Link>
  );
  const editButton = (x: SoftwareItem) => (
    <Button label={tc('actions.edit')} aria-label={recordActionLabel(tc('actions.edit'), x.name)} size="sm" variant="secondary" onClick={() => setForm({ software: x })} />
  );
  const columns: DataColumn<SoftwareItem>[] = [
    { key: 'vendor', header: t('software.vendor'), renderCell: (x) => x.vendor },
    { key: 'name', header: t('software.name'), renderCell: (x) => <Text type="label">{x.name}</Text> },
    { key: 'licences', header: t('list.title'), renderCell: licenceLink },
    { key: 'status', header: t('software.status'), renderCell: (x) => <ActiveToken isActive={x.isActive} /> },
    { key: 'actions', header: t('software.actions'), renderCell: editButton },
  ];
  const renderCard = (x: SoftwareItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <Text type="label" wordBreak="break-word">{`${x.vendor} ${x.name}`}</Text>
        <ActiveToken isActive={x.isActive} />
      </HStack>
      <HStack gap={2} hAlign="between" vAlign="center">
        {licenceLink(x)}
        {editButton(x)}
      </HStack>
    </VStack>
  );
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.licences.icon}
        actions={<Button label={t('software.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ software: null })} />}
      />
      <ListToolbar
        title={t('software.title')}
        search={list.search}
        searchLabel={t('software.search')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <Selector
            label={t('software.status')}
            placeholder={t('list.all')}
            options={[
              { value: ACTIVE_VALUE, label: tc('status.active') },
              { value: INACTIVE_VALUE, label: tc('status.inactive') },
            ]}
            value={active === undefined ? '' : String(active)}
            onChange={(value) => list.setFilter('active', value)}
          />
        }
      />
      <Card padding={0}>
        {software.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {software.isError && <ErrorState error={software.error} onRetry={() => void software.refetch()} />}
        {software.data && software.data.items.length === 0 && <ListEmptyState
            activeCount={list.activeCount}
            onClear={list.clear}
            message={t('software.empty')}
            action={<Button label={t('software.new')} variant="secondary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ software: null })} />}
          />}
        {software.data && software.data.items.length > 0 && (
          <DataList items={software.data.items} columns={columns} getKey={(x) => x.id} renderCard={renderCard} caption={t('software.caption')} />
        )}
      </Card>
      {software.data && software.data.items.length > 0 && (
        <Pagination page={software.data.page} pageSize={software.data.pageSize} totalItems={software.data.totalCount} onChange={list.setPage} />
      )}
      {form !== null && <SoftwareFormDialog software={form.software} onClose={() => setForm(null)} />}
    </VStack>
  );
}
