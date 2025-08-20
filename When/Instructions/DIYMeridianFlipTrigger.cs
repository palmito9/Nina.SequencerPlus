using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using NINA.Astrometry;
using NINA.Equipment.Interfaces.Mediator;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using NINA.Core.Enum;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Sequencer.Interfaces;
using NINA.WPF.Base.Interfaces;
using System.Globalization;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.SequenceItem.Platesolving;
using System.Windows.Media;
using System.Windows;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Equipment.Interfaces;
using NINA.PlateSolving.Interfaces;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Core.Utility.Notification;
using NINA.Core.Locale;
using NINA.Profile;

namespace WhenPlugin.When {

    [ExportMetadata("Name", "DIY Meridian Flip Trigger")]
    [ExportMetadata("Description", "Trigger for DYI Meridian Flip")]
    [ExportMetadata("Icon", "MeridianFlipSVG")]
    [ExportMetadata("Category", "Powerups (Meridian Flip)")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]

    public class DIYMeridianFlipTrigger : SequenceTrigger, IMeridianFlipTrigger, IValidatable, IDSOTargetProxy {
        protected IProfileService profileService;
        protected ITelescopeMediator telescopeMediator;
        protected IApplicationStatusMediator applicationStatusMediator;
        protected ICameraMediator cameraMediator;
        protected IFocuserMediator focuserMediator;
        protected IMeridianFlipVMFactory meridianFlipVMFactory;
        protected IGuiderMediator guiderMediator;
        protected IImageHistoryVM history;
        protected IFilterWheelMediator filterWheelMediator;
        protected IAutoFocusVMFactory autoFocusVMFactory;
        protected IDomeMediator domeMediator;
        protected IDomeFollower domeFollower;
        protected IPlateSolverFactory plateSolverFactory;
        protected IWindowServiceFactory windowServiceFactory;
        protected IImagingMediator imagingMediator;


        private GeometryGroup GuiderIcon = (GeometryGroup)Application.Current.Resources["GuiderSVG"];
        private GeometryGroup MeridianFlipIcon = (GeometryGroup)Application.Current.Resources["MeridianFlipSVG"];
        private GeometryGroup CameraIcon = (GeometryGroup)Application.Current.Resources["CameraSVG"];
        private GeometryGroup PlatesolveIcon = (GeometryGroup)Application.Current.Resources["PlatesolveSVG"];
        private GeometryGroup HourglassIcon = (GeometryGroup)Application.Current.Resources["HourglassSVG"];
        protected DateTime lastFlipTime = DateTime.MinValue;
        protected Coordinates lastFlipCoordiantes;

        static public DIYMeridianFlipTrigger INSTANCE = null;

        static public bool SequenceRunning = false;

