using Accord.IO;
using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyPlanetarium;
using NINA.Profile;
using NINA.Sequencer;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Foo")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [Export(typeof(ISequenceContainer))]
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]

    public class TemplateContainer : IfContainer {

        public TemplateContainer() : base() {
            DropIntoTemplateCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoTemplate);
        }

        public TemplateContainer (TemplateContainer copyMe) : this() {
            Items = new ObservableCollection<ISequenceItem>(Items.Select(i => i.Clone() as ISequenceItem));
            foreach (var item in Items) {
                item.AttachNewParent(this);
            }
            AttachNewParent(copyMe.Parent);
        }

        public override TemplateContainer Clone() {
            return new TemplateContainer(this);
        }

        private object lockObj = new object();

        public override string ToString() {
            if (PseudoParent != null) {
                return "Template Container " + PseudoParent.ToString();
            }
            return "TemplateContainer?";
        }

        public ICommand DropIntoTemplateCommand { get; set; }

        // TODO: Allow only ONE instruction to be added to Instructions
        public void DropIntoTemplate(DropIntoParameters parameters) {
            lock (lockObj) {
                TemplatedSequenceContainer tsc = parameters.Source as TemplatedSequenceContainer;
                TemplateContainer tc = parameters.Target as TemplateContainer;
                ((TemplateByReference)tc.PseudoParent).SelectedTemplate = tsc;
            }
        }

    }
}
