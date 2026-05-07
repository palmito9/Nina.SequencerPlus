#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.Validations;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Locale;
using NINA.Sequencer.SequenceItem;
using NINA.Astrometry;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Cool Camera +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Camera_CoolCamera_Description")]
    [ExportMetadata("Icon", "SnowflakeSVG")]
    [ExportMetadata("Category", "Sequencer+ (Enhanced Instructions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class CoolCamera : SequenceItem, IValidatable {

        IProfileService profileService;

        [ImportingConstructor]
        public CoolCamera(IProfileService profileService, ICameraMediator cameraMediator) {
            this.cameraMediator = cameraMediator;
            this.profileService = profileService;
            CameraSettings = profileService.ActiveProfile.CameraSettings;
            TempExpr = new Expr(this);
            DurExpr = new Expr(this);
        }

        private CoolCamera(CoolCamera cloneMe) : this(cloneMe.profileService, cloneMe.cameraMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            CoolCamera clone = new CoolCamera(this) {
            };
            clone.TempExpr = new Expr(clone, this.TempExpr.Expression);
            clone.TempExpr.Setter = ValidateTemperature;
            clone.DurExpr = new Expr(clone, this.DurExpr.Expression);
            clone.DurExpr.Default = 0;
            return clone;
        }

        private ICameraMediator cameraMediator;

        private ICameraSettings cameraSettings;

        public ICameraSettings CameraSettings {
            get {
                if (cameraSettings.Temperature == null) {
                    cameraSettings.Temperature = 0;
                }
                return cameraSettings;
                    }
            private set {
                cameraSettings = value;
                RaisePropertyChanged();
            }
        }

        private Expr _TempExpr = null;

        [JsonProperty]
        public Expr TempExpr {
            get => _TempExpr;
            set {
                _TempExpr = value;
                RaisePropertyChanged();
            }
        }
        
        private Expr _DurExpr = null;

        [JsonProperty]
        public Expr DurExpr {
            get => _DurExpr;
            set {
                _DurExpr = value;
                RaisePropertyChanged();
            }
        }

        // Legacy support
        
        [JsonProperty]
        public string TemperatureExpr {
            get => null;
            set {
                TempExpr.Expression = value;
                RaisePropertyChanged("TempExpr");
            }
        }
 
        [JsonProperty]
        public string DurationExpr {
            get => null;
            set {
                DurExpr.Expression = value;
                RaisePropertyChanged("DurExpr");
            }
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            return cameraMediator.CoolCamera(TempExpr.Value, TimeSpan.FromMinutes(DurExpr.Value), progress, token);
        }

        private static string BAD_TEMPERATURE = "Temperature must be between -30C and 30C";
        
        public bool Validate() {
            var i = new List<string>();
            var info = cameraMediator.GetInfo();
            if (!info.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            } else if (!info.CanSetTemperature) {
                i.Add(Loc.Instance["Lbl_SequenceItem_Validation_CameraCannotSetTemperature"]);
            }

            if (cameraSettings.Temperature != null) {
                TempExpr.Default = (double)cameraSettings.Temperature;
            }
            DurExpr.Default = (double)cameraSettings.CoolingDuration;

            Expr.AddExprIssues(i, TempExpr, DurExpr);

            if (!Double.IsNaN(TempExpr.Value) && (TempExpr.Value < -40 || TempExpr.Value > 30)) {
                i.Add(BAD_TEMPERATURE);
            }

            Issues = i;
            return i.Count == 0;
        }

        // Always Validate after Parent changed!
        public override void AfterParentChanged() {
            Validate();
        }

        public void ValidateTemperature(Expr expr) {
            if (expr.Value < -40 || expr.Value > 30) {
                expr.Error = "Must be between -40 and 30";
            }
        }

        public override TimeSpan GetEstimatedDuration() {
            return DurExpr.Value > 0 ? TimeSpan.FromMinutes(DurExpr.Value) : TimeSpan.FromMinutes(1);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(CoolCamera)}, Temperature: {TempExpr.Value}, Duration: {DurExpr.Value}";
        }
    }
}