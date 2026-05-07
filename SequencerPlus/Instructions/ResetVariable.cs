using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.Container;
using System.Diagnostics;
using NINA.Core.Enum;
using NINA.Sequencer;
using NINA.Core.Utility;
using NCalc.Domain;
using System.Text.RegularExpressions;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Profile;
using System.Windows.Input;
using System.Windows.Media;
using Antlr.Runtime;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Set Variable")]
    [ExportMetadata("Description", "If the variable has been previously defined, its value will become the result of the specified expression")]
    [ExportMetadata("Icon", "VariableSVG")]
    [ExportMetadata("Category", "Sequencer+ (Expressions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class ResetVariable : SequenceItem, IValidatable {
        [ImportingConstructor]


        public ResetVariable() {
            Icon = Icon;
            Expr = new Expr(this);
        }

        public ResetVariable(ResetVariable copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        public override object Clone() {
            ResetVariable clone = new ResetVariable(this) { };
            clone.Expr = new Expr(clone, this.Expr.Expression);
            clone.Expr.Type = "Any";
            clone.Variable = this.Variable;
            return clone;
        }

        private Expr _Expr = null;

        [JsonProperty]
        public Expr Expr {
            get => _Expr;
            set {
                _Expr = value;
                RaisePropertyChanged();
            }
        }

        private string variable;

        [JsonProperty]
        public string Variable {
            get => variable;
            set {
                if (value == variable) {
                    return;
                }
                variable = value;
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
  
        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            // Bunch of reasons the instructiopn might be invalid
            if (Issues.Count != 0) {
                throw new SequenceEntityFailedException("The instruction is invalid");
            }
            
            Symbol.UpdateSwitchWeatherData();
            Expr.Evaluate();

            // Find Symbol, make sure it's valid
            Symbol sym = Symbol.FindSymbol(Variable, Parent);
            if (sym == null || !(sym is SetVariable)) {
                throw new SequenceEntityFailedException("The symbol isn't found or isn't a Variable");
            } else if (Expr.Error != null) {
                throw new SequenceEntityFailedException("The value of the expression '" + Expr.Expression + "' was invalid");
            }
            SetVariable sv = sym as SetVariable;
            if (sv == null || sv.Executed == false) {
                throw new SequenceEntityFailedException("The Variable definition has not been executed");
            }

            string oldDefinition = sym.Definition;

            if (Expr.StringValue != null) {
                sym.Expr.Error = null;
                sym.Definition = "'" + Expr.StringValue + "'";
            } else {
                sym.Definition = Expr.Value.ToString();
            }

            Logger.Info("ResetVariable: " + Variable + " from " + oldDefinition + " to " + sym.Definition);

            // Make sure references are updated
            Symbol.SymbolDirty(sym);

            Expr.Refresh();
            return Task.CompletedTask;
        }
        private bool IsAttachedToRoot() {
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is SequenceRootContainer) {
                    return true;
                }
                p = p.Parent;
            }
            return false;
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            //Expr.Validate();
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ResetVariable)}, Variable: {variable}, Expr: {Expr}";
        }

        public bool Validate() {
            if (!IsAttachedToRoot()) return true;

            var i = new List<string>();
            Symbol sym = null;

            if (Expr.Expression.Length == 0 || (Variable == null || Variable.Length == 0)) {
                i.Add("The variable and new value expression must both be specified");
            } else if (Variable.Length > 0 && !Regex.IsMatch(Variable, Symbol.VALID_SYMBOL)) {
                i.Add("'" + Variable + "' is not a legal Variable name");
            } else {
                sym = Symbol.FindSymbol(Variable, Parent);
                if (sym == null) {
                    i.Add("The Variable '" + Variable + "' is not in scope.");
                } else if (sym is SetConstant) {
                    i.Add("The symbol '" + Variable + "' is a Constant and may not be used with this instruction");
                }
            }

            if (sym is SetVariable sv) {
                if (!sv.Executed) {
                    Expr.Evaluate();
                }
            }
            Expr.AddExprIssues(i, Expr);

            Issues = i;
            return Issues.Count == 0;
        }

        // Legacy

        [JsonProperty]
        public string CValueExpr {
            get => null;
            set {
                Expr.Expression = value;
                RaisePropertyChanged("Expr.Expression");
            }
        }
    }
}
