namespace DA_Assets.DM
{
    public enum DependencyManagerLocKey
    {
        /// <see cref="DA_Assets.DM.DependencyManager"/>
        log_script_change_detected,
        log_package_change_detected,
        log_manual_check_started,
        log_no_status_change,
        log_dependency_items_found,
        log_changes_detected,
        log_dependencies_up_to_date,
        log_manual_removal_protection,
        log_status_changed,
        log_final_desired_defines,
        log_final_defines_named_target,
        log_final_defines_group,
        /// <see cref="DA_Assets.DM.DependencyManager.ScriptSearch"/>
        log_processing_check_type,
        log_processing_type_found,
        log_processing_type_not_found,
        log_assembly_location_failed,
        log_script_found_at,
        log_script_found_but_ignored,
        /// <see cref="DA_Assets.DM.DependencyManager.AsmdefManagement"/>
        log_asmdef_references_updated,
        log_asmdef_created,
        log_asmdef_deleted,
        /// <see cref="DA_Assets.DM.DependencyManager.AsmdefReferences"/>
        log_asmdef_path_not_found,
        log_asmdef_guid_added,
        log_asmdef_guid_removed,
        log_asmdef_plain_name_removed,
        log_asmdef_not_ready,
        /// <see cref="DA_Assets.DM.DependencyItemEditor"/>
        label_current_status,
        label_is_enabled,
        label_script_path,
        label_status,
        label_removed_manually,
        label_check_dependency_now,
        /// <see cref="DA_Assets.DM.DependencyManagerWindow"/>
        label_dependency_manager_window,
        label_dependencies_count,
        label_refresh,
        label_auto_detect,
        label_disabled_manually,
        label_auto_managed,
        label_define,
        label_type,
        label_path
    }

    public static class DependencyManagerLocExtensions
    {
        public static string Localize(this DependencyManagerLocKey key, params object[] args) =>
            DependencyManagerConfig.Instance.Localizator.GetLocalizedText(key, null, args);
    }
}