        [ImportingConstructor]
        public DIYMeridianFlipTrigger(IProfileService profileService, ICameraMediator cameraMediator, ITelescopeMediator telescopeMediator,
            IFocuserMediator focuserMediator, IApplicationStatusMediator applicationStatusMediator, IMeridianFlipVMFactory meridianFlipVMFactory,
            IGuiderMediator guiderMediator, IImageHistoryVM history, IFilterWheelMediator filterWheelMediator, IAutoFocusVMFactory autoFocusVMFactory,
            IDomeMediator domeMediator, IDomeFollower domeFollower, IPlateSolverFactory plateSolverFactory, IWindowServiceFactory windowServiceFactory,
            IImagingMediator imagingMediator) : base() {
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            this.cameraMediator = cameraMediator;
            this.focuserMediator = focuserMediator;
            this.meridianFlipVMFactory = meridianFlipVMFactory;
            this.guiderMediator = guiderMediator;
            this.history = history;
            this.filterWheelMediator = filterWheelMediator;
            this.autoFocusVMFactory = autoFocusVMFactory;
            this.domeMediator = domeMediator;
            this.domeFollower = domeFollower;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.imagingMediator = imagingMediator;
            Name = Name;
            Icon = Icon;
            FlipStatus = "Waiting for a NINA sequence to start...";
            TriggerRunner = new IfContainer();
            AddItem(TriggerRunner, new StopGuiding(guiderMediator) { Name = "Stop Guiding", Icon = GuiderIcon }); ;
            AddItem(TriggerRunner, new PassMeridian(telescopeMediator, profileService) { Name = "Wait to Pass Meridian", Icon = MeridianFlipIcon });
            AddItem(TriggerRunner, new DoFlip(telescopeMediator, domeMediator, domeFollower) { Name = "Flip Scope", Icon = MeridianFlipIcon });
            AddItem(TriggerRunner, new WaitForTimeSpan() { Name = "Settle (Wait for Time Span)", Icon = HourglassIcon, Time = 10 });
            AddItem(TriggerRunner, new RunAutofocus(profileService, history, cameraMediator, filterWheelMediator, focuserMediator,
                autoFocusVMFactory) { Name = "Run Autofocus", Icon = CameraIcon });
            NINA.Sequencer.SequenceItem.Platesolving.Center c = new(profileService, telescopeMediator, imagingMediator, filterWheelMediator, guiderMediator,
                domeMediator, domeFollower, plateSolverFactory, windowServiceFactory) { Name = "Slew and center", Icon = PlatesolveIcon };
            AddItem(TriggerRunner, c);
            AddItem(TriggerRunner, new StartGuiding(guiderMediator) { Name = "Start Guiding", Icon = GuiderIcon });
            AddItem(TriggerRunner, new WaitForTimeSpan() { Name = "Settle (Wait for Time Span)", Icon = HourglassIcon, Time = 5 });

            PauseTimeBeforeMeridian = profileService.ActiveProfile.MeridianFlipSettings.PauseTimeBeforeMeridian;
            MaxMinutesAfterMeridian = profileService.ActiveProfile.MeridianFlipSettings.MaxMinutesAfterMeridian;
            MinutesAfterMeridian = profileService.ActiveProfile.MeridianFlipSettings.MinutesAfterMeridian;
        }

        private void AddItem(SequentialContainer runner, ISequenceItem item) {
            runner.Items.Add(item);
            item.AttachNewParent(runner);
        }

        private DIYMeridianFlipTrigger(DIYMeridianFlipTrigger copyMe) : this(copyMe.profileService,
                                                               copyMe.cameraMediator,
                                                               copyMe.telescopeMediator,
                                                               copyMe.focuserMediator,
                                                               copyMe.applicationStatusMediator,
                                                               copyMe.meridianFlipVMFactory,
                                                               copyMe.guiderMediator,
                                                               copyMe.history,
                                                               copyMe.filterWheelMediator,
                                                               copyMe.autoFocusVMFactory,
                                                               copyMe.domeMediator,
                                                               copyMe.domeFollower,
                                                               copyMe.plateSolverFactory,
                                                               copyMe.windowServiceFactory,
                                                               copyMe.imagingMediator) {
            CopyMetaData(copyMe);
            Name = copyMe.Name;
            Icon = copyMe.Icon;
            FlipStatus = copyMe.FlipStatus;
            // Fix for crash; unsure how we get here...
            TriggerRunner = (IfContainer)copyMe.TriggerRunner.Clone();
            TriggerRunner.AttachNewParent(Parent);
            ((IfContainer)TriggerRunner).PseudoParent = this;

            PauseTimeBeforeMeridian = copyMe.PauseTimeBeforeMeridian;
            MaxMinutesAfterMeridian = copyMe.MaxMinutesAfterMeridian;
            MinutesAfterMeridian = copyMe.MinutesAfterMeridian;
        }

        public override object Clone() {
            return new DIYMeridianFlipTrigger(this) {
            };
        }

        [JsonProperty]
        public String FlipStatus { get; set; }

        public bool InFlight { get; set; }

