#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Sequencer.Validations;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using NINA.Core.Enum;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Trigger;
using Newtonsoft.Json;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Core.Model;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "When")]
    [ExportMetadata("Description", "Runs a customizable set of instructions when the specified Expression is true.")]
    [ExportMetadata("Icon", "ShieldSVG")]
    [ExportMetadata("Category", "Sequencer+ (Expressions)")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]

    public class WhenSwitch : When, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public WhenSwitch(ISafetyMonitorMediator safetyMediator, ISequenceMediator sequenceMediator, IApplicationStatusMediator applicationStatusMediator, ISwitchMediator switchMediator,
                IWeatherDataMediator weatherMediator, ICameraMediator cameraMediator, ITelescopeMediator telescopeMediator)
            : base(safetyMediator, sequenceMediator, applicationStatusMediator, switchMediator, weatherMediator, cameraMediator, telescopeMediator) {
            IfExpr = new Expr(this);
        }

        public ICameraConsumer cameraConsumer {  get; set; } 

        protected WhenSwitch(WhenSwitch cloneMe) : base(cloneMe.safetyMediator, cloneMe.sequenceMediator, cloneMe.applicationStatusMediator, cloneMe.switchMediator, cloneMe.weatherMediator, cloneMe.cameraMediator,
                cloneMe.telescopeMediator) {
            if (cloneMe != null) {
                CopyMetaData(cloneMe);
                IfExpr = new Expr(this, cloneMe.IfExpr.Expression);
                Instructions = (IfContainer)cloneMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
                Instructions.Name = cloneMe.Name;
                Instructions.Icon = cloneMe.Icon;
                Predicate = cloneMe.Predicate;
                OnceOnly = cloneMe.OnceOnly;
                Interrupt = cloneMe.Interrupt;
            }
        }

        public bool Disabled { get; set; } = false;

        private bool iOnceOnly = false;

        [JsonProperty]
        public bool OnceOnly {
            get => iOnceOnly;
            set {
                iOnceOnly = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Predicate {
            get => null;
            set {
                IfExpr.Expression = value;
                RaisePropertyChanged("IfExpr");

            }
        }

        private Expr _IfExpr;
        [JsonProperty]
        public Expr IfExpr {
            get => _IfExpr;
            set {
                _IfExpr = value;
                RaisePropertyChanged();
            }
        }

        public string ValidateConstant(double temp) {
            if ((int)temp == 0) {
                return "False";
            } else if ((int)temp == 1) {
                return "True";
            }
            return string.Empty;
        }
        public override object Clone() {
            return new WhenSwitch(this);
        }

        protected bool IsActive() {
            return ItemUtility.IsInRootContainer(Parent) && Parent.Status == SequenceEntityStatus.RUNNING && Status != SequenceEntityStatus.DISABLED;
        }

        public override bool Check() {
            Logger.Trace("When Check " + whenId + ", Expr = " + IfExpr);
            if (Disabled) {
                Logger.Trace("Check = TRUE (Disabled)");
                return true;
            }

            Symbol.UpdateSwitchWeatherData();
            if (IfExpr.ImageVolatile) {
                Logger.Trace("ImageVolatile");
                while (TakeExposure.LastImageProcessTime < TakeExposure.LastExposureTIme) {
                    Logger.Info("Waiting 250ms for processing...");
                    Task.Delay(250);
                }
                // Get latest values
                Logger.Trace("ImageVolatile, new data");
            }
            IfExpr.Evaluate();

            if (!string.Equals(IfExpr.ValueString, "0", StringComparison.OrdinalIgnoreCase) && (IfExpr.Error == null)) {
                Logger.Trace("Check = FALSE");
                return false;
            }
            Logger.Info("When Check = TRUE, Expr = " + IfExpr);
            return true;
        }

        public override string ToString() {
            return $"Trigger: {nameof(When)} Expression: {IfExpr.Expression} Value: {IfExpr.ValueString}";
        }

        public override bool AllowMultiplePerSet => true;

        public IList<string> Switches { get; set; } = null;
        public new bool Validate() {

            CommonValidate();

            var i = new List<string>();
            Expr.AddExprIssues(i, IfExpr);

            Switches = Symbol.GetSwitches();
            RaisePropertyChanged("Switches");

            Issues = i;
            return i.Count == 0;
        }
    }
}
