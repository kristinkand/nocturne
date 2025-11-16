using System;
using System.Collections.Generic;
using Nocturne.Connectors.Core.Models;

namespace Nocturne.Aspire.Host.Constants
{
    public static class ConnectorConfigurationConstants
    {
        public static readonly IReadOnlyDictionary<ConnectSource, string> TypeMapping =
            new Dictionary<ConnectSource, string>
            {
                {
                    ConnectSource.Dexcom,
                    "Nocturne.Connectors.Dexcom.Models.DexcomConnectorConfiguration, Nocturne.Connectors.Dexcom"
                },
                {
                    ConnectSource.Glooko,
                    "Nocturne.Connectors.Glooko.Models.GlookoConnectorConfiguration, Nocturne.Connectors.Glooko"
                },
                {
                    ConnectSource.CareLink,
                    "Nocturne.Connectors.MiniMed.Models.CareLinkConnectorConfiguration, Nocturne.Connectors.MiniMed"
                },
                {
                    ConnectSource.LibreLinkUp,
                    "Nocturne.Connectors.FreeStyle.Models.LibreLinkUpConnectorConfiguration, Nocturne.Connectors.FreeStyle"
                },
                {
                    ConnectSource.Nightscout,
                    "Nocturne.Connectors.Nightscout.Models.NightscoutConnectorConfiguration, Nocturne.Connectors.Nightscout"
                },
                {
                    ConnectSource.MyFitnessPal,
                    "Nocturne.Connectors.MyFitnessPal.Models.MyFitnessPalConnectorConfiguration, Nocturne.Connectors.MyFitnessPal"
                },
            };

        public static class Glooko
        {
            public const string Email = "GlookoEmail";
            public const string Password = "GlookoPassword";
            public const string Server = "GlookoServer";
            public const string TimezoneOffset = "GlookoTimezoneOffset";
        }

        public static class Dexcom
        {
            public const string Username = "DexcomUsername";
            public const string Password = "DexcomPassword";
            public const string Region = "DexcomRegion";
            public const string Server = "DexcomServer";
        }

        public static class LibreLinkUp
        {
            public const string Username = "LibreUsername";
            public const string Password = "LibrePassword";
            public const string Region = "LibreRegion";
            public const string PatientId = "LibrePatientId";
        }

        public static class CareLink
        {
            public const string Username = "CarelinkUsername";
            public const string Password = "CarelinkPassword";
            public const string Region = "CarelinkRegion";
            public const string CountryCode = "CarelinkCountryCode";
        }
    }
}
