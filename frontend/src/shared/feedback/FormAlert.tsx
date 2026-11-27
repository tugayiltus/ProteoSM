/**
 * @input A message, or nothing; optional supporting text under it.
 * @output An error banner that screen readers announce, or nothing.
 * @position Top of a form, for errors that no single field owns.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { VStack } from '@astryxdesign/core/VStack';

/** Astryx `Banner` has no live role, so the wrapper announces it. */
export function FormAlert({ message, description }: { message: string | null | undefined; description?: string }) {
  if (!message) {
    return null;
  }
  return (
    <VStack role="alert">
      <Banner status="error" title={message} description={description} />
    </VStack>
  );
}
