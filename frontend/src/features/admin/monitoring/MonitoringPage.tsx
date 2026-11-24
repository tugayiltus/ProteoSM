/**
 * @input Nothing; loads the monitoring sources.
 * @output The sources with team, last event and refused requests; create, edit, replace the secret. A new secret shows once.
 * @position `/admin/monitoring`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { InfoDialog } from '../../../shared/feedback/InfoDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { formatDateTime } from '../../../shared/lib/format';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { ActiveToken } from '../../../shared/page/ActiveToken';
import { Icons } from '../../../shared/ui/icons';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';

import { useMonitoringSources, useReplaceMonitoringSecret, type MonitoringSource } from './api';
import { MonitoringSourceFormDialog } from './MonitoringSourceFormDialog';

const EVENTS_PATH = (id: string) => `/api/v1/monitoring/${id}/events`;

interface ShownSecret {
  name: string;
  secret: string;
}

function ReplaceSecretDialog({ source, onSecret, onClose }: { source: MonitoringSource; onSecret: (shown: ShownSecret) => void; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const replace = useReplaceMonitoringSecret();
  return (
    <ConfirmDialog
      title={t('monitoring.replace')}
      message={t('monitoring.replaceMessage', { name: source.name })}
      confirmLabel={t('monitoring.replace')}
      variant="danger"
      isPending={replace.isPending}
      error={replace.error}
      onConfirm={() => replace.mutate({ id: source.id, version: source.version }, { onSuccess: (result) => onSecret({ name: result.source.name, secret: result.secret }) })}
      onCancel={onClose}
    />
  );
}

export function MonitoringPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const sources = useMonitoringSources();
  const [form, setForm] = useState<{ source: MonitoringSource | null } | null>(null);
  const [replacing, setReplacing] = useState<MonitoringSource | null>(null);
  const [shown, setShown] = useState<ShownSecret | null>(null);
  const lastEvent = (x: MonitoringSource) => (x.lastEventAt === null ? t('monitoring.never') : formatDateTime(x.lastEventAt));
  const actions = (x: MonitoringSource) => (
    <HStack gap={2} wrap="wrap">
      <Button label={tc('actions.edit')} aria-label={recordActionLabel(tc('actions.edit'), x.name)} size="sm" variant="secondary" onClick={() => setForm({ source: x })} />
      <Button label={t('monitoring.replace')} aria-label={recordActionLabel(t('monitoring.replace'), x.name)} size="sm" variant="secondary" onClick={() => setReplacing(x)} />
    </HStack>
  );
  const address = (x: MonitoringSource) => (
    <Text type="supporting" wordBreak="break-all">
      {EVENTS_PATH(x.id)}
    </Text>
  );
  const columns: DataColumn<MonitoringSource>[] = [
    {
      key: 'name',
      header: t('monitoring.fields.name'),
      renderCell: (x) => (
        <VStack gap={0}>
          <Text type="label">{x.name}</Text>
          {address(x)}
        </VStack>
      ),
    },
    { key: 'team', header: t('monitoring.fields.team'), renderCell: (x) => x.teamName },
    { key: 'last', header: t('monitoring.lastEvent'), renderCell: lastEvent },
    { key: 'failed', header: t('monitoring.failed'), renderCell: (x) => x.failedCount },
    { key: 'status', header: t('monitoring.status'), renderCell: (x) => <ActiveToken isActive={x.isActive} /> },
    { key: 'actions', header: t('monitoring.actions'), renderCell: actions },
  ];
  const renderCard = (x: MonitoringSource) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <Text type="label" wordBreak="break-word">
          {x.name}
        </Text>
        <ActiveToken isActive={x.isActive} />
      </HStack>
      {address(x)}
      <Text type="supporting">{t('monitoring.cardMeta', { team: x.teamName, last: lastEvent(x), failed: x.failedCount })}</Text>
      {actions(x)}
    </VStack>
  );
  const showSecret = (value: ShownSecret) => {
    setForm(null);
    setReplacing(null);
    setShown(value);
  };
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminMonitoring.icon}
        titleOverride={t('monitoring.title')}
        actions={<Button label={t('monitoring.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ source: null })} />}
      />
      <Card padding={0}>
        {sources.isPending && <LoadingState />}
        {sources.isError && <ErrorState error={sources.error} onRetry={() => void sources.refetch()} />}
        {sources.data && sources.data.length === 0 && <EmptyState message={t('monitoring.empty')} />}
        {sources.data && sources.data.length > 0 && (
          <DataList items={sources.data} columns={columns} getKey={(x) => x.id} renderCard={renderCard} caption={t('monitoring.title')} />
        )}
      </Card>
      {form !== null && <MonitoringSourceFormDialog source={form.source} onSecret={(name, secret) => showSecret({ name, secret })} onClose={() => setForm(null)} />}
      {replacing !== null && <ReplaceSecretDialog source={replacing} onSecret={showSecret} onClose={() => setReplacing(null)} />}
      {shown !== null && (
        <InfoDialog title={t('monitoring.secretTitle', { name: shown.name })} message={t('monitoring.secretMessage')} onClose={() => setShown(null)}>
          <Text type="code" wordBreak="break-all">
            {shown.secret}
          </Text>
        </InfoDialog>
      )}
    </VStack>
  );
}
