using Argon;

namespace Defra.WasteObligations.Testing.Extensions;

public static class VerifyExtensions
{
    public static SettingsTask ScrubTopLevelIdMember(this SettingsTask settingsTask)
    {
        return settingsTask.ScrubInstance<JValue>(x => x.Path == "id");
    }
}
