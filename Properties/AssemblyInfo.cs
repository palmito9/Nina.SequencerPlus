using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("9bb66de3-b145-433c-b07b-1c432454817a")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin

// Odd minor releases for Beta
[assembly: AssemblyVersion("3.29.0.9")]
[assembly: AssemblyFileVersion("3.29.0.9")]

// [MANDATORY] The name of your plugingit st
[assembly: AssemblyTitle("Sequencer+")]
// [MANDATORY] A short description of your plugin
//[assembly: AssemblyDescription("*** BETA RELEASE ***")]
[assembly: AssemblyDescription("Get the most out of the Advanced Sequencer!")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany(" Carl Björk (Elveteek Sàrl)")]
// The product name that this plugin is part ofgit 
[assembly: AssemblyProduct("SequencerPlus")] 
[assembly: AssemblyCopyright("Copyright © 2026 Elveteek Sàrl - Carl Björk")]

// The minimum Version of N.I.N.A. that this plugin is compatible withq
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/palmito9/Nina.SequencerPlus")]

// The following attributes are optional for the official manifest meta data

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Sequencer,Utility,Constants,Variables,Expressions,Safety,Interrupt,If,When")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://github.com/palmito9/Nina.SequencerPlus/blob/main/images/SequencerPlus_icon.png?raw=true")]
[assembly: AssemblyMetadata("ScreenshotURL", "https://github.com/palmito9/Nina.SequencerPlus/blob/main/images/SequencerPlus_DIYMeridianFlip.png?raw=true")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://github.com/palmito9/Nina.SequencerPlus/blob/main/images/SequencerPlus_WhenUnsafe.png?raw=true")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"## This plugin contains a variety of useful instructions that enhance the power of the Advanced Sequencer.  The set of these instructions is expected to increase over time; consider them 'utility' instructions.  Many of these instructions allow you to take arbitrary sets of actions when specific circumstances arise; you specify these actions by dragging instructions into place, just as you would to create any instruction set or template.

## Among the more powerful Sequencer+ instructions are those related to Constants, Variables, and Expressions, and the Template by Reference instruction.


# Complete documentation for Sequencer+ will be available in the near future

## Comments, suggestions, bug reports, and feature requests are welcomed! You can reach me on the NINA Discord server or use the contact form at https://elveteek.ch. Please note that while I'll do my best to handle bugs, new features, and maintenance, I realistically won't have time for how-to questions — the NINA Discord community will be your best bet there.
")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]