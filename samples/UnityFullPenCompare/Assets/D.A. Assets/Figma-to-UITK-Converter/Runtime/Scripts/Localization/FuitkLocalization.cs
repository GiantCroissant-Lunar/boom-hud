namespace DA_Assets.FCU
{
    public enum FuitkLocKey
    {
        log_instantiate_game_objects,
        log_gameobject_missing,
        log_sync_helper_missing
    }

    public static class FuitkLocExtensions
    {
        public static string Localize(this FuitkLocKey key, params object[] args) =>
            FuitkConfig.Instance.Localizator.GetLocalizedText(key, null, args);
    }
}
