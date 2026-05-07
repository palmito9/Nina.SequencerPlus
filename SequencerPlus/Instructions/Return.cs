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
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Container;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Return")]
    [ExportMetadata("Description", "Return a value from a Function")]
    [ExportMetadata("Icon", "MoveFocuserRelativeSVG")]
    [ExportMetadata("Category", "Sequencer+ (Fun-ctions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class Return : SequenceItem, IValidatable {

        [ImportingConstructor]
        public Return(IFocuserMediator focuserMediator) {
            this.focuserMediator = focuserMediator;
            RExpr = new Expr(this);
        }

        private Return(Return cloneMe) : this(cloneMe.focuserMediator) {
            CopyMetaData(cloneMe);
            RExpr = new Expr(this, cloneMe.RExpr.Expression, "Integer");
            RExpr.Default = 0;
        }

        public override object Clone() {
            return new Return(this) {
            };
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            Validate();
        }

        [JsonProperty]
        public Expr RExpr { get; set; }


        private IFocuserMediator focuserMediator;


        [JsonProperty]
        public string RelativePositionExpr {
            get => null;
            set {
                RExpr.Expression = value;
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
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is TemplateContainer tc && tc.PseudoParent is Call c) {
                    p.Interrupt();
                    p.Status = NINA.Core.Enum.SequenceEntityStatus.FINISHED;
                    foreach (var item in p.Items) {
                        p.Status = NINA.Core.Enum.SequenceEntityStatus.FINISHED;
                    }
                    string resultName = c.ResultExpr.Expression;
                    if (resultName == null || resultName.Length == 0) {
                        return Task.CompletedTask; // throw new SequenceEntityFailedException("There must be a result Variable specified in order to use the Return instruction");
                    }
                    Symbol sym = Symbol.FindSymbol(resultName, tc);
                    if (sym != null && sym is SetVariable sv) {
                        RExpr.Evaluate();
                        Logger.Warning("Call " + tc.Items[0].Name + " with Arg1 = " + c.Arg1Expr.ValueString + " is returning: " + RExpr.ValueString);
                        sv.Definition = RExpr.Value.ToString();
                        c.ResultExpr.Evaluate();
                    } else {
                        throw new SequenceEntityFailedException("Result Variable is not defined");
                    }
                    return Task.CompletedTask;
                }
                p = p.Parent;
            }
            throw new SequenceEntityFailedException("Return is not within a Call instruction");
        }

        public bool Validate() {
            var i = new List<string>();

            RExpr.Validate();
            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(Return)}, Result: {RExpr.Value}";
        }
    }
}