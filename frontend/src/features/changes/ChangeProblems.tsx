/**
 * @input A change number.
 * @output The problems this change fixes, each with its status and a link. Read-only: links are managed on the problem.
 * @position Change detail page (full view), main column.
 */
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { ErrorState } from '../../shared/feedback/States';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { problemPath } from '../../shared/routes';
import { useChangeProblems, type ProblemChangeSideItem } from '../problems/api';
import { ProblemStatusBadge } from '../problems/ProblemBadges';

const META_SEPARATOR = ' · ';

function Row({ item }: { item: ProblemChangeSideItem }) {
  const location = useLocation();
  return (
    <ListItem
      label={
        <StateLink href={problemPath(item.number)} state={linkState(location)}>
          {`${item.number}${META_SEPARATOR}${item.title}`}
        </StateLink>
      }
      endContent={<ProblemStatusBadge status={item.status} />}
    />
  );
}

export function ChangeProblems({ number }: { number: string }) {
  const { t } = useTranslation('changes');
  const problems = useChangeProblems(number);
  const items = problems.data ?? [];
  return (
    <SectionCard title={t('problems.title')}>
      {problems.isError && <ErrorState error={problems.error} onRetry={() => void problems.refetch()} />}
      {items.length === 0 ? (
        problems.data && <Text type="supporting">{t('problems.empty')}</Text>
      ) : (
        <List hasDividers density="compact" aria-label={t('problems.title')}>
          {items.map((item) => (
            <Row key={item.number} item={item} />
          ))}
        </List>
      )}
    </SectionCard>
  );
}
