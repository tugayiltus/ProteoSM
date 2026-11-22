/**
 * @input A problem.
 * @output The Bilinen hata makalesi box: the linked article, or "Makale oluştur" while the problem is a known error or resolved.
 * @position Problem detail page, side column.
 */
import { Button } from '@astryxdesign/core/Button';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { articlePath } from '../../shared/routes';

import { useProblemAction, type ProblemDetail, type ProblemStatus } from './api';

/** The server creates the draft article only in these states (ADR-0019 article flow applies afterwards). */
const ARTICLE_STATUSES: readonly ProblemStatus[] = ['known_error', 'resolved'];

export function ProblemArticleBox({ problem }: { problem: ProblemDetail }) {
  const { t } = useTranslation('problems');
  const location = useLocation();
  const create = useProblemAction(problem);
  const canCreate = problem.canWork && problem.articleId === null && ARTICLE_STATUSES.includes(problem.status);
  return (
    <SectionCard title={t('article.title')}>
      <VStack gap={3}>
        <FormAlert message={create.error ? describeError(create.error) : null} />
        {problem.articleId === null ? (
          <Text type="supporting">{canCreate ? t('article.none') : t('article.hint')}</Text>
        ) : (
          <StateLink href={articlePath(problem.articleId)} state={linkState(location)}>
            {problem.articleTitle ?? problem.articleId}
          </StateLink>
        )}
        {canCreate && (
          <Button label={t('article.create')} variant="secondary" isLoading={create.isPending} onClick={() => create.mutate({ kind: 'article' })} />
        )}
      </VStack>
    </SectionCard>
  );
}
