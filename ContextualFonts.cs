using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Last.Message;
using Last.Management;
using Last.Data.Parameters;
using UnityEngine;
using UnityEngine.UI;

namespace FFPRContextualFonts
{
    [BepInPlugin("ffpr.contextualfonts", "FFPR Contextual Fonts", "1.0.10")]
    public sealed class Plugin : BasePlugin
    {
        internal static ManualLogSource Logger;
        internal static ConfigEntry<string> DialogueFont;
        internal static ConfigEntry<string> SpeakerFont;
        internal static ConfigEntry<string> DescriptionFont;
        internal static ConfigEntry<string> BattleAbilitiesFont;
        internal static ConfigEntry<string> BattleDescriptionsFont;
        internal static ConfigEntry<string> BattleAnnouncementsFont;
        internal static ConfigEntry<string> LocationFont;
        internal static ConfigEntry<string> MenuExplanationsFont;
        internal static ConfigEntry<string> BestiaryListFont;
        internal static ConfigEntry<string> BestiaryEntryHeaderFont;
        internal static ConfigEntry<string> BestiaryEntryDetailsFont;
        internal static ConfigEntry<string> MenuCommandsFont;
        internal static ConfigEntry<string> BattleCommandsFont;
        internal static ConfigEntry<string> StatusBattleCommandsFont;
        internal static ConfigEntry<string> TitleMenuCommandsFont;
        internal static ConfigEntry<string> TitleSubmenuCommandsFont;
        internal static ConfigEntry<string> TitlePromptsFont;
        internal static ConfigEntry<string> TitleNewGameFont;
        internal static ConfigEntry<string> TitleLoadGameFont;
        internal static ConfigEntry<string> TitleConfigurationFont;
        internal static ConfigEntry<string> CampCharacterNamesFont;
        internal static ConfigEntry<string> CampCharacterJobsFont;
        internal static ConfigEntry<string> MusicPlayerTextFont, GalleryTextFont, BestiaryCommandsFont;
        internal static readonly Dictionary<string, ConfigEntry<string>> ExpandedFonts = new Dictionary<string, ConfigEntry<string>>();
        internal static ConfigEntry<bool> FitOtherNumericFields;
        internal static readonly Dictionary<string, ConfigEntry<string>> UniversalFonts = new Dictionary<string, ConfigEntry<string>>();
        internal static readonly Dictionary<string, ConfigEntry<string>> GameFonts = new Dictionary<string, ConfigEntry<string>>();
        internal static int GameNumber;
        internal static ConfigEntry<bool> HierarchyEnabled;
        internal static ConfigEntry<string> HierarchyRoots;
        internal static ConfigEntry<int> HierarchyMaxRoots;
        internal static ConfigEntry<int> HierarchyMaxTexts;
        internal static ConfigEntry<bool> TextProbeEnabled;
        internal static ConfigEntry<string> TextProbeTerms;
        internal static ConfigEntry<int> TextProbeMaxMatches;
        internal static ConfigEntry<int> TextProbeMaxNearby;
        internal static ConfigEntry<int> TextProbeAncestorDepth;
        private static int DetectGame()
        {
            string n = (Application.productName ?? "").ToUpperInvariant().Trim();
            // Match longer roman numerals before shorter ones.
            string[] suffix = { " VI", " V", " IV", " III", " II", " I" };
            int[] games = { 6, 5, 4, 3, 2, 1 };
            for (int i = 0; i < suffix.Length; i++)
                if (n.EndsWith(suffix[i], StringComparison.Ordinal)) return games[i];
            // The first Pixel Remaster is named simply "FINAL FANTASY" without " I".
            // A zero game number disables all FF1-scoped hierarchy matches.
            if (n == "FINAL FANTASY") return 1;
            return 0;
        }
        private void BindUniversal(string name, string parent)
        {
            UniversalFonts[name] = Config.Bind("Fonts", name, "Inherit",
                "Inherit (" + parent + "), Default, Classic, Modernized.");
        }
        private void BindGame(int game, string name, string parent)
        {
            GameFonts[game + ":" + name] = Config.Bind("Fonts.FF" + game, name, "Inherit",
                "Inherit (" + parent + "), Default, Classic, Modernized. Only applies to FF" + game + ".");
        }

        internal static ConfigEntry<string> FitMode;
        internal static ConfigEntry<string> DiagnosticLevel;
        internal static bool VerboseDiagnostics => DiagnosticLevel != null && string.Equals(DiagnosticLevel.Value.Trim(), "Verbose", StringComparison.OrdinalIgnoreCase);
        internal static bool BasicDiagnostics => DiagnosticLevel != null && (VerboseDiagnostics || string.Equals(DiagnosticLevel.Value.Trim(), "Basic", StringComparison.OrdinalIgnoreCase));
        internal static ConfigEntry<bool> FitBattleItemQuantities;
        internal static ConfigEntry<bool> FitCampLevels;
        internal static ConfigEntry<bool> FitItemQuantities;
        internal static ConfigEntry<bool> FitCampMagicLevels;
        internal static ConfigEntry<bool> FitBattleMagicLevels;
        internal static ConfigEntry<int> FitMinimumSize;
        private Harmony harmony;
        private ConfigFile DiagnosticsConfig;

        private void BindExpanded(string name, string parent)
        {
            ExpandedFonts[name] = Config.Bind("Fonts", name, "Inherit",
                "Inherit (" + parent + "), Default, Classic, Modernized.");
        }

        public override void Load()
        {
            Logger = Log;
            DiagnosticsConfig = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "ffpr.contextualfonts.diagnostics.cfg"), true);
            DialogueFont = Config.Bind("Fonts", "Dialogue", "Modernized", "NPC dialogue font: Default, Classic, Modernized. Default leaves the game/KupoUI font alone.");
            SpeakerFont = Config.Bind("Fonts", "Speaker", "Default", "Speaker-name font: Default, Classic, Modernized.");
            DescriptionFont = Config.Bind("Fonts", "Description", "Default", "Item, magic, and equipment description font: Default, Classic, Modernized. Default follows game/KupoUI.");
            BattleAbilitiesFont = Config.Bind("Fonts", "BattleAbilities", "Default", "Battle spell-selection names: Default, Classic, Modernized.");
            BattleDescriptionsFont = Config.Bind("Fonts", "BattleDescriptions", "Default", "Battle spell and item descriptions: Default, Classic, Modernized.");
            BattleAnnouncementsFont = Config.Bind("Fonts", "BattleAnnouncements", "Default", "Battle action announcements: Default, Classic, Modernized.");
            LocationFont = Config.Bind("Fonts", "Location", "Default", "Shared area-name system-message text (including Bestiary map hover labels): Default, Classic, Modernized. May also display other system messages.");
            MenuExplanationsFont = Config.Bind("Fonts", "MenuExplanations", "Default", "Config and Boost explanatory descriptions: Default, Classic, Modernized.");
            BestiaryListFont = Config.Bind("Fonts", "BestiaryList", "Default", "Bestiary monster-list IDs and names: Default, Classic, Modernized.");
            BestiaryEntryHeaderFont = Config.Bind("Fonts", "BestiaryEntryHeader", "Default", "Bestiary detail-page monster ID and name: Default, Classic, Modernized.");
            BestiaryEntryDetailsFont = Config.Bind("Fonts", "BestiaryEntryDetails", "Default", "Bestiary detail-page stats, weaknesses, drops, habitats and resistances: Default, Classic, Modernized.");
            MenuCommandsFont = Config.Bind("Fonts", "MenuCommands", "Default", "Main camp-menu command labels: Default, Classic, Modernized.");
            BattleCommandsFont = Config.Bind("Fonts", "BattleCommands", "Default", "Battle command selection labels: Default, Classic, Modernized.");
            StatusBattleCommandsFont = Config.Bind("Fonts", "StatusBattleCommands", "Inherit", "Status-screen battle command labels: Inherit, Default, Classic, Modernized. Inherit follows BattleCommands.");
            TitleMenuCommandsFont = Config.Bind("Fonts", "TitleMenuCommands", "Default", "Title main-menu commands: Default, Classic, Modernized.");
            TitleSubmenuCommandsFont = Config.Bind("Fonts", "TitleSubmenuCommands", "Default", "Title Extras, Options and Configuration submenu commands: Default, Classic, Modernized.");
            TitlePromptsFont = Config.Bind("Fonts", "TitlePrompts", "Default", "PRESS ANY BUTTON title prompt: Default, Classic, Modernized.");
            TitleNewGameFont = Config.Bind("Fonts", "TitleNewGame", "Default", "New Game character creation labels: Default, Classic, Modernized.");
            TitleLoadGameFont = Config.Bind("Fonts", "TitleLoadGame", "Default", "Load Game save-slot labels and names: Default, Classic, Modernized.");
            TitleConfigurationFont = Config.Bind("Fonts", "TitleConfiguration", "Default", "Title configuration setting names: Default, Classic, Modernized.");
            CampCharacterNamesFont = Config.Bind("Fonts", "CampCharacterNames", "Default", "Main camp party display character names only: Default, Classic, Modernized.");
            CampCharacterJobsFont = Config.Bind("Fonts", "CampCharacterJobs", "Default", "Main camp party display job names only: Default, Classic, Modernized.");






















