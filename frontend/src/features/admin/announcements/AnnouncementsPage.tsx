/**
 * @input Nothing; loads the announcements.
 * @output The announcements with state and period; create and edit. An end date takes one down.
 * @position `/admin/announcements`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { formatDateTime } from '../../../shared/lib/format';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { Icons } from '../../../shared/ui/icons';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';

import { AnnouncementFormDialog } from './AnnouncementFormDialog';
import { useAnnouncements, type Announcement } from './api';

const STATE_COLORS = { scheduled: 'blue', active: 'green', ended: 'gray' } as const;

/** Judged against the time the list was fetched, so rendering stays pure. */
function stateOf(item: Announcement, fetchedAt: number): keyof typeof STATE_COLORS {
  if (Date.parse(item.startsAt) > fetchedAt) {
    return 'scheduled';
  }
  return item.endsAt !== null && Date.parse(item.endsAt) <= fetchedAt ? 'ended' : 'active';
}

export function AnnouncementsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const list = useAnnouncements();
  const [form, setForm] = useState<{ announcement: Announcement | null } | null>(null);
  const end = (x: Announcement) => (x.endsAt === null ? t('announcements.noEnd') : formatDateTime(x.endsAt));
  const stateToken = (x: Announcement) => {
    const state = stateOf(x, list.dataUpdatedAt);
    return <Token size="sm" color={STATE_COLORS[state]} label={t(`announcements.states.${state}`)} />;
  };
  const editButton = (x: Announcement) => (
    <Button label={tc('actions.edit')} aria-label={recordActionLabel(tc('actions.edit'), x.title)} size="sm" variant="secondary" onClick={() => setForm({ announcement: x })} />
  );
  const columns: DataColumn<Announcement>[] = [
    { key: 'title', header: t('announcements.fields.title'), renderCell: (x) => <Text type="label">{x.title}</Text> },
    { key: 'level', header: t('announcements.fields.level'), renderCell: (x) => t(`announcements.levels.${x.level}`) },
    { key: 'start', header: t('announcements.fields.startsAt'), renderCell: (x) => formatDateTime(x.startsAt) },
    { key: 'end', header: t('announcements.fields.endsAt'), renderCell: end },
    { key: 'status', header: t('announcements.status'), renderCell: stateToken },
    { key: 'actions', header: t('announcements.actions'), renderCell: editButton },
  ];
  const renderCard = (x: Announcement) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <Text type="label" wordBreak="break-word">
          {x.title}
        </Text>
        {stateToken(x)}
      </HStack>
      <Text type="supporting">{t('announcements.cardMeta', { start: formatDateTime(x.startsAt), end: end(x) })}</Text>
      <HStack>{editButton(x)}</HStack>
    </VStack>
  );
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminAnnouncements.icon}
        titleOverride={t('announcements.title')}
        actions={<Button label={t('announcements.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ announcement: null })} />}
      />
      <Card padding={0}>
        {list.isPending && <LoadingState />}
        {list.isError && <ErrorState error={list.error} onRetry={() => void list.refetch()} />}
        {list.data?.length === 0 && <EmptyState message={t('announcements.empty')} />}
        {list.data !== undefined && list.data.length > 0 && (
          <DataList items={list.data} columns={columns} getKey={(x) => x.id} renderCard={renderCard} caption={t('announcements.title')} />
        )}
      </Card>
      {form !== null && <AnnouncementFormDialog announcement={form.announcement} onClose={() => setForm(null)} />}
    </VStack>
  );
}