        protected IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = ImmutableList.CreateRange(value);
                RaisePropertyChanged();
            }
        }

        protected DateTime latestFlipTime;
        protected DateTime earliestFlipTime;

        private double minutesAfterMeridian;

        public ISequenceContainer TriggerContext { get; set; }

        [JsonProperty]
        public virtual double MinutesAfterMeridian {
            get => minutesAfterMeridian;
            set {
                minutesAfterMeridian = value;
                RaisePropertyChanged();
            }
        }

        private double pauseTimeBeforeMeridian;

        [JsonProperty]
        public virtual double PauseTimeBeforeMeridian {
            get => pauseTimeBeforeMeridian;
            set {
                pauseTimeBeforeMeridian = value;
                RaisePropertyChanged();
            }
        }

        private double maxMinutesAfterMeridian;

        [JsonProperty]
        public virtual double MaxMinutesAfterMeridian {
            get => maxMinutesAfterMeridian;
            set {
                maxMinutesAfterMeridian = value;
                RaisePropertyChanged();
            }
        }

        public virtual bool UseSideOfPier {
            get {
                return profileService.ActiveProfile.MeridianFlipSettings.UseSideOfPier;
            }
            set { }
        }

        public virtual DateTime LatestFlipTime {
            get => latestFlipTime;
            protected set {
                latestFlipTime = value;
                RaisePropertyChanged();
            }
        }

        public virtual DateTime EarliestFlipTime {
            get => earliestFlipTime;
            protected set {
                earliestFlipTime = value;
                RaisePropertyChanged();
            }
        }

        MeridianFlipSettings MFSettings = new();

        public virtual double TimeToMeridianFlip {
            get {
                TelescopeInfo info = telescopeMediator.GetInfo();
                try {
                    if (info.TrackingEnabled) {
                        MFSettings.MinutesAfterMeridian = MinutesAfterMeridian;
                        MFSettings.MaxMinutesAfterMeridian = MaxMinutesAfterMeridian;
                        MFSettings.PauseTimeBeforeMeridian = PauseTimeBeforeMeridian;
                        return NINA.Astrometry.MeridianFlip.TimeToMeridianFlip(
                            settings: MFSettings,
                            coordinates: info.Coordinates,
                            localSiderealTime: Angle.ByHours(info.SiderealTime),
                            currentSideOfPier: info.SideOfPier).TotalHours;
                    }
                } catch (Exception ex) {
                    Logger.Error(ex);
                    Notification.ShowExternalError(ex.Message, Loc.Instance["LblASCOMDriverError"]);
                }
                return 24;
            }
            set { }
        }

        public override void AfterParentChanged() {
            lastFlipTime = DateTime.MinValue;
            lastFlipCoordiantes = null;
            foreach (ISequenceItem item in TriggerRunner.Items) {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            TriggerRunner.AttachNewParent(Parent);
        }

        protected virtual TimeSpan CalculateMinimumTimeRemaining() {
            //Substract delta from maximum to get minimum time
            var delta = MaxMinutesAfterMeridian - MinutesAfterMeridian;
            var time = CalculateMaximumTimeRemainaing() - TimeSpan.FromMinutes(delta);
            if (time < TimeSpan.Zero) {
                time = TimeSpan.Zero;
            }
            return time;
        }

        protected virtual TimeSpan CalculateMaximumTimeRemainaing() {
            return TimeSpan.FromHours(TimeToMeridianFlip);
        }

        protected virtual TimeSpan CalculateTransitTime() {
            return TimeSpan.FromHours(TimeToMeridianFlip) - TimeSpan.FromMinutes(MaxMinutesAfterMeridian);
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            // Don't recurse
            if (InFlight) return false;

            var telescopeInfo = telescopeMediator.GetInfo();
            IMeridianFlipSettings settings = profileService.ActiveProfile.MeridianFlipSettings;

            if (!telescopeInfo.Connected || double.IsNaN(telescopeInfo.TimeToMeridianFlip)) {
                EarliestFlipTime = DateTime.MinValue;
                LatestFlipTime = DateTime.MinValue;
                FlipStatus = "Telescope is not connected.";
                RaisePropertyChanged("FlipStatus");
                Logger.Error(FlipStatus);
                return false;
            }

            if (!telescopeInfo.TrackingEnabled) {
                EarliestFlipTime = DateTime.MinValue;
                LatestFlipTime = DateTime.MinValue;
                FlipStatus = "Telescope is not tracking.";
                RaisePropertyChanged("FlipStatus");
                Logger.Info("Telescope is not tracking. Skip flip evaluation");
                return false;
            }

            CheckTarget();

            // When side of pier is disabled - check if the last flip time was less than 11 hours ago and further check if the current position is similar to the last flip position. If all are true, no flip is required.
            if (UseSideOfPier == false && (DateTime.Now - lastFlipTime) < TimeSpan.FromHours(11) && lastFlipCoordiantes != null && (lastFlipCoordiantes - telescopeInfo.Coordinates).Distance.ArcMinutes < 20) {
                //A flip for the same target is only expected every 12 hours on planet earth and
                FlipStatus = $"Flip for the current target already happened at {TimeString(lastFlipTime)}.";
                RaisePropertyChanged("FlipStatus");
                Logger.Info(FlipStatus);
                return false;
            }

            var nextInstructionTime = nextItem?.GetEstimatedDuration().TotalSeconds ?? 0;

            //The time to meridian flip reported by the telescope is the latest time for a flip to happen
            var minimumTimeRemaining = CalculateMinimumTimeRemaining();
            var maximumTimeRemaining = CalculateMaximumTimeRemainaing();
            var originalMaximumTimeRemaining = maximumTimeRemaining;
            if (PauseTimeBeforeMeridian != 0) {
                //A pause prior to a meridian flip is a hard limit due to equipment obstruction. There is no possibility for a timerange as we have to pause early and wait for meridian to pass
                minimumTimeRemaining = minimumTimeRemaining - TimeSpan.FromMinutes(MinutesAfterMeridian) - TimeSpan.FromMinutes(PauseTimeBeforeMeridian);
                maximumTimeRemaining = minimumTimeRemaining;
            }

            UpdateMeridianFlipTimeTriggerValues(minimumTimeRemaining, originalMaximumTimeRemaining, TimeSpan.FromMinutes(PauseTimeBeforeMeridian), TimeSpan.FromMinutes(MaxMinutesAfterMeridian));

            if (minimumTimeRemaining <= TimeSpan.Zero && maximumTimeRemaining > TimeSpan.Zero) {
                FlipStatus = $"Flip due now through {TimeString(maximumTimeRemaining)}.";
                RaisePropertyChanged("FlipStatus");
                Logger.Info($"Meridian Flip - Remaining Time is between minimum and maximum flip time. Minimum time remaining {minimumTimeRemaining}, maximum time remaining {maximumTimeRemaining}. Flip should happen now");
                return true;
            } else {
                if (UseSideOfPier && telescopeInfo.SideOfPier == PierSide.pierUnknown) {
                    FlipStatus = "Side of pier is unknown; ignoring when calculating the flip time";
                    RaisePropertyChanged("FlipStatus");
                    Logger.Error("Side of Pier usage is enabled, however the side of pier reported by the driver is unknown. Ignoring side of pier to calculate the flip time");
                }

                if (UseSideOfPier && telescopeInfo.SideOfPier != PierSide.pierUnknown) {
                    //The minimum time to flip has not been reached yet. Check if a flip is required based on the estimation of the next instruction
                    var noRemainingTime = maximumTimeRemaining <= TimeSpan.FromSeconds(nextInstructionTime);
                    if (noRemainingTime) {
                        // There is no more time remaining. Project the side of pier to that at the time after the flip and check if this flip is required
                        var projectedSiderealTime = Angle.ByHours(AstroUtil.EuclidianModulus(telescopeInfo.SiderealTime + originalMaximumTimeRemaining.TotalHours, 24));
                        var targetSideOfPier = NINA.Astrometry.MeridianFlip.ExpectedPierSide(
                            coordinates: telescopeInfo.Coordinates,
                            localSiderealTime: projectedSiderealTime);
                        if (telescopeInfo.SideOfPier == targetSideOfPier) {
                            Logger.Info($"Meridian Flip - Telescope already reports expected pier side {telescopeInfo.SideOfPier}. Automated Flip is not necessary.");
                            return false;
                        } else {
                            if (nextItem != null) {
                                Logger.Info($"Meridian Flip - No more remaining time available before flip. Max remaining time {maximumTimeRemaining}, next instruction time {nextInstructionTime}, next instruction {nextItem}. Flip should happen now");
                            } else {
                                Logger.Info($"Meridian Flip - No more remaining time available before flip. Max remaining time {maximumTimeRemaining}. Flip should happen now");
                            }
                            Logger.Info("TTMF: " + TimeToMeridianFlip + ", Dec: " + telescopeInfo.DeclinationString + ", RA: " + telescopeInfo.RightAscensionString);
                            Logger.Info("MTR: " + maximumTimeRemaining + ", NIT: " + TimeSpan.FromSeconds(nextInstructionTime));
                            Logger.Info("Pause: " + PauseTimeBeforeMeridian);
                            FlipStatus = $"Flip sequence start due now through {TimeString(maximumTimeRemaining)}!";
                            RaisePropertyChanged("FlipStatus");
                            Logger.Info(FlipStatus);
                            return true;
                        }
                    } else {
                        // There is still time remaining. A flip is likely not required. Double check by checking the current expected side of pier with the actual side of pier
                        var targetSideOfPier = NINA.Astrometry.MeridianFlip.ExpectedPierSide(
                            coordinates: telescopeInfo.Coordinates,
                            localSiderealTime: Angle.ByHours(telescopeInfo.SiderealTime));
                        if (telescopeInfo.SideOfPier == targetSideOfPier) {
                            string timeDiff = maximumTimeRemaining.ToString("hh\\:mm");
                            string pier =
                                telescopeInfo.SideOfPier == PierSide.pierEast ? "East" :
                                telescopeInfo.SideOfPier == PierSide.pierWest ? "West" :
                                "unknown";
                            if (minimumTimeRemaining == maximumTimeRemaining) {
                                FlipStatus = $"Flip sequence start expected around {TimeString(minimumTimeRemaining)}; transit at {TimeString(CalculateTransitTime())}; telescope side is {pier}.";
                            } else {
                                FlipStatus = $"Flip sequence start expected between {TimeString(minimumTimeRemaining)} and {TimeString(maximumTimeRemaining)}; transit at {TimeString(CalculateTransitTime())}; telescope side is {pier}.";

                            }
                            RaisePropertyChanged("FlipStatus");
                            Logger.Info($"Meridian Flip - There is still time remaining - max remaining time {maximumTimeRemaining}, next instruction time {nextInstructionTime}, next instruction {nextItem} - and the telescope reports expected pier side {telescopeInfo.SideOfPier}. Automated Flip is not necessary.");
                            return false;
                        } else {
                            // When pier side doesn't match the target, but remaining time indicating that a flip happened, the flip seems to have not happened yet and must be done immediately
                            // Only allow delayed flip behavior for the first hour after a flip should've happened
                            var delayedFlip =
                                maximumTimeRemaining <= TimeSpan.FromHours(12)
                                && maximumTimeRemaining
                                    >= (TimeSpan.FromHours(11)
                                        - TimeSpan.FromMinutes(MaxMinutesAfterMeridian)
                                        - TimeSpan.FromMinutes(PauseTimeBeforeMeridian)
                                       );

                            if (delayedFlip) {
                                FlipStatus = $"Flip didn't happen, as Side Of Pier is {telescopeInfo.SideOfPier} but expected to be {targetSideOfPier}. Flip should happen now";
                                RaisePropertyChanged("FlipStatus");
                                Logger.Info("TTMF: " + TimeToMeridianFlip + ", Dec: " + telescopeInfo.DeclinationString + ", RA: " + telescopeInfo.RightAscensionString);
                                Logger.Info("MTR: " + maximumTimeRemaining + ", NIT: " + TimeSpan.FromSeconds(nextInstructionTime));
                                Logger.Info("Pause: " + PauseTimeBeforeMeridian);
                                Logger.Info($"Meridian Flip - Flip seems to not happened in time as Side Of Pier is {telescopeInfo.SideOfPier} but expected to be {targetSideOfPier}. Flip should happen now");
                            }
                            return delayedFlip;
                        }
                    }
                } else {
                    //The minimum time to flip has not been reached yet. Check if a flip is required based on the estimation of the next instruction plus a 2 minute window due to not having side of pier access for dalyed flip evaluation
                    var noRemainingTime = maximumTimeRemaining <= (TimeSpan.FromSeconds(nextInstructionTime) + TimeSpan.FromMinutes(2));

                    if (noRemainingTime) {
                        if (nextItem != null) {
                            FlipStatus = $"Latest sequence start time is  {TimeString(maximumTimeRemaining)}, {nextItem} due at {nextInstructionTime}. Flip should happen now";
                            RaisePropertyChanged("FlipStatus");
                            Logger.Info(FlipStatus);
                        } else {
                            FlipStatus = $"Latest sequence start time is {TimeString(maximumTimeRemaining)}. Flip should happen now!";
                            RaisePropertyChanged("FlipStatus");
                            Logger.Info(FlipStatus);
                        }
                        return true;
                    } else {
                        if (minimumTimeRemaining == maximumTimeRemaining) {
                            FlipStatus = $"Flip sequence start expected around {TimeString(minimumTimeRemaining)}; transit at {TimeString(CalculateTransitTime())}; telescope side unknown.";
                        } else {
                            FlipStatus = $"Flip sequence start expected between {TimeString(minimumTimeRemaining)} and {TimeString(maximumTimeRemaining)}; transit at {TimeString(CalculateTransitTime())}; telescope side unknown.";

                        }
                        RaisePropertyChanged("FlipStatus");
                        Logger.Info($"Meridian Flip - (Side of Pier usage is disabled) There is still time remaining. Max remaining time {maximumTimeRemaining}, next instruction time {nextInstructionTime}, next instruction {nextItem}");
                        return false;
                    }
                }
            }
        }

        private void CheckTarget() {

            InputTarget t = DSOTarget.FindTarget(Parent);
            if (t != null) {
                Target = t;
                SPLogger.Debug("Found Target: " + Target);
                RaisePropertyChanged("Target");
                UpdateChildren(TriggerRunner);
            } else {
                SPLogger.Debug("Running target not found");
            }
        }

        private string TimeString(DateTime min) {
            return min.ToString("T", CultureInfo.CurrentCulture);
        }

        private string TimeString(TimeSpan min) {
            return (DateTime.Now + min).ToString("T", CultureInfo.CurrentCulture);
        }

        protected virtual void UpdateMeridianFlipTimeTriggerValues(TimeSpan minimumTimeRemaining, TimeSpan maximumTimeRemaining, TimeSpan pauseBeforeMeridian, TimeSpan maximumTimeAfterMeridian) {
            //Update the FlipTimes
            if (pauseBeforeMeridian == TimeSpan.Zero) {
                EarliestFlipTime = DateTime.Now + minimumTimeRemaining;
                LatestFlipTime = DateTime.Now + maximumTimeRemaining;
            } else {
                EarliestFlipTime = DateTime.Now + maximumTimeRemaining - maximumTimeAfterMeridian - pauseBeforeMeridian;
                LatestFlipTime = DateTime.Now + maximumTimeRemaining - maximumTimeAfterMeridian - pauseBeforeMeridian;
            }
        }

        public override string ToString() {
            return $"Trigger: DIY Meridian Flip Trigger+";
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            InFlight = true;
            try {
                await TriggerRunner.Run(progress, token);
            } finally {
                InFlight = false;
            }
        }

        public virtual bool Validate() {
            // Validate the Items (this will update their status)
            if (TriggerRunner == null) return true;
            if (!(TriggerRunner is IfContainer)) {
                IfContainer ifc = new IfContainer();
                foreach (ISequenceItem item in TriggerRunner.Items) {
                    ISequenceItem i = (ISequenceItem)item.Clone();
                    ifc.Items.Add(i);
                    i.AttachNewParent(ifc);
                }
                ifc.AttachNewParent(Parent);
                TriggerRunner = ifc;
            }
            ((IfContainer)TriggerRunner).PseudoParent = this;
            bool valid = true;
            foreach (ISequenceItem item in TriggerRunner.Items) {
                if (item is IValidatable vitem) {
                    valid &= vitem.Validate();
                }
            }

            CheckTarget();

            Issues.Clear();
            if (!valid) {
                Issues.Add("Expand the trigger to see the problematic instructions.");
            }
            RaisePropertyChanged("Issues");
            return valid;
        }

        private void UpdateChildren(ISequenceContainer c) {
            foreach (var item in c.Items) {
                item.AfterParentChanged();
            }
        }

        public InputTarget DSOProxyTarget() {
            return Target;
        }

        public InputTarget Target { get; set; }

        public InputTarget FindTarget(ISequenceContainer c) {
            while (c != null) {
                if (c is IDeepSkyObjectContainer dso) {
                    return dso.Target;
                } else {
                    c = c.Parent;
                }
            }
            return null;
        }
    }
}