using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Prepend to Last Image")]
    [ExportMetadata("Description", "Prepends the specified prefix to the file name of the most recently saved image")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "When")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class PrependToLastImage : RunnerInstruction, IValidatable
    {
        [ImportingConstructor]
        public PrependToLastImage() {
            Prepend = "BAD_";
        }

        public PrependToLastImage(PrependToLastImage copyMe) : this() {
           if (copyMe != null) {
                CopyMetaData(copyMe);
                Prepend = copyMe.Prepend;
            }
        }
 
        [JsonProperty]
        public string Prepend { get; set; }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {

            // Find the Runner responsible for this command
            IInstructionResults conditional = GetRunnerInstruction();

            if (conditional != null) {
                InstructionResult results = conditional.GetResults();
                // Need to get file name from If?  Or previous TakeExposure
                if (conditional.GetResults() != null) {
                    results.TryGetValue("_ImageUri_", out object fn);
                    if (fn != null) {
                        Uri uri = fn as Uri;
                        if (uri.IsFile) {
                            string oldFileName = uri.LocalPath;
                            if (File.Exists(oldFileName)) {
                                string fileName = Prepend + Path.GetFileName(oldFileName);
                                string dirName = Path.GetDirectoryName(oldFileName);
                                string newFileName = Path.Join(dirName, fileName);
                                try {
                                    File.Move(oldFileName, newFileName);
                                    Logger.Info("PrependToLastImage: image renamed to " + newFileName);
                                } catch (Exception ex) {
                                    Logger.Info("PrependToLastImage: image rename failed, " + ex.Message);
                                }
                                return Task.CompletedTask;
                            }
                        }
                    }
                    Logger.Warning("PrependToLastImage: Image file name not found?");
                }
            } else {
                Logger.Warning("PrependToLastImage: Can't find corresponding If instruction.");
            }
            return Task.CompletedTask;
        }

        public override object Clone() {
            return new PrependToLastImage(this) {
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SetConstant)}, Prepend: {Prepend}";
        }
    }
}
