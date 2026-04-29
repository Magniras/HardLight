using System.Collections.Generic;

namespace Content.Client.Administration.UI.Bwoink
{
    /// <summary>
    ///     Client-side static registry mapping a normalized keyword (the lowercased
    ///     alphanumeric form produced by <see cref="BwoinkPanel"/>) to the list of
    ///     candidate entity prototype IDs that match it.
    ///     The <c>ahelpitemmenu</c> console command consults this registry to show
    ///     a chooser when the admin clicks a highlighted item mention.
    /// </summary>
    public static class AhelpItemMentionRegistry
    {
        private static readonly Dictionary<string, List<string>> Entries = new();

        public static void Set(string normalizedKeyword, IReadOnlyList<string> prototypeIds)
        {
            if (string.IsNullOrEmpty(normalizedKeyword))
                return;

            Entries[normalizedKeyword] = new List<string>(prototypeIds);
        }

        public static bool TryGet(string normalizedKeyword, out IReadOnlyList<string> prototypeIds)
        {
            if (Entries.TryGetValue(normalizedKeyword, out var list))
            {
                prototypeIds = list;
                return true;
            }

            prototypeIds = System.Array.Empty<string>();
            return false;
        }

        public static void Clear() => Entries.Clear();
    }
}
