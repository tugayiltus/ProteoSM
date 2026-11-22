/**
 * @input A problem.
 * @output The work actions its state allows: one primary step first, the other steps secondary, cancel apart.
 * @position Problem detail page, first in the side column.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useIsFetching } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { SectionCard } from '../../shared/page/SectionCard';

import { isClosed, problemKeys, useProblemAction, type ProblemActionKind, type ProblemDetail, type ProblemStatus } from './api';
import { ProblemActionDialog, type ProblemDialogKind } from './ProblemActionDialog';
import { ProblemFormDialog } from './ProblemFormDialog';

type DirectKind = Extract<ProblemActionKind, 'start' | 'known-error'>;
type StepKind = DirectKind | Extract<ProblemDialogKind, 'resolve'>;

const DIRECT_KINDS: readonly StepKind[] = ['start', 'known-error'];
/** Work actions that do not move the state; offered while the problem is not closed. */
const SIDE_DIALOGS: readonly ProblemDialogKind[] = ['transfer', 'owner'];

/** Mirrors the domain state machine; the first step is the primary one (the server still decides). */
const STEPS: Record<ProblemStatus, readonly StepKind[]> = {
  open: ['start', 'known-error', 'resolve'],
  investigating: ['known-error', 'resolve'],
  known_error: ['resolve'],
  resolved: [],
  cancelled: [],
};

const isDirect = (kind: StepKind): kind is DirectKind => DIRECT_KINDS.includes(kind);
const hasText = (value: string | null) => value !== null && value.trim() !== '';

export function ProblemActions({ problem }: { problem: ProblemDetail }) {
  const { t } = useTranslation('problems');
  const direct = useProblemAction(problem);
  const [dialog, setDialog] = useState<ProblemDialogKind | null>(null);
  const [editing, setEditing] = useState(false);
  // Every action sends the version shown here; wait for a refetch so a dialog does not open with an old version.
  const busy = useIsFetching({ queryKey: problemKeys.detail(problem.number) }) > 0 || direct.isPending;
  if (!problem.canWork || isClosed(problem.status)) {
    return null;
  }

  const steps = STEPS[problem.status];
  const knownErrorReady = hasText(problem.rootCause) && hasText(problem.workaround);
  const openDialog = (kind: ProblemDialogKind) => {
    direct.reset();
    setDialog(kind);
  };
  return (
    <SectionCard title={t('actions.title')}>
      <VStack gap={3}>
        <FormAlert message={direct.error ? describeError(direct.error) : null} />
        <HStack gap={2} wrap="wrap">
          {steps.map((kind, index) => {
            const variant = index === 0 ? 'primary' : 'secondary';
            if (!isDirect(kind)) {
              return <Button key={kind} label={t(`actions.${kind}`)} variant={variant} isDisabled={busy} onClick={() => openDialog(kind)} />;
            }

            return (
              <Button
                key={kind}
                label={t(`actions.${kind}`)}
                variant={variant}
                isDisabled={busy || (kind === 'known-error' && !knownErrorReady)}
                isLoading={direct.isPending && direct.variables?.kind === kind}
                onClick={() => direct.mutate({ kind })}
              />
            );
          })}
          <Button label={t('actions.edit')} variant="secondary" isDisabled={busy} onClick={() => setEditing(true)} />
          {SIDE_DIALOGS.map((kind) => (
            <Button key={kind} label={t(`actions.${kind}`)} variant="secondary" isDisabled={busy} onClick={() => openDialog(kind)} />
          ))}
          <Button label={t('actions.cancel')} variant="destructive" isDisabled={busy} onClick={() => openDialog('cancel')} />
        </HStack>
        {steps.includes('known-error') && !knownErrorReady && <Text type="supporting">{t('actions.knownErrorHint')}</Text>}
      </VStack>
      {dialog !== null && <ProblemActionDialog key={dialog} problem={problem} kind={dialog} onClose={() => setDialog(null)} />}
      {editing && <ProblemFormDialog problem={problem} onSaved={() => setEditing(false)} onClose={() => setEditing(false)} />}
    </SectionCard>
  );
}
