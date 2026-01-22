namespace Nocturne.Core.Constants;

/// <summary>
/// Defines external URLs used throughout the Nocturne application.
/// </summary>
public static class UrlConstants
{
    public static class External
    {
        /// <summary>Nocturne website URL (temporary until custom domain is purchased)</summary>
        public const string NocturneWebsite = "https://nightscoutfoundation.org/nocturne";

        /// <summary>Nocturne documentation URL</summary>
        public const string NocturneDocsBase = NocturneWebsite + "/docs";

        /// <summary>Dexcom connector documentation</summary>
        public const string DocsDexcom = NocturneDocsBase + "/connectors/dexcom";

        /// <summary>Libre connector documentation</summary>
        public const string DocsLibre = NocturneDocsBase + "/connectors/libre";

        /// <summary>CareLink connector documentation</summary>
        public const string DocsCareLink = NocturneDocsBase + "/connectors/carelink";

        /// <summary>Nightscout connector documentation</summary>
        public const string DocsNightscout = NocturneDocsBase + "/connectors/nightscout";

        /// <summary>Glooko connector documentation</summary>
        public const string DocsGlooko = NocturneDocsBase + "/connectors/glooko";
    }
}
