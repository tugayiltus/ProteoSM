namespace Proteone.Domain.Audit;

/// <summary>
/// Audit action codes. Every constant needs a Turkish label under "auditActions" in
/// frontend/src/shared/i18n/locales/tr/admin.json (checked by TranslationCoverageTests).
/// </summary>
public static class AuditActions
{
    public const string FirstAdminCreated = "setup.first_admin_created";

    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string LockedOut = "auth.locked_out";
    public const string PasswordChanged = "auth.password_changed";
    public const string AuthSettingsUpdated = "auth.settings_updated";
    public const string AuthSettingsTested = "auth.settings_tested";
    public const string LoginMethodChanged = "auth.method_changed";
    public const string DirectorySignIn = "auth.directory_sign_in";

    public const string UserCreated = "users.created";
    public const string UserCreatedFromDirectory = "users.created_from_directory";
    public const string UserUpdated = "users.updated";
    public const string UserRoleChanged = "users.role_changed";
    public const string UserDeactivated = "users.deactivated";
    public const string UserReactivated = "users.reactivated";
    public const string UserPasswordReset = "users.password_reset";
    public const string UserActivationLinkSent = "users.activation_link_sent";
    public const string UserPasswordResetLinkSent = "users.password_reset_link_sent";
    public const string UserPasswordSetByLink = "users.password_set_by_link";

    public const string DepartmentCreated = "departments.created";
    public const string DepartmentUpdated = "departments.updated";
    public const string DepartmentDeactivated = "departments.deactivated";
    public const string DepartmentReactivated = "departments.reactivated";

    public const string LocationCreated = "locations.created";
    public const string LocationUpdated = "locations.updated";
    public const string LocationDeactivated = "locations.deactivated";
    public const string LocationReactivated = "locations.reactivated";

    public const string TeamCreated = "teams.created";
    public const string TeamUpdated = "teams.updated";
    public const string TeamDeactivated = "teams.deactivated";
    public const string TeamReactivated = "teams.reactivated";
    public const string TeamMemberAdded = "teams.member_added";
    public const string TeamMemberRemoved = "teams.member_removed";

    public const string AssetCreated = "assets.created";
    public const string AssetTagRenumbered = "assets.tag_renumbered";
    public const string AssetUpdated = "assets.updated";
    public const string AssetAssigned = "assets.assigned";
    public const string AssetReturned = "assets.returned";
    public const string AssetTransferred = "assets.transferred";
    public const string AssetMaintenanceStarted = "assets.maintenance_started";
    public const string AssetMaintenanceEnded = "assets.maintenance_ended";
    public const string AssetLost = "assets.lost";
    public const string AssetFound = "assets.found";
    public const string AssetRetired = "assets.retired";
    public const string AssetsImported = "assets.imported";

    public const string CategoryCreated = "categories.created";
    public const string CategoryUpdated = "categories.updated";
    public const string CategoryDeactivated = "categories.deactivated";
    public const string CategoryReactivated = "categories.reactivated";

    public const string BrandCreated = "brands.created";
    public const string BrandRenamed = "brands.renamed";
    public const string BrandDeactivated = "brands.deactivated";
    public const string BrandReactivated = "brands.reactivated";
    public const string AssetModelCreated = "asset_models.created";
    public const string AssetModelRenamed = "asset_models.renamed";
    public const string AssetModelDeactivated = "asset_models.deactivated";
    public const string AssetModelReactivated = "asset_models.reactivated";
    public const string RequestTypeCreated = "request_types.created";
    public const string RequestTypeUpdated = "request_types.updated";
    public const string RequestTypeDeactivated = "request_types.deactivated";
    public const string RequestTypeReactivated = "request_types.reactivated";

