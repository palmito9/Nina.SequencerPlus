using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.DragDrop;
using System.Windows.Input;
using System.Text.RegularExpressions;
using NINA.Core.Utility;
using Serilog.Debugging;
using Google.Protobuf.WellKnownTypes;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Send via Ground Station")]
    [ExportMetadata("Description", "Send a message via Ground Station, including Sequencer+ Expressions.")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Sequencer+ (Fun-ctions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class GSSend : IfCommand {

        [ImportingConstructor]
        public GSSend() {
            Condition = new IfContainer();
            Instructions = new IfContainer();
            DropIntoIfCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoCondition);
        }
        public GSSend(GSSend copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Condition = (IfContainer)copyMe.Condition.Clone();
                Instructions = (IfContainer)copyMe.Instructions.Clone();
            }
        }

        public override object Clone() {
            return new GSSend(this) {
            };
        }

        public ICommand DropIntoIfCommand { get; set; }

        public string ProcessedScript(string message) {
            string value = message;
            if (value != null) {
                while (true) {
                    string toReplace = Regex.Match(value, @"\{([^\}]+)\}").Groups[1].Value;
                    if (toReplace == null) {
                        Logger.Error("toReplace is null?");
                        break;
                    }
                    if (toReplace.Length == 0) break;
                    Expr ex = new Expr(this, toReplace, "Any");
                    if (ex.Error != null) {
                        Logger.Warning("Send via Ground Station, error processing script, " + ex.Error);
                        value = value.Replace("{" + toReplace + "}", ex.Error);
                    } else if (ex.StringValue != null) {
                        value = value.Replace("{" + toReplace + "}", ex.StringValue);
                    } else {
                        value = value.Replace("{" + toReplace + "}", ex.ValueString);
                    }
                    Logger.Info("Replacing " + toReplace + " with " + ex.ValueString);
                }
            }
            return value;
        }


        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            ISequenceItem condition = Condition.Items[0];

            if (condition == null) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            // Execute the conditional
            condition.Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;

            var messageProperty = condition.GetType().GetProperty("Message");
            if (messageProperty == null) {
                messageProperty = condition.GetType().GetProperty("Payload");
                if (messageProperty == null) {
                    throw new SequenceEntityFailedException("Not a Ground Station instruction?");
                }
            }
            string message = (string)messageProperty.GetValue(condition);
            if (message == null) {
                throw new SequenceEntityFailedException("Message is null?");
            }
            message = message.Replace('\t', ' ');
            var processedMessage = ProcessedScript(message);
            Logger.Info("Sending to Ground Station: " + processedMessage);
            if (processedMessage == null) {
                throw new SequenceEntityFailedException("Processed message is null?");
            }
            messageProperty.SetValue(condition, processedMessage, null);
            condition.AttachNewParent(Parent);
            await condition.Run(progress, token);
            messageProperty.SetValue(condition, message, null);
        }

        // Allow only ONE instruction to be added to Condition
        public void DropIntoCondition (DropIntoParameters parameters) {
            lock (lockObj) {
                ISequenceItem item;
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

        public override bool Validate() {
            Issues.Clear();
            if (Condition == null || Condition.Items.Count == 0) {
                issues.Add("There must be a Ground Station instruction included in this instruction");
            } else {
                var c = Condition.Items[0];

                var messageProperty = c.GetType().GetProperty("Message");
                if (messageProperty == null) {
                    messageProperty = c.GetType().GetProperty("Payload");
                    if (messageProperty == null) {
                        issues.Add("This instruction cannot be used with Send via Ground Station");
                    }
                }
             }
            RaisePropertyChanged("Issues");
            return issues.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(GSSend)}";
        }
    }
}
