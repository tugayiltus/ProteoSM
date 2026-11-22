/**
 * @input A problem status or impact level.
 * @output A colored token with the translated name; color is never the only cue.
 * @position Problem list and problem detail.
 */
import { Token } from '@astryxdesign/core/Token';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import type { Level, ProblemStatus } from './api';

type TokenColor = NonNullable<ComponentProps<typeof Token>['color']>;

const STATUS_COLOR: Record<ProblemStatus, TokenColor> = {
  open: 'blue',
  investigating: 'cyan',
  known_error: 'yellow',
  resolved: 'green',
  cancelled: 'gray',
};

const LEVEL_COLOR: Record<Level, TokenColor> = {
  low: 'green',
  medium: 'yellow',
  high: 'red',
};

export function ProblemStatusBadge({ status }: { status: ProblemStatus }) {
  const { t } = useTranslation('problems');
  return <Token size="sm" color={STATUS_COLOR[status]} label={t(`statuses.${status}`)} />;
}

export function ImpactBadge({ impact }: { impact: Level }) {
  const { t } = useTranslation('problems');
  return <Token size="sm" color={LEVEL_COLOR[impact]} label={`${t('detail.impact')}: ${t(`levels.${impact}`)}`} />;
}
