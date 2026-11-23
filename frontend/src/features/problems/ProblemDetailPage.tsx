/**
 * @input The problem number from the route.
 * @output The problem: status, impact, owner, service; Belirti, Kök neden, Geçici çözüm, Kalıcı çözüm; linked incidents and changes,
 *   the known error article, history with notes and the work actions.
 * @position `/problems/:number`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { MetadataList } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

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
import { Field } from '../changes/Field';
import { ServiceField } from '../services/ServiceField';

import { isClosed, useProblem, useSetProblemService, type ProblemDetail } from './api';
import { ProblemActions } from './ProblemActions';
import { ProblemArticleBox } from './ProblemArticleBox';
import { ImpactBadge, ProblemStatusBadge } from './ProblemBadges';
import { ProblemLinks } from './ProblemLinks';
import { ProblemTimeline } from './ProblemTimeline';

/** Wide screens: content takes two of three columns, the side column one. */
const WIDE_COLUMNS = 3;
const MAIN_SPAN = 2;

const dateTimeOrNull = (iso: string | null) => (iso === null ? null : formatDateTime(iso));

function ProblemView({ problem }: { problem: ProblemDetail }) {
  const { t } = useTranslation('problems');
  const isPhone = useIsPhone();
  const saveService = useSetProblemService(problem);
  const side = (
    <VStack gap={4}>
      <ProblemActions problem={problem} />
      <SectionCard title={t('detail.sideSection')}>
        <MetadataList>
          <Field label={t('detail.team')} value={problem.ownerTeamName} />
          <Field label={t('detail.owner')} value={problem.ownerName} />
          <ServiceField service={problem.service} canEdit={problem.canWork} save={saveService} />
          <Field label={t('detail.createdAt')} value={formatDateTime(problem.createdAt)} />
          <Field label={t('detail.knownErrorAt')} value={dateTimeOrNull(problem.knownErrorAt)} />
          <Field label={t('detail.resolvedAt')} value={dateTimeOrNull(problem.resolvedAt)} />
        </MetadataList>
      </SectionCard>
      <ProblemArticleBox problem={problem} />
    </VStack>
  );
  const main = (
    <VStack gap={4}>
      <SectionCard title={t('detail.contentSection')}>
        <MetadataList label={{ position: 'top' }}>
          <Field label={t('detail.description')} value={problem.description} />
          <Field label={t('detail.rootCause')} value={problem.rootCause} />
          <Field label={t('detail.workaround')} value={problem.workaround} />
          <Field label={t('detail.resolution')} value={problem.resolution} />
          {problem.cancelReason && <Field label={t('detail.cancelReason')} value={problem.cancelReason} />}
        </MetadataList>
      </SectionCard>
      <ProblemLinks number={problem.number} canEdit={problem.canWork} isClosed={isClosed(problem.status)} />
      <SectionCard title={t('timeline.title')}>
        <ProblemTimeline problem={problem} />
      </SectionCard>
    </VStack>
  );
  return (
    <VStack gap={4}>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <Text type="supporting">{problem.number}</Text>
        <ProblemStatusBadge status={problem.status} />
        <ImpactBadge impact={problem.impact} />
      </HStack>
      {/* Phones show the actions before the long content; wide screens put them in a side column. */}
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

/** Keyed by number: switching problems starts with fresh dialogs and errors. */
export function ProblemDetailPage() {
  const { number = '' } = useParams();
  return <ProblemDetailView key={number} number={number} />;
}

function ProblemDetailView({ number }: { number: string }) {
  const { t } = useTranslation('problems');
  const problem = useProblem(number);
  const backTo = useReturnTo(ROUTES.problems);
  // Only from loaded data: a 404 must not show the number typed into the URL as a title.
  useBreadcrumbLabel('problemDetail', problem.data?.number);
  if (problem.isPending) {
    return <LoadingState />;
  }

  if (problem.isError) {
    return <ErrorState error={problem.error} onRetry={() => void problem.refetch()} />;
  }

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.problems.icon}
        titleOverride={problem.data.title}
        actions={<Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />}
      />
      <ProblemView problem={problem.data} />
    </VStack>
  );
}
