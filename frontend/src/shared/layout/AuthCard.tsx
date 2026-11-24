/**
 * @input The screen's content.
 * @output A centered card under the product name, with no menu and no breadcrumb.
 * @position Sign-in, password links, forced password change and the 404 page.
 */
import { Card } from '@astryxdesign/core/Card';
import { Center } from '@astryxdesign/core/Center';
import { VStack } from '@astryxdesign/core/VStack';
import type { PropsWithChildren } from 'react';

import { BrandLogo } from '../brand/BrandLogo';

/** Readable width for a short form. */
const CARD_MAX_WIDTH = 440;

export function AuthCard({ children }: PropsWithChildren) {
  return (
    // No app shell here, so this screen supplies the main landmark itself.
    <main>
      <Center minHeight="100dvh" padding={4}>
      <VStack gap={4} hAlign="center" width="100%" maxWidth={CARD_MAX_WIDTH}>
        <BrandLogo />
        <Card padding={6} width="100%">
          <VStack gap={3}>{children}</VStack>
        </Card>
        </VStack>
      </Center>
    </main>
  );
}
