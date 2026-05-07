#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Antlr.Runtime;
using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.ViewModel.Sequencer;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Wait Until")]
    [ExportMetadata("Description", "Waits until the expression is true.")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Sequencer+ (Expressions)")]
    [Export(typeof(ISequenceItem))]
    public class WaitUntil : SequenceItem, IValidatable, ITrueFalse {
        private ISafetyMonitorMediator safetyMonitorMediator;
        protected ISequenceMediator sequenceMediator;
        private IProfileService profileService;

        [ImportingConstructor]
        public WaitUntil(ISafetyMonitorMediator safetyMonitorMediator, ISequenceMediator seqMediator, IProfileService pService) {
            this.safetyMonitorMediator = safetyMonitorMediator;
            this.sequenceMediator = seqMediator;
            this.profileService = pService;
            PredicateExpr = new Expr(this);
        }

        private WaitUntil(WaitUntil cloneMe) : this(cloneMe.safetyMonitorMediator, cloneMe.sequenceMediator, cloneMe.profileService) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            WaitUntil clone = new WaitUntil(this);
            clone.PredicateExpr = new Expr(clone, this.PredicateExpr.Expression);
            return clone;
        }

        [JsonProperty]
        public string Predicate {
            get => null;
            set {
                PredicateExpr.Expression = value;
                RaisePropertyChanged("PredicateExpr");
            }
        }

        private Expr _PredicateExpr;
        [JsonProperty]
        public Expr PredicateExpr {
            get => _PredicateExpr;
            set {
                _PredicateExpr = value;
                RaisePropertyChanged();
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

        public TimeSpan WaitInterval { get; set; } = TimeSpan.FromSeconds(5);

        public bool Validate() {
            var i = new List<string>();
            
            Expr.AddExprIssues(i, PredicateExpr);
            
            Issues = i;
            return i.Count == 0;
        }
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(WaitUntil)}";
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            while (Parent != null) {
                PredicateExpr.Evaluate();
                if (!string.Equals(PredicateExpr.ValueString, "0", StringComparison.OrdinalIgnoreCase) && (PredicateExpr.Error == null)) {
                    break;
                }
                progress?.Report(new ApplicationStatus() { Status = "Waiting..." });
                await CoreUtil.Wait(WaitInterval, token, default);
            }
        }
    }
}