using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ModNook
{
    /// <summary>
    /// Finds every loaded BepInEx plugin that has settings, and groups those settings the way the
    /// plugin author already grouped them in its config file.
    ///
    /// Discovery is one-directional on purpose: nothing here references another mod's assembly, and
    /// no other mod references this one. A plugin gets a settings page by doing what it already had
    /// to do - call <c>Config.Bind</c>.
    /// </summary>
    internal static class ModCatalog
    {
        /// <summary>One plugin's worth of settings.</summary>
        internal sealed class ModEntry
        {
            internal string Guid;
            internal string Name;
            internal string Version;
            internal ConfigFile Config;
            internal List<SectionEntry> Sections = new List<SectionEntry>();

            internal int SettingCount => Sections.Sum(s => s.Entries.Count);
        }

        /// <summary>A config file's <c>[Section]</c>, kept in the order the author bound them.</summary>
        internal sealed class SectionEntry
        {
            internal string Name;
            internal List<ConfigEntryBase> Entries = new List<ConfigEntryBase>();
        }

        internal static List<ModEntry> Discover()
        {
            var mods = new List<ModEntry>();

            foreach (var info in Chainloader.PluginInfos.Values)
            {
                try
                {
                    var mod = Read(info);
                    if (mod != null)
                    {
                        mods.Add(mod);
                    }
                }
                catch (Exception e)
                {
                    // One malformed plugin must not cost the player the whole list.
                    ModNookPlugin.Log.LogWarning(
                        $"Skipping {info?.Metadata?.Name ?? "an unnamed plugin"}: {e.Message}");
                }
            }

            mods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return mods;
        }

        private static ModEntry Read(PluginInfo info)
        {
            var config = info?.Instance?.Config;
            if (config == null)
            {
                return null;
            }

            // Obsolete in newer BepInEx, but the replacement it names does not exist on the 5.4.23
            // ConfigFile this game ships with. Keep the call that works.
#pragma warning disable CS0618
            var entries = config.GetConfigEntries();
#pragma warning restore CS0618

            if (entries == null || entries.Length == 0)
            {
                // A plugin with no settings has nothing to show. Listing it would only be a dead end.
                return null;
            }

            var mod = new ModEntry
            {
                Guid = info.Metadata?.GUID ?? "unknown",
                Name = info.Metadata?.Name ?? info.Metadata?.GUID ?? "Unnamed mod",
                Version = info.Metadata?.Version?.ToString() ?? string.Empty,
                Config = config,
            };

            foreach (var entry in entries)
            {
                if (entry == null || IsHidden(entry))
                {
                    continue;
                }

                var sectionName = entry.Definition?.Section ?? "General";
                var section = mod.Sections.FirstOrDefault(
                    s => string.Equals(s.Name, sectionName, StringComparison.Ordinal));

                if (section == null)
                {
                    section = new SectionEntry { Name = sectionName };
                    mod.Sections.Add(section);
                }

                section.Entries.Add(entry);
            }

            return mod.SettingCount == 0 ? null : mod;
        }

        /// <summary>
        /// A mod can keep a setting out of the panel without depending on it, by tagging the
        /// description. Same trick as the rest of the tag vocabulary - a plain string that costs
        /// nothing when no panel is installed.
        /// </summary>
        private static bool IsHidden(ConfigEntryBase entry)
        {
            return Tags.Has(entry, "Hidden");
        }
    }
}
