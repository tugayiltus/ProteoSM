/**
 * @input A release status.
 * @output A colored token with the translated name; color is never the only cue.
 * @position Release list, release detail and the change detail.
 */
import { Token } from '@astryxdesign/core/Token';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import type { ReleaseStatus } from './api';

const STATUS_COLOR: Record<ReleaseStatus, NonNullable<ComponentProps<typeof Token>['color']>> = {
  planned: 'blue',
  in_progress: 'cyan',
  completed: 'green',
  cancelled: 'gray',
};

export function ReleaseStatusBadge({ status }: { status: ReleaseStatus }) {
  const { t } = useTranslation('releases');
  return <Token size="sm" color={STATUS_COLOR[status]} label={t(`statuses.${status}`)} />;
}