            MusicPlayerTextFont = Config.Bind("Fonts", "MusicPlayerText", "Inherit", "Music Player text: Inherit (TitleSubmenuCommands), Default, Classic, Modernized. Applies to SoundUi.");
            GalleryTextFont = Config.Bind("Fonts", "GalleryText", "Inherit", "Gallery text: Inherit (TitleSubmenuCommands), Default, Classic, Modernized. Applies to GalleryUi and GalleryDetailsUi.");
            BestiaryCommandsFont = Config.Bind("Fonts", "BestiaryCommands", "Inherit", "Bestiary Fight and related command text: Inherit (MenuCommands), Default, Classic, Modernized. Experimental hierarchy-based recognition.");
            // All new categories inherit their existing parent setting by default.
            BindExpanded("ShopNames", "MenuCommands");
            BindExpanded("ShopDialogue", "Dialogue");
            BindExpanded("ShopItems", "MenuCommands");
            BindExpanded("ShopPrices", "ShopItems");
            BindExpanded("ShopQuantities", "ShopItems");
            BindExpanded("ShopCommands", "MenuCommands");
            BindExpanded("ShopSpellLevels", "ShopQuantities");
            BindExpanded("MusicTrackTitles", "MusicPlayerText");
            BindExpanded("MusicTrackDetails", "MusicPlayerText");
            BindExpanded("MusicTrackNumbers", "MusicPlayerText");
            BindExpanded("MusicPlayerCommands", "MusicPlayerText");
            BindExpanded("GalleryTitles", "GalleryText");
            BindExpanded("GalleryDescriptions", "GalleryText");
            BindExpanded("GalleryEntryNumbers", "GalleryText");
            BindExpanded("GalleryCommands", "GalleryText");
            BindExpanded("TitleNewGameCharacterNames", "TitleNewGame");
            BindExpanded("TitleNewGameCharacterJobs", "TitleNewGame");
            BindExpanded("TitleNewGameJobChoices", "TitleNewGame");
            BindExpanded("TitleNewGameInstructions", "TitleNewGame");
            BindExpanded("TitleNewGameCommands", "TitleNewGame");
            FitOtherNumericFields = Config.Bind("FontFitting", "OtherNumericFields", true,
                "In Targeted mode, fit additional hierarchy-identified numeric UI fields in shops, menus and title-screen collections.");
            GameNumber = DetectGame();
            string[] universal = {
                "ButtonPrompts|TitleSubmenuCommands", "MenuHeaders|TitleSubmenuCommands",
                "TitleCopyright|TitlePrompts", "LoadingText|TitlePrompts",
                "NameEntryPrompts|TitleNewGame", "NameEntryText|TitleNewGame",
                "CampCharacterStats|MenuCommands", "CampLocation|Location",
                "CampTime|MenuCommands", "CampGil|MenuCommands",
                "SaveSlotDetails|TitleLoadGame", "SaveLoadCommands|MenuCommands",
                "SaveLoadConfirmations|MenuCommands",
                "ItemNames|MenuCommands", "ItemQuantities|MenuCommands", "ItemCommands|MenuCommands",
                "MagicNames|MenuCommands", "MagicCharges|MenuCommands", "MagicCommands|MenuCommands",
                "EquipmentNames|MenuCommands", "EquipmentStats|MenuCommands", "EquipmentCommands|MenuCommands",
                "StatusLabels|MenuCommands", "StatusValues|MenuCommands", "StatusCommands|StatusBattleCommands",
                "ConfigurationLabels|TitleConfiguration", "ConfigurationChoices|TitleConfiguration",
                "ConfigurationValues|TitleConfiguration", "LicenseText|TitleSubmenuCommands",
                "BattleCharacterNames|BattleCommands", "BattleEnemyNames|BattleCommands",
                "BattleStats|BattleCommands", "BattleDamage|BattleAnnouncements",
                "BattleTargeting|BattleCommands", "BattleResults|BattleAnnouncements",
                "ShopDescriptions|Description", "DialogueChoices|MenuCommands", "ServiceConfirmations|MenuCommands",
                "MapLabels|MenuCommands", "MapLocations|Location", "MapPrompts|ButtonPrompts",
                "FieldItemUse|ItemNames", "ItemDescriptions|Description", "EmptyStateMessages|MenuExplanations", "PopupMessages|TitlePrompts", "PopupChoices|MenuCommands", "PauseCommands|MenuCommands", "PauseControls|ButtonPrompts", "StoryNarration|Dialogue", "BestiarySummary|BestiaryCommands", "BestiaryEncounterCount|BestiaryCommands", "ShopEquipmentStats|EquipmentStats", "AbilityNames|MenuCommands", "AbilityDescriptions|Description", "MagicSubmenuChoices|MagicCommands", "MagicCharacterInfo|CampCharacterStats"
            };
            foreach (string entry in universal)
            {
                string[] parts = entry.Split('|');
                BindUniversal(parts[0], parts[1]);
            }
            BindGame(2, "KeyTermNames", "ItemNames");
            BindGame(2, "KeyTermDescriptions", "Description");
            BindGame(2, "SkillProgression", "BattleResults");
            BindGame(2, "StatGrowth", "BattleResults");
            BindGame(3, "JobNames", "AbilityNames");
            BindGame(3, "JobLevels", "StatusValues");
            BindGame(5, "JobNames", "AbilityNames");
            BindGame(5, "JobDescriptions", "AbilityDescriptions");
            BindGame(5, "JobLevels", "StatusValues");
            BindGame(5, "AbilityNames", "AbilityNames");
            BindGame(5, "AbilityDescriptions", "AbilityDescriptions");
            BindGame(5, "ABPValues", "StatusValues");
            BindGame(6, "AbilityNames", "AbilityNames");
            BindGame(6, "AbilityDescriptions", "AbilityDescriptions");
            BindGame(6, "SpecialCommands", "BattleCommands");
            // Esper categories are configurable now, but matching awaits a verified Esper UI trace.
            BindGame(6, "EsperNames", "AbilityNames");
            BindGame(6, "EsperDescriptions", "AbilityDescriptions");
            BindGame(6, "MagicLearning", "StatusValues");
            HierarchyEnabled = DiagnosticsConfig.Bind("Diagnostics.Hierarchy", "Enabled", false,
                "Diagnostic-only snapshots of UI Text under selected window roots. Does not change font assignments.");
            HierarchyRoots = DiagnosticsConfig.Bind("Diagnostics.Hierarchy", "Roots", "",
                "Semicolon-separated case-insensitive window-name fragments. Set to exact observed names to reduce noise.");
            HierarchyMaxRoots = DiagnosticsConfig.Bind("Diagnostics.Hierarchy", "MaxRoots", 18,
                "Maximum distinct matching window roots logged per session (1-100).");
            HierarchyMaxTexts = DiagnosticsConfig.Bind("Diagnostics.Hierarchy", "MaxTextsPerRoot", 90,
                "Maximum text components per snapshot (1-250).");
            TextProbeEnabled = DiagnosticsConfig.Bind("Diagnostics.TextProbe", "Enabled", false,
                "Capture matching Unity UI Text components and nearby siblings; diagnostics only.");
            TextProbeTerms = DiagnosticsConfig.Bind("Diagnostics.TextProbe", "Terms", "",
                "Case-insensitive semicolon-separated substrings. No font matching depends on these terms.");
            TextProbeMaxMatches = DiagnosticsConfig.Bind("Diagnostics.TextProbe", "MaxMatches", 50,
                "Maximum distinct matching paths logged per session (1-500).");
            TextProbeMaxNearby = DiagnosticsConfig.Bind("Diagnostics.TextProbe", "MaxNearby", 35,
                "Maximum nearby UI Text components per match (0-150).");
            TextProbeAncestorDepth = DiagnosticsConfig.Bind("Diagnostics.TextProbe", "AncestorDepth", 2,
                "How many parents above a match to inspect (0-5).");
            DiagnosticLevel = DiagnosticsConfig.Bind("Diagnostics", "Level", "Off", "Off, Basic, Verbose. TextProbe and Hierarchy are controlled separately. Errors and warnings are always logged.");
            FitMode = Config.Bind("FontFitting", "Mode", "Targeted", "Off, Targeted, AllMenus (experimental). AllMenus enables Best Fit on previously non-fitting eligible menu UI.Text objects.");
            FitBattleItemQuantities = Config.Bind("FontFitting", "BattleItemQuantities", true, "Auto-fit battle item-list quantities in Targeted mode.");




            FitCampLevels = Config.Bind("FontFitting", "CampLevels", true, "Auto-fit main camp party levels only.");
            FitItemQuantities = Config.Bind("FontFitting", "ItemQuantities", true, "Auto-fit camp item-list quantities only.");
            FitCampMagicLevels = Config.Bind("FontFitting", "CampMagicLevels", true, "Auto-fit camp magic list level numbers only.");
            FitBattleMagicLevels = Config.Bind("FontFitting", "BattleMagicLevels", true, "Auto-fit battle magic list level numbers only.");
            FitMinimumSize = Config.Bind("FontFitting", "MinimumFontSize", 12, "Minimum auto-fit font size (clamped to 2-32); only applies to enabled fitting categories.");
            // Explicitly persist both configs after all entries are bound. Some BepInEx
            // IL2CPP configurations do not auto-write a newly created ConfigFile.
            // Save() preserves loaded values and does not reset existing user choices.
            Config.Save();
            DiagnosticsConfig.Save();
            harmony = new Harmony("ffpr.contextualfonts");
            harmony.PatchAll(typeof(LanguageFontMappingProbe));
            harmony.PatchAll(typeof(ContextualFontPatch));
            harmony.PatchAll(typeof(ContextualFontSetterPatch));
            harmony.PatchAll(typeof(ContextualTextEnablePatch));




