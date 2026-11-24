/**
 * @input The release number from the route.
 * @output Release fields, actions, changes and history.
 * @position `/releases/:number`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { MetadataList } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { SectionCard } from '../../shared/page/SectionCard';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { Field, NodeField } from '../changes/Field';
import { WindowText } from '../changes/WindowText';

import { isOpenRelease, useCancelRelease, useRelease, useReleaseStep, useReleaseTimeline, type ReleaseDetail, type ReleaseStep, type ReleaseTimelineItem } from './api';
import { ReleaseChanges } from './ReleaseChanges';
import { ReleaseFormDialog } from './ReleaseFormDialog';
import { ReleaseStatusBadge } from './ReleaseStatusBadge';

const WIDE_COLUMNS = 3;
const MAIN_SPAN = 2;
const META_SEPARATOR = ' · ';
const REASON_MAX_LENGTH = 500;
const TEXT_KEYS = ['reason', 'number'] as const;

function CancelDialog({ release, onClose }: { release: ReleaseDetail; onClose: () => void }) {
  const { t } = useTranslation('releases');
  const cancel = useCancelRelease();
  const [reason, setReason] = useState('');
  const clean = reason.trim();
  return (
    <ActionDialog
      title={t('actions.cancel')}
      confirmLabel={t('actions.cancel')}
      isPending={cancel.isPending}
      isConfirmDisabled={clean === '' || clean.length > REASON_MAX_LENGTH}
      error={cancel.error}
      onConfirm={() => cancel.mutate({ number: release.number, version: release.version, reason: clean }, { onSuccess: onClose })}
      onClose={onClose}
    >
      <Text>{t('actions.cancelMessage')}</Text>
      <TextArea label={t('actions.reason')} value={reason} onChange={setReason} />
    </ActionDialog>
  );
}

function ReleaseActions({ release }: { release: ReleaseDetail }) {
  const { t } = useTranslation('releases');
  const { t: tc } = useTranslation();
  const step = useReleaseStep();
  const [dialog, setDialog] = useState<'edit' | 'cancel' | null>(null);
  if (!release.canWork || !isOpenRelease(release.status)) {
    return null;
  }
  const next: ReleaseStep = release.status === 'planned' ? 'start' : 'complete';
  return (
    <SectionCard title={t('actions.title')}>
      <VStack gap={3}>
        <FormAlert message={step.error ? describeError(step.error) : null} />
        <HStack gap={2} wrap="wrap">
          <Button label={t(`actions.${next}`)} variant="primary" isLoading={step.isPending} onClick={() => step.mutate({ number: release.number, version: release.version, step: next })} />
          <Button label={tc('actions.edit')} variant="secondary" isDisabled={step.isPending} onClick={() => setDialog('edit')} />
          <Button label={t('actions.cancel')} variant="destructive" isDisabled={step.isPending} onClick={() => setDialog('cancel')} />
        </HStack>
      </VStack>
      {dialog === 'edit' && <ReleaseFormDialog release={release} onSaved={() => setDialog(null)} onClose={() => setDialog(null)} />}
      {dialog === 'cancel' && <CancelDialog release={release} onClose={() => setDialog(null)} />}
    </SectionCard>
  );
}

function textOf(event: ReleaseTimelineItem): string | null {
  const data = (typeof event.data === 'object' && event.data !== null ? event.data : {}) as Record<string, unknown>;
  const value = TEXT_KEYS.map((key) => data[key]).find((item) => typeof item === 'string');
  return typeof value === 'string' ? value : null;
}

function Timeline({ number }: { number: string }) {
  const { t } = useTranslation('releases');
  const timeline = useReleaseTimeline(number);
  if (timeline.isPending) {
    return <LoadingState />;
  }
  if (timeline.isError) {
    return <ErrorState error={timeline.error} onRetry={() => void timeline.refetch()} />;
  }
  return (
    <List listStyle="decimal" density="compact" aria-label={t('timeline.title')}>
      {timeline.data.map((event) => (
        <ListItem
          key={event.id}
          label={t(`timeline.events.${event.kind}`)}
          description={[textOf(event), formatDateTime(event.occurredAt), event.actorName].filter(Boolean).join(META_SEPARATOR)}
        />
      ))}
    </List>
  );
}

function ReleaseView({ release }: { release: ReleaseDetail }) {
  const { t } = useTranslation('releases');
  const isPhone = useIsPhone();
  const dateOrNull = (value: string | null) => (value === null ? null : formatDateTime(value));
  const side = (
    <VStack gap={4}>
      <ReleaseActions release={release} />
      <SectionCard>
        <MetadataList>
          <Field label={t('columns.team')} value={release.ownerTeamName} />
          <Field label={t('form.owner')} value={release.ownerName} />
          <NodeField label={t('columns.window')}>
            <WindowText start={release.windowStart} end={release.windowEnd} />
          </NodeField>
          <Field label={t('detail.createdAt')} value={formatDateTime(release.createdAt)} />
          <Field label={t('detail.startedAt')} value={dateOrNull(release.startedAt)} />
          <Field label={t('detail.closedAt')} value={dateOrNull(release.closedAt)} />
        </MetadataList>
      </SectionCard>
    </VStack>
  );
  const main = (
    <VStack gap={4}>
      <SectionCard title={t('detail.content')}>
        <MetadataList label={{ position: 'top' }}>
          <Field label={t('form.description')} value={release.description} />
          <Field label={t('form.notes')} value={release.notes} />
          {release.cancelReason !== null && <Field label={t('actions.reason')} value={release.cancelReason} />}
        </MetadataList>
      </SectionCard>
      <ReleaseChanges release={release} />
      <SectionCard title={t('timeline.title')}>
        <Timeline number={release.number} />
      </SectionCard>
    </VStack>
  );
  return (
    <VStack gap={4}>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <Text type="supporting">{release.number}</Text>
        <ReleaseStatusBadge status={release.status} />
      </HStack>
      {isPhone ? (
        <VStack gap={4}>
          {side}
          {main}
        </VStack>
      ) : (
        <Grid columns={WIDE_COLUMNS} gap={4} align="start">
          <GridSpan columns={MAIN_SPAN}>{main}</GridSpan>
          {side}
        </Grid>
      )}
    </VStack>
  );
}

/** Keyed by number: switching releases starts with fresh dialogs and errors. */
export function ReleaseDetailPage() {
  const { number = '' } = useParams();
  return <ReleaseDetailView key={number} number={number} />;
}

function ReleaseDetailView({ number }: { number: string }) {
  const { t } = useTranslation('releases');
  const release = useRelease(number);
  const backTo = useReturnTo(ROUTES.releases);
  useBreadcrumbLabel('releaseDetail', release.data?.number);
  if (release.isPending) {
    return <LoadingState />;
  }
  if (release.isError) {
    return <ErrorState error={release.error} onRetry={() => void release.refetch()} />;
  }
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.releases.icon}
        titleOverride={release.data.title}
        actions={<Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />}
      />
      <ReleaseView release={release.data} />
    </VStack>
  );
}
