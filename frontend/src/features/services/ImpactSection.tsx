/**
 * @input A section title, its total, its rows, and optionally a filtered-list link and a note.
 * @output One impact section: title, total, the capped rows and "Tümünü göster" when the total is larger.
 *   A row the viewer cannot open shows its number, title and status without a link (ADR-0033).
 * @position Service detail page, one per section (assets, open records, changes, open problems).
 */
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';

export interface ImpactRow {
  key: string;
  label: string;
  /** Absent when the viewer cannot open the record. */
  href?: string;
  status: ReactNode;
}

export interface ImpactSectionProps {
  title: string;
  total: number;
  rows: readonly ImpactRow[];
  /** Service name; a record opened here shows it in its breadcrumb and returns to this page. */
  serviceName: string;
  /** Filtered list with every row; shown only when the total is larger than the rows. */
  showAllHref?: string;
  /** Shown under the rows when the total is larger and no list can show the rest. */
  cappedNote?: string;
}

export function ImpactSection({ title, total, rows, serviceName, showAllHref, cappedNote }: ImpactSectionProps) {
  const { t } = useTranslation('services');
  const location = useLocation();
  const isCapped = total > rows.length;
  return (
    <SectionCard
      title={title}
      actions={
        <HStack gap={3} vAlign="center">
          <Text type="supporting">{t('detail.total', { count: total })}</Text>
          {isCapped && showAllHref !== undefined && <Link href={showAllHref}>{t('detail.showAll')}</Link>}
        </HStack>
      }
    >
      {rows.length === 0 && <Text type="supporting">{t('detail.empty')}</Text>}
      {rows.length > 0 && (
        <List hasDividers density="compact" aria-label={title}>
          {rows.map((row) => (
            <ListItem
              key={row.key}
              label={
                row.href === undefined ? (
                  row.label
                ) : (
                  <StateLink href={row.href} state={linkState(location, serviceName)}>
                    {row.label}
                  </StateLink>
                )
              }
              description={row.href === undefined ? t('detail.notOpenable') : undefined}
              endContent={row.status}
            />
          ))}
        </List>
      )}
      {isCapped && showAllHref === undefined && cappedNote !== undefined && <Text type="supporting">{cappedNote}</Text>}
    </SectionCard>
  );
}
