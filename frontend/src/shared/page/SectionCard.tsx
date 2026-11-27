/**
 * @input An optional title and actions, the padding, and the content.
 * @output A card with a level-2 heading row above its content. A titled card is a region named by its title.
 * @position Grouped content inside a page: details, comments, history, settings groups.
 */
import { Card } from '@astryxdesign/core/Card';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useId, type ReactNode } from 'react';

const DEFAULT_PADDING = 5;
const COMPACT_PADDING = 3;

export interface SectionCardProps {
  title?: string;
  actions?: ReactNode;
  /** 0 lets a list or table reach the card edges; the heading keeps its own padding. */
  padding?: 0 | typeof DEFAULT_PADDING;
  /** An empty section: the title, this message and the actions share one row, so an empty list takes no height. */
  emptyMessage?: string;
  children?: ReactNode;
}

export function SectionCard({ title, actions, padding = DEFAULT_PADDING, emptyMessage, children }: SectionCardProps) {
  const titleId = useId();
  if (emptyMessage !== undefined) {
    return (
      <Card padding={COMPACT_PADDING}>
        <VStack gap={2} role={title === undefined ? undefined : 'region'} aria-labelledby={title === undefined ? undefined : titleId}>
          <HStack gap={3} hAlign="between" vAlign="center" wrap="wrap">
            <HStack gap={3} vAlign="center" wrap="wrap">
              {title !== undefined && (
                <Heading level={2} id={titleId}>
                  {title}
                </Heading>
              )}
              <Text type="supporting">{emptyMessage}</Text>
            </HStack>
            {actions}
          </HStack>
          {children}
        </VStack>
      </Card>
    );
  }

  const header = (title !== undefined || actions !== undefined) && (
    <HStack gap={2} hAlign="between" vAlign="center" wrap="wrap" padding={padding === 0 ? DEFAULT_PADDING : 0}>
      {title !== undefined && (
        <Heading level={2} id={titleId}>
          {title}
        </Heading>
      )}
      {actions}
    </HStack>
  );
  return (
    <Card padding={padding}>
      <VStack gap={padding === 0 ? 0 : 3} role={title === undefined ? undefined : 'region'} aria-labelledby={title === undefined ? undefined : titleId}>
        {header}
        {children}
      </VStack>
    </Card>
  );
}
