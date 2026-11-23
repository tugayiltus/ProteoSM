/**
 * @input Service list filters or a service id.
 * @output Queries for the IT services list, one service and its impact view.
 * @position Data layer of `/services` and `/services/:id` (IT staff and admins). Reuses the admin service queries,
 *   so admin writes also refresh these pages.
 */
import type { components } from '../../shared/api/schema';

export {
  useAdminServices as useServices,
  useServiceDetail,
  useServiceImpact,
  type ImpactAsset,
  type ServiceDetail,
  type ServiceListItem,
  type ServiceListParams,
} from '../admin/services/api';

export type ServiceImpact = components['schemas']['ServiceImpact'];
export type ImpactRecord = components['schemas']['ImpactRecord'];
