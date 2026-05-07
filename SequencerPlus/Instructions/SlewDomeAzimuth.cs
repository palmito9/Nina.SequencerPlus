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
using NINA.Sequencer.Validations;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Locale;
using NINA.Sequencer.SequenceItem;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Slew Dome Azimuth +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Dome_SetDomeAzimuth_Description")]
    [ExportMetadata("Icon", "RotatorSVG")]
    [ExportMetadata("Category", "Sequencer+ (Enhanced Instructions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SlewDomeAzimuth : SequenceItem, IValidatable {

        [ImportingConstructor]
        public SlewDomeAzimuth(IDomeMediator domeMediator) {
            this.domeMediator = domeMediator;
            AzExpr = new Expr(this);
        }

        private SlewDomeAzimuth(SlewDomeAzimuth cloneMe) : this(cloneMe.domeMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            SlewDomeAzimuth clone = new SlewDomeAzimuth(this);
            clone.AzExpr = new Expr(clone, AzExpr.Expression);
            return clone;
        }

        private IDomeMediator domeMediator;
        private IList<string> issues = new List<string>();

        private Expr _AzExpr = null;

        [JsonProperty]
        public Expr AzExpr {
            get => _AzExpr;
            set {
                _AzExpr = value;
                RaisePropertyChanged();
            }
        }


        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            return domeMediator.SlewToAzimuth(AzExpr.Value, token);
        }

        public bool Validate() {
            var i = new List<string>();
            var domeInfo = domeMediator.GetInfo();
            if (!domeInfo.Connected) {
                i.Add(Loc.Instance["LblDomeNotConnected"]);
            } else {
                if (!domeInfo.CanSetAzimuth) {
                    i.Add(Loc.Instance["LblDomeCannotSetAzimuth"]);
                }
            }

            Expr.AddExprIssues(i, AzExpr);

            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SlewDomeAzimuth)}, Azimuth: {AzExpr.ValueString}°";
        }
    }
}