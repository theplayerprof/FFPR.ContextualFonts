# FFPR Contextual Fonts

Version 1.0.10 — initial public release

A BepInEx plugin for the Pixel Remasters that allows you to set different types of interface text to use either Classic or Modernized fonts. Font choices can be configured separately for dialogue, descriptions, battle text, menus, shops, and other UI categories, with additional game-specific categories for things that only appear in certain games (like FFII's keyword system or FFV's abilities).

This was inspired by the SNES-era games (and PSX-era remakes), which had one 8x8 font used in menus and an 8x16 font used for dialogue/descriptions/etc. I wanted to be able to replicate that distinction.

The other main feature of CF is auto-fitting of fonts, which helps ensure that text and numbers fit their available space, even for oversized fonts. (In vanilla, this can be an issue for fonts with particularly wide numbers, or for mods that increase HP to four digits, or that otherwise go wider than the PR planned for.) It will resize those labels if you have that feature turned on.

## Installation

1. Install BepinEx. (If you need a guide to how to install it, check out my tutorial at https://www.youtube.com/watch?v=qA6bdz_St-I!)
2. Place `FFPR.ContextualFonts.dll` in the game's `BepInEx/plugins` folder.
3. Start the game once. The plugin generates `BepInEx/config/ffpr.contextualfonts.cfg` and `BepInEx/config/ffpr.contextualfonts.diagnostics.cfg`.
4. Close the game and edit the configuration files as desired, then relaunch.

## Configuration

The main configuration file (`ffpr.contextualfonts.cfg`) controls font choices (under `[Fonts]`), game-specific categories (under sections such as `[Fonts.FF5]`), and font fitting (under `[FontFitting]`).

For the font selections:
- **Classic** selects the font defined as "Classic."
- **Modernized** selects the font defined as "Modernized."
- **Default** leaves the game's existing font alone. (This means it will by default take on either whichever font you have set as your font in Configuration. If you are using other mods' font assignments, such as KupoUI's font replacement feature, it should leave that as-is.)
- **Inherit** means the category will take on the setting of its parent category.

The fonts used for the Classic and Modernized fonts are those taken from the font_en.bundle and font_ja.bundle files. (The former handles text, at least in Latin-character languages, and the latter handles numbers.) This means that if you have your fonts replaced, it will in fact use your new fonts. (I have not tested using old font packages that have a single font, only ones that have the Classic and Modernized combo of fonts; use it with those old packages at your own risk, and know that it might crash!)

For an example of use, set `Description = Modernized` and `BattleDescriptions = Modernized` under `[Fonts]` to ensure general and battle descriptions appear using the Modernized font, whether your main font is set to Modernized or Classic. (If you select "Inherit" for BattleDescriptions, you should have the same result, as Description is a parent for BattleDescriptions.) Game-specific settings, such as FFII's keyword descriptions or FFV's ability descriptions, can be configured separately in the game-specific Fonts sections. The available categories and their parent relationships are documented in the generated config file.

For font fitting:

`[FontFitting]` includes fitting controls both broad and specific (largely numbers), such as magic levels on the camp menu and in battle or item quantities. This works independently of the font-setting features; you can use KupoUI to change your font, and then have everything set to "Default" and have FontFitting turned on, and the KupoUI font will be properly fit to its fields.

There's a separate diagnostics configuration file (`ffpr.contextualfonts.diagnostics.cfg`) that is for advanced users, and most people won't need to use it. It contains optional `TextProbe` (to search text strings) and `Hierarchy` (to inspect UI text under named window categories). Normally, leave both disabled unless you're looking for something specific. Both of these tools write their findings to the BepInEx log.

## Compatibility and limitations

- Font availability varies depending on the language. Font settings will only work with those languages that support separate Classic and Modernized fonts in the PR system. (This includes Japanese, English, French, German, Spanish, and Portuguese. Russian, Thai, Korean, and both Simplified and Traditional Chinese have a single font setting each, and so the Fonts side of this plugin is not supported in those languages. The FontFitting should still work, though.)
- Interactions with other UI/font mods may vary; `Default` is the safest choice for categories another mod controls. (I've tested some with KupoUI, but not any other font mods.)
- I've tested what I could, but my PC saves of some of these games aren't very far (even if I platinumed all the PRs on console). This means that some things, like FFVI's Esper menu, I couldn't verify works in-game. Some game-specific or unusual UI text may not yet be working. If you run into that, let me know!
- This plugin does not supply new fonts. It just works with the ones you already have.

## Troubleshooting

If changes do not appear, confirm the plugin loaded in `BepInEx/LogOutput.log`, check the relevant font category and inheritance settings, and restart the game after editing configuration. For an unidentified text field, enable `TextProbe` or `Hierarchy` temporarily and inspect the log.

See `CHANGELOG.md` for the initial public release notes.
