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
using CsvHelper;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Move Focuser Relative +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Focuser_MoveFocuserRelative_Description")]
    [ExportMetadata("Icon", "MoveFocuserRelativeSVG")]
    [ExportMetadata("Category", "Sequencer+ (Enhanced Instructions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class MoveFocuserRelative : SequenceItem, IValidatable {

        [ImportingConstructor]
        public MoveFocuserRelative(IFocuserMediator focuserMediator) {
            this.focuserMediator = focuserMediator;
            PExpr = new Expr(this);
        }

        private MoveFocuserRelative(MoveFocuserRelative cloneMe) : this(cloneMe.focuserMediator) {
            CopyMetaData(cloneMe);
            PExpr = new Expr(this, cloneMe.PExpr.Expression, "Integer");
            PExpr.Default = 0;
        }

        public override object Clone() {
            return new MoveFocuserRelative(this) {
            };
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            Validate();
        }

        [JsonProperty]
        public Expr PExpr { get; set; }
        
        
        private IFocuserMediator focuserMediator;


        [JsonProperty]
        public string RelativePositionExpr {
            get => null;
            set {
                PExpr.Expression = value;
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
            return focuserMediator.MoveFocuserRelative((int)PExpr.Value, token);
        }

        public bool Validate() {
            var i = new List<string>();
            if (!focuserMediator.GetInfo().Connected) {
                i.Add(Loc.Instance["LblFocuserNotConnected"]);
            }
            Expr.AddExprIssues(i, PExpr);
            Issues = i;
            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(MoveFocuserRelative)}, Relative Position: {PExpr.Value}";
        }
    }
}