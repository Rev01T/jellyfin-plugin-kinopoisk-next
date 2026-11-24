using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Photos of people met in the cast lists of titles.
    /// </summary>
    /// <remarks>
    /// The card of a title already contains the photo of every person in it, so Jellyfin can get the
    /// person's image without a separate API request per person. The free token has only a small number
    /// of requests a day. The cache lives in memory: after a restart the photos are known again as soon
    /// as the title is refreshed.
    /// </remarks>
    public static class PersonPhotoCache
    {
        private const int MaxEntries = 50000;
        private static readonly Regex Size = new Regex(@"/\d+x\d+$", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<int, string> Photos = new ConcurrentDictionary<int, string>();

        public static void Remember(IEnumerable<PersonInfo> people)
        {
            if (people is null)
                return;

            foreach (var person in people)
            {
                if (person is null
                    || string.IsNullOrWhiteSpace(person.ImageUrl)
                    || !person.TryGetProviderId(Constants.ProviderId, out var idValue)
                    || !int.TryParse(idValue, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id))
                    continue;

                if (Photos.Count >= MaxEntries)
                    Photos.Clear();
                Photos[id] = person.ImageUrl;
            }
        }

        /// <summary>The photo in the original size, if the person was seen in a cast list.</summary>
        public static bool TryGetOriginal(int personId, out string url)
        {
            if (Photos.TryGetValue(personId, out var photo) && !string.IsNullOrWhiteSpace(photo))
            {
                // The cast lists have 300x300 thumbnails; the person card has the original image under /orig.
                url = Size.Replace(photo, "/orig");
                return true;
            }

            url = null;
            return false;
        }
    }
}
