using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Rotate Image")]
    [ExportMetadata("Description", "Rotates the image in the Imaging pane by 180 degrees")]
    [ExportMetadata("Icon", "MeridianFlipSVG")]
    [ExportMetadata("Category", "Sequencer+ (Meridian Flip)")]
    [Export(typeof(ISequenceItem))]

    [JsonObject(MemberSerialization.OptIn)]

    public class RotateImage : SequenceItem, IValidatable {

        private IImagingMediator imagingMediator;

        [ImportingConstructor]
        public RotateImage(IImagingMediator imagingMediator) {
            this.imagingMediator = imagingMediator;
        }

        public RotateImage(RotateImage copyMe) : this(copyMe.imagingMediator) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
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
            imagingMediator.SetImageRotation(imagingMediator.GetImageRotation() + 180);
            return Task.CompletedTask;
        }


        public override object Clone() {
            return new RotateImage(this) {
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(RotateImage)}";
        }

        public bool Validate() {
            var i = new List<string>();
            Issues = i;
            return i.Count == 0;
        }
    }
}
