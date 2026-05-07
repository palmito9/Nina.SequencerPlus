using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces;
using NINA.PlateSolving.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.SequenceItem.Utility;
using NINA.Sequencer.Trigger;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.Interfaces;
using System;
using System.ComponentModel.Composition;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Autofocus Trigger")]
    [ExportMetadata("Description", "This trigger will run an Autofocus operation after the currently running instruction finishes.")]
    [ExportMetadata("Icon", "AutoFocusSVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    [Export(typeof(ISequenceTrigger))]
    
    [JsonObject(MemberSerialization.OptIn)]
    public class AutofocusTrigger : SequenceTrigger {

        protected IProfileService profileService;
        protected ITelescopeMediator telescopeMediator;
        protected IApplicationStatusMediator applicationStatusMediator;
        protected ICameraMediator cameraMediator;
        protected IFocuserMediator focuserMediator;
        protected IMeridianFlipVMFactory meridianFlipVMFactory;
        protected IGuiderMediator guiderMediator;
        protected IImageHistoryVM history;
        protected IFilterWheelMediator filterWheelMediator;
        protected IAutoFocusVMFactory autoFocusVMFactory;
        protected IDomeMediator domeMediator;
        protected IDomeFollower domeFollower;
        protected IPlateSolverFactory plateSolverFactory;
        protected IWindowServiceFactory windowServiceFactory;
        protected IImagingMediator imagingMediator;
        private GeometryGroup HourglassIcon = (GeometryGroup)Application.Current.Resources["HourglassSVG"];
        private GeometryGroup CameraIcon = (GeometryGroup)Application.Current.Resources["CameraSVG"];

        [ImportingConstructor]
        public AutofocusTrigger(IProfileService profileService, ICameraMediator cameraMediator, ITelescopeMediator telescopeMediator,
            IFocuserMediator focuserMediator, IApplicationStatusMediator applicationStatusMediator, IMeridianFlipVMFactory meridianFlipVMFactory,
            IGuiderMediator guiderMediator, IImageHistoryVM history, IFilterWheelMediator filterWheelMediator, IAutoFocusVMFactory autoFocusVMFactory,
            IDomeMediator domeMediator, IDomeFollower domeFollower, IPlateSolverFactory plateSolverFactory, IWindowServiceFactory windowServiceFactory,
            IImagingMediator imagingMediator) : base() {
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            this.cameraMediator = cameraMediator;
            this.focuserMediator = focuserMediator;
            this.meridianFlipVMFactory = meridianFlipVMFactory;
            this.guiderMediator = guiderMediator;
            this.history = history;
            this.filterWheelMediator = filterWheelMediator;
            this.autoFocusVMFactory = autoFocusVMFactory;
            this.domeMediator = domeMediator;
            this.domeFollower = domeFollower;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.imagingMediator = imagingMediator;
            Name = Name;
            Icon = Icon;
            AddItem(TriggerRunner, new RunAutofocus(profileService, history, cameraMediator, filterWheelMediator, focuserMediator,
                autoFocusVMFactory) { Name = "Run Autofocus", Icon = CameraIcon });
        }
        private void AddItem(SequentialContainer runner, ISequenceItem item) {
            runner.Items.Add(item);
            item.AttachNewParent(runner);
        }

        private AutofocusTrigger(AutofocusTrigger copyMe) {
            CopyMetaData(copyMe);
            Name = copyMe.Name;
            Icon = copyMe.Icon;
            TriggerRunner = (SequentialContainer)copyMe.TriggerRunner.Clone();
        }

        public override object Clone() {
            return new AutofocusTrigger(this);
        }

        public bool InFlight { get; set; }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            InFlight = true;
            try {
                await TriggerRunner.Run(progress, token);
            } finally {
                //InFlight = false;
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return !InFlight;
        }

        Random random = new Random();

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(InterruptTrigger)}";
        }
    }
}