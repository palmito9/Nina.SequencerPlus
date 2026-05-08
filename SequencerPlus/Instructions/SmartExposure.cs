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
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Sequencer.Utility;
using NINA.Sequencer.SequenceItem;
using NINA.Equipment.Equipment.MyFilterWheel;
using NINA.Core.Model;
using NINA.Core.Utility;
using System.Threading;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Smart Exposure +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Imaging_SmartExposure_Description")]
    [ExportMetadata("Icon", "CameraSVG")]
    [ExportMetadata("Category", "Sequencer+ (Enhanced Instructions)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SmartExposure : SequentialContainer, IImmutableContainer {

        private static IProfileService ProfileService;
        private static IFilterWheelMediator FilterWheelMediator;
        private static IImageHistoryVM ImageHistoryVM;
        private static IGuiderMediator GuiderMediator;

        [OnDeserializing]
        public void OnDeserializing(StreamingContext context) {
            this.Items.Clear();
            this.Conditions.Clear();
            this.Triggers.Clear();
        }

        [ImportingConstructor]
        public SmartExposure(
                IProfileService profileService,
                ICameraMediator cameraMediator,
                IImagingMediator imagingMediator,
                IImageSaveMediator imageSaveMediator,
                IImageHistoryVM imageHistoryVM,
                IFilterWheelMediator filterWheelMediator,
                IGuiderMediator guiderMediator) : this(
                    null,
                    new SwitchFilter(profileService, filterWheelMediator),
                    new TakeExposure(profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVM),
                    new LoopCondition(),
                    new DitherAfterExposures(guiderMediator, imageHistoryVM, profileService)
                ) {
            ProfileService = profileService;
            FilterWheelMediator = filterWheelMediator;
            ImageHistoryVM = imageHistoryVM;
            GuiderMediator = guiderMediator;
            IterExpr = new Expr(this, "", "Integer");
            DExpr = new Expr(this, "", "Integer");
            FExpr = new Expr(this, "", "Integer");
            RExpr = new Expr(this, "", "Integer");

            XExpr = new Expr(this, "", "Integer");
            YExpr = new Expr(this, "", "Integer");
            WExpr = new Expr(this, "", "Integer");
            HExpr = new Expr(this, "", "Integer");

        }

        /// <summary>
        /// Clone Constructor
        /// </summary>
        public SmartExposure(
                SmartExposure cloneMe,
                ISequenceItem switchFilter,
                TakeExposure takeExposure,
                LoopCondition loopCondition,
                DitherAfterExposures ditherAfterExposures
                ) {
            this.Add(switchFilter);
            this.Add(takeExposure);
            this.Add(loopCondition);
            this.Add(ditherAfterExposures);

            IsExpanded = false;

            if (cloneMe != null) {
                CopyMetaData(cloneMe);
                // NOTE THAT SETTER MUST BE IN THE CTOR!!
                IterExpr = new Expr(this, cloneMe.IterExpr.Expression, "Integer", SetIterationCount, 1);
                DExpr = new Expr(this, cloneMe.DExpr.Expression, "Integer", SetDitherCount, 0);
                FExpr = new Expr(this, cloneMe.FExpr.Expression, "Integer");
                RExpr = new Expr(this, cloneMe.RExpr.Expression, "Integer", SetROI, 100);
                FilterExpr = cloneMe.FilterExpr;
                
                ROIOption = cloneMe.ROIOption;
                IsROI = cloneMe.IsROI;
                IsDimensions = cloneMe.IsDimensions;

                XExpr = new Expr(this, cloneMe.XExpr.Expression, "Integer", SetX, 0);
                YExpr = new Expr(this, cloneMe.YExpr.Expression, "Integer", SetY, 0);
                WExpr = new Expr(this, cloneMe.WExpr.Expression, "Integer", SetWidth, 0);
                HExpr = new Expr(this, cloneMe.HExpr.Expression, "Integer", SetHeight, 0);

            }
        }

        private InstructionErrorBehavior errorBehavior = InstructionErrorBehavior.ContinueOnError;

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            Validate();
        }

        [JsonProperty]
        public Expr IterExpr { get; set; }
        [JsonProperty]
        public Expr DExpr { get; set; }
        [JsonProperty]
        public Expr FExpr { get; set; }
        [JsonProperty]
        public Expr RExpr { get; set; }
        [JsonProperty]
        public Expr XExpr { get; set; }
        [JsonProperty]
        public Expr YExpr { get; set; }
        [JsonProperty]
        public Expr WExpr { get; set; }
        [JsonProperty]
        public Expr HExpr { get; set; }

        public void SetX(Expr expr) {
            int x = (int)expr.Value;
            TakeExposure te = GetTakeExposure();
            if (te != null && WExpr != null && (x < 0 || (x + WExpr.Value > te.CameraInfo.XSize))) {
                expr.Error = "Invalid dimensions";
            }
            if (WExpr != null) WExpr.Evaluate();
        }
        public void SetY(Expr expr) {
            int y = (int)expr.Value;
            TakeExposure te = GetTakeExposure();
            if (te != null && HExpr != null && (y < 0 || (y + HExpr.Value > te.CameraInfo.YSize))) {
                expr.Error = "Invalid dimensions";
            }
            if (HExpr != null) HExpr.Evaluate();
        }
        public void SetWidth(Expr expr) {
            int w = (int)expr.Value;
            TakeExposure te = GetTakeExposure();
            if (te != null && XExpr != null && (w < 0 || (w + XExpr.Value > te.CameraInfo.XSize))) {
                expr.Error = "Invalid dimensions";
            }
            if (XExpr != null) XExpr.Evaluate();
        }

        public void SetHeight(Expr expr) {
            int h = (int)expr.Value;
            TakeExposure te = GetTakeExposure();
            if (te != null && YExpr != null && (h < 0 || (h + YExpr.Value > te.CameraInfo.YSize))) {
                expr.Error = "Invalid dimensions";
            }
            if (YExpr != null) YExpr.Evaluate();
        }

        [JsonProperty]
        public override InstructionErrorBehavior ErrorBehavior {
            get => errorBehavior;
            set {
                errorBehavior = value;
                foreach (var item in Items) {
                    item.ErrorBehavior = errorBehavior;
                }
                RaisePropertyChanged();
            }
        }

        private int attempts = 1;

        [JsonProperty]
        public override int Attempts {
            get => attempts;
            set {
                if (value > 0) {
                    attempts = value;
                    foreach (var item in Items) {
                        item.Attempts = attempts;
                    }
                    RaisePropertyChanged();
                }
            }
        }

        public ISequenceItem GetSwitchFilter() {
            SwitchFilter sw = Items[0] as SwitchFilter;
            if (sw == null) {
                // For upgrading SmartExposure to NINA
                if (Items[0] is ISequenceItem ninaSF) {
                    return ninaSF;
                }
                sw = new SwitchFilter(ProfileService, FilterWheelMediator);
                if (Items[0] is NINA.Sequencer.SequenceItem.FilterWheel.SwitchFilter oldSw) {
                    if (oldSw.Filter != null) {
                        sw.FilterExpr = oldSw.Filter.Name;
                    }
                }
                Items[0] = sw;
            }
            return Items[0] as SwitchFilter;
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (CVFilter) {
                FilterExpr = FilterExpr;
            }
            return base.Execute(progress, token);
        }

        public TakeExposure GetTakeExposure() {
            if (Items.Count > 1) {
                return Items[1] as TakeExposure;
            }
            return null;
        }

        public DitherAfterExposures GetDitherAfterExposures() {
            return Triggers[0] as DitherAfterExposures;
        }

        public LoopCondition GetLoopCondition() {
            return Conditions[0] as LoopCondition;
        }

        public bool CanSubsample { 
            get {
                var te = GetTakeExposure();
                return (te == null) ? false : te.CanSubsample;
            }
            set { }
        }

        [JsonProperty]
        public string IterationsExpr {
            get => null;
            set {
                IterExpr.Expression = value;
            }
        }

        private int iterationCount;
        public int IterationCount {
            get => iterationCount;
            set {
                //
                if (Conditions.Count == 0) return;
                LoopCondition lc = Conditions[0] as LoopCondition;
                iterationCount = lc.Iterations = value;
            }
        }

        public void SetIterationCount (Expr expr) {
            IterationCount = (int)expr.Value;
        }

        private List<string> iFilterNames = new List<string>();
        public List<string> FilterNames {
            get => iFilterNames;
            set {
                iFilterNames = value;
            }
        }

        public string SelectedFilter { get; set; }

        public bool CVFilter { get; set; } = false;
        private void SetFInfo() {
            SwitchFilter sw = Items.Count == 0 ? null : GetSwitchFilter() as SwitchFilter;
            if (sw != null) {
                FilterWheelInfo filterWheelInfo = FilterWheelMediator.GetInfo();
                var fwi = ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters;
                if (Filter == -1) {
                    if (filterWheelInfo.Connected && filterWheelInfo.SelectedFilter != null) {
                        Filter = filterWheelInfo.SelectedFilter.Position;
                    }
                    sw.FilterExpr = null;
                } else if (Filter < fwi.Count) {
                    sw.FilterExpr = FilterExpr;
                }
            }
        }

        private string iFilterExpr = null;
        [JsonProperty]
        public string FilterExpr {
            get => iFilterExpr;
            set {
                value ??= "(Current)";
                if (value.Length == 0) {
                    value = "(Current)";
                }
                iFilterExpr = value;
                FExpr.Expression = value;
                FExpr.IsExpression = true;

                // Find in FilterWheelInfo
                var fwi = ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters;
                Filter = -1;
                CVFilter = false;
 
                foreach (var fw in fwi) {
                    if (fw.Name.Equals(value)) {
                        Filter = fw.Position;
                        FExpr.Value = Filter;
                        FExpr.Error = null;
                        break;
                    }
                }

                if (Filter == -1 && !value.Equals("(Current)")) {
                    CVFilter = true;
                    if (FExpr.Error == null && FExpr.Value < fwi.Count) {
                        Filter = (int)FExpr.Value;
                    }
                }

                SetFInfo();
                RaisePropertyChanged(nameof(CVFilter));
                RaisePropertyChanged();
            }
        }


        private int iFilter = -1;
        public int Filter {
            get => iFilter;
            set {
                iFilter = value;
                RaisePropertyChanged();
            }
        }

        public void SetFilter (Expr expr) {

        }

        public void SetROI (Expr expr) {
            if (expr.Value < 0 || expr.Value > 100) {
                expr.Error = "ROI must be between 0 and 100 (percent)";
            } else if (GetTakeExposure() != null) {
                GetTakeExposure().ROI = expr.Value;
            }
        }

        public string[] ROIOptions {
            get {
                return new string[] { "None", "ROI", "Dimensions" };
            }
            set { }
        }

        private string iROIOption = "None";
        [JsonProperty]
        public string ROIOption {
            get {
                return iROIOption;
            }
            set {
                iROIOption = value;
                TakeExposure te = GetTakeExposure();
                if (te != null) {
                    te.ROIType = value.Equals("ROI");
                }
                RaisePropertyChanged("ROIOption");
                RaisePropertyChanged("IsROI");
                RaisePropertyChanged("IsDimensions");
            }
        }

        [JsonProperty]
        public bool IsDimensions {
            get {
                return iROIOption.Equals("Dimensions");
            }
            set {

            }
        }

        [JsonProperty]
        public bool IsROI {
            get {
                return iROIOption.Equals("ROI");
            }
            set {

            }
        }

        [JsonProperty]
        public string DitherExpr {
            get => null;
            set {
                DExpr.Expression = value;
                RaisePropertyChanged();
            }
        }

        public int DitherCount {
            get => (Triggers[0] as DitherAfterExposures).AfterExposures;
            set {
                if (Triggers.Count == 0) return;
                DitherAfterExposures lc = Triggers[0] as DitherAfterExposures;
                lc.AfterExposures = value;
                RaisePropertyChanged("DitherCount");
            }
        }

        public void SetDitherCount (Expr expr) {
            DitherCount = (int)expr.Value;
        }

        public override bool Validate() {
            try {
                var i = new List<string>();
                var sw = GetSwitchFilter();
                var te = GetTakeExposure();
                var dither = GetDitherAfterExposures();

                bool valid = true;

                valid = te.Validate() && valid;
                i.AddRange(te.Issues);

                if (sw is SwitchFilter spsw && spsw.FInfo != null) {
                    valid = spsw.Validate() && valid;
                    i.AddRange(spsw.Issues);
                }

                if (dither == null) {
                    return false;
                }

                if (dither.AfterExposures > 0) {
                    valid = dither.Validate() && valid;
                    i.AddRange(dither.Issues);
                }

                SetFInfo();
                if (FilterNames.Count == 0) {
                    var fwi = ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters;
                    foreach (var fw in fwi) {
                        FilterNames.Add(fw.Name);
                    }
                    RaisePropertyChanged("FilterNames");
                }

                Expr.AddExprIssues(i, IterExpr, DExpr, RExpr, XExpr, YExpr, WExpr, HExpr); // FExpr?
                                                               // 
                RaisePropertyChanged("CanSubsample");

                Issues = i;
                RaisePropertyChanged("Issues");
                return (Issues.Count == 0) && valid;
            } catch (Exception) {
                return false;
            }
        }

        public override object Clone() {
            if (GetSwitchFilter() == null) {
                Logger.Warning("Where is SwitchFilter?");
                Items[0] = new SwitchFilter(ProfileService, FilterWheelMediator);

            }
            ISequenceItem sf = (ISequenceItem)this.GetSwitchFilter().Clone();
            TakeExposure te = (TakeExposure)this.GetTakeExposure().Clone();
            LoopCondition lc = (LoopCondition)this.GetLoopCondition().Clone();
            DitherAfterExposures dae = (DitherAfterExposures)this.GetDitherAfterExposures();
            if (dae == null) {
                NINA.Sequencer.Trigger.Guider.DitherAfterExposures oldDae = (NINA.Sequencer.Trigger.Guider.DitherAfterExposures)this.Triggers[0];
                if (oldDae != null) {
                    dae = new DitherAfterExposures(GuiderMediator, ImageHistoryVM, ProfileService);
                    dae.AfterExpr.Expression = oldDae.AfterExposures.ToString();
                }
            } else {
                dae = (DitherAfterExposures)dae.Clone();
            }
            var clone = new SmartExposure(this, sf, te, lc, dae);
            return clone;
        }

        public override TimeSpan GetEstimatedDuration() {
            return GetTakeExposure().GetEstimatedDuration();
        }

        /// When an inner instruction interrupts this set, it should reroute the interrupt to the real parent set

        public override Task Interrupt() {
            return this.Parent?.Interrupt();
        }
    }
}