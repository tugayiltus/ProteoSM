/**
 * @input The record's name and active state, the edit and toggle callbacks, and optional extra buttons.
 * @output Düzenle and Pasifleştir/Aktifleştir (or the given toggle labels) buttons whose names include the record, e.g. "Düzenle Muhasebe".
 * @position Last column (or card footer) of admin lists.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { recordActionLabel } from '../ui/recordActionLabel';

export interface RecordActionsProps {
  name: string;
  isActive: boolean;
  onEdit: () => void;
  onToggle: () => void;
  /** Buttons before Düzenle, such as "Üyeler". */
  extra?: ReactNode;
  /** Toggle text when a page uses its own words, such as "Pasife al". */
  deactivateLabel?: string;
  reactivateLabel?: string;
}

export function RecordActions({ name, isActive, onEdit, onToggle, extra, deactivateLabel, reactivateLabel }: RecordActionsProps) {
  const { t } = useTranslation();
  const toggleLabel = isActive ? (deactivateLabel ?? t('actions.deactivate')) : (reactivateLabel ?? t('actions.reactivate'));
  return (
    <HStack gap={2} wrap="wrap">
      {extra}
      <Button label={t('actions.edit')} aria-label={recordActionLabel(t('actions.edit'), name)} size="sm" variant="secondary" onClick={onEdit} />
      <Button label={toggleLabel} aria-label={recordActionLabel(toggleLabel, name)} size="sm" variant={isActive ? 'ghost' : 'secondary'} onClick={onToggle} />
    </HStack>
  );
}
