using Newtonsoft.Json;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Utility;
using System.Windows.Controls;
using NINA.Core.Model;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Loop While")]
    [ExportMetadata("Description", "Loops while the specified expression is not false.")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Sequencer+ (Expressions)")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]

    public class LoopWhile : SequenceCondition, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public LoopWhile() {
            ConditionWatchdog = new ConditionWatchdog(InterruptWhenFails, TimeSpan.FromSeconds(5));
            PredicateExpr = new Expr(this);
        }

        public LoopWhile(LoopWhile copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        public override object Clone() {
            LoopWhile clone = new LoopWhile(this);
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

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(LoopWhile)}, Predicate: {PredicateExpr.Expression}";
        }


        public IList<string> Issues { get; set; }

        public bool Validate() {

            var i = new List<string>();

            PredicateExpr.Validate(i);

            Switches = Symbol.GetSwitches();
            RaisePropertyChanged("Switches");

            Expr.AddExprIssues(i, PredicateExpr);

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }

        private bool Debugging = false;

        private void LogInfo(string str) {
            if (Debugging) {
                Logger.Info(str);
            }
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {

            if (string.IsNullOrEmpty(PredicateExpr.Expression)) {
                //Logger.Warning("LoopWhile: Check, Predicate Expression is null or empty, " + PredicateExpr + " (Expression = " + PredicateExpr.Expression + ")");
                throw new SequenceEntityFailedException("LoopWhile, PredicateExpr is null or empty");
            }

            if (!Symbol.SwitchWeatherConnectionStatusCurrent()) {
                Symbol.UpdateSwitchWeatherData();
            }

            PredicateExpr.Evaluate();
           
            if (PredicateExpr.Error != null) {
                //Logger.Warning("LoopWhile: Check, error in PredicateExpr: " + PredicateExpr.Error);
                throw new SequenceEntityFailedException(PredicateExpr.Error);
            } else {
                try {
                    foreach (var kvp in PredicateExpr.Parameters) {
                        //SPLogger.Debug(kvp.Key + ": " + kvp.Value);
                    }
                } catch (Exception) {
                    // These could be modified by another thread
                }
                if (!string.Equals(PredicateExpr.ValueString, "0", StringComparison.OrdinalIgnoreCase)) {
                    //SPLogger.Debug("LoopWhile, Predicate is true, " + PredicateExpr);
                    return true;
                } else {
                    //Logger.Info("LoopWhile, Predicate is false, " + PredicateExpr);
                    return false;
                }
            }
        }

        public override void AfterParentChanged() {
            if (Parent == null) {
                SequenceBlockTeardown();
            } else {
                if (Parent.Status == SequenceEntityStatus.RUNNING) {
                    SequenceBlockInitialize();
                }
            }
            PredicateExpr.Evaluate();
        }

        public override void SequenceBlockTeardown() {
            try { ConditionWatchdog?.Cancel(); } catch { }
        }

        public override void SequenceBlockInitialize() {
            ConditionWatchdog?.Start();
        }

        public IList<string> Switches { get; set; } = null;

        private async Task InterruptWhenFails() {
 
            if (!Check(null, null)) {
                if (this.Parent != null) {
                    if (ItemUtility.IsInRootContainer(Parent) && this.Parent.Status == SequenceEntityStatus.RUNNING && this.Status != SequenceEntityStatus.DISABLED) {
                        Logger.Info("Expression returned false - Interrupting current Instruction Set");
                        await this.Parent.Interrupt();
                    }
                }
            }
        }

    }
}
