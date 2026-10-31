using System;

namespace Dagobert;

internal static class BellSelection
{
    internal const uint StandardBellDataId = 2000401;

    // Excel nouns may be lowercase while the displayed object name is title case.
    internal static bool Matches(string name, string localizedName, uint dataId) =>
        dataId == StandardBellDataId
        || (!string.IsNullOrWhiteSpace(localizedName)
            && name.Trim().Equals(localizedName.Trim(), StringComparison.OrdinalIgnoreCase))
        || name.Trim().Equals("リテイナーベル", StringComparison.Ordinal);

    internal static float InteractionRange(bool housing, bool inn) => housing ? 6.5f : inn ? 4.75f : 4.6f;

    internal static bool WithinRange(float distance, bool targetable, bool housing, bool inn) =>
        targetable && float.IsFinite(distance) && distance >= 0 && distance <= InteractionRange(housing, inn);
}
