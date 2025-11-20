using System;
using System.Threading.Tasks;

using System.Threading;
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
using NINA.Sequencer.SequenceItem;
using System.ComponentModel.Composition;
using NINA.Core.Utility;
using System.Windows.Input;
using NINA.Sequencer.Conditions;
using System.Text;
using Accord;

namespace WhenPlugin.When {

    [ExportMetadata("Name", "Assign Variables")]
    [ExportMetadata("Description", "Assign Variables in For Each loop")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Powerups (Fun-ctions)")]
    //[Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AssignVariables : SequenceItem {

        [ImportingConstructor]
        public AssignVariables() {
        }

        private AssignVariables(AssignVariables cloneMe) : base(cloneMe) {
        }

        public override object Clone() {
            return new AssignVariables() { Name = this.Name, Icon = this.Icon };
        }

        private string assignments = "";
        public string Assignments {
            get {
                return assignments;
            }
            protected set {
                assignments = value;
                RaisePropertyChanged("Assignments");
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {

            ForEachList fe = Parent as ForEachList;
            if (fe == null) {
                throw new SequenceEntityFailedException("Assign Variables is not a child of For Each List");
            }

            string v = fe.Variable;
            string le = fe.ListExpression;

            string val = fe.ValidateArguments();
            if (val != null) {
                Logger.Error("Validation failed: " + val);
                Logger.Error("Variable: " +  fe.Variable);
                Logger.Error("ListExpression: " + fe.ListExpression);
                throw new SequenceEntityFailedException("Syntax error in Variable/List Expression");
            }

            LoopCondition lc = fe.Conditions[0] as LoopCondition;
            if (lc == null) {
                throw new SequenceEntityFailedException("No LoopCondition in ForEachList?");
            }

            int currentIteration = lc.CompletedIterations;

            try {
                string[] exprsList = fe.ETokens[currentIteration].Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                StringBuilder sb = new StringBuilder("Assignments: ");
                for (int vv = 0; vv < fe.VTokens.Length; vv++) {
                    string var = fe.VTokens[vv];

                    string expr = exprsList[vv];

                    ResetVariable rv = new ResetVariable();
                    rv.AttachNewParent(Parent);
                    rv.Variable = var;
                    double d;
                    if (!Double.TryParse(expr, out d)) {
                        expr = "'" + expr + "'";
                    }

                    rv.Expr.Expression = expr;
                    Logger.Info("ForEach iteration: Variable = " + var + ", Expression: " + expr);
                    sb.Append(var + " = " + expr + "  ");
                    await rv.Execute(progress, token);
                }
                Assignments = sb.ToString();
            } catch (Exception e) {
                await Parent.Interrupt();
                throw new SequenceEntityFailedException("Exception in AssignVariables: " + e.Message);

            }
        } 
            

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(AssignVariables)}";
        }
    }
}