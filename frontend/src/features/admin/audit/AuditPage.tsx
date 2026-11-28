/**
 * @input Nothing; entity type and page come from the URL query.
 * @output The audit event list with Turkish action names and the old and new values per event.
 * @position `/admin/audit`, inside the admin shell.
 */
import { Card } from '@astryxdesign/core/Card';
import { CodeBlock } from '@astryxdesign/core/CodeBlock';
import { Collapsible } from '@astryxdesign/core/Collapsible';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

import { ListStatus } from '../../../shared/feedback/ListStatus';
import { translateOr } from '../../../shared/i18n/dynamic';
import adminTexts from '../../../shared/i18n/locales/tr/admin.json';
import { formatDateTime } from '../../../shared/lib/format';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { REFERENCE_PAGE_SIZE, withCurrentOption } from '../../../shared/reference/referenceOptions';
import { fromLocalInput } from '../../changes/dateTime';
import { DayField } from '../../reports/DayField';
import { useUsers } from '../users/api';

import { useAuditEvents, type AuditEventItem } from './api';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const ENTITY_FILTER_KEY = 'entity';
const ACTION_FILTER_KEY = 'action';
const ACTOR_FILTER_KEY = 'actor';
const FROM_FILTER_KEY = 'from';
const TO_FILTER_KEY = 'to';
const FILTER_KEYS = [ENTITY_FILTER_KEY, ACTION_FILTER_KEY, ACTOR_FILTER_KEY, FROM_FILTER_KEY, TO_FILTER_KEY] as const;
const DAY_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const START_OF_DAY = 'T00:00';
const END_OF_DAY = 'T23:59';
/** The end of the chosen day runs to the last millisecond of its final minute. */
const END_OF_MINUTE_MS = 59_999;
const NO_BOUND = '';
/** Action names are `group.action`; the locale file nests them by group. */
const ACTION_TYPES = Object.entries(adminTexts.auditActions).flatMap(([group, actions]) => Object.keys(actions).map((action) => `${group}.${action}`));
const META_SEPARATOR = ' · ';
const CODE_MAX_HEIGHT = 240;
const JSON_INDENT = 2;
/** Every audited entity type: TranslationCoverageTests guarantees each backend AuditEntityTypes value has a text here. */
const ENTITY_TYPES = Object.keys(adminTexts.entityTypes);

const isDay = (value: string) => DAY_PATTERN.test(value);
const startOfDay = (day: string) => (isDay(day) ? (fromLocalInput(`${day}${START_OF_DAY}`) ?? undefined) : undefined);
const endOfDay = (day: string) => {
  const end = isDay(day) ? fromLocalInput(`${day}${END_OF_DAY}`) : null;
  return end === null ? undefined : new Date(Date.parse(end) + END_OF_MINUTE_MS).toISOString();
};

const actionLabel = (action: string) => translateOr(`admin:auditActions.${action}`, action);
const entityLabel = (entityType: string) => translateOr(`admin:entityTypes.${entityType}`, entityType);

function prettyJson(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }
  try {
    return JSON.stringify(JSON.parse(value), null, JSON_INDENT);
  } catch {
    return value;
  }
}

function AuditDetails({ event }: { event: AuditEventItem }) {
  const { t } = useTranslation('admin');
  const before = prettyJson(event.oldValues);
  const after = prettyJson(event.newValues);
  return (
    <Collapsible defaultIsOpen={false} trigger={<Text type="label">{t('audit.details')}</Text>}>
      <VStack gap={2}>
        {before && <CodeBlock title={t('audit.before')} code={before} language="json" hasLanguageLabel={false} isWrapped maxHeight={CODE_MAX_HEIGHT} />}
        {after && <CodeBlock title={t('audit.after')} code={after} language="json" hasLanguageLabel={false} isWrapped maxHeight={CODE_MAX_HEIGHT} />}
        <Text type="supporting" wordBreak="break-all">{`${t('audit.correlation')}: ${event.correlationId}`}</Text>
      </VStack>
    </Collapsible>
  );
}

