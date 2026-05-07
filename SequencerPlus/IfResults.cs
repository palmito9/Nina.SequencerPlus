using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using NCalc;
using Castle.Core.Internal;
using NINA.Core.Utility.Notification;
using NINA.Core.Enum;
using System.Linq;
using System.Text;
using Accord.IO;
using Namotion.Reflection;
using NINA.Sequencer.DragDrop;
using System.Windows.Input;
using NINA.Sequencer.Conditions;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "If Results")]
    [ExportMetadata("Description", "Executes an instruction set if the predicate, based on the results of the previous instruction, is true")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "When")]
    //[Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class IfResults : IfCommand, IValidatable {
    
        [ImportingConstructor]
        public IfResults() {
            Predicate = "";
            Instructions = new IfContainer();
            Condition = new IfContainer();
            DropIntoIfCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoIf);
        }
        public IfResults(IfResults copyMe) : this() {
           if (copyMe != null) {
                CopyMetaData(copyMe);
                Predicate = copyMe.Predicate;
                Condition = (IfContainer)copyMe.Condition.Clone();
                Instructions = (IfContainer)copyMe.Instructions.Clone();
            }
        }

        public ICommand DropIntoIfCommand { get; set; }
        
        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            ISequenceItem condition = Condition.Items[0];

            if (Predicate.IsNullOrEmpty()) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            int iterations = 1;
            LoopCondition loop = null;

            // If this is a TakeManyExposures or SmartExposure, we need to be in a LoopForIterations
            if (condition is SequentialContainer seq && !seq.Items.IsNullOrEmpty() && !seq.Conditions.IsNullOrEmpty()) {
                condition = seq.Items[0];
                if (seq.Conditions[0] is LoopCondition l) {
                    loop = l;
                    iterations = loop.Iterations;
                } else {
                    /// WTF
                }
            }

            // The TakeManyExposures loop
            while (iterations-- > 0) {

                // The Retry loop - wheels within wheels!
                while (true) {

                    // Run the instruction whose instructions we await
                    condition.Status = SequenceEntityStatus.CREATED;
                    await condition.Run(progress, token);

                    // This must be true or it couldn't have been added to IfResults
                    IInstructionResults instructionWithResults = condition as IInstructionResults;
                    InstructionResult results = instructionWithResults.GetResults();

                    NCalc.Expression e = new NCalc.Expression(Predicate);

                    foreach (var item in results) {
                        string key = RemoveSpecialCharacters(item.Key);
                        e.Parameters[key] = item.Value;
                    }

                    // Evaluate predicate
                    if (e.HasErrors()) {
                        // Syntax error...
                        Notification.ShowError("There is a syntax error in your predicate expression.");
                    }

                    var result = e.Evaluate();
                    if (result != null && result is Boolean && (Boolean)result) {
                        Notification.ShowSuccess("If Predicate is true!");
                        Runner runner = new Runner(Instructions, instructionWithResults, progress, token);
                        await runner.RunConditional();
                        if (runner.ShouldRetry) {
                            runner.ResetProgress();
                            runner.ShouldRetry = false;
                            Notification.ShowSuccess("IfResult; retrying the failed instruction");
                            continue;
                        }
                    } else {
                        Notification.ShowSuccess("IfSwitch Predicate is false!");
                        break;
                    }

                    condition.ResetProgress();
                    if (loop != null) {
                        loop.CompletedIterations++;
                    }
                }

                // Let's do the whole thing again
                Instructions.ResetProgress();
            }
        }

        public override object Clone() {
            return new IfResults(this) {
            };
        }
  
        [JsonProperty]
        public string Predicate { get; set; }

        public override void ResetProgress() {
            base.ResetProgress();
            Condition.Items[0].ResetProgress();   
            foreach (ISequenceItem item in Instructions.Items) {
                item.ResetProgress();
            }
        }

        public string ClassName {
            get => GetType().FullName + "," + GetType().Assembly;
            set { }
        }

        private void DropIntoIf(DropIntoParameters parameters) {
            lock (lockObj) {
                ISequenceItem item;
                if (!(parameters.Source is IInstructionResults itm)) {
                    return;
                }

                var source = parameters.Source as ISequenceItem;

                if (source.Parent != null && !parameters.Duplicate) {
                    item = source;
                } else {
                    item = (ISequenceItem)source.Clone();
                }

                if (item.Parent != Condition) {
                    item.Parent?.Remove(item);
                    item.AttachNewParent(Condition);
                }

                Condition.Items.Clear();
                Condition.Items.Add(item);
            }
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfResults)}, Predicate: {Predicate}";
        }

        public new bool Validate() {
            IList<string> i = new List<string>();   

            if (Condition.Items.IsNullOrEmpty()) {
                i.Add("There must be an instruction to execute");
            } else if (Condition.Items[0] is IValidatable val) {
                val.Validate();
            }
            
            foreach(ISequenceItem item in Instructions.Items) {
                if (item is IValidatable valu) {
                    valu.Validate();
                }
            }

            if (Predicate.IsNullOrEmpty()) {
                i.Add("There must be a condition to evaluate");
            } else {
                try {
                    NCalc.Expression e = new NCalc.Expression(Predicate);
                    if (e.HasErrors()) {
                        i.Add("The expression could not be parsed: " + e.Error);
                    }
                } catch (Exception ex) {
                    i.Add("The expression could not be parsed: " + ex.Message);
                }
            }

            Issues = i;
            return i.Count == 0;
       }
    }
}
