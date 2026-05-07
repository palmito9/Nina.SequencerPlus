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
using NINA.Sequencer.Container;
using NINA.Sequencer.Validations;
using NINA.Astrometry;
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
using NINA.Core.Utility.Notification;
using NINA.Sequencer.SequenceItem;
using NINA.Profile;
using NINA.Profile.Interfaces;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Slew to Alt/Az +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Telescope_SlewScopeToAltAz_Description")]
    [ExportMetadata("Icon", "SlewToAltAzSVG")]
    [ExportMetadata("Category", "Sequencer+ (Enhanced Instructions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SlewToAltAz : SequenceItem, IValidatable {

        [ImportingConstructor]
        public SlewToAltAz(ITelescopeMediator telescopeMediator, IGuiderMediator guiderMediator, IProfileService profileService) {
            this.telescopeMediator = telescopeMediator;
            this.guiderMediator = guiderMediator;
            this.profileService = profileService;
            Coordinates = new InputTopocentricCoordinates(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude));
            AltExpr = new Expr(this);
            AzExpr = new Expr(this);
        }

        private SlewToAltAz(SlewToAltAz cloneMe) : this(cloneMe.telescopeMediator, cloneMe.guiderMediator, cloneMe.profileService) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            SlewToAltAz clone = new SlewToAltAz(this) {
                Coordinates = new InputTopocentricCoordinates(Coordinates.Coordinates.Copy())
            };
            clone.AltExpr = new Expr(clone, this.AltExpr.Expression);
            clone.AltExpr.Setter = AltSetter;
            clone.AzExpr = new Expr(clone, this.AzExpr.Expression);
            clone.AzExpr.Setter = AzSetter;
            return clone;
        }

        private IProfileService profileService;
        private ITelescopeMediator telescopeMediator;
        private IGuiderMediator guiderMediator;

        public void AzSetter (Expr expr) {
            expr.Error = null;
            if (expr.Value < 0 ||  expr.Value > 360) {
                expr.Error = "Azimuth must be between 0° and 360°";
            }
        }

        public void AltSetter (Expr expr) {
            expr.Error = null;
            if (expr.Value > 90 || expr.Value < 0) {
                expr.Error = "Altitude must be between 0° and 90°";
            }
        }

        // 0 to 24
        private Expr _AltExpr = null;

        [JsonProperty]
        public Expr AltExpr {
            get => _AltExpr;
            set {
                _AltExpr = value;
                RaisePropertyChanged();
            }
        }
        
        // -90 to 90
        private Expr _AzExpr = null;

        [JsonProperty]
        public Expr AzExpr {
            get => _AzExpr;
            set {
                _AzExpr = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public InputTopocentricCoordinates Coordinates { get; set; }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (telescopeMediator.GetInfo().AtPark) {
                Notification.ShowError(Loc.Instance["LblTelescopeParkedWarning"]);
                throw new SequenceEntityFailedException(Loc.Instance["LblTelescopeParkedWarning"]);
            }
            var stoppedGuiding = await guiderMediator.StopGuiding(token);
            Coordinates.Coordinates.Altitude = Angle.ByDegree(AltExpr.Value);
            Coordinates.Coordinates.Azimuth = Angle.ByDegree(AzExpr.Value);
            await telescopeMediator.SlewToCoordinatesAsync(Coordinates.Coordinates, token);
            if (stoppedGuiding) {
                await guiderMediator.StartGuiding(false, progress, token);
            }
        }

        public override void AfterParentChanged() {
            Validate();
        }
        public bool Validate() {
            var i = new List<string>();
            var info = telescopeMediator.GetInfo();
            if (!info.Connected) {
                i.Add(Loc.Instance["LblTelescopeNotConnected"]);
            }

            Expr.AddExprIssues(i, AltExpr, AzExpr);

            Issues = i;
            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SlewToRADec)}, Coordinates: {Coordinates}";
        }
    }
}