using Accord.Collections;
using Microsoft.Win32;
using Namotion.Reflection;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.ImageData;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Plugin.SequencerPlus;
using NINA.Plugin.SequencerPlus.Properties;
using NINA.Plugin.SequencerPlus.ViewModels;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.ViewModel.Sequencer;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Settings = NINA.Plugin.SequencerPlus.Properties.Settings;

namespace NINA.Plugin.SequencerPlus {
    /// <summary>
    /// This class exports the IPluginManifest interface and will be used for the general plugin information and options
    /// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
    /// 
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "When_Options" where When corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class SequencerPlusPlugin : PluginBase, INotifyPropertyChanged {
        private static IPluginOptionsAccessor PluginSettings;
        public static IProfileService ProfileService;
        private static ISequenceMediator SequenceMediator;
        public static IFilterWheelMediator FilterWheelMediator;
        static protected ISequenceNavigationVM sequenceNavigationVM;
        private static protected ISequence2VM s2vm;

        // Implementing a file pattern
        private GeometryGroup ConstantsIcon = (GeometryGroup)Application.Current.Resources["Pen_NoFill_SVG"];

        [ImportingConstructor]
        public SequencerPlusPlugin(IProfileService profileService, IOptionsVM options, IImageSaveMediator imageSaveMediator,
            ISwitchMediator switchMediator, IWeatherDataMediator weatherDataMediator, ICameraMediator cameraMediator, IDomeMediator domeMediator,
                IFlatDeviceMediator flatMediator, IFilterWheelMediator filterWheelMediator, IRotatorMediator rotatorMediator, ISafetyMonitorMediator safetyMonitorMediator,
                IFocuserMediator focuserMediator, ITelescopeMediator telescopeMediator, IImagingMediator imagingMediator, ISequenceMediator sequenceMediator, IMessageBroker messageBroker,
                IGuiderMediator guiderMediator) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            // This helper class can be used to store plugin settings that are dependent on the current profile
            PluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            ProfileService = profileService;
            // React on a changed profile
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            // Add image data variables
            imagingMediator.ImagePrepared += TakeExposure.ProcessResults;

            SequenceMediator = sequenceMediator;
            FilterWheelMediator = filterWheelMediator;

            // Hook into image saving for adding FITS keywords or image file patterns
            Symbol.SequencerPlusPluginObject = this;
            Symbol.InitMediators(switchMediator, weatherDataMediator, cameraMediator, domeMediator, flatMediator, filterWheelMediator, profileService,
                rotatorMediator, safetyMonitorMediator, focuserMediator, telescopeMediator, messageBroker, guiderMediator);
            CreateGlobalSetConstants(this);

            imageSaveMediator.BeforeFinalizeImageSaved += ImageSaveMediator_BeforeFinalizeImageSaved;

            OpenRoofFilePathDiagCommand = new RelayCommand(OpenRoofFilePathDiag);

            // Initialize conversion view model
            Conversion = new ConversionViewModel(profileService);

        }

        public override Task Teardown() {
            // Make sure to unregister an event when the object is no longer in use. Otherwise garbage collection will be prevented.
            ProfileService.ProfileChanged -= ProfileService_ProfileChanged;
            return base.Teardown();
        }
        private Task ImageSaveMediator_BeforeFinalizeImageSaved(object sender, BeforeFinalizeImageSavedEventArgs e) {
            foreach (AddImagePattern.ImagePatternExpr pe in AddImagePattern.ImagePatterns) {
                ImagePattern p = pe.Pattern;
                Expr expr = pe.Expr;

                if (expr.SequenceEntity == null || expr.SequenceEntity.Parent == null) {
                    continue;
                }

                expr.Evaluate();
                string v = (expr.Error != null) ? "ERROR" : expr.ValueString;
                e.AddImagePattern(new ImagePattern(p.Key, p.Description, p.Category) { Value = v });
            }
            return Task.CompletedTask;
        }

        public static ISequenceItem GetRunningItem() {
            if (sequenceNavigationVM == null) {
                FieldInfo fi = SequenceMediator.GetType().GetField("sequenceNavigation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null) {
                    sequenceNavigationVM = (ISequenceNavigationVM)fi.GetValue(SequenceMediator);
                    s2vm = sequenceNavigationVM.Sequence2VM;
                }
            } else if (s2vm == null) {
                s2vm = sequenceNavigationVM.Sequence2VM;
            }

            try {
                if (SequenceMediator.IsAdvancedSequenceRunning()) {
                    ISequenceRootContainer root = s2vm.Sequencer.MainContainer;
                    Type type = typeof(SequenceRootContainer);
                    FieldInfo f = type.GetField("runningItems", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) {
                        try {
                            List<ISequenceItem> runningItems = (List<ISequenceItem>)f.GetValue(root);
                            if (runningItems.Count > 0) {
                                return runningItems[0];
                            }
                        } catch (Exception) {
                            Logger.Error("Can't get running items!");
                        }
                    }
                }
            } catch (Exception ex) {
                Logger.Warning("Can't get running items: " + ex.Message);
            }
            return null;
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            // Rase the event that this profile specific value has been changed due to the profile switch
            Globals.Items.Clear();
            CreateGlobalSetConstants(this);
            RaisePropertyChanged(nameof(ProfileSpecificNotificationMessage));
        }