    public const string TicketCreated = "tickets.created";
    public const string TicketAssigned = "tickets.assigned";
    public const string TicketTransferred = "tickets.transferred";
    public const string TicketStatusChanged = "tickets.status_changed";
    public const string TicketPriorityChanged = "tickets.priority_changed";
    public const string TicketCompleted = "tickets.completed";
    public const string TicketCancelled = "tickets.cancelled";
    public const string TicketAssetLinked = "tickets.asset_linked";
    public const string TicketAssetUnlinked = "tickets.asset_unlinked";
    public const string TaskCreated = "tasks.created";
    public const string TaskAssigned = "tasks.assigned";
    public const string TaskTransferred = "tasks.transferred";
    public const string TaskStatusChanged = "tasks.status_changed";
    public const string ProblemCreated = "problems.created";
    public const string ProblemUpdated = "problems.updated";
    public const string ProblemStatusChanged = "problems.status_changed";
    public const string ProblemTransferred = "problems.transferred";
    public const string ProblemOwnerChanged = "problems.owner_changed";
    public const string ProblemArticleCreated = "problems.article_created";
    public const string ProblemTicketLinked = "problems.ticket_linked";
    public const string ProblemTicketUnlinked = "problems.ticket_unlinked";
    public const string ProblemChangeLinked = "problems.change_linked";
    public const string ProblemChangeUnlinked = "problems.change_unlinked";
    public const string TicketProblemLinked = "tickets.problem_linked";
    public const string TicketProblemUnlinked = "tickets.problem_unlinked";
    public const string ChangeProblemLinked = "changes.problem_linked";
    public const string ChangeProblemUnlinked = "changes.problem_unlinked";
    public const string ServiceCreated = "services.created";
    public const string ServiceUpdated = "services.updated";
    public const string ServiceActivated = "services.activated";
    public const string ServiceDeactivated = "services.deactivated";
    public const string ServiceAssetLinked = "services.asset_linked";
    public const string ServiceAssetUnlinked = "services.asset_unlinked";
    public const string TicketServiceSet = "tickets.service_set";
    public const string ChangeServiceSet = "changes.service_set";
    public const string ProblemServiceSet = "problems.service_set";
    public const string ChangeCreated = "changes.created";
    public const string ChangeUpdated = "changes.updated";
    public const string ChangeStatusChanged = "changes.status_changed";
    public const string ChangeApprovalDecided = "changes.approval_decided";
    public const string ChangeApprovalVoided = "changes.approval_voided";
    public const string ChangeClosed = "changes.closed";
    public const string ChangeTicketLinked = "changes.ticket_linked";
    public const string ChangeTicketUnlinked = "changes.ticket_unlinked";
    public const string TicketChangeLinked = "tickets.change_linked";
    public const string TicketChangeUnlinked = "tickets.change_unlinked";
    public const string RelatedRequestCreated = "tickets.related_request_created";
    public const string ApprovalApproved = "approvals.approved";
    public const string ApprovalRejected = "approvals.rejected";
    public const string ApprovalReassigned = "approvals.reassigned";
    public const string ApprovalVoided = "approvals.voided";

    public const string EmailTemplateUpdated = "email_templates.updated";

    public const string EmailSettingsUpdated = "email_settings.updated";
    public const string EmailSettingsTested = "email_settings.tested";
    public const string EmailSettingsActivated = "email_settings.activated";
    public const string EmailSettingsEnvironmentRestored = "email_settings.environment_restored";

    public const string EmailDeliveryRetried = "email_delivery.retried";

    public const string SlaCalendarUpdated = "sla.calendar_updated";
    public const string SlaHolidayAdded = "sla.holiday_added";
    public const string SlaHolidayRemoved = "sla.holiday_removed";
    public const string SlaPolicyVersioned = "sla.policy_versioned";

    public const string KnowledgeCreated = "knowledge.created";
    public const string KnowledgeUpdated = "knowledge.updated";
    public const string KnowledgePublished = "knowledge.published";
    public const string KnowledgeArchived = "knowledge.archived";
    public const string KnowledgeRestored = "knowledge.restored";
    public const string KnowledgeVisibilityChanged = "knowledge.visibility_changed";
    public const string TicketArticleLinked = "tickets.article_linked";
    public const string TicketArticleUnlinked = "tickets.article_unlinked";
    public const string RatingSubmitted = "ratings.submitted";
    public const string RatingUpdated = "ratings.updated";

    /// <summary>Written by the migration that replaced the former product name (ADR-0037).</summary>
    public const string ProductNameReplaced = "audit.product_name_replaced";

    public const string SystemSettingsUpdated = "system_settings.updated";
    public const string AnnouncementCreated = "announcements.created";
    public const string AnnouncementUpdated = "announcements.updated";

    public const string MonitoringSourceCreated = "monitoring.created";
    public const string MonitoringSourceUpdated = "monitoring.updated";
    public const string MonitoringSecretReplaced = "monitoring.secret_replaced";
    public const string MonitoringLimitReached = "monitoring.limit_reached";

    public const string ReleaseCreated = "releases.created";
    public const string ReleaseUpdated = "releases.updated";
    public const string ReleaseStatusChanged = "releases.status_changed";
    public const string ReleaseChangeAdded = "releases.change_added";
    public const string ReleaseChangeRemoved = "releases.change_removed";

    public const string SoftwareCreated = "software.created";
    public const string SoftwareUpdated = "software.updated";
    public const string LicenceCreated = "licences.created";
    public const string LicenceUpdated = "licences.updated";
    public const string LicenceAssigned = "licences.assigned";
    public const string LicenceReleased = "licences.released";
    public const string LicenceKeySet = "licences.key_set";
    public const string LicenceKeyCleared = "licences.key_cleared";
    public const string LicenceKeyShown = "licences.key_shown";

    public const string ReportExported = "reports.exported";
    public const string ReportExportRequested = "reports.export_requested";
    public const string ReportExportDownloaded = "reports.export_downloaded";
}
