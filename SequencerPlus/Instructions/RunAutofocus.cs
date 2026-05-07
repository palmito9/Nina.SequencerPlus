#region "copyright"

/*
    Copyright © 2016 - 2024 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Validations;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Core.Utility.WindowService;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Model.Equipment;
using NINA.Core.Locale;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.Interfaces;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.Container;
using NINA.WPF.Base.Utility.AutoFocus;

namespace NINA.Sequencer.SequenceItem.Autofocus {

    [ExportMetadata("Name", "Run Autofocus +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Autofocus_RunAutofocus_Description")]
    [ExportMetadata("Icon", "AutoFocusSVG")]
    [ExportMetadata("Category", "Sequencer+ (Test)")]
    //[Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class RunAutofocus : SequenceItem, IValidatable {
        private IProfileService profileService;
        private IImageHistoryVM history;
        private ICameraMediator cameraMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IFocuserMediator focuserMediator;
        private IAutoFocusVMFactory autoFocusVMFactory;

        [ImportingConstructor]
        public RunAutofocus(
            IProfileService profileService, IImageHistoryVM history, ICameraMediator cameraMediator, IFilterWheelMediator filterWheelMediator, IFocuserMediator focuserMediator, IAutoFocusVMFactory autoFocusVMFactory) {
            this.profileService = profileService;
            this.history = history;
            this.cameraMediator = cameraMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.focuserMediator = focuserMediator;
            this.autoFocusVMFactory = autoFocusVMFactory;
        }

        private RunAutofocus(RunAutofocus cloneMe) : this(cloneMe.profileService, cloneMe.history, cloneMe.cameraMediator, cloneMe.filterWheelMediator, cloneMe.focuserMediator, cloneMe.autoFocusVMFactory) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new RunAutofocus(this);
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public IWindowServiceFactory WindowServiceFactory { get; set; } = new WindowServiceFactory();

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            AutoFocusReport report = new AutoFocusReport() {
                Timestamp = DateTime.Now,
                StarDetectorName = "Sim",
                AutoFocuserName = "Sim",
                Duration = TimeSpan.FromSeconds(60),
            };
            history.AppendAutoFocusPoint(report);
            Notification.ShowInformation("Test Autofocus Run completed");
        }

        public bool Validate() {
            var i = new List<string>();
            if (!cameraMediator.GetInfo().Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            }
            if (!focuserMediator.GetInfo().Connected) {
                i.Add(Loc.Instance["LblFocuserNotConnected"]);
            }

            Issues = i;
            return issues.Count == 0;
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override TimeSpan GetEstimatedDuration() {
            return TimeSpan.FromSeconds(60);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(RunAutofocus)}";
        }
    }
}