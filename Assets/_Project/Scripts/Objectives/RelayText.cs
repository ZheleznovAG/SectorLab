namespace SectorLab.Objectives
{
    // One wording for relay effects across HUD cards and notices.
    public static class RelayText
    {
        public static string Effect(RelayConfig config) => config.kind == RelayKind.Score
            ? $"+{config.scorePerSecond:0.#} очк/с" : $"+{config.healingPerSecond:0.#} HP/с";
    }
}
