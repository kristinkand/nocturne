using System.Text.Json;
using Nocturne.Connectors.MyLife.Constants;
using Nocturne.Connectors.MyLife.Mappers.Helpers;
using Nocturne.Connectors.MyLife.Models;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.MyLife.Mappers.Handlers;

internal sealed class IndicationTreatmentHandler : IMyLifeTreatmentHandler
{
    public bool CanHandle(MyLifeEvent ev)
    {
        return ev.EventTypeId == MyLifeEventTypeIds.Indication;
    }

    public IEnumerable<Treatment> Handle(MyLifeEvent ev, MyLifeTreatmentContext context)
    {
        var info = MyLifeMapperHelpers.ParseInfo(ev.InformationFromDevice);
        string? key = null;
        if (info != null)
        {
            if (info.Value.TryGetProperty(MyLifeJsonKeys.Key, out var keyElement))
            {
                if (keyElement.ValueKind == JsonValueKind.String)
                {
                    key = keyElement.GetString();
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            if (string.Equals(key, MyLifeJsonKeys.IndicationBasalProfileXChanged, StringComparison.OrdinalIgnoreCase))
            {
                var profileSwitch = MyLifeTreatmentFactory.CreateWithSuffix(
                    ev,
                    MyLifeTreatmentTypes.ProfileSwitch,
                    MyLifeIdSuffixes.ProfileSwitch
                );
                profileSwitch.Notes = ev.InformationFromDevice;

                if (info != null)
                {
                    if (info.Value.TryGetProperty(MyLifeJsonKeys.Parameter0, out var profileElement))
                    {
                        if (profileElement.ValueKind == JsonValueKind.String)
                        {
                            var profile = profileElement.GetString();
                            if (!string.IsNullOrWhiteSpace(profile))
                            {
                                profileSwitch.Profile = profile;
                            }
                        }
                    }
                }

                return new List<Treatment>
                {
                    profileSwitch
                };
            }

            if (string.Equals(key, MyLifeJsonKeys.IndicationBasalProfileChanged, StringComparison.OrdinalIgnoreCase))
            {
                var profileSwitch = MyLifeTreatmentFactory.CreateWithSuffix(
                    ev,
                    MyLifeTreatmentTypes.ProfileSwitch,
                    MyLifeIdSuffixes.ProfileSwitch
                );
                profileSwitch.Notes = ev.InformationFromDevice;

                if (info != null)
                {
                    if (info.Value.TryGetProperty(MyLifeJsonKeys.Parameter1, out var profileElement))
                    {
                        if (profileElement.ValueKind == JsonValueKind.String)
                        {
                            var profile = profileElement.GetString();
                            if (!string.IsNullOrWhiteSpace(profile))
                            {
                                profileSwitch.Profile = profile;
                            }
                        }
                    }
                }

                return new List<Treatment>
                {
                    profileSwitch
                };
            }
        }

        var treatment = MyLifeTreatmentFactory.Create(ev, MyLifeTreatmentTypes.Indication);
        if (MyLifeMapperHelpers.IsBatteryRemovedIndication(info))
        {
            treatment.EventType = MyLifeTreatmentTypes.PumpBatteryChange;
        }

        treatment.Notes = ev.InformationFromDevice;
        return new List<Treatment>
        {
            treatment
        };
    }
}
