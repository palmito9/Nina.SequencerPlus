using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Retry")]
    [ExportMetadata("Description", "Resets and retries the conditional instruction")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    //[ExportMetadata("Category", "When (and If)")]
    //[Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class Retry : RunnerInstruction, IValidatable {
        [ImportingConstructor]
        public Retry() {
            Iterations = 1;
        }

        public Retry(Retry copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Iterations = copyMe.Iterations;
                CompletedIterations = 0;
            }
        }

        private int iterations = 1;
        
        [JsonProperty]
        public int Iterations {
            get => iterations;
            set {
                iterations = value;
                RaisePropertyChanged("Iterations"); 
            } 
        }

        [JsonProperty]
        public int CompletedIterations { get; set; }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            // Nothing to do here
            if (CompletedIterations++ < Iterations) {
                GetRunner().ShouldRetry = true;
                RaisePropertyChanged("CompletedIterations");
            }
            return Task.CompletedTask;
        }

        public override object Clone() {
            return new Retry(this) {
            };
        }

        public override void ResetProgress() {
            base.ResetProgress();
            CompletedIterations = 0;
            RaisePropertyChanged(nameof(CompletedIterations));
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(Retry)}";
        }
    }
}
