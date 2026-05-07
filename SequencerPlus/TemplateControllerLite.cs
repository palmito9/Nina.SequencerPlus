#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Model;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Serialization;
using NINA.Core.Utility;
using NINA.Astrometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using NINA.Core.Utility.Notification;
using NINA.Core.Locale;
using System.Threading.Tasks;
using NINA.Sequencer;
using System.Data.Entity.Migrations.Model;
using System.Diagnostics;

namespace NINA.Plugin.SequencerPlus {

    public class TemplateControllerLite {
        private readonly SequenceJsonConverter sequenceJsonConverter;
        private readonly IProfileService profileService;
        private readonly string defaultTemplatePath;
        private FileSystemWatcher sequenceTemplateFolderWatcher;
        private string userTemplatePath;
        public const string DefaultTemplatesGroup = "LblTemplate_DefaultTemplates";
        private const string UserTemplatesGroup = "LblTemplate_UserTemplates";
        public const string TemplateFileExtension = ".template.json";
        private ISequenceSettings activeSequenceSettings;
        public static Object TemplateLock = new object();

        public IList<NINA.Sequencer.TemplatedSequenceContainer> UserTemplates => Templates.Where(t => t.Group == UserTemplatesGroup).ToList();

        public IList<TemplatedSequenceContainer> Templates { get; }
        public IList<TemplatedSequenceContainer> TBRTemplates { get; set; }


        public TemplateControllerLite(SequenceJsonConverter sequenceJsonConverter, IProfileService profileService) {
            lock (TemplateLock) {
                this.sequenceJsonConverter = sequenceJsonConverter;
                this.profileService = profileService;
                defaultTemplatePath = Path.Combine(NINA.Core.Utility.CoreUtil.APPLICATIONDIRECTORY, "Sequencer", "Examples");

                Templates = new List<TemplatedSequenceContainer>();
                try {
                    if (!Directory.Exists(defaultTemplatePath)) {
                        Directory.CreateDirectory(defaultTemplatePath);
                    }
                    foreach (var file in Directory.GetFiles(defaultTemplatePath, "*" + TemplateFileExtension)) {
                        try {
                            var container = sequenceJsonConverter.Deserialize(File.ReadAllText(file)) as ISequenceContainer;
                            if (container is ISequenceRootContainer) continue;
                            Templates.Add(new TemplatedSequenceContainer(profileService, DefaultTemplatesGroup, container));
                        } catch (Exception ex) {
                            Logger.Error("Invalid template JSON", ex);
                        }
                    }
                    TBRTemplates = new List<TemplatedSequenceContainer>();
                    foreach (var template in Templates) {
                        TBRTemplates.Add(template);
                    }

                } catch (Exception ex) {
                    Logger.Error("Error occurred while loading default templates", ex);
                }

                LoadUserTemplates().ContinueWith(t => {
                    sequenceTemplateFolderWatcher = new FileSystemWatcher(profileService.ActiveProfile.SequenceSettings.SequencerTemplatesFolder, "*" + TemplateFileExtension);
                    sequenceTemplateFolderWatcher.Changed += SequenceTemplateFolderWatcher_Changed;
                    sequenceTemplateFolderWatcher.Deleted += SequenceTemplateFolderWatcher_Changed;
                    sequenceTemplateFolderWatcher.IncludeSubdirectories = true;
                    sequenceTemplateFolderWatcher.EnableRaisingEvents = true;

                    profileService.ProfileChanged += ProfileService_ProfileChanged;
                    activeSequenceSettings = profileService.ActiveProfile.SequenceSettings;
                    activeSequenceSettings.PropertyChanged += SequenceSettings_SequencerTemplatesFolderChanged;
                });
            }
        }

        private async void SequenceTemplateFolderWatcher_Changed(object sender, FileSystemEventArgs e) {
            try {
                sequenceTemplateFolderWatcher.EnableRaisingEvents = false;
                await LoadUserTemplates();
                Updated = true;
                UpdatedFile = e.Name;
            } finally {
                sequenceTemplateFolderWatcher.EnableRaisingEvents = true;
            }
        }

        private bool updated = false;

        public string UpdatedFile { get; set; }

        public bool Updated {
            get {
                if (updated) {
                    updated = false;
                    return true;
                }
                return updated;
            }
            set {
                updated = value;
            }
        }

        private async void SequenceSettings_SequencerTemplatesFolderChanged(object sender, System.EventArgs e) {
            if ((e as PropertyChangedEventArgs)?.PropertyName == nameof(profileService.ActiveProfile.SequenceSettings.SequencerTemplatesFolder)) {
                sequenceTemplateFolderWatcher.Path = profileService.ActiveProfile.SequenceSettings.SequencerTemplatesFolder;
                try {
                    sequenceTemplateFolderWatcher.EnableRaisingEvents = false;
                    await LoadUserTemplates();
                } finally {
                    sequenceTemplateFolderWatcher.EnableRaisingEvents = true;
                }
            }
        }

        private async void ProfileService_ProfileChanged(object sender, System.EventArgs e) {
            activeSequenceSettings.PropertyChanged -= SequenceSettings_SequencerTemplatesFolderChanged;
            activeSequenceSettings = profileService.ActiveProfile.SequenceSettings;
            activeSequenceSettings.PropertyChanged += SequenceSettings_SequencerTemplatesFolderChanged;
            try {
                sequenceTemplateFolderWatcher.EnableRaisingEvents = false;
                await LoadUserTemplates();
            } finally {
                sequenceTemplateFolderWatcher.EnableRaisingEvents = true;
            }
        }

        private Task LoadUserTemplates() {
            return Task.Run(async () => {
                try {
                    userTemplatePath = profileService.ActiveProfile.SequenceSettings.SequencerTemplatesFolder;
                    var rootParts = userTemplatePath.Split(new char[] { Path.DirectorySeparatorChar }, System.StringSplitOptions.RemoveEmptyEntries);

                    if (!Directory.Exists(userTemplatePath)) {
                        Directory.CreateDirectory(userTemplatePath);
                    }

                    foreach (var template in Templates.Where(t => t.Group != DefaultTemplatesGroup).ToList()) {
                        await Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => Templates.Remove(template)));
                    }

                    foreach (var file in Directory.GetFiles(userTemplatePath, "*" + TemplateFileExtension, SearchOption.AllDirectories)) {
                        try {
                            var container = sequenceJsonConverter.Deserialize(File.ReadAllText(file));
                            if (container is ISequenceRootContainer) continue;
                            var template = new NINA.Sequencer.TemplatedSequenceContainer(profileService, UserTemplatesGroup, container);
                            var fileInfo = new FileInfo(file);
                            container.Name = fileInfo.Name.Replace(TemplateFileExtension, "");
                            var parts = fileInfo.Directory.FullName.Split(new char[] { Path.DirectorySeparatorChar }, System.StringSplitOptions.RemoveEmptyEntries);
                            template.SubGroups = parts.Except(rootParts).ToArray();
                            Templates.Add(template);
                        } catch (Exception ex) {
                            Logger.Error("Invalid template JSON", ex);
                        }
                    }

                    Updated = true;
                    lock (TemplateLock) {
                        TBRTemplates = new List<TemplatedSequenceContainer>();
                        foreach (var template in Templates) {
                            TBRTemplates.Add(template);
                        }
                    }


                } catch (Exception ex) {
                    Logger.Error(ex);
                    Notification.ShowError(Loc.Instance["Lbl_SequenceTemplateController_LoadUserTemplatesFailed"]);
                }
            });
        }
     }
}