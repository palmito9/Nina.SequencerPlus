using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("9075c999-dacb-4c24-9e14-b696a1ac9e89")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin

// Odd minor releases for Beta
[assembly: AssemblyVersion("3.29.0.1")]
[assembly: AssemblyFileVersion("3.29.0.1")]

// [MANDATORY] The name of your plugingit st
[assembly: AssemblyTitle("Sequencer Powerups")]
// [MANDATORY] A short description of your plugin
//[assembly: AssemblyDescription("*** BETA RELEASE ***")]
[assembly: AssemblyDescription("Get the most out of the Advanced Sequencer!")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("Marc Blank")]
// The product name that this plugin is part ofgit 
[assembly: AssemblyProduct("When")] 
[assembly: AssemblyCopyright("Copyright © 2023-5 Marc Blank")]

// The minimum Version of N.I.N.A. that this plugin is compatible withq
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/marcblank/nina.plugin.when")]

// The following attributes are optional for the official manifest meta data

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Sequencer,Utility,Powerups,Constants,Variables,Expressions,Safety,Interrupt,If,When")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://github.com/marcblank/nina.plugin.when/blob/newdevelop/images/Powerups.png?raw=true")]
[assembly: AssemblyMetadata("ScreenshotURL", "https://bitbucket.org/zorkmid/nina.plugin.when/downloads/LoopWhile.png")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://1drv.ms/u/s!AjBSqKNCEWOTgfIGHf3eIXv2hZfYAw?e=LLHMJF")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"## This plugin contains a variety of useful instructions that enhance the power of the Advanced Sequencer.  The set of these instructions is expected to increase over time; consider them 'utility' instructions.  Many of these instructions allow you to take arbitrary sets of actions when specific circumstances arise; you specify these actions by dragging instructions into place, just as you would to create any instruction set or template.

## Among the more powerful Powerups are those related to Constants, Variables, and Expressions, and the Template by Reference instruction.


# Complete documentation for Sequencer Powerups is at [Powerups Docs](https://marcblank.github.io)

## Comments, suggestions, bug reports, etc. are welcomed!  Contact me by DM @chatter on the NINA Discord server, or post in the #sequencer-powerups channel.
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