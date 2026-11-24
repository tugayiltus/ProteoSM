/**
 * @input The service id from the route.
 * @output The service details and its impact: linked assets, open records, changes and open problems.
 * @position `/services/:id`, inside the app shell (IT staff and admins). Records opened here return to it.
 */
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ActiveToken } from '../../shared/page/ActiveToken';
import { SectionCard } from '../../shared/page/SectionCard';
import { assetPath, changePath, problemPath, ROUTES, serviceFilteredPath, ticketPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import type { AssetStatus } from '../assets/api';
import { AssetStatusBadge } from '../assets/AssetStatusBadge';
import type { ChangeStatus } from '../changes/api';
import { ChangeStatusBadge } from '../changes/ChangeBadges';
import type { ProblemStatus } from '../problems/api';
import { ProblemStatusBadge } from '../problems/ProblemBadges';
import type { TicketStatus } from '../tickets/api';
import { TicketStatusBadge } from '../tickets/TicketBadges';
import { serviceTicketsPath } from '../workspace/queuePath';

import { useServiceDetail, useServiceImpact, type ImpactAsset, type ImpactRecord, type ServiceDetail } from './api';
import { ImpactSection, type ImpactRow } from './ImpactSection';

const WIDE_COLUMNS = 3;
const MAIN_SPAN = 2;
const META_SEPARATOR = ' · ';

const assetRow = (asset: ImpactAsset): ImpactRow => ({
  key: asset.id,
  label: `${asset.tag}${META_SEPARATOR}${asset.name}`,
  href: assetPath(asset.id),
  // The impact view sends statuses as plain strings; the values are the asset status enum.
  status: <AssetStatusBadge status={asset.status as AssetStatus} />,
});

const recordRow = (record: ImpactRecord, path: (number: string) => string, status: ImpactRow['status']): ImpactRow => ({
  key: record.number,
  label: `${record.number}${META_SEPARATOR}${record.title}`,
  href: record.canOpen ? path(record.number) : undefined,
  status,
});

function ImpactSections({ service }: { service: ServiceDetail }) {
  const { t } = useTranslation('services');
  const impact = useServiceImpact(service.id);
  if (impact.isPending) {
    return <LoadingState />;
  }
  if (impact.isError) {
    return <ErrorState error={impact.error} onRetry={() => void impact.refetch()} />;
  }
  const { assets, tickets, changes, problems } = impact.data;
  return (
    <VStack gap={4}>
      <ImpactSection
        title={t('detail.sections.assets')}
        total={assets.total}
        rows={assets.items.map(assetRow)}
        serviceName={service.name}
        showAllHref={serviceFilteredPath(ROUTES.assets, service.id)}
      />
      <ImpactSection
        title={t('detail.sections.tickets')}
        total={tickets.total}
        rows={tickets.items.map((x) => recordRow(x, ticketPath, <TicketStatusBadge status={x.status as TicketStatus} />))}
        serviceName={service.name}
        showAllHref={serviceTicketsPath(service.id)}
      />
      <ImpactSection
        title={t('detail.sections.changes')}
        total={changes.total}
        rows={changes.items.map((x) => recordRow(x, changePath, <ChangeStatusBadge status={x.status as ChangeStatus} />))}
        serviceName={service.name}
        showAllHref={serviceFilteredPath(ROUTES.changes, service.id, true)}
      />
      <ImpactSection
        title={t('detail.sections.problems')}
        total={problems.total}
        rows={problems.items.map((x) => recordRow(x, problemPath, <ProblemStatusBadge status={x.status as ProblemStatus} />))}
        serviceName={service.name}
        showAllHref={serviceFilteredPath(ROUTES.problems, service.id, true)}
      />
    </VStack>
  );
}

function ServiceFields({ service }: { service: ServiceDetail }) {
  const { t } = useTranslation('services');
  return (
    <SectionCard>
      <MetadataList>
        <MetadataListItem label={t('detail.fields.ownerTeam')}>
          <Text>{service.ownerTeamName}</Text>
        </MetadataListItem>
        <MetadataListItem label={t('detail.fields.status')}>
          <ActiveToken isActive={service.isActive} />
        </MetadataListItem>
        <MetadataListItem label={t('detail.fields.description')}>
          {service.description === null ? (
            <Text type="supporting">{t('detail.noDescription')}</Text>
          ) : (
            <Text wordBreak="break-word">{service.description}</Text>
          )}
        </MetadataListItem>
      </MetadataList>
    </SectionCard>
  );
}

function ServiceView({ service }: { service: ServiceDetail }) {
  const isPhone = useIsPhone();
  const main = <ImpactSections service={service} />;
  const side = <ServiceFields service={service} />;
  // Phones show the details before the long impact sections; wide screens put them in a side column.
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

/** Keyed by id: switching services starts with fresh queries. */
export function ServiceDetailPage() {
  const { id = '' } = useParams();
  return <ServiceDetailView key={id} id={id} />;
}

function ServiceDetailView({ id }: { id: string }) {
  const { t } = useTranslation('services');
  const service = useServiceDetail(id);
  const backTo = useReturnTo(ROUTES.services);
  // Only from loaded data: a 404 must not show the id typed into the URL as a title.
  useBreadcrumbLabel('serviceDetail', service.data?.name);
  if (service.isPending) {
    return <LoadingState />;
  }
  if (service.isError) {
    return <ErrorState error={service.error} onRetry={() => void service.refetch()} />;
  }
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.services.icon}
        titleOverride={service.data.name}
        actions={<Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />}
      />
      <ServiceView service={service.data} />
    </VStack>
  );
}
