using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Enum;
using NINA.Core.Utility;
using System.Text;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "If/Then/Else")]
    [ExportMetadata("Description", "Executes an instruction set if the Expression is True (or 1)")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Sequencer+ (Expressions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]

    public class IfThenElse : IfCommand, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public IfThenElse() {
            IfExpr = new Expr(this);
            Instructions = new IfContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            Instructions.Name = Name;
            Instructions.Icon = Icon;
            ElseInstructions = new IfContainer();
            ElseInstructions.AttachNewParent(Parent);
            ElseInstructions.PseudoParent = this;
            ElseInstructions.Name = Name;
            ElseInstructions.Icon = Icon;
        }

        public IfThenElse(IfThenElse copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                IfExpr = new Expr(this, copyMe.IfExpr.Expression);
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
                Instructions.Name = Name;
                Instructions.Icon = Icon;
                ElseInstructions = (IfContainer)copyMe.ElseInstructions.Clone();
                ElseInstructions.AttachNewParent(Parent);
                ElseInstructions.PseudoParent = this;
                ElseInstructions.Name = Name;
                ElseInstructions.Icon = Icon;
            }
        }

        public override object Clone() {
            return new IfThenElse(this) {
            };
        }

        [JsonProperty]
        public IfContainer ElseInstructions { get; set; }

        public bool Check() {

            return false;
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {

            Logger.Info("Execute, Predicate: " + IfExpr.Expression);
            if (string.IsNullOrEmpty(IfExpr.Expression)) {
                Status = SequenceEntityStatus.FAILED;
                return;
            }

            try {
                // Always get latest data...
                await Symbol.UpdateSwitchWeatherData();

                if (IfExpr.ImageVolatile) {
                    Logger.Info("ImageVolatile");
                    while (TakeExposure.LastImageProcessTime < TakeExposure.LastExposureTIme) {
                        Logger.Info("Waiting 250ms for processing...");
                        progress?.Report(new ApplicationStatus() { Status = "" });
                        await CoreUtil.Wait(TimeSpan.FromMilliseconds(250), token, default);
                    }
                    // Get latest values
                    Logger.Info("ImageVolatile, new data");
                }

                IfExpr.Evaluate();

                if (!string.Equals(IfExpr.ValueString, "0", StringComparison.OrdinalIgnoreCase) && (IfExpr.Error == null)) {
                    Logger.Info("Predicate is true; running Then");
                    await Instructions.Run(progress, token);
                } else {
                    Logger.Info("Predicate is false; running Else");
                    await ElseInstructions.Run(progress, token);
                }
            } catch (ArgumentException ex) {
                Logger.Info("If error: " + ex.Message);
                Status = SequenceEntityStatus.FAILED;
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
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfThenElse)}, Expr: {IfExpr}";
        }

        public IList<string> Switches { get; set; } = null;

        public override void ResetProgress() {
            base.ResetProgress();
            ElseInstructions.ResetAll();
            foreach (ISequenceItem item in ElseInstructions.Items) {
                item.ResetProgress();
            }
        }

        public override void ResetAll() {
            base.ResetAll();
            ElseInstructions.ResetAll();
        }


        public override void AfterParentChanged() {
            base.AfterParentChanged();
            foreach (ISequenceItem item in ElseInstructions.Items) {
                item.AfterParentChanged();
            }
        }

        public new bool Validate() {

            var i = new List<string>();

            ValidateInstructions(Instructions);
            ValidateInstructions(ElseInstructions);

            Expr.AddExprIssues(i, IfExpr);

            Issues = i;
            return i.Count == 0;
        }

    }
}
