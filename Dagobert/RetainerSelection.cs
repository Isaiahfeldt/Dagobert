using System.Collections.Generic;
using System.Linq;

namespace Dagobert;

internal static class RetainerSelection
{
    internal const string None = "__ALL_DISABLED__";

    internal static bool IsEnabled(HashSet<string> selection, string name) =>
        !selection.Contains(None) && (selection.Count == 0 || selection.Contains(name));

    // Always return a new set so editing a scheduled selection cannot mutate manual settings.
    internal static HashSet<string> WithEnabled(HashSet<string> selection, IEnumerable<string> knownNames, string name, bool enabled)
    {
        var result = selection.Count == 0 ? knownNames.ToHashSet()
            : selection.Where(value => value != None).ToHashSet();
        if (enabled) result.Add(name);
        else result.Remove(name);
        if (result.Count == 0) result.Add(None);
        return result;
    }
}
