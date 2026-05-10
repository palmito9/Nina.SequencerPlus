# Sequencer+ — Changelog

## 3.29.0.11 — 2026-05-08

**Bug fix**

- Fixed: Assembly name written into converted sequence files was incorrect, causing Sequencer+ to fail to load those sequences. This affected files produced by the Sequencer Powerups → Sequencer+ conversion tool introduced in 3.29.0.10.

---

## 3.29.0.10 — 2026-05-08 — Community Recovery

This is the first release of **Sequencer+**, a community-maintained continuation of the **Sequencer Powerups** plugin originally developed by Marc Blank and released under the Mozilla Public License 2.0.

After Marc removed the plugin from the N.I.N.A. plugin repository and deleted the original GitHub repository, this project was reconstructed from a local clone of the original repository (v3.29.0.1) and the most recent released binary (v3.29.0.9). Changes introduced between those two versions were recovered by decompiling the v3.29.0.9 binary and diffing it against the cloned source.

The plugin has been rebranded as **Sequencer+** to avoid conflicts with any future reappearance of the original plugin. All assemblies, namespaces, identifiers, and sequence file tags have been renamed accordingly.

**What's included in this release**

- Full feature set of Sequencer Powerups v3.29.0.9: variables, constants, expressions, control flow (If / Loop While / Wait Until / For Each), enhanced instructions (`+` variants), Template by Reference, Functions, Arrays, safety handling (When Becomes Unsafe / Once Safe), custom triggers (DIY Trigger, Autofocus Trigger, Interrupt Trigger), DIY Meridian Flip, External Script+, and additional utility instructions.
- **Conversion tool** in the plugin options panel: migrates existing sequence files, targets, and templates between the Sequencer Powerups format and the Sequencer+ format in either direction (single file or folder, with optional sub-folder traversal).

A complete archive of the original repository as it existed at v3.29.0.1, including all 51 branches, is attached to the initial recovery release on GitHub for historical reference.