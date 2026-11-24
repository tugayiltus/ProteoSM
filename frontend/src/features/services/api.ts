/**
 * @input Service list filters or a service id.
 * @output Queries for the IT services list, one service, its impact view, and the service suggestion for a record.
 * @position Data layer of `/services` and `/services/:id` (IT staff and admins). Reuses the admin service queries,
 *   so admin writes also refresh these pages.
 */
import { useQueries } from '@tanstack/react-query';

import type { components } from '../../shared/api/schema';
import { assetServicesQuery } from '../assets/api';

import type { ServiceRef } from './serviceApi';

export {
  useAdminServices as useServices,
  useServiceDetail,
  useServiceFilterOptions,
  useServiceImpact,
  type ImpactAsset,
  type ServiceDetail,
  type ServiceListItem,
  type ServiceListParams,
  type ServiceRef,
} from './serviceApi';

export type ServiceImpact = components['schemas']['ServiceImpact'];
export type ImpactRecord = components['schemas']['ImpactRecord'];

/**
 * The one active service that the given assets carry, or `null` when they carry none or more than one.
 * A suggestion only: the caller pre-selects it, and the user still saves it.
 */
export function useSuggestedService(assetTags: readonly string[], enabled: boolean): ServiceRef | null {
  return useQueries({
    queries: assetTags.map((tag) => ({ ...assetServicesQuery(tag), enabled })),
    combine: (results) => {
      if (results.some((result) => result.data === undefined)) {
        return null;
      }
      const active = new Map(results.flatMap((result) => result.data ?? []).filter((service) => service.isActive).map((service) => [service.id, service]));
      return active.size === 1 ? ([...active.values()][0] ?? null) : null;
    },
  });
}
