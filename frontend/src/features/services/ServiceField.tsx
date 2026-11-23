/**
 * @input A record's service, whether the viewer may change it, the record's set-service mutation and, for tickets,
 *   the tags of the linked assets.
 * @output A "Hizmet" metadata row: the service link (with "Pasif" when inactive) or "Hizmet yok", plus the
 *   choose, change and clear actions.
 * @position Inside the side `MetadataList` of the ticket, change and problem detail pages (IT staff and admins only).
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import type { UseMutationResult } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { ActiveToken } from '../../shared/page/ActiveToken';
import { servicePath } from '../../shared/routes';

import { useSuggestedService, type ServiceRef } from './api';
import { ServicePicker } from './ServicePicker';

const NO_TAGS: readonly string[] = [];

/** The record's set-service call: a service id sets it, `null` clears it. */
export type SetServiceMutation = UseMutationResult<unknown, Error, string | null>;

export interface ServiceFieldProps {
  service: ServiceRef | null;
  canEdit: boolean;
  save: SetServiceMutation;
  /** Tags of the record's linked assets. When they carry exactly one active service, the picker pre-selects it. */
  suggestionTags?: readonly string[];
}

export function ServiceField({ service, canEdit, save, suggestionTags = NO_TAGS }: ServiceFieldProps) {
  const { t } = useTranslation('services');
  const location = useLocation();
  const [isPicking, setIsPicking] = useState(false);
  const suggestion = useSuggestedService(suggestionTags, isPicking && service === null);

  const openPicker = () => {
    save.reset();
    setIsPicking(true);
  };
  const closePicker = () => {
    save.reset();
    setIsPicking(false);
  };

  return (
    <MetadataListItem label={t('field.label')}>
      <VStack gap={2}>
        {service === null ? (
          <Text type="supporting">{t('field.none')}</Text>
        ) : (
          <HStack gap={2} vAlign="center" wrap="wrap">
            <StateLink href={servicePath(service.id)} state={linkState(location)}>
              {service.name}
            </StateLink>
            {!service.isActive && <ActiveToken isActive={false} />}
          </HStack>
        )}
        {canEdit && (
          <>
            {!isPicking && <FormAlert message={save.error ? describeError(save.error) : null} />}
            <HStack gap={2} wrap="wrap">
              <Button label={service === null ? t('field.choose') : t('field.change')} variant="secondary" size="sm" onClick={openPicker} />
              {service !== null && (
                <Button
                  label={t('field.clear')}
                  variant="ghost"
                  size="sm"
                  isLoading={save.isPending && save.variables === null}
                  isDisabled={save.isPending}
                  onClick={() => save.mutate(null)}
                />
              )}
            </HStack>
          </>
        )}
      </VStack>
      {isPicking && (
        <ServicePicker
          current={service}
          suggestion={suggestion}
          isPending={save.isPending}
          error={save.error}
          onSave={(serviceId) => save.mutate(serviceId, { onSuccess: () => setIsPicking(false) })}
          onClose={closePicker}
        />
      )}
    </MetadataListItem>
  );
}