        public SequenceContainer Globals {
            get => Symbol.GlobalContainer;
            set { }
        }

        public static double GetLatitude() {
            return ProfileService.ActiveProfile.AstrometrySettings.Latitude;
        }

        public static double GetLongitude() {
            return ProfileService.ActiveProfile.AstrometrySettings.Longitude;
        }

        public ICommand OpenRoofFilePathDiagCommand { get; private set; }

        private void OpenRoofFilePathDiag(object obj) {
            var dialog = GetFilteredFileDialog(string.Empty, string.Empty, "Text File (*.txt)|*.txt");
            if (dialog.ShowDialog() == true) {
                RoofStatus = dialog.FileName;
            }
        }

        private bool iLogMode = false;
        public bool LogMode {
            get {
                return iLogMode;
            }
            set {
                iLogMode = value;
                SPLogger.LogLevel = value ? SPLogger.Level.DEBUG : SPLogger.Level.INFO;
            }
        }

        public static Microsoft.Win32.OpenFileDialog GetFilteredFileDialog(string path, string filename, string filter) {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            if (File.Exists(path)) {
                dialog.InitialDirectory = Path.GetDirectoryName(path);
            }
            dialog.FileName = filename;
            dialog.Filter = filter;
            return dialog;
        }

        private void CreateGlobalSetConstants (SequencerPlusPlugin plugin) {
            Globals.Name = "Global Constants";
            var def = Properties.Settings.Default;
            Globals.Items.Add(new SetConstant() { Constant = Name1, CValueExpr = Value1, AllProfiles = All1, GlobalName = "Name1", GlobalValue = "Value1", GlobalAll = "All1" });
            Globals.Items.Add(new SetConstant() { Constant = Name2, CValueExpr = Value2, AllProfiles = All2, GlobalName = "Name2", GlobalValue = "Value2", GlobalAll = "All2" });
            Globals.Items.Add(new SetConstant() { Constant = Name3, CValueExpr = Value3, AllProfiles = All3, GlobalName = "Name3", GlobalValue = "Value3", GlobalAll = "All3" });
            Globals.Items.Add(new SetConstant() { Constant = Name4, CValueExpr = Value4, AllProfiles = All4, GlobalName = "Name4", GlobalValue = "Value4", GlobalAll = "All4" });
            Globals.Items.Add(new SetConstant() { Constant = Name5, CValueExpr = Value5, AllProfiles = All5, GlobalName = "Name5", GlobalValue = "Value5", GlobalAll = "All5" });
            Globals.Items.Add(new SetConstant() { Constant = Name6, CValueExpr = Value6, AllProfiles = All6, GlobalName = "Name6", GlobalValue = "Value6", GlobalAll = "All6" });
            Globals.Items.Add(new SetConstant() { Constant = Name7, CValueExpr = Value7, AllProfiles = All7, GlobalName = "Name7", GlobalValue = "Value7", GlobalAll = "All7" });
            Globals.Items.Add(new SetConstant() { Constant = Name8, CValueExpr = Value8, AllProfiles = All8, GlobalName = "Name8", GlobalValue = "Value8", GlobalAll = "All8" });
            Globals.Items.Add(new SetConstant() { Constant = Name9, CValueExpr = Value9, AllProfiles = All9, GlobalName = "Name9", GlobalValue = "Value9", GlobalAll = "All9" });
            Globals.Items.Add(new SetConstant() { Constant = Name10, CValueExpr = Value10, AllProfiles = All10, GlobalName = "Name10", GlobalValue = "Value10", GlobalAll = "All10" });


            foreach (var item in Globals.Items) {
                item.AttachNewParent(Globals);
                item.Icon = ConstantsIcon;
                item.Name = "Global Constant";
            }

            Globals.Validate();
            RaisePropertyChanged("Globals");
        }

        public string DockableExprs {
            get {
                return PluginSettings.GetValueString(nameof(DockableExprs), Settings.Default.DockableExprs);
            }
            set {
                PluginSettings.SetValueString(nameof(DockableExprs), value);
            }
        }