            Log.LogInfo("[ContextualFonts] v1.0.10. Diagnostics=" + DiagnosticLevel.Value + ", FitMode=" + FitMode.Value + ", Dialogue=" + DialogueFont.Value + ", Speaker=" + SpeakerFont.Value + ", Description=" + DescriptionFont.Value);
        }
    }


    // Required: capture the game's language-specific font mapping without modifying it.
    [HarmonyPatch(typeof(FontManager), "CreateFontParameter")]
    internal static class LanguageFontMappingProbe
    {
        [HarmonyPostfix]
        private static void Postfix(FontManager.FontType __0, Language __1, FontManager.FontParameter __result)
        {
            if (__result == null) return;
            try { LanguageAwareFonts.Record(__0.ToString(), __1.ToString(), __result.FontName ?? "<null>"); }
            catch (Exception ex) { Plugin.Logger.LogWarning("[ContextualFonts] Font mapping observation failed: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(Text), nameof(Text.text), MethodType.Setter)]
    internal static class ContextualFontPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Text __instance)
        {
            FontRefresh.Apply(__instance, "text");
            TextContentProbe.Observe(__instance, "text");
            HierarchyProbe.Observe(__instance, "text");
            NumericAutoFit.Apply(__instance);
            // Some speaker labels are assigned before or after the dialogue text.
            // A dialogue update gives us another opportunity to enforce the speaker.
            FontRefresh.ApplySiblingSpeaker(__instance);
        }
    }

    [HarmonyPatch(typeof(Text), nameof(Text.font), MethodType.Setter)]
    internal static class ContextualFontSetterPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Text __instance)
        {
            FontRefresh.Apply(__instance, "font");
            TextContentProbe.Observe(__instance, "font");
            HierarchyProbe.Observe(__instance, "font");
            NumericAutoFit.Apply(__instance);
        }
    }

    // UI elements can become visible without receiving another text assignment.
    [HarmonyPatch(typeof(Text), "OnEnable")]
    internal static class ContextualTextEnablePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Text __instance)
        {
            FontRefresh.Apply(__instance, "enable");
            TextContentProbe.Observe(__instance, "enable");
            HierarchyProbe.Observe(__instance, "enable");
            NumericAutoFit.Apply(__instance);
        }
    }



    // Dev2: language-aware, conservative resolver. Mapping data comes from the game's
    // CreateFontParameter calls; no font-manager cache is modified by this plugin.
    internal static class LanguageAwareFonts
    {
        private static readonly Dictionary<string, string> Mappings = new Dictionary<string, string>();
        private static string activeLanguage;
        private static readonly HashSet<string> Reported = new HashSet<string>();

        internal static void Record(string type, string language, string asset)
        {
            if (string.IsNullOrEmpty(language) || string.IsNullOrEmpty(type)) return;
            if (activeLanguage != language)
            {
                activeLanguage = language;
            }
            Mappings[language + ":" + type] = asset;
        }

        private static bool SupportsDualStyle(string language)
        {
            // Only verified dual-style languages. Unknown/single-style languages
            // preserve the game's own font, including any third-party replacements.
            switch (language)
            {
                case "En": case "Fr": case "Ja": return true;
                // Other Latin languages can be enabled after their mappings are tested.
                default: return false;
            }
        }

        internal static Font Resolve(bool classic, Text text, out string reason)
        {
            string language = activeLanguage;
            if (string.IsNullOrEmpty(language)) { reason = "language mapping not initialized"; return null; }
            if (!SupportsDualStyle(language)) { reason = "native dual-style fonts not verified for " + language; return null; }
            string type = classic ? "Font08" : "Font01";
            string asset;
            if (!Mappings.TryGetValue(language + ":" + type, out asset) || string.IsNullOrEmpty(asset) || asset == "<null>")
            { reason = "no mapping for " + language + "/" + type; return null; }

            // FontName is an asset filename; loaded Unity Font.name may omit its extension.
            string baseName = System.IO.Path.GetFileNameWithoutExtension(asset);
            Font target = null;
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < fonts.Length; i++)
            {
                Font candidate = fonts[i];
                if (candidate == null) continue;
                if (string.Equals(candidate.name, asset, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.name, baseName, StringComparison.OrdinalIgnoreCase))
                { target = candidate; break; }
            }
            if (target == null) { reason = "mapped asset not loaded: " + language + "/" + type + "=" + asset; return null; }

            // Japanese font roles need validation. Never assign a font if it lacks
            // characters already present in the component (particularly kana/kanji).
            if (language == "Ja" && text != null)
            {
                string value = text.text ?? "";
                for (int i = 0; i < value.Length; i++)
                {
                    char ch = value[i];
                    if (char.IsWhiteSpace(ch) || char.IsControl(ch) || char.IsSurrogate(ch)) continue;
                    if (!target.HasCharacter(ch))
                    { reason = "Japanese mapped font lacks glyph U+" + ((int)ch).ToString("X4") + " (" + type + ")"; return null; }
                }
            }
            reason = "resolved " + language + "/" + type + "=" + asset;
            return target;
        }
    }

    internal static class FontRefresh
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static bool applying;
        private static readonly Dictionary<string, Font> CachedFonts = new Dictionary<string, Font>();

        private static string ResolveInherited(string selected, string parent)
        {
            return string.Equals((selected ?? "").Trim(), "Inherit", StringComparison.OrdinalIgnoreCase) ? parent : selected;
        }

        internal static void ApplySiblingSpeaker(Text text)
        {
            if (applying || text == null || text.gameObject == null) return;
            try
            {
                var tr = text.transform;
                for (int depth = 0; depth < 8 && tr != null; depth++, tr = tr.parent)
                {
                    var view = tr.gameObject.GetComponent<MessageWindowView>();
                    if (view == null) continue;
                    if (view.messageText != null && view.messageText.Pointer == text.Pointer && view.spekerText != null)
                        Apply(view.spekerText, "dialogue-sibling");
                    return;
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[ContextualFonts] Speaker refresh error: " + ex); }
        }

        internal static void Apply(Text text, string trigger)
        {
            if (applying || text == null) return;
            try
            {
                if (text.gameObject == null) return;
                string category = null;
                string choice = null;
                var tr = text.transform;
                for (int depth = 0; depth < 8 && tr != null; depth++, tr = tr.parent)
                {
                    var view = tr.gameObject.GetComponent<MessageWindowView>();
                    if (view == null) continue;
                    if (view.messageText != null && view.messageText.Pointer == text.Pointer)
                    { category = "Dialogue"; choice = Plugin.DialogueFont.Value; }
                    else if (view.spekerText != null && view.spekerText.Pointer == text.Pointer)
                    { category = "Speaker"; choice = Plugin.SpeakerFont.Value; }
                    break;
                }
                // Exact, scene-scoped matches identified by the v1.0.4 text probes.
                // Keep persistent confirmation popups and battle ability interfaces untouched.
                if (category == null)
                {
                    category = Rc3DescriptionMatcher.Category(text) ?? Dev7FontMatcher.Category(text) ?? ProbeConfirmedMatcher.Category(text) ?? TargetedCoverageMatcher.Category(text) ?? VerifiedMenuMatcher.Category(text);
                    if (category == "BattleDescriptions") choice = Plugin.BattleDescriptionsFont.Value;
                    else if (category == "BattleAbilities") choice = Plugin.BattleAbilitiesFont.Value;
                    else if (category != null) choice = CrossGameFonts.Choice(category);
                }
                // Recognize party-card identity fields before older, overly strict hierarchy rules.
                if (category == null)
                {
                    category = CampPartyIdentityMatcher.Category(text);
                    if (category == "CampCharacterNames") choice = Plugin.CampCharacterNamesFont.Value;
                    else if (category == "CampCharacterJobs") choice = Plugin.CampCharacterJobsFont.Value;
                }
                if (category == null)
                {
                    category = CampPartyFontMatcher.Category(text);
                    if (category == "CampCharacterNames") choice = Plugin.CampCharacterNamesFont.Value;
                    else if (category == "CampCharacterJobs") choice = Plugin.CampCharacterJobsFont.Value;
                }
                if (category == null && LocationTextMatcher.IsLocationPopup(text))
                { category = "Location"; choice = Plugin.LocationFont.Value; }
                if (category == null && MenuExplanationTextMatcher.IsExplanation(text))
                { category = "MenuExplanations"; choice = Plugin.MenuExplanationsFont.Value; }
                if (category == null)
                {
                    category = BestiaryTextMatcher.Category(text);
                    if (category == "BestiaryList") choice = Plugin.BestiaryListFont.Value;
                    else if (category == "BestiaryEntryHeader") choice = Plugin.BestiaryEntryHeaderFont.Value;
                    else if (category == "BestiaryEntryDetails") choice = Plugin.BestiaryEntryDetailsFont.Value;
                }
                if (category == null)
                {
                    category = ExpandedFontMatcher.Category(text);
                    if (category != null)
                    {
                        string parent = ExpandedFontMatcher.Parent(category);
                        choice = ExpandedFontMatcher.ResolveChoice(category, parent);
                    }
                }
                if (category == null)
                {
                    category = CommandTextMatcher.Category(text);
                    if (category == "MenuCommands") choice = Plugin.MenuCommandsFont.Value;
                    else if (category == "BattleCommands") choice = Plugin.BattleCommandsFont.Value;
                }
                if (category == null)
                {
                    category = AdditionalFontMatcher.Category(text);
                    if (category == "StatusBattleCommands")
                    {
                        choice = Plugin.StatusBattleCommandsFont.Value;
                        if (string.Equals(choice == null ? "" : choice.Trim(), "Inherit", StringComparison.OrdinalIgnoreCase))
                            choice = Plugin.BattleCommandsFont.Value;
                    }
                    else if (category == "TitleMenuCommands") choice = Plugin.TitleMenuCommandsFont.Value;
                    else if (category == "TitleSubmenuCommands") choice = Plugin.TitleSubmenuCommandsFont.Value;
                    else if (category == "TitlePrompts") choice = Plugin.TitlePromptsFont.Value;
                    else if (category == "TitleNewGame") choice = Plugin.TitleNewGameFont.Value;
                    else if (category == "TitleLoadGame") choice = Plugin.TitleLoadGameFont.Value;
                    else if (category == "TitleConfiguration") choice = Plugin.TitleConfigurationFont.Value;
                    else if (category == "MusicPlayerText") choice = ResolveInherited(Plugin.MusicPlayerTextFont.Value, Plugin.TitleSubmenuCommandsFont.Value);
                    else if (category == "GalleryText") choice = ResolveInherited(Plugin.GalleryTextFont.Value, Plugin.TitleSubmenuCommandsFont.Value);
                    else if (category == "BestiaryCommands") choice = ResolveInherited(Plugin.BestiaryCommandsFont.Value, Plugin.MenuCommandsFont.Value);
                }
                if (category == null)
                {
                    category = CrossGameFonts.Category(text);
                    if (category != null) choice = CrossGameFonts.Choice(category);
                }
                if (category == null && DescriptionTextMatcher.IsDescription(text))
                { category = "Description"; choice = Plugin.DescriptionFont.Value; }
                if (category == null)
                {
                    category = BattleTextMatcher.Category(text);
                    if (category == "BattleAbilities") choice = Plugin.BattleAbilitiesFont.Value;
                    else if (category == "BattleDescriptions") choice = Plugin.BattleDescriptionsFont.Value;
                    else if (category == "BattleAnnouncements") choice = Plugin.BattleAnnouncementsFont.Value;
                }
                if (category == null || string.IsNullOrWhiteSpace(choice) ||
                    string.Equals(choice.Trim(), "Default", StringComparison.OrdinalIgnoreCase)) return;
                bool classic = string.Equals(choice.Trim(), "Classic", StringComparison.OrdinalIgnoreCase);
                bool modern = string.Equals(choice.Trim(), "Modernized", StringComparison.OrdinalIgnoreCase);
                if (!classic && !modern)
                {
                    if (Missing.Add("invalid:" + category + ":" + choice))
                        Plugin.Logger.LogWarning("[ContextualFonts] Invalid " + category + " font choice '" + choice + "'.");
                    return;
                }
                string reason;
                Font target = LanguageAwareFonts.Resolve(classic, text, out reason);
                if (target == null)
                {
                    return;
                }
                Font before = text.font;
                if (before != null && before.Pointer == target.Pointer) return;
                applying = true;
                try
                {
                    text.font = target;
                    string key = category + ": " + (before == null ? "<null>" : before.name) + " -> " + target.name;
                    if (Plugin.BasicDiagnostics && Reported.Add(key)) Plugin.Logger.LogInfo("[ContextualFonts] " + key + " (size=" + text.fontSize + ").");
                }
                finally { applying = false; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[ContextualFonts] Override error (" + trigger + "): " + ex); }
        }
    }

}

namespace FFPRContextualFonts
{
    // Diagnostics only: the names below are filters, NOT assertions about which component is a description.
    // Logs paths and text previews so we can identify exact components before implementing overrides.

}

namespace FFPRContextualFonts
{
    internal static class DescriptionTextMatcher
    {
        // Exact terminal paths taken from the v0.4 FF1 log. Matching the full
        // component hierarchy prevents changing labels, spell names or numbers.
        private static readonly string[] Endings = {
            "/SubMenuParent/item_info(Clone)/description_window/Text",
            "/SubMenuParent/ability_info(Clone)/root/root/ability_list_window/description/Text",
            "/SubMenuParent/equipment_info(Clone)/root/equipment_description_window(Clone)/Text"
        };

        internal static bool IsDescription(Text text)
        {
            if (text == null || text.gameObject == null) return false;
            // These verified components are in CampMenu. Don't touch the
            // similarly named alternate objects under MessageWindow.
            if (!string.Equals(text.gameObject.scene.name, "CampMenu", StringComparison.Ordinal)) return false;
            var tr = text.transform;
            if (tr == null) return false;
            string path = tr.gameObject.name;
            for (int i = 0; i < 24 && tr.parent != null; i++)
            {
                tr = tr.parent;
                path = tr.gameObject.name + "/" + path;
            }
            foreach (string ending in Endings)
                if (path.EndsWith(ending, StringComparison.Ordinal)) return true;
            return false;
        }
    }


}

namespace FFPRContextualFonts
{


}

namespace FFPRContextualFonts
{
    // RC3: TextProbe-confirmed description components, scoped to their scenes and games.
    internal static class Rc3DescriptionMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null) return null;
            string scene = text.gameObject.scene.name.ToLowerInvariant();
            string path = ContextualPath.Get(text.transform).ToLowerInvariant();
            if (scene == "battlemenu" &&
                path.Contains("/equip_select_window(clone)/root/description_window/root/description_text") &&
                path.EndsWith("/root/description_text"))
                return "BattleDescriptions";
            if (Plugin.GameNumber == 5 && scene == "campmenu")
            {
                if (path.Contains("/jobchange_info(clone)/info_root/descript_root/jobname_text") &&
                    path.EndsWith("/descript_root/jobname_text")) return "JobDescriptions";
                if (path.Contains("/ability_change_info(clone)/info_root/descript_root/info_text") &&
                    path.EndsWith("/descript_root/info_text")) return "AbilityDescriptions";
            }
            return null;
        }
    }

    internal static class BattleTextMatcher
    {
        // Conservative matching: only the three families observed in FF1 battle
        // diagnostics. Exclude help/key labels and unrelated descriptions.
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null ||
                !string.Equals(text.gameObject.scene.name, "BattleMenu", StringComparison.OrdinalIgnoreCase)) return null;
            var tr = text.transform;
            if (tr == null) return null;
            string path = tr.gameObject.name;
            for (int i = 0; i < 26 && tr.parent != null; i++)
            { tr = tr.parent; path = tr.gameObject.name + "/" + path; }
            if (path.IndexOf("key_help_content", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            if (path.IndexOf("ability_select_content_FF1", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/name_text", StringComparison.OrdinalIgnoreCase)) return "BattleAbilities";
            if (path.IndexOf("description_window", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/root/description_text", StringComparison.OrdinalIgnoreCase) &&
                (path.IndexOf("ability_info_window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("item_info_window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("equip_select_window(Clone)", StringComparison.OrdinalIgnoreCase) >= 0)) return "BattleDescriptions";
            if (path.IndexOf("command_message_window", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/last_text", StringComparison.OrdinalIgnoreCase)) return "BattleAnnouncements";
            return null;
        }
    }


}


namespace FFPRContextualFonts
{
    // v0.9 diagnostic only. Does not change any fonts.
    // A separate budget for each scene and renderer avoids menu noise exhausting
    // all samples. Watch-text matches are never budgeted or filtered.



}

namespace FFPRContextualFonts
{
    internal static class LocationTextMatcher
    {
        // Observed in v0.9 Chaos Shrine WATCH logs. Exact scene and hierarchy,
        // not a broad MessageWindow match. This widget may be reused by other
        // system notifications; no claim of location-only semantics is made.
        internal static bool IsLocationPopup(UnityEngine.UI.Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null) return false;
            if (!string.Equals(text.gameObject.scene.name, "MessageWindow", StringComparison.Ordinal) &&
                !string.Equals(text.gameObject.scene.name, "ArBattleMessageWindow", StringComparison.Ordinal)) return false;
            var tr = text.transform;
            string path = tr.gameObject.name;
            for (int i = 0; i < 32 && tr.parent != null; i++)
            {
                tr = tr.parent;
                path = tr.gameObject.name + "/" + path;
            }
            return path.EndsWith("/system_message_parent/message_system_window(Clone)/last_text", StringComparison.Ordinal);
        }
    }


    internal static class MenuExplanationTextMatcher
    {
        internal static bool IsExplanation(Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null ||
                !string.Equals(text.gameObject.name, "last_text", StringComparison.Ordinal)) return false;
            string path = ContextualPath.Get(text.transform);
            return (path.IndexOf("/config(Clone)/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (path.EndsWith("/top_root/description_root/last_text", StringComparison.Ordinal) ||
                 path.EndsWith("/cheat_mode_settings_window/description_root/last_text", StringComparison.Ordinal)));
        }
    }

    internal static class ContextualPath
    {
        internal static string Get(Transform tr)
        {
            string path = tr.gameObject.name;
            for (int i = 0; i < 40 && tr.parent != null; i++)
            {
                tr = tr.parent;
                path = tr.gameObject.name + "/" + path;
            }
            return path;
        }
    }


    // v1.0.9: confirmed hierarchy paths from the FF I, II, III and V text probes.
    // No displayed-language strings are used to select categories.
    // v1.0.10-dev7: paths confirmed with targeted TextProbe, not guessed from labels.
    internal static class Dev7FontMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null) return null;
            string scene = text.gameObject.scene.name.ToLowerInvariant();
            string path = ContextualPath.Get(text.transform).ToLowerInvariant();
            if (scene == "battlemenu" &&
                path.Contains("/ability_root/ability_info_window(clone)/") &&
                path.EndsWith("/ability_content(clone)/root/lv/last_text"))
                return "BattleAbilities";
            if (scene == "campmenu" &&
                path.Contains("/ability_info(clone)/") &&
                path.EndsWith("/ability_list_window/description/text"))
                return "AbilityDescriptions";
            // FF I camp equipment detail: verified by Ragnarok TextProbe.
            // The description is a standalone UnityEngine.UI.Text, separate
            // from equipment_status_window(Clone) statistics and item names.
            if (scene == "campmenu" &&
                path.Contains("/submenuparent/equipment_info(clone)/root/") &&
                path.EndsWith("/equipment_description_window(clone)/text"))
                return "Description";
            if (scene == "campmenu" && path.Contains("/item_info(clone)/") &&
                (path.EndsWith("/description_window/text") || path.EndsWith("/description/text")))
                return "ItemDescriptions";
            if (scene == "shopmenu" && path.Contains("/shop_base(clone)/") &&
                (path.Contains("/equip_menu_window(clone)/") ||
                 path.Contains("/item_menu_window(clone)/") ||
                 path.Contains("/magic_menu_window(clone)/") ||
                 path.Contains("/magic_item_menu_window(clone)/")) &&
                path.EndsWith("/info/item_info/text"))
                return "ShopDescriptions";
            return null;
        }
    }

    internal static class ProbeConfirmedMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null) return null;
            string scene = text.gameObject.scene.name.ToLowerInvariant();
            string p = ContextualPath.Get(text.transform).ToLowerInvariant();

            // FFII CAMP glossary; the conversation keyword_info_base(Clone) is separate
            // and already working. Include inactive empty-state templates.
            if (Plugin.GameNumber == 2 && scene == "campmenu" &&
                p.Contains("/words_info(clone)/"))
            {
                if (p.Contains("description") || p.Contains("explain") ||
                    p.Contains("detail") || p.Contains("info_text")) return "KeyTermDescriptions";
                if (p.EndsWith("/last_text") && !p.Contains("/list_window/")) return "KeyTermDescriptions";
                return "KeyTermNames";
            }

            // FFIII field-use overlay is hosted in MessageWindow, not CampMenu.
            if (Plugin.GameNumber == 3 && scene == "messagewindow" &&
                p.Contains("/select_field_content_ff3(clone)/"))
            {
                if (p.Contains("/description_window/")) return "ItemDescriptions";
                if (p.Contains("/list_root/last_text")) return "EmptyStateMessages";
                if (p.Contains("/info_root/") || p.Contains("/command_name")) return "ItemCommands";
                if (p.Contains("/count_text")) return "ItemQuantities";
                return "FieldItemUse";
            }

            // v1.0.9: verified shared FFV/FFVI command selectors, including extended window.
            if ((Plugin.GameNumber == 5 || Plugin.GameNumber == 6) && scene == "campmenu" &&
                p.Contains("/ability_info(clone)/command_menu/") &&
                p.Contains("/commandcontents/") &&
                p.Contains("/ability_command_content/") && p.EndsWith("/last_text"))
                return "MagicSubmenuChoices";

            // FFVI special ability list (Blitz); preserve already-modernized input hints.
            if (Plugin.GameNumber == 6 && scene == "campmenu" &&
                p.Contains("/ability_info(clone)/root/root/special_list_root/"))
            {
                if (p.Contains("/special_select_content(clone)/root/icon_text/") && p.EndsWith("/last_text"))
                    return "AbilityNames";
                if (p.EndsWith("/special_list_root/description/text"))
                    return "AbilityDescriptions";
            }

            // Item commands and sort choices: scoped to actual camp item window.
            if (scene == "campmenu" && p.Contains("/item_info(clone)/menu/menu_window/root/root/"))
            {
                if (p.Contains("/item_command_content(clone)/") && p.EndsWith("/text")) return "ItemCommands";
                if (p.Contains("/root/content/common_command_content(clone)/") && p.EndsWith("/text")) return "ItemCommands";
            }

            // MessageWindow choices are not the dialogue body or speaker label.
            if (p.Contains("/message_choices_window(clone)/root/root/message_multiple_content/") &&
                p.EndsWith("/last_text")) return "DialogueChoices";

            // Main battle status names (not the separate, already-working target panel).
            if (scene.Contains("battle") && p.Contains("/battle_info_window(clone)/"))
            {
                if (p.Contains("/player_info_window/root/player_info_content(clone)/root/") && p.EndsWith("/last_text"))
                    return "BattleCharacterNames";
                if (p.Contains("/enemy_info_window/root/enamy_info_content(clone)/") && p.EndsWith("/last_text"))
                    return "BattleEnemyNames";
                if (p.Contains("/item_info_window/root/root/list_root/item_list/"))
                {
                    if (p.Contains("/item_select_content(clone)/root/icon_text/") && p.EndsWith("/last_text"))
                        return "ItemNames";
                    if (p.Contains("/equiplist/item_equip_content(clone)/icon_text/") && p.EndsWith("/last_text"))
                        return "EquipmentNames";
                }
            }

            // Shop description panels; avoid the already-working equipment panel.
            if (scene == "shopmenu" && p.Contains("/shop_base(clone)/root/menuparent/") &&
                (p.Contains("/item_menu_window(clone)/") ||
                 p.Contains("/magic_menu_window(clone)/") ||
                 p.Contains("/magic_item_menu_window(clone)/")) &&
                p.EndsWith("/info/item_info/text")) return "ShopDescriptions";

            // FFV skillset selector: standard and extended-window command layouts.
            // Do not match the separate ability_change_info(Clone) management screen.
            // FFI title and in-game Bestiary share library_menu_base(Clone).
            // Leave the already-working LibraryDungeonUi alone.
            if (Plugin.GameNumber == 1 &&
                (scene == "libraryui" || scene == "menulibraryui") &&
                p.Contains("/library_menu_base(clone)/"))
            {
                if (p.Contains("/root/library_view/info_base/count_area/")) return "BestiaryEncounterCount";
                if (p.Contains("/root/library_view/info_base/")) return "BestiarySummary";
                if (p.Contains("/root/library_list/info_base/")) return "BestiarySummary";
            }

            // FFI title-screen Config arrow choice values (Classic/Modernized,
            // Original/Arranged). Do not reclassify in-game config or labels.
            if (Plugin.GameNumber == 1 && scene == "titlescreen" &&
                p.Contains("/option(clone)/main_window/config_root/") &&
                p.Contains("/config_tool_command/root/arrowbutton_type_root/centor/") &&
                p.EndsWith("/last_text")) return "ConfigurationChoices";
            return null;
        }
    }

    // v1.0.7: paths observed in previous game logs. Deliberately narrower than
    // generic "item" or "equipment" rules; other screens remain untouched.
    internal static class TargetedCoverageMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null) return null;
            string p = ContextualPath.Get(text.transform).ToLowerInvariant();
            string scene = text.gameObject.scene.name.ToLowerInvariant();
            // FFI: narrowly scoped, previously captured Unity hierarchies.
            if (Plugin.GameNumber == 1)
            {
                if (p.Contains("/equip_root/equip_select_window(clone)/"))
                {
                    if (p.EndsWith("/description_window/root/description_text")) return "Description";
                    if (p.EndsWith("/last_text") && (p.Contains("/equip_select_content/") || p.Contains("/item_equip_content/"))) return "EquipmentNames";
                }
                if (p.Contains("/magic_item_menu_window(clone)/shop_gil/root/"))
                {
                    if (p.EndsWith("/text")) return "CampGil";
                    if (p.EndsWith("/value")) return "CampGil";
                }
                if (p.Contains("/message_selected_window(clone)/root/message_select_content/") && p.EndsWith("/last_text")) return "DialogueChoices";
                if (p.Contains("/shop_base(clone)/root/listparent/sell_list_window(clone)/list_main_content_parent/") && p.EndsWith("/last_text")) return "EmptyStateMessages";
                if (p.Contains("/gps_menu_base(clone)/root/listparent/gps_list/") &&
                    (p.EndsWith("/place") || p.EndsWith("/info/root/place"))) return "MapLocations";
                if (p.Contains("/gps_menu_base(clone)/root/listparent/gps_list/") &&
                    p.Contains("/info/root/chest/") &&
                    (p.EndsWith("/text") || p.EndsWith("/num") || p.EndsWith("/separation") || p.EndsWith("/max"))) return "MapLabels";
                if (scene == "ArBattleMessageWindow" &&
                    p.EndsWith("/system_message_parent/message_system_window(clone)/last_text")) return "Location";
            }
            if (Plugin.GameNumber == 3 && p.Contains("noitemtext")) return "EmptyStateMessages";
            if (Plugin.GameNumber == 2 && scene.Contains("battle"))
            {
                if (p.Contains("item_info_window") && p.EndsWith("/last_text"))
                    return "EmptyStateMessages";
                if (p.Contains("equip_select_window(clone)"))
                {
                    if (p.Contains("status") || p.Contains("parameter") || p.Contains("power") ||
                        p.Contains("attack") || p.Contains("defense")) return "EquipmentStats";
                    if (p.Contains("command") || p.Contains("button") || p.Contains("select"))
                        return "EquipmentCommands";
                    return "EquipmentNames";
                }
            }
            return null;
        }
    }

    // Diagnostic-only window census. Captures complete component paths and fonts,
    // not localized strings for matching. Triggered by text/font/enable hooks.
    internal static class HierarchyProbe
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static bool Busy;
        internal static void Observe(Text text, string trigger)
        {
            if (Busy || Plugin.HierarchyEnabled == null || !Plugin.HierarchyEnabled.Value ||
                text == null || text.gameObject == null) return;
            try
            {
                Transform node = text.transform;
                Transform selected = null;
                string selectedTerm = null;
                while (node != null)
                {
                    foreach (string term in (Plugin.HierarchyRoots.Value ?? "").Split(';'))
                    {
                        string fragment = term.Trim();
                        if (fragment.Length > 0 && node.gameObject.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                        { selected = node; selectedTerm = fragment; break; }
                    }
                    if (selected != null) break;
                    node = node.parent;
                }
                if (selected == null) return;
                string path = ContextualPath.Get(selected);
                string key = Plugin.GameNumber + "|" + path;
                if (Seen.Contains(key) || Seen.Count >= Math.Max(1, Math.Min(100, Plugin.HierarchyMaxRoots.Value))) return;
                Busy = true;
                Seen.Add(key);
                Plugin.Logger.LogInfo("[ContextualFonts][HierarchyRoot] game=FF" + Plugin.GameNumber +
                    " scene='" + text.gameObject.scene.name + "' trigger=" + trigger +
                    " selector='" + selectedTerm + "' path='" + path + "'");
                int count = 0;
                Walk(selected, ref count, Math.Max(1, Math.Min(250, Plugin.HierarchyMaxTexts.Value)));
                Plugin.Logger.LogInfo("[ContextualFonts][HierarchyEnd] path='" + path + "' textCount=" + count);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[ContextualFonts][Hierarchy] " + ex.GetType().Name + ": " + ex.Message); }
            finally { Busy = false; }
        }
        private static void Walk(Transform node, ref int count, int limit)
        {
            if (node == null || count >= limit) return;
            Text t = node.gameObject.GetComponent<Text>();
            if (t != null)
            {
                count++;
                string value = t.text ?? "";
                value = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                if (value.Length > 100) value = value.Substring(0, 100);
                Plugin.Logger.LogInfo("[ContextualFonts][HierarchyText] path='" + ContextualPath.Get(node) +
                    "' font='" + (t.font == null ? "<null>" : t.font.name) +
                    "' active=" + node.gameObject.activeInHierarchy + " text='" + value + "'");
            }
            for (int i = 0; i < node.childCount && count < limit; i++) Walk(node.GetChild(i), ref count, limit);
        }
    }

    // v1.0.4 probe-confirmed Unity UI.Text hierarchy matches.
    // No localized strings are used to assign fonts.
    internal static class VerifiedMenuMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null) return null;
            string scene = text.gameObject.scene.name.ToLowerInvariant();
            string path = ContextualPath.Get(text.transform).ToLowerInvariant();
            // Persistent loading indicator: confirmed in FFI/II logs under
            // DontDestroyOnLoad/.../SystemIndicator/key_text_parent/key_text(Clone).
            // Include its animated period_text descendants. Do not use displayed words.
            if (scene == "dontdestroyonload" &&
                path.Contains("/systemindicator/key_text_parent/key_text(clone)"))
                return "LoadingText";
            // FFV ability-management empty slots use a generic Arial template.
            // Match all UI.Text under the confirmed ability screen, including inactive slots.
            // (The scene-specific ability matcher below supplies AbilityNames.)
            // The FFIII intro uses this MessageWindow child. Other games are deliberately
            // eligible only if they use the same renderer hierarchy.
            if (path.Contains("fade_message_window(clone)/") &&
                (scene == "messagewindow" || scene == "campmenu")) return "StoryNarration";
            if (scene != "campmenu") return null;
            int game = Plugin.GameNumber;
            // Camp magic/Esper selection overlays are distinct from spell lists.
            // Require explicit selector hierarchy markers to avoid reclassifying spells.
            if ((path.Contains("magic") || path.Contains("esper") || path.Contains("ability")) &&
                (path.Contains("type_select") || path.Contains("magic_type") ||
                 path.Contains("ability_type") || path.Contains("esper_select") ||
                 path.Contains("select_command") || path.Contains("command_type")))
                return "MagicSubmenuChoices";
            // Character panels within magic/Esper menus, not the main camp party cards.
            if ((path.Contains("magic") || path.Contains("esper")) &&
                (path.Contains("character_info") || path.Contains("chara_info") ||
                 path.Contains("chara_status"))) return "MagicCharacterInfo";
            if ((game == 3 || game == 5) && path.Contains("jobchange_info(clone)/"))
            {
                // jobchange_popup(Clone) is intentionally excluded.
                if (game == 5 && (path.Contains("description") || path.Contains("explain") ||
                    path.Contains("help_text"))) return "JobDescriptions";
                if (game == 5 && (path.Contains("details_value/") || path.Contains("abp")))
                    return "ABPValues";
                if (path.Contains("skilllevel_content/") || path.Contains("joblevel") ||
                    path.Contains("job_level") || path.Contains("level_text"))
                    return "JobLevels";
                return "JobNames";
            }
            if (game == 5 && path.Contains("ability_change_info(clone)/"))
            {
                if (path.Contains("description") || path.Contains("explain") ||
                    path.Contains("help_text")) return "AbilityDescriptions";
                return "AbilityNames";
            }
            return null;
        }
    }

    // Main camp party cards have different parent nesting in I-VI.
    // Restrict by scene and status_info_content, not by localized names.
    internal static class CampPartyIdentityMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null ||
                text.gameObject.scene.name != "CampMenu") return null;
            string p = ContextualPath.Get(text.transform).ToLowerInvariant();
            if (!p.Contains("status_info_content(clone)")) return null;
            // Avoid applying party-card rules to a separate status-details screen.
            if (p.Contains("status_details(clone)")) return null;
            string leaf = text.gameObject.name.ToLowerInvariant();
            if (leaf == "name_text" || leaf == "character_name" || leaf == "chara_name" || leaf == "name") return "CampCharacterNames";
            if (leaf == "job_text" || leaf == "job_name") return "CampCharacterJobs";
            return null;
        }
    }

    // Diagnostic-only substring probe. Captures bounded nearby Unity UI.Text elements
    // under the closest few parents; it never assigns a font based on English text.
    internal static class TextContentProbe
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static readonly HashSet<string> Snapshots = new HashSet<string>();
        private static bool Busy;
        private static string Short(string s)
        {
            if (s == null) return "";
            s = s.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            return s.Length > 140 ? s.Substring(0, 140) : s;
        }
        internal static void Observe(Text t, string trigger)
        {
            if (Busy || Plugin.TextProbeEnabled == null || !Plugin.TextProbeEnabled.Value ||
                t == null || t.gameObject == null) return;
            try
            {
                string value = t.text;
                if (string.IsNullOrEmpty(value)) return;
                string matched = null;
                foreach (string term in Plugin.TextProbeTerms.Value.Split(';'))
                {
                    string word = term.Trim();
                    if (word.Length > 0 && value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    { matched = word; break; }
                }
                if (matched == null) return;
                string path = ContextualPath.Get(t.transform);
                string key = Plugin.GameNumber + "|" + path + "|" + matched;
                if (Seen.Contains(key) || Seen.Count >= Math.Max(1, Math.Min(500, Plugin.TextProbeMaxMatches.Value))) return;
                Busy = true;
                Seen.Add(key);
                Plugin.Logger.LogInfo("[ContextualFonts][TextProbe] game=FF" + Plugin.GameNumber +
                    " term='" + matched + "' trigger=" + trigger + " scene='" + t.gameObject.scene.name +
                    "' path='" + path + "' component=UnityEngine.UI.Text font='" +
                    (t.font == null ? "<null>" : t.font.name) + "' active=" + t.gameObject.activeInHierarchy +
                    " text='" + Short(value) + "'");
                int max = Math.Max(0, Math.Min(150, Plugin.TextProbeMaxNearby.Value));
                if (max == 0) return;
                Transform root = t.transform;
                int depth = Math.Max(0, Math.Min(5, Plugin.TextProbeAncestorDepth.Value));
                for (int i = 0; i < depth && root.parent != null; i++) root = root.parent;
                string snapshotKey = Plugin.GameNumber + "|" + ContextualPath.Get(root);
                if (!Snapshots.Add(snapshotKey)) return;
                int count = 0;
                Snapshot(root, ref count, max);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[ContextualFonts][TextProbe] " + ex.GetType().Name + ": " + ex.Message);
            }
            finally { Busy = false; }
        }
        private static void Snapshot(Transform node, ref int count, int max)
        {
            if (node == null || count >= max) return;
            Text t = node.gameObject.GetComponent<Text>();
            if (t != null)
            {
                count++;
                Plugin.Logger.LogInfo("[ContextualFonts][TextProbeNearby] path='" + ContextualPath.Get(node) +
                    "' font='" + (t.font == null ? "<null>" : t.font.name) +
                    "' active=" + node.gameObject.activeInHierarchy + " text='" + Short(t.text) + "'");
            }
            for (int i = 0; i < node.childCount && count < max; i++)
                Snapshot(node.GetChild(i), ref count, max);
        }
    }

    internal static class CampPartyFontMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null ||
                text.gameObject.scene.name != "CampMenu") return null;
            string leaf = text.gameObject.name;
            if (leaf != "name_text" && leaf != "job_text") return null;
            string path = ContextualPath.Get(text.transform);
            // Match the verified party-card container, not the Status submenu.
            if (path.IndexOf("/SubMenuParent/status_info(Clone)/root/window/Contents/", StringComparison.Ordinal) < 0 ||
                path.IndexOf("/status_info_content(Clone)/Root/root/root/status/", StringComparison.Ordinal) < 0)
                return null;
            return leaf == "name_text" ? "CampCharacterNames" : "CampCharacterJobs";
        }
    }

    // Bestiary path rules derived from the v1.1 BestiaryProbe log. No global object scan.
    internal static class BestiaryTextMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null || text.transform == null) return null;
            string scene = text.gameObject.scene.name;
            if (scene == "MenuLibraryUi" || scene == "LibraryUi")
            {
                string path = ContextualPath.Get(text.transform);
                if (path.IndexOf("/library_menu_base(Clone)/root/library_list/view_base/", StringComparison.Ordinal) >= 0 &&
                    (path.EndsWith("/library_list_content/id_text", StringComparison.Ordinal) ||
                     path.EndsWith("/library_list_content/name_text", StringComparison.Ordinal))) return "BestiaryList";
            }
            else if (scene == "MenuLibraryInfo" || scene == "LibraryInfo")
            {
                string path = ContextualPath.Get(text.transform);
                if (path.IndexOf("/library_info_base(Clone)/root/library_info_view/root/info/name_text_area/", StringComparison.Ordinal) >= 0 &&
                    (path.EndsWith("/last_text", StringComparison.Ordinal) || path.EndsWith("/last_text (1)", StringComparison.Ordinal)))
                    return "BestiaryEntryHeader";
                if (path.IndexOf("/library_info_base(Clone)/root/library_info_view/root/view/Scroll View/Viewport/Content/info_content(Clone)/", StringComparison.Ordinal) >= 0)
                    return "BestiaryEntryDetails";
            }
            return null;
        }
    }

    // Diagnostic only. Restrict to plausible command labels and a small sample cap.
    // A text match alone is NOT safe evidence for a font override.

    // Exact hierarchy rules captured in v1.2 command diagnostics; no text-value matching.
    internal static class CommandTextMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null) return null;
            string scene = text.gameObject.scene.name;
            if (scene != "CampMenu" && scene != "BattleMenu") return null;
            string path = ContextualPath.Get(text.transform);
            if (scene == "CampMenu" && path.IndexOf("/CommandMenuParent/comand_menu(Clone)/Contents/comand_menu_content(Clone)/Text", StringComparison.Ordinal) >= 0 &&
                path.EndsWith("/CommandMenuParent/comand_menu(Clone)/Contents/comand_menu_content(Clone)/Text", StringComparison.Ordinal))
                return "MenuCommands";
            if (scene == "BattleMenu" && path.IndexOf("/command_select_parent/battle_info_window(Clone)/root/root/command_select_window/root/command_select_content(Clone)/root/last_text", StringComparison.Ordinal) >= 0 &&
                path.EndsWith("/command_select_window/root/command_select_content(Clone)/root/last_text", StringComparison.Ordinal))
                return "BattleCommands";
            return null;
        }
    }

    // Discovery-only, limited to text-hook events. Never enumerate all loaded Text objects.


    // Shared settings and game-scoped overrides. Unknown games never activate a game profile.
    internal static class CrossGameFonts
    {
        private static string Resolve(string category, int depth = 0)
        {
            if (depth > 8) return "Default";
            ConfigEntry<string> cfg;
            if (Plugin.UniversalFonts.TryGetValue(category, out cfg))
            {
                string choice = cfg.Value.Trim();
                if (!choice.Equals("Inherit", StringComparison.OrdinalIgnoreCase)) return choice;
            }
            if (category == "TitleSubmenuCommands") return Plugin.TitleSubmenuCommandsFont.Value;
            if (category == "TitlePrompts") return Plugin.TitlePromptsFont.Value;
            if (category == "BestiaryCommands") return Plugin.BestiaryCommandsFont.Value.Equals("Inherit", StringComparison.OrdinalIgnoreCase) ? Plugin.MenuCommandsFont.Value : Plugin.BestiaryCommandsFont.Value;
            if (category == "MenuExplanations") return Plugin.MenuExplanationsFont.Value;
            if (category == "Dialogue") return Plugin.DialogueFont.Value;
            if (category == "TitleNewGame") return Plugin.TitleNewGameFont.Value;
            if (category == "TitleLoadGame") return Plugin.TitleLoadGameFont.Value;
            if (category == "TitleConfiguration") return Plugin.TitleConfigurationFont.Value;
            if (category == "StatusBattleCommands")
            {
                string value = Plugin.StatusBattleCommandsFont.Value;
                return value.Equals("Inherit", StringComparison.OrdinalIgnoreCase) ? Plugin.BattleCommandsFont.Value : value;
            }
            if (category == "MenuCommands") return Plugin.MenuCommandsFont.Value;
            if (category == "BattleCommands") return Plugin.BattleCommandsFont.Value;
            if (category == "BattleAnnouncements") return Plugin.BattleAnnouncementsFont.Value;
            if (category == "Description") return Plugin.DescriptionFont.Value;
            if (category == "Location") return Plugin.LocationFont.Value;
            string parent;
            return Parents.TryGetValue(category, out parent) ? Resolve(parent, depth + 1) : "Default";
        }
        private static readonly Dictionary<string, string> Parents = new Dictionary<string, string> {
            {"ButtonPrompts","TitleSubmenuCommands"}, {"MenuHeaders","TitleSubmenuCommands"},
            {"TitleCopyright","TitlePrompts"}, {"LoadingText","TitlePrompts"},
            {"NameEntryPrompts","TitleNewGame"}, {"NameEntryText","TitleNewGame"},
            {"CampCharacterStats","MenuCommands"}, {"CampLocation","Location"},
            {"CampTime","MenuCommands"}, {"CampGil","MenuCommands"},
            {"SaveSlotDetails","TitleLoadGame"}, {"SaveLoadCommands","MenuCommands"},
            {"SaveLoadConfirmations","MenuCommands"},
            {"ItemNames","MenuCommands"}, {"ItemQuantities","MenuCommands"}, {"ItemCommands","MenuCommands"},
            {"MagicNames","MenuCommands"}, {"MagicCharges","MenuCommands"}, {"MagicCommands","MenuCommands"},
            {"EquipmentNames","MenuCommands"}, {"EquipmentStats","MenuCommands"}, {"EquipmentCommands","MenuCommands"},
            {"StatusLabels","MenuCommands"}, {"StatusValues","MenuCommands"},
            {"StatusCommands","StatusBattleCommands"},
            {"ConfigurationLabels","TitleConfiguration"}, {"ConfigurationChoices","TitleConfiguration"},
            {"ConfigurationValues","TitleConfiguration"}, {"LicenseText","TitleSubmenuCommands"},
            {"BattleCharacterNames","BattleCommands"}, {"BattleEnemyNames","BattleCommands"},
            {"BattleStats","BattleCommands"}, {"BattleDamage","BattleAnnouncements"},
            {"BattleTargeting","BattleCommands"}, {"BattleResults","BattleAnnouncements"},
            {"ShopDescriptions","Description"}, {"DialogueChoices","MenuCommands"}, {"ServiceConfirmations","MenuCommands"},
            {"MapLabels","MenuCommands"}, {"MapLocations","Location"}, {"MapPrompts","ButtonPrompts"},
            {"FieldItemUse","ItemNames"}, {"ItemDescriptions","Description"}, {"EmptyStateMessages","MenuExplanations"}, {"PopupMessages","TitlePrompts"}, {"PopupChoices","MenuCommands"}, {"PauseCommands","MenuCommands"}, {"PauseControls","ButtonPrompts"}, {"StoryNarration","Dialogue"}, {"BestiarySummary","BestiaryCommands"}, {"BestiaryEncounterCount","BestiaryCommands"}, {"ShopEquipmentStats","EquipmentStats"}, {"AbilityNames","MenuCommands"},
            {"AbilityDescriptions","Description"}, {"MagicSubmenuChoices","MagicCommands"}, {"MagicCharacterInfo","CampCharacterStats"}
        };
        internal static string Choice(string category)
        {
            string gameKey = Plugin.GameNumber + ":" + category;
            ConfigEntry<string> cfg;
            if (Plugin.GameNumber > 0 && Plugin.GameFonts.TryGetValue(gameKey, out cfg) &&
                !cfg.Value.Trim().Equals("Inherit", StringComparison.OrdinalIgnoreCase))
                return cfg.Value;
            string parent;
            if (Plugin.GameNumber > 0 && Plugin.GameFonts.ContainsKey(gameKey) &&
                GameParents.TryGetValue(gameKey, out parent)) return Resolve(parent);
            return Resolve(category);
        }
        private static readonly Dictionary<string, string> GameParents = new Dictionary<string, string> {
            {"2:KeyTermNames","ItemNames"}, {"2:KeyTermDescriptions","Description"},
            {"2:SkillProgression","BattleResults"}, {"2:StatGrowth","BattleResults"},
            {"3:JobNames","AbilityNames"},
            {"3:JobLevels","StatusValues"},
            {"5:JobNames","AbilityNames"}, {"5:JobDescriptions","AbilityDescriptions"},
            {"5:JobLevels","StatusValues"}, {"5:AbilityNames","AbilityNames"},
            {"5:AbilityDescriptions","AbilityDescriptions"}, {"5:ABPValues","StatusValues"},
            {"6:AbilityNames","AbilityNames"}, {"6:AbilityDescriptions","AbilityDescriptions"},
            {"6:SpecialCommands","BattleCommands"}, {"6:EsperNames","AbilityNames"},
            {"6:EsperDescriptions","AbilityDescriptions"}, {"6:MagicLearning","StatusValues"}
        };
        internal static string Category(Text text)
        {
            string scene = text.gameObject.scene.name.ToLowerInvariant();
            string p = ContextualPath.Get(text.transform).ToLowerInvariant();
            // Persistent popups are hosted in DontDestroyOnLoad, not their invoking menu scene.
            if (p.Contains("popupcanvas") || p.Contains("popup_parent"))
                return p.Contains("command") || p.Contains("button") || p.Contains("select") || p.Contains("yes_no") ? "PopupChoices" : "PopupMessages";
            if (p.Contains("field_help(clone)") || p.Contains("pause_menu") || p.Contains("pause_root") ||
                (scene == "battlemenu" && p.EndsWith("/pause_view(clone)/menu_root/pause_text", StringComparison.Ordinal)) ||
                (scene == "battlemenu" && p.Contains("/pause_view(clone)/menu_root/root/command_root/")))
                return p.Contains("config_controller") || p.Contains("key_") || p.Contains("control") ? "PauseControls" : "PauseCommands";
            if (p.Contains("story_text") || p.Contains("opening_text") || p.Contains("ending_text") || p.Contains("scroll_text")) return "StoryNarration";
            if (p.Contains("bestiary") || scene.Contains("library"))
            {
                if (p.Contains("encounter") || p.Contains("discover_count")) return "BestiaryEncounterCount";
                if (p.Contains("header") || p.Contains("title") || p.Contains("total_count")) return "BestiarySummary";
                if (p.Contains("map") || p.Contains("location")) return "MapLocations";
            }
            if (p.Contains("no_item") || p.Contains("empty_text") || p.Contains("nothing_text") || p.Contains("not_found")) return "EmptyStateMessages";
            if (p.Contains("shop") && p.Contains("equip") && (p.Contains("parameter") || p.Contains("status") || p.Contains("power"))) return "ShopEquipmentStats";
            if (p.Contains("dropdown_type_root") || p.Contains("dropdown/label")) return "ConfigurationChoices";
            if (p.Contains("status_info_content(clone)") || p.Contains("status_info(clone)"))
            {
                if (p.Contains("name_text")) return "CampCharacterNames";
                if (p.Contains("job_text")) return "CampCharacterJobs";
                if (p.Contains("hp") || p.Contains("mp") || p.Contains("level") || p.Contains("exp") || p.Contains("status")) return "CampCharacterStats";
            }
            if (Plugin.GameNumber == 3 && (p.Contains("job_info") || p.Contains("job_window") || p.Contains("job_contents") || p.Contains("job_content")))
                return p.Contains("level") ? "JobLevels" : "JobNames";
            if (Plugin.GameNumber == 3 && (p.Contains("item_use") || p.Contains("use_item") || p.Contains("item_target") || p.Contains("item_select"))) return "FieldItemUse";
            if (scene == "campmenu" && (p.Contains("magic") || p.Contains("ability") || p.Contains("esper")))
            {
                if (p.Contains("character_info") || p.Contains("chara_info") || p.Contains("chara_status"))
                    return "MagicCharacterInfo";
                if (p.Contains("type_select") || p.Contains("magic_type") || p.Contains("ability_type") ||
                    p.Contains("select_command") || p.Contains("command_type") || p.Contains("esper_select"))
                    return "MagicSubmenuChoices";
            }
            if (p.Contains("copy_right_root")) return "TitleCopyright";
            if (p.Contains("now_loading") || p.Contains("loading_text")) return "LoadingText";
            if (p.Contains("key_help") || p.Contains("button_guide") || p.Contains("key_guide"))
                return "ButtonPrompts";
            if (p.Contains("license") && (scene.Contains("title") || scene.Contains("license"))) return "LicenseText";
            if (p.Contains("name_input") || p.Contains("name_entry") || p.Contains("name_window"))
                return p.Contains("input") || p.Contains("name_text") ? "NameEntryText" : "NameEntryPrompts";
            if (scene.Contains("map") || p.Contains("map_window"))
                return p.Contains("location") || p.Contains("place") ? "MapLocations" : "MapLabels";
            if (scene.Contains("config") || p.Contains("config(clone)") ||
                (scene == "titlescreen" && p.Contains("/option(clone)/main_window/")))
            {
                if (p.Contains("slider") || p.Contains("value_root")) return "ConfigurationValues";
                if (p.Contains("select") || p.Contains("choice") || p.Contains("item_root")) return "ConfigurationChoices";
                return "ConfigurationLabels";
            }
            if (p.Contains("result_menu(clone)"))
            {
                if (Plugin.GameNumber == 2 && p.Contains("skill_level_view")) return "SkillProgression";
                if (Plugin.GameNumber == 2 && p.Contains("levelup_view")) return "StatGrowth";
                if (Plugin.GameNumber == 5 && p.Contains("job_level")) return "JobLevels";
                return "BattleResults";
            }
            if (Plugin.GameNumber == 2 && (p.Contains("keyword") || p.Contains("key_word")))
                return p.Contains("description") || p.Contains("explain") ? "KeyTermDescriptions" : "KeyTermNames";
            if ((Plugin.GameNumber == 3 || Plugin.GameNumber == 5) &&
                (p.Contains("job_menu") || p.Contains("job_change") || p.Contains("job_select")))
            {
                if (p.Contains("level") || p.Contains("/lv") || p.Contains("abp")) return Plugin.GameNumber == 5 && p.Contains("abp") ? "ABPValues" : "JobLevels";
                return Plugin.GameNumber == 5 && (p.Contains("explain") || p.Contains("description")) ? "JobDescriptions" : "JobNames";
            }
            if ((Plugin.GameNumber == 5 || Plugin.GameNumber == 6) &&
                (p.Contains("ability_menu") || p.Contains("ability_root") || p.Contains("ability_list")))
                return p.Contains("explain") || p.Contains("description") ? "AbilityDescriptions" : "AbilityNames";
            if (p.Contains("status_details") || scene.Contains("status"))
            {
                if (p.Contains("battle_commands") || p.Contains("command")) return "StatusCommands";
                if (p.Contains("value") || p.Contains("number") || p.Contains("_text") || p.Contains("parameter")) return "StatusValues";
                return "StatusLabels";
            }
            if (p.Contains("save") || p.Contains("load_game"))
            {
                if (p.Contains("popup") || p.Contains("confirm")) return "SaveLoadConfirmations";
                if (p.Contains("slot") || p.Contains("data")) return "SaveSlotDetails";
            }
            if (scene == "campmenu" || p.Contains("camp_menu"))
            {
                if (p.Contains("time")) return "CampTime";
                if (p.Contains("gil")) return "CampGil";
                if (p.Contains("location") || p.Contains("place")) return "CampLocation";
                if (p.Contains("current_hp") || p.Contains("max_hp") || p.Contains("current_mp") || p.Contains("max_mp")) return "CampCharacterStats";
                if (p.Contains("equipment") || p.Contains("equip_menu"))
                    return p.Contains("status") || p.Contains("parameter") ? "EquipmentStats" : "EquipmentNames";
                if (p.Contains("magic_menu") || p.Contains("magic_list") ||
                    p.Contains("/submenuparent/ability_info(clone)/"))
                    return p.Contains("charge") || p.Contains("count") ? "MagicCharges" : "MagicNames";
                if (p.Contains("item_menu") || p.Contains("item_list"))
                    return p.Contains("count") || p.Contains("quantity") ? "ItemQuantities" : "ItemNames";
            }
            if (scene.Contains("battle") || scene == "arbattlemenu")
            {
                if (p.Contains("damage") || p.Contains("damage_num")) return "BattleDamage";
                if (p.Contains("target")) return "BattleTargeting";
                if (p.Contains("enemy") && p.Contains("name")) return "BattleEnemyNames";
                if (p.Contains("chara") && p.Contains("name")) return "BattleCharacterNames";
                if (p.Contains("hp") || p.Contains("mp") || p.Contains("status")) return "BattleStats";
            }
            return null;
        }
    }

    // Scene-scoped path classification; never infer categories from localized text.
    internal static class ExpandedFontMatcher
    {
        private static readonly Dictionary<string, string> Parents = new Dictionary<string, string> {
            {"ShopNames","MenuCommands"}, {"ShopDialogue","Dialogue"}, {"ShopItems","MenuCommands"},
            {"ShopPrices","ShopItems"}, {"ShopQuantities","ShopItems"}, {"ShopCommands","MenuCommands"},
            {"ShopSpellLevels","ShopQuantities"},
            {"MusicTrackTitles","MusicPlayerText"}, {"MusicTrackDetails","MusicPlayerText"},
            {"MusicTrackNumbers","MusicPlayerText"}, {"MusicPlayerCommands","MusicPlayerText"},
            {"GalleryTitles","GalleryText"}, {"GalleryDescriptions","GalleryText"},
            {"GalleryEntryNumbers","GalleryText"}, {"GalleryCommands","GalleryText"},
            {"TitleNewGameCharacterNames","TitleNewGame"}, {"TitleNewGameCharacterJobs","TitleNewGame"},
            {"TitleNewGameJobChoices","TitleNewGame"}, {"TitleNewGameInstructions","TitleNewGame"},
            {"TitleNewGameCommands","TitleNewGame"}
        };
        internal static string Parent(string category) { string parent; return Parents.TryGetValue(category, out parent) ? parent : null; }
        private static string ResolveChoiceParent(string parent)
        {
            if (parent == "MusicPlayerText") return Plugin.MusicPlayerTextFont.Value.Trim().Equals("Inherit", StringComparison.OrdinalIgnoreCase) ? Plugin.TitleSubmenuCommandsFont.Value : Plugin.MusicPlayerTextFont.Value;
            if (parent == "GalleryText") return Plugin.GalleryTextFont.Value.Trim().Equals("Inherit", StringComparison.OrdinalIgnoreCase) ? Plugin.TitleSubmenuCommandsFont.Value : Plugin.GalleryTextFont.Value;
            if (parent == "TitleNewGame") return Plugin.TitleNewGameFont.Value;
            if (parent == "MenuCommands") return Plugin.MenuCommandsFont.Value;
            if (parent == "Dialogue") return Plugin.DialogueFont.Value;
            return "Default";
        }
        internal static string ResolveChoice(string category, string parent)
        {
            ConfigEntry<string> own;
            if (!Plugin.ExpandedFonts.TryGetValue(category, out own))
                return ResolveChoiceParent(parent ?? category);
            string value = own.Value;
            if (!string.Equals(value.Trim(), "Inherit", StringComparison.OrdinalIgnoreCase)) return value;
            ConfigEntry<string> ancestor;
            if (parent != null && Plugin.ExpandedFonts.TryGetValue(parent, out ancestor))
                return ResolveChoice(parent, Parent(parent));
            if (parent == "Dialogue") return Plugin.DialogueFont.Value;
            if (parent == "MenuCommands") return Plugin.MenuCommandsFont.Value;
            if (parent == "TitleNewGame") return Plugin.TitleNewGameFont.Value;
            if (parent == "MusicPlayerText") return (Plugin.MusicPlayerTextFont.Value.Trim().Equals("Inherit", StringComparison.OrdinalIgnoreCase) ? Plugin.TitleSubmenuCommandsFont.Value : Plugin.MusicPlayerTextFont.Value);
            if (parent == "GalleryText") return (Plugin.GalleryTextFont.Value.Trim().Equals("Inherit", StringComparison.OrdinalIgnoreCase) ? Plugin.TitleSubmenuCommandsFont.Value : Plugin.GalleryTextFont.Value);
            return "Default";
        }
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null) return null;
            string scene = text.gameObject.scene.name;
            if (scene != "ShopMenu" && scene != "SoundUi" && scene != "GalleryUi" &&
                scene != "GalleryDetailsUi" && scene != "TitleScreen") return null;
            string p = ContextualPath.Get(text.transform).ToLowerInvariant();
            if (scene == "ShopMenu")
            {
                if (p.Contains("/shop_name/") || p.Contains("/shop_name_window/")) return "ShopNames";
                if (p.Contains("message") || p.Contains("dialogue") || p.Contains("explain")) return "ShopDialogue";
                if (p.Contains("price") || p.Contains("/gil") || p.Contains("cost")) return "ShopPrices";
                if (p.Contains("/have") || p.Contains("/equip") && !p.Contains("equipment_status") ||
                    p.Contains("count_text") || p.Contains("quantity") || p.Contains("/stock")) return "ShopQuantities";
                if (p.Contains("/lv/") || p.EndsWith("/lv") ||
                    (p.Contains("/trade_magic_popup(clone)/") && p.EndsWith("/level"))) return "ShopSpellLevels";
                if (p.Contains("command") || p.Contains("/menu/")) return "ShopCommands";
                if (p.Contains("item_list") || p.Contains("equip_list") || p.Contains("magic_list")) return "ShopItems";
                return null;
            }
            if (scene == "SoundUi")
            {
                if (p.Contains("/soundlistparent/"))
                {
                    if (p.Contains("number") || p.Contains("/num") || p.Contains("index")) return "MusicTrackNumbers";
                    if (p.Contains("time") || p.Contains("duration") || p.Contains("/min") || p.Contains("/sec")) return "MusicTrackDetails";
                    return "MusicTrackTitles";
                }
                if (p.Contains("command") || p.Contains("button") || p.Contains("key_help")) return "MusicPlayerCommands";
                if (p.Contains("/explain/") || p.Contains("time") || p.Contains("origin")) return "MusicTrackDetails";
                if (p.Contains("/title/")) return "MusicTrackTitles";
                return "MusicPlayerText";
            }
            if (scene == "GalleryUi" || scene == "GalleryDetailsUi")
            {
                if (p.Contains("number") || p.Contains("/num") || p.Contains("index")) return "GalleryEntryNumbers";
                if (p.Contains("command") || p.Contains("button") || p.Contains("key_help")) return "GalleryCommands";
                if (p.Contains("explain") || p.Contains("description") || p.Contains("detail")) return "GalleryDescriptions";
                return "GalleryTitles";
            }
            if (!p.Contains("/new_game(clone)/")) return null;
            if (p.Contains("/job_list_window/job_contents/")) return "TitleNewGameJobChoices";
            if (p.Contains("/description_window/") || p.Contains("/top_description_window/")) return "TitleNewGameInstructions";
            if (p.Contains("/key_help_window/") || p.Contains("/start_popup/") ||
                p.Contains("/chara_contents/all/")) return "TitleNewGameCommands";
            if (p.Contains("/name_list_window/name_contents/")) return "TitleNewGameCharacterNames";
            if (p.Contains("/character_content/") || p.Contains("/character_content(clone)/"))
            {
                if (p.Contains("/gameobject/last_text")) return "TitleNewGameCharacterJobs";
                return "TitleNewGameCharacterNames";
            }
            return null;
        }
    }

    // Strict scene and hierarchy matching. Never classify from displayed words alone.
    internal static class AdditionalFontMatcher
    {
        internal static string Category(Text text)
        {
            if (text == null || text.gameObject == null) return null;
            string scene = text.gameObject.scene.name;
            if (scene == "SoundUi") return "MusicPlayerText";
            if (scene == "GalleryUi" || scene == "GalleryDetailsUi") return "GalleryText";
            if (scene == "LibraryDungeonUi" || scene == "MenuLibraryDungeonUi") return "BestiaryCommands";
            if (scene != "CampMenu" && scene != "TitleScreen") return null;
            string path = ContextualPath.Get(text.transform);
            if (scene == "CampMenu")
            {
                if (path.Contains("/SubMenuParent/status_details(Clone)/battle_commands/") &&
                    (path.EndsWith("/command1") || path.EndsWith("/command2") || path.EndsWith("/command3") || path.EndsWith("/command4")))
                    return "StatusBattleCommands";
                return null;
            }
            if (path.Contains("/title(Clone)/content_root/start/last_text")) return "TitlePrompts";
            if (path.Contains("/title(Clone)/content_root/menu/"))
            {
                if (path.Contains("/main_menu/title_command_content(Clone)/last_text")) return "TitleMenuCommands";
                if ((path.Contains("/sub_menu/") || path.Contains("/option_menu/") || path.Contains("/config_menu/")) &&
                    path.EndsWith("/title_command_content(Clone)/last_text")) return "TitleSubmenuCommands";
            }
            if (path.Contains("/new_game(Clone)/") &&
                (path.Contains("/job_list_window/job_contents/jobList/job_content(Clone)/root/command_content/root/last_text") ||
                 path.Contains("/select_list_window/chara_contents/") || path.Contains("/name_list_window/name_contents/Text")))
                return "TitleNewGame";
            if (path.Contains("/loadGame(Clone)/") &&
                (path.EndsWith("/menu_name_window/last_text") || path.Contains("/save_slot(Clone)/"))) return "TitleLoadGame";
            if (path.Contains("/option(Clone)/main_window/config_root/") &&
                path.EndsWith("/config_tool_command/root/command_name/last_text")) return "TitleConfiguration";
            return null;
        }
    }

    // v1.6: Targeted retains v1.5 behavior. AllMenus is opt-in and does not scan the scene.
    internal static class NumericAutoFit
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();
        internal static void Apply(Text text)
        {
            if (text == null || text.gameObject == null) return;
            try
            {
                string mode = Plugin.FitMode.Value.Trim();
                if (mode.Equals("Off", StringComparison.OrdinalIgnoreCase)) return;
                string scene = text.gameObject.scene.name;
                bool broad = mode.Equals("AllMenus", StringComparison.OrdinalIgnoreCase);
                if (!broad && !scene.Equals("CampMenu") && !scene.Equals("BattleMenu") && !Plugin.FitOtherNumericFields.Value) return;
                if (broad && !IsMenuScene(scene)) return;
                string path = ContextualPath.Get(text.transform);
                string category = null;
                if (broad)
                {
                    string exclusion = ExclusionReason(path, text);
                    if (exclusion != null) { Report(scene, path, "excluded:" + exclusion); return; }
                    if (text.resizeTextForBestFit) { Report(scene, path, "already-enabled"); return; }
                    category = "AllMenus";
                }
                else if (scene == "CampMenu")
                {
                    if (Plugin.FitCampLevels.Value && path.Contains("/SubMenuParent/status_info(Clone)/") &&
                        path.EndsWith("/level_text", StringComparison.Ordinal)) category = "CampLevels";
                    else if (Plugin.FitItemQuantities.Value && path.Contains("/SubMenuParent/item_info(Clone)/root/list_window/") &&
                        path.EndsWith("/item_list_content/count_text", StringComparison.Ordinal)) category = "ItemQuantities";
                    else if (Plugin.FitCampMagicLevels.Value && path.Contains("/SubMenuParent/ability_info(Clone)/root/root/ability_list_window/") &&
                        path.EndsWith("/ability_select_content_list(Clone)/root/lv/last_text", StringComparison.Ordinal)) category = "CampMagicLevels";
                }
                else if (scene == "BattleMenu")
                {
                    if (Plugin.FitBattleMagicLevels.Value && path.Contains("/ability_root/ability_info_window(Clone)/") &&
                        path.EndsWith("/ability_content(Clone)/root/lv/last_text", StringComparison.Ordinal)) category = "BattleMagicLevels";
                    else if (Plugin.FitBattleItemQuantities.Value && path.Contains("/item_info_window/") &&
                        path.EndsWith("/item_select_content(Clone)/root/count_text", StringComparison.Ordinal)) category = "BattleItemQuantities";
                }
                if (category == null && !broad && Plugin.FitOtherNumericFields.Value &&
                    IsMenuScene(scene) && ExclusionReason(path, text) == null &&
                    IsNumericFieldPath(path))
                    category = "OtherNumericFields";
                if (category == null) return;
                int max = Math.Max(1, text.fontSize);
                if (text.resizeTextForBestFit && text.resizeTextMaxSize > max)
                    max = text.resizeTextMaxSize;
                int min = Math.Min(max, Math.Max(2, Math.Min(32, Plugin.FitMinimumSize.Value)));
                if (broad && (text.rectTransform.rect.width <= 0 || text.rectTransform.rect.height <= 0))
                { Report(scene, path, "excluded:empty-rect"); return; }
                if (!text.resizeTextForBestFit || (!broad && (text.resizeTextMinSize != min || text.resizeTextMaxSize != max)))
                {
                    text.resizeTextMinSize = min;
                    text.resizeTextMaxSize = max;
                    text.resizeTextForBestFit = true;
                    if (broad) Report(scene, path, "enabled:min=" + min + ":max=" + max);
                    else if (Plugin.BasicDiagnostics && Reported.Add(category)) Plugin.Logger.LogInfo("[ContextualFonts][AutoFit] Enabled " + category +
                        " min=" + min + " max=" + max + " path='" + path + "'");
                }
            }
            catch (Exception ex) { if (Reported.Add("error:" + ex.GetType().Name))
                Plugin.Logger.LogWarning("[ContextualFonts][AutoFit] " + ex.GetType().Name + ": " + ex.Message); }
        }
        private static bool IsNumericFieldPath(string path)
        {
            string p = path.ToLowerInvariant();
            return p.Contains("price") || p.Contains("/have") || p.Contains("/stock") ||
                p.Contains("count_text") || p.Contains("quantity") || p.Contains("/gil") ||
                p.Contains("/level_text") || p.Contains("/lv/") || p.Contains("/exp") ||
                p.Contains("/hp") || p.Contains("/mp") || p.Contains("current_hp") || p.Contains("max_hp") || p.Contains("next_exp") || p.Contains("current_mp") || p.Contains("max_mp") || p.Contains("job_level") || p.Contains("abp") || p.Contains("duration") ||
                p.Contains("/min/") || p.Contains("/sec/") ||
                (p.Contains("/playinfo/time/") &&
                 (p.EndsWith("/usertimetext") || p.EndsWith("/usertimetext (2)"))) ||
                (p.Contains("/time/") && (p.EndsWith("/time") || p.Contains("/time ("))) || p.Contains("track_number") ||
                p.Contains("entry_number") || p.Contains("item_num") ||
                p.Contains("/shop_gil/root/value") ||
                (p.Contains("/gps_menu_base(clone)/") && p.Contains("/info/root/chest/") &&
                 (p.EndsWith("/num") || p.EndsWith("/max"))) ||
                (p.Contains("/camp") && (p.Contains("/time/") || p.Contains("play_time")));
        }
        private static bool IsMenuScene(string scene)
        {
            return scene == "CampMenu" || scene == "BattleMenu" || scene == "TitleScreen" ||
                scene == "MessageWindow" || scene == "ArBattleMessageWindow" || scene.IndexOf("Bestiary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                scene.IndexOf("Monster", StringComparison.OrdinalIgnoreCase) >= 0 ||
                scene.IndexOf("Library", StringComparison.OrdinalIgnoreCase) >= 0 ||
                scene.IndexOf("Shop", StringComparison.OrdinalIgnoreCase) >= 0 || scene.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0 || scene.IndexOf("Config", StringComparison.OrdinalIgnoreCase) >= 0 || scene == "SoundUi" || scene == "GalleryUi" || scene == "GalleryDetailsUi";
        }
        private static string ExclusionReason(string path, Text text)
        {
            // Hierarchy-based exclusion, never by current text (a one-digit field may grow).
            string p = path.ToLowerInvariant();
            if (p.Contains("copy_right_root")) return "copyright";
            if (p.Contains("/arrow") || p.Contains("arrow/") || p.Contains("cursor") ||
                p.Contains("/icon") || p.Contains("icon/") || p.Contains("button_icon") ||
                p.Contains("key_guide") || p.Contains("button_guide") || p.Contains("/guide/") ||
                p.Contains("/navigation/") || p.Contains("/scrollbar") || p.Contains("/scroll_bar"))
                return "decorative-path";
            if (text.fontSize <= 0) return "invalid-size";
            return null;
        }
        private static void Report(string scene, string path, string decision) { }
    }

    // Diagnostics only. Logs Text layout properties; never changes font size, fitting, or rectangles.


}