export function AuditPage() {
  const { t } = useTranslation('admin');
  const list = useListParams(FILTER_KEYS);
  const entityType = list.filters.entity;
  const { action, actor, from, to } = list.filters;
  // Events of deactivated users stay filterable, so the options cover every user, not only active ones.
  const users = useUsers({ search: '', isActive: undefined, role: undefined, page: 1, pageSize: REFERENCE_PAGE_SIZE });
  const events = useAuditEvents({
    entityType: entityType === '' ? undefined : entityType,
    action: action === '' ? undefined : action,
    actorUserId: actor === '' ? undefined : actor,
    from: startOfDay(from),
    to: endOfDay(to),
    page: list.page,
    pageSize: PAGE_SIZE,
  });

  // A user beyond the loaded page stays visible while the URL filters on it: named by one of its events, else generically.
  const actorOptions = useMemo(() => {
    const options = (users.data?.items ?? []).map((user) => ({ value: user.id, label: `${user.firstName} ${user.lastName}` }));
    const knownName = events.data?.items.find((event) => event.actorUserId === actor)?.actorName ?? t('audit.selectedActor');
    return withCurrentOption(options, actor, knownName);
  }, [users.data, events.data, actor, t]);

  const actorName = (event: AuditEventItem) => event.actorName ?? t('audit.system');

  const columns: DataColumn<AuditEventItem>[] = [
    { key: 'time', header: t('audit.columns.time'), renderCell: (event) => formatDateTime(event.occurredAt) },
    { key: 'actor', header: t('audit.columns.actor'), renderCell: actorName },
    { key: 'action', header: t('audit.columns.action'), renderCell: (event) => <Text type="label">{actionLabel(event.action)}</Text> },
    { key: 'entity', header: t('audit.columns.entity'), renderCell: (event) => entityLabel(event.entityType) },
    { key: 'details', header: t('audit.details'), renderCell: (event) => <AuditDetails event={event} /> },
  ];

  const renderCard = (event: AuditEventItem) => (
    <VStack gap={2}>
      <VStack gap={0}>
        <Text type="label">{actionLabel(event.action)}</Text>
        <Text type="supporting">{[formatDateTime(event.occurredAt), actorName(event), entityLabel(event.entityType)].join(META_SEPARATOR)}</Text>
      </VStack>
      <AuditDetails event={event} />
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminAudit.icon} titleOverride={t('audit.title')} description={t('audit.access')} />
      <ListToolbar
        title={t('audit.title')}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
          <Selector
            label={t('audit.actorFilter')}
            placeholder={t('audit.allActors')}
            hasSearch
            value={actor}
            options={actorOptions}
            onChange={(value) => list.setFilter(ACTOR_FILTER_KEY, value)}
          />
          <Selector
            label={t('audit.actionFilter')}
            placeholder={t('audit.allActions')}
            hasSearch
            value={action}
            options={ACTION_TYPES.map((type) => ({ value: type, label: actionLabel(type) }))}
            onChange={(value) => list.setFilter(ACTION_FILTER_KEY, value)}
          />
          <DayField label={t('audit.fromFilter')} min={NO_BOUND} max={to} value={from} isValid={isDay} onChange={(value) => list.setFilter(FROM_FILTER_KEY, value)} />
          <DayField label={t('audit.toFilter')} min={from} max={NO_BOUND} value={to} isValid={isDay} onChange={(value) => list.setFilter(TO_FILTER_KEY, value)} />
          <Selector
            label={t('audit.entityFilter')}
            placeholder={t('audit.allEntities')}
            hasSearch
            value={entityType}
            options={ENTITY_TYPES.map((type) => ({ value: type, label: entityLabel(type) }))}
            onChange={(value) => list.setFilter(ENTITY_FILTER_KEY, value)}
          />
          </>
        }
      />
      <Card padding={0}>
        <ListStatus isPending={events.isPending} isError={events.isError} error={events.error} count={events.data?.items.length} activeCount={list.activeCount} onClear={list.clear} onRetry={() => void events.refetch()} firstUseMessage={t('audit.firstUse')} rows={SKELETON_ROWS} />
        {events.data && events.data.items.length > 0 && (
          <DataList items={events.data.items} columns={columns} getKey={(event) => event.id} renderCard={renderCard} caption={t('audit.listCaption')} />
        )}
      </Card>
      {events.data && events.data.items.length > 0 && (
        <Pagination page={events.data.page} pageSize={events.data.pageSize} totalItems={events.data.totalCount} onChange={list.setPage} />
      )}
    </VStack>
  );
}