        public string RoofStatus {
            get {
                if (!All1) {
                    return PluginSettings.GetValueString(nameof(RoofStatus), Settings.Default.RoofStatus);
                } else {
                    return Settings.Default.RoofStatus;
                }
            }
            set {
                if (!All1) {
                    PluginSettings.SetValueString(nameof(RoofStatus), value);
                } else {
                    Settings.Default.RoofStatus = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public string RoofOpenString {
            get {
                if (!All1) {
                    return PluginSettings.GetValueString(nameof(RoofOpenString), Settings.Default.RoofOpenString);
                } else {
                    return Settings.Default.RoofOpenString;
                }
            }
            set {
                if (!All1) {
                    PluginSettings.SetValueString(nameof(RoofOpenString), value);
                } else {
                    Settings.Default.RoofOpenString = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name1 {
            get {
                if (!All1) {
                    return PluginSettings.GetValueString(nameof(Name1), Settings.Default.Name1);
                } else {
                    return Settings.Default.Name1;
                }
            }
            set {
                if (!All1) {
                    PluginSettings.SetValueString(nameof(Name1), value);
                } else {
                    Settings.Default.Name1 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value1 {
            get {
                if (!All1) {
                    return PluginSettings.GetValueString(nameof(Value1), Settings.Default.Value1);
                } else {
                    return Settings.Default.Value1;
                }
            }
            set {
                if (!All1) {
                    PluginSettings.SetValueString(nameof(Value1), value);
                } else {
                    Settings.Default.Value1 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name2 {
            get {
                if (!All2) {
                    return PluginSettings.GetValueString(nameof(Name2), Settings.Default.Name2);
                } else {
                    return Settings.Default.Name2;
                }
            }
            set {
                if (!All2) {
                    PluginSettings.SetValueString(nameof(Name2), value);
                } else {
                    Settings.Default.Name2 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value2 {
            get {
                if (!All2) {
                    return PluginSettings.GetValueString(nameof(Value2), Settings.Default.Value2);
                } else {
                    return Settings.Default.Value2;
                }
            }
            set {
                if (!All2) {
                    PluginSettings.SetValueString(nameof(Value2), value);
                } else {
                    Settings.Default.Value2 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name3 {
            get {
                if (!All3) {
                    return PluginSettings.GetValueString(nameof(Name3), Settings.Default.Name3);
                } else {
                    return Settings.Default.Name3;
                }
            }
            set {
                if (!All3) {
                    PluginSettings.SetValueString(nameof(Name3), value);
                } else {
                    Settings.Default.Name3 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value3 {
            get {
                if (!All3) {
                    return PluginSettings.GetValueString(nameof(Value3), Settings.Default.Value4);
                } else {
                    return Settings.Default.Value3;
                }
            }
            set {
                if (!All3) {
                    PluginSettings.SetValueString(nameof(Value3), value);
                } else {
                    Settings.Default.Value3 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name4 {
            get {
                if (!All4) {
                    return PluginSettings.GetValueString(nameof(Name4), Settings.Default.Name4);
                } else {
                    return Settings.Default.Name4;
                }
            }
            set {
                if (!All4) {
                    PluginSettings.SetValueString(nameof(Name4), value);
                } else {
                    Settings.Default.Name4 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value4 {
            get {
                if (!All4) {
                    return PluginSettings.GetValueString(nameof(Value4), Settings.Default.Value4);
                } else {
                    return Settings.Default.Value4;
                }
            }
            set {
                if (!All4) {
                    PluginSettings.SetValueString(nameof(Value4), value);
                } else {
                    Settings.Default.Value4 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name5 {
            get {
                if (!All5) {
                    return PluginSettings.GetValueString(nameof(Name5), Settings.Default.Name5);
                } else {
                    return Settings.Default.Name5;
                }
            }
            set {
                if (!All5) {
                    PluginSettings.SetValueString(nameof(Name5), value);
                } else {
                    Settings.Default.Name5 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value5 {
            get {
                if (!All5) {
                    return PluginSettings.GetValueString(nameof(Value5), Settings.Default.Value5);
                } else {
                    return Settings.Default.Value5;
                }
            }
            set {
                if (!All5) {
                    PluginSettings.SetValueString(nameof(Value5), value);
                } else {
                    Settings.Default.Value5 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name6 {
            get {
                if (!All6) {
                    return PluginSettings.GetValueString(nameof(Name6), Settings.Default.Name6);
                } else {
                    return Settings.Default.Name6;
                }
            }
            set {
                if (!All6) {
                    PluginSettings.SetValueString(nameof(Name6), value);
                } else {
                    Settings.Default.Name6 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value6 {
            get {
                if (!All6) {
                    return PluginSettings.GetValueString(nameof(Value6), Settings.Default.Value6);
                } else {
                    return Settings.Default.Value6;
                }
            }
            set {
                if (!All6) {
                    PluginSettings.SetValueString(nameof(Value6), value);
                } else {
                    Settings.Default.Value6 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name7 {
            get {
                if (!All7) {
                    return PluginSettings.GetValueString(nameof(Name7), Settings.Default.Name7);
                } else {
                    return Settings.Default.Name7;
                }
            }
            set {
                if (!All7) {
                    PluginSettings.SetValueString(nameof(Name7), value);
                } else {
                    Settings.Default.Name7 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value7 {
            get {
                if (!All7) {
                    return PluginSettings.GetValueString(nameof(Value7), Settings.Default.Value7);
                } else {
                    return Settings.Default.Value7;
                }
            }
            set {
                if (!All7) {
                    PluginSettings.SetValueString(nameof(Value7), value);
                } else {
                    Settings.Default.Value7 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name8 {
            get {
                if (!All8) {
                    return PluginSettings.GetValueString(nameof(Name8), Settings.Default.Name8);
                } else {
                    return Settings.Default.Name8;
                }
            }
            set {
                if (!All8) {
                    PluginSettings.SetValueString(nameof(Name8), value);
                } else {
                    Settings.Default.Name8 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value8 {
            get {
                if (!All8) {
                    return PluginSettings.GetValueString(nameof(Value8), Settings.Default.Value8);
                } else {
                    return Settings.Default.Value8;
                }
            }
            set {
                if (!All8) {
                    PluginSettings.SetValueString(nameof(Value8), value);
                } else {
                    Settings.Default.Value8 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name9 {
            get {
                if (!All9) {
                    return PluginSettings.GetValueString(nameof(Name9), Settings.Default.Name9);
                } else {
                    return Settings.Default.Name9;
                }
            }
            set {
                if (!All9) {
                    PluginSettings.SetValueString(nameof(Name9), value);
                } else {
                    Settings.Default.Name9 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value9 {
            get {
                if (!All9) {
                    return PluginSettings.GetValueString(nameof(Value9), Settings.Default.Value9);
                } else {
                    return Settings.Default.Value9;
                }
            }
            set {
                if (!All9) {
                    PluginSettings.SetValueString(nameof(Value9), value);
                } else {
                    Settings.Default.Value9 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Name10 {
            get {
                if (!All10) {
                    return PluginSettings.GetValueString(nameof(Name10), Settings.Default.Name10);
                } else {
                    return Settings.Default.Name10;
                }
            }
            set {
                if (!All10) {
                    PluginSettings.SetValueString(nameof(Name10), value);
                } else {
                    Settings.Default.Name10 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string Value10 {
            get {
                if (!All10) {
                    return PluginSettings.GetValueString(nameof(Value10), Settings.Default.Value10);
                } else {
                    return Settings.Default.Value10;
                }
            }
            set {
                if (!All10) {
                    PluginSettings.SetValueString(nameof(Value10), value);
                } else {
                    Settings.Default.Value10 = value;
                }
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }



        public bool All1 {
            get { 
                return Settings.Default.All1;
            }
            set {
                Settings.Default.All1 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All2 {
            get {
                return Settings.Default.All2;
            }
            set {
                Settings.Default.All2 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All3 {
            get {
                return Settings.Default.All3;
            }
            set {
                Settings.Default.All3 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All4 {
            get {
                return Settings.Default.All4;
            }
            set {
                Settings.Default.All4 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All5 {
            get {
                return Settings.Default.All5;
            }
            set {
                Settings.Default.All5 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All6 {
            get {
                return Settings.Default.All6;
            }
            set {
                Settings.Default.All6 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All7 {
            get {
                return Settings.Default.All7;
            }
            set {
                Settings.Default.All7 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All8 {
            get {
                return Settings.Default.All8;
            }
            set {
                Settings.Default.All8 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All9 {
            get {
                return Settings.Default.All9;
            }
            set {
                Settings.Default.All9 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public bool All10 {
            get {
                return Settings.Default.All10;
            }
            set {
                Settings.Default.All10 = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }


        private string GetValue(string name) {
            return PluginSettings.GetValueString(name, string.Empty);
        }

        private void SetValue(string name, string value) {
            PluginSettings.SetValueString(name, value);
            CoreUtil.SaveSettings(Settings.Default);
        }


        public string ProfileSpecificNotificationMessage {
            get {
                return PluginSettings.GetValueString(nameof(ProfileSpecificNotificationMessage), string.Empty);
            }
            set {
                PluginSettings.SetValueString(nameof(ProfileSpecificNotificationMessage), value);
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ConversionViewModel Conversion { get; private set; }

    }
}
