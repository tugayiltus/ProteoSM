/**
 * @input The licence id from the route.
 * @output Licence details, seats in use, earlier seats, and for admins the key actions.
 * @position `/licences/:id`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate, formatDateTime } from '../../shared/lib/format';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { SectionCard } from '../../shared/page/SectionCard';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { ADMIN_ONLY, useSession } from '../auth/session';

import { useLicence, useLicenceAssignments, useReleaseLicence, type LicenceDetail } from './api';
import { AssignLicenceDialog } from './AssignLicenceDialog';
import { LicenceFormDialog } from './LicenceFormDialog';
import { LicenceKeyDialog, RevealKeyDialog } from './LicenceKeyDialogs';
import { LicenceSeats } from './LicenceTokens';

const WIDE_COLUMNS = 3;
const MAIN_SPAN = 2;
const PAGE_SIZE = 20;
const FIRST_PAGE = 1;

function SeatList({ licence, released }: { licence: LicenceDetail; released: boolean }) {
  const { t } = useTranslation('licences');
  const [page, setPage] = useState(FIRST_PAGE);
  const seats = useLicenceAssignments(licence.id, released, page, PAGE_SIZE);
  const release = useReleaseLicence();
  const title = released ? t('detail.released') : t('detail.assignments');
  return (
    <VStack gap={3}>
      <FormAlert message={release.error ? describeError(release.error) : null} />
      {seats.isPending && <LoadingState />}
      {seats.isError && <ErrorState error={seats.error} onRetry={() => void seats.refetch()} />}
      {seats.data && seats.data.items.length === 0 && <Text type="supporting">{t('detail.none')}</Text>}
      {seats.data && seats.data.items.length > 0 && (
        <List hasDividers aria-label={title}>
          {seats.data.items.map((seat) => {
            const target = seat.userName ?? seat.assetTag ?? '';
            return (
              <ListItem
                key={seat.id}
                label={target}
                description={t(released ? 'detail.releasedAt' : 'detail.assignedAt', { date: formatDateTime(seat.releasedAt ?? seat.assignedAt) })}
                endContent={
                  released ? undefined : (
                    <HStack gap={2} vAlign="center" wrap="wrap">
                      {!seat.targetAvailable && <Token label={t('detail.unavailable')} color="orange" />}
                      <Button
                        label={t('detail.release')}
                        aria-label={recordActionLabel(t('detail.release'), target)}
                        size="sm"
                        variant="ghost"
                        isLoading={release.isPending && release.variables?.assignmentId === seat.id}
                        isDisabled={release.isPending}
                        onClick={() => release.mutate({ id: licence.id, assignmentId: seat.id })}
                      />
                    </HStack>
                  )
                }
              />
            );
          })}
        </List>
      )}
      {seats.data && seats.data.totalCount > PAGE_SIZE && <Pagination page={page} pageSize={PAGE_SIZE} totalItems={seats.data.totalCount} onChange={setPage} />}
    </VStack>
  );
}

function KeySection({ licence }: { licence: LicenceDetail }) {
  const { t } = useTranslation('licences');
  const [dialog, setDialog] = useState<'set' | 'reveal' | null>(null);
  return (
    <SectionCard title={t('key.title')}>
      <VStack gap={3}>
        <Text type="supporting">{licence.hasKey ? t('key.stored') : t('key.none')}</Text>
        <HStack gap={2} wrap="wrap">
          {licence.hasKey && <Button label={t('key.show')} variant="secondary" onClick={() => setDialog('reveal')} />}
          <Button label={licence.hasKey ? t('key.change') : t('key.add')} variant="secondary" onClick={() => setDialog('set')} />
        </HStack>
      </VStack>
      {dialog === 'set' && <LicenceKeyDialog licence={licence} onClose={() => setDialog(null)} />}
      {dialog === 'reveal' && <RevealKeyDialog licenceId={licence.id} onClose={() => setDialog(null)} />}
    </SectionCard>
  );
}

function LicenceFields({ licence }: { licence: LicenceDetail }) {
  const { t } = useTranslation('licences');
  return (
    <SectionCard>
      <MetadataList>
        <MetadataListItem label={t('columns.software')}>
          <Text>{`${licence.vendor} ${licence.softwareName}`}</Text>
        </MetadataListItem>
        <MetadataListItem label={t('columns.kind')}>
          <Text>{t(`kinds.${licence.kind}`)}</Text>
        </MetadataListItem>
        <MetadataListItem label={t('columns.seats')}>
          <LicenceSeats licence={licence} />
        </MetadataListItem>
        <MetadataListItem label={t('columns.team')}>
          <Text>{licence.ownerTeamName}</Text>
        </MetadataListItem>
        <MetadataListItem label={t('columns.end')}>
          <Text>{licence.expiresOn === null ? t('noEnd') : formatDate(licence.expiresOn)}</Text>
        </MetadataListItem>
        {licence.notes !== null && (
          <MetadataListItem label={t('form.notes')}>
            <Text wordBreak="break-word">{licence.notes}</Text>
          </MetadataListItem>
        )}
      </MetadataList>
    </SectionCard>
  );
}

function LicenceView({ licence }: { licence: LicenceDetail }) {
  const { t } = useTranslation('licences');
  const isPhone = useIsPhone();
  const isAdmin = ADMIN_ONLY.includes(useSession().role);
  const side = (
    <VStack gap={4}>
      <LicenceFields licence={licence} />
      {isAdmin && <KeySection licence={licence} />}
    </VStack>
  );
  const main = (
    <VStack gap={4}>
      <SectionCard title={t('detail.assignments')}>
        <SeatList licence={licence} released={false} />
      </SectionCard>
      <SectionCard title={t('detail.released')}>
        <SeatList licence={licence} released />
      </SectionCard>
    </VStack>
  );
  return isPhone ? (
    <VStack gap={4}>
      {side}
      {main}
    </VStack>
  ) : (
    <Grid columns={WIDE_COLUMNS} gap={4} align="start">
      <GridSpan columns={MAIN_SPAN}>{main}</GridSpan>
      {side}
    </Grid>
  );
}

/** Keyed by id: switching licences starts with fresh queries. */
export function LicenceDetailPage() {
  const { id = '' } = useParams();
  return <LicenceDetailView key={id} id={id} />;
}

function LicenceDetailView({ id }: { id: string }) {
  const { t } = useTranslation('licences');
  const { t: tc } = useTranslation();
  const licence = useLicence(id);
  const backTo = useReturnTo(ROUTES.licences);
  const [dialog, setDialog] = useState<'edit' | 'assign' | null>(null);
  useBreadcrumbLabel('licenceDetail', licence.data?.name);
  if (licence.isPending) {
    return <LoadingState />;
  }
  if (licence.isError) {
    return <ErrorState error={licence.error} onRetry={() => void licence.refetch()} />;
  }
  const canAssign = !licence.data.isExpired && licence.data.used < licence.data.quantity;
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.licences.icon}
        titleOverride={licence.data.name}
        actions={
          <HStack gap={2} wrap="wrap">
            <Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />
            <Button label={tc('actions.edit')} variant="secondary" onClick={() => setDialog('edit')} />
            {canAssign && <Button label={t('detail.assign')} variant="primary" onClick={() => setDialog('assign')} />}
          </HStack>
        }
      />
      <LicenceView licence={licence.data} />
      {dialog === 'edit' && <LicenceFormDialog licence={licence.data} onClose={() => setDialog(null)} />}
      {dialog === 'assign' && <AssignLicenceDialog licence={licence.data} onClose={() => setDialog(null)} />}
    </VStack>
  );
}
