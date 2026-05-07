using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System.ComponentModel.Composition;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Core.Locale;
using NINA.Core.MyMessageBox;
using NINA.Core.Utility.Notification;
using NINA.Sequencer;
using NINA.ViewModel.Sequencer;
using System.Reflection;
using NINA.Sequencer.Interfaces.Mediator;
using System.Linq;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Constant/Variable Container")]
    [ExportMetadata("Description", "A container for Constant and Variable definitions, and Annotations.")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Sequencer+ (Expressions)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]

    public class CVContainer : SequenceContainer, IValidatable {

        static protected ISequenceMediator sequenceMediator;
        static protected ISequenceNavigationVM sequenceNavigationVM;
        static protected TemplateController ninaTemplateController;
        private static ISequencerFactory sequencerFactory;

        [ImportingConstructor]
        public CVContainer(ISequenceMediator seqMediator) : base(new SequentialStrategy()) {
            sequenceMediator = seqMediator;
            if (ninaTemplateController == null) {
                FieldInfo fi = sequenceMediator.GetType().GetField("sequenceNavigation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null) {
                    sequenceNavigationVM = (ISequenceNavigationVM)fi.GetValue(sequenceMediator);
                    ISequence2VM s2vm = sequenceNavigationVM.Sequence2VM;
                    if (s2vm != null) {
                        sequencerFactory = s2vm.SequencerFactory;
                        PropertyInfo pi = s2vm.GetType().GetRuntimeProperty("TemplateController");
                        ninaTemplateController = (TemplateController)pi.GetValue(s2vm);
                    }
                }
            }
        }

        public CVContainer(CVContainer copyMe) : this(sequenceMediator) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
              }
        }

        public CVContainer(IExecutionStrategy strategy) : base(strategy) {
        }

        public override object Clone() {
            var clone = new CVContainer(this) {
                Icon = Icon,
                Name = Name,
                Items = new ObservableCollection<ISequenceItem>(Items.Select(i => i.Clone() as ISequenceItem))
            };
            foreach (var item in clone.Items) {
                item.AttachNewParent(clone);
            }
            return clone;

        }

        private GalaSoft.MvvmLight.Command.RelayCommand addTemplate;

        public ICommand AddTemplateCommand => addTemplate ??= new GalaSoft.MvvmLight.Command.RelayCommand(AddTemplate);

        public bool ShowAddTemplate {  get; set; }

        public override bool Validate() {
            return base.Validate();
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            foreach (var item in Items) {
                item.AfterParentChanged();
            }
        }

        private void AddTemplate() {
            ISequenceContainer clonedContainer = Clone() as ISequenceContainer;
            clonedContainer.AttachNewParent(null);
            clonedContainer.ResetAll();
 
            bool addTemplate = true;
            var templateExists = ninaTemplateController.UserTemplates.Any(t => t.Container.Name == clonedContainer.Name);
            if (templateExists) {
                var result = MyMessageBox.Show(string.Format(Loc.Instance["LblTemplate_OverwriteTemplateMessageBox_Text"], clonedContainer.Name),
                    Loc.Instance["LblTemplate_OverwriteTemplateMessageBox_Caption"], System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxResult.Cancel);
                addTemplate = result == System.Windows.MessageBoxResult.OK;
            }

            if (addTemplate) {
                ninaTemplateController.AddNewUserTemplate(clonedContainer);
                if (templateExists) {
                    Notification.ShowSuccess(string.Format(Loc.Instance["LblTemplate_Updated"], clonedContainer.Name));
                } else {
                    Notification.ShowSuccess(string.Format(Loc.Instance["LblTemplate_Created"], clonedContainer.Name));
                }
            }
        }
    }
}
