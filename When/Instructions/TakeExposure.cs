#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Validations;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Core.Model.Equipment;
using NINA.Core.Locale;
using NINA.Equipment.Model;
using NINA.Equipment.Equipment.MyCamera;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Sequencer.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Image.Interfaces;
using NINA.Image.ImageData;
using Namotion.Reflection;
using System.Diagnostics;
using Antlr.Runtime;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Windows.Media.Converters;
using NINA.Sequencer.SequenceItem.Imaging;

namespace WhenPlugin.When {

    [ExportMetadata("Name", "Take Exposure +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Imaging_TakeExposure_Description")]
    [ExportMetadata("Icon", "CameraSVG")]
    [ExportMetadata("Category", "Powerups (Enhanced Instructions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TakeExposure : SequenceItem, IExposureItem, IValidatable {
        private ICameraMediator cameraMediator;
        private IImagingMediator imagingMediator;
        private IImageSaveMediator imageSaveMediator;
        private IImageHistoryVM imageHistoryVM;
        private IProfileService profileService;
        Task imageProcessingTask;

        [ImportingConstructor]
        public TakeExposure(IProfileService profileService, ICameraMediator cameraMediator, IImagingMediator imagingMediator, IImageSaveMediator imageSaveMediator, IImageHistoryVM imageHistoryVM) {
            ImageType = CaptureSequence.ImageTypes.LIGHT;
            this.cameraMediator = cameraMediator;
            this.imagingMediator = imagingMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVM = imageHistoryVM;
            this.profileService = profileService;
            CameraInfo = this.cameraMediator.GetInfo();
            EExpr = new Expr(this);
            GExpr = new Expr(this, "", "Integer");
            OExpr = new Expr(this, "", "Integer");

        }

        private TakeExposure(TakeExposure cloneMe) : this(cloneMe.profileService, cloneMe.cameraMediator, cloneMe.imagingMediator, cloneMe.imageSaveMediator, cloneMe.imageHistoryVM) {
            CopyMetaData(cloneMe);
            GExpr = new Expr(this, cloneMe.GExpr.Expression, "Integer", ValidateGain);
            OExpr = new Expr(this, cloneMe.OExpr.Expression, "Integer", ValidateOffset);
            EExpr = new Expr(this, cloneMe.EExpr.Expression);
            EExpr.Default = 0;
        }

        public override object Clone() {
            var clone = new TakeExposure(this) {
                ExposureCount = 0,
                Binning = Binning,
                ImageType = ImageType,
            };

            if (clone.Binning == null) {
                clone.Binning = new BinningMode(1, 1);
            }

            return clone;
        }

        [JsonProperty]
        public Expr EExpr { get; set; }
        [JsonProperty]
        public Expr GExpr { get; set; }
        [JsonProperty]
        public Expr OExpr { get; set; }



        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }


        [JsonProperty]
        public string ExposureTimeExpr {
            get => null;
            set {
                EExpr.Expression = value;
            }
        }

        [JsonProperty]
        public string GainExpr {
            get => null;
            set {
                GExpr.Expression = value;
            }
        }

 
        [JsonProperty]
        public string OffsetExpr {
            get => null;
            set {
                OExpr.Expression = value;
            }
        }

        public string ValidateOffset(double offset) {
            return iValidateOffset(offset, new List<string>());
        }

        public string iValidateOffset(double gain, List<string> i) {
            var iCount = i.Count;

            CameraInfo = this.cameraMediator.GetInfo();
            if (!CameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            } else if (OExpr.Value < 0) {
                i.Add("Offset cannot be less than 0");
            } else if (CameraInfo.CanSetOffset && OExpr.Value > -1 && (OExpr.Value < CameraInfo.OffsetMin | OExpr.Value > CameraInfo.OffsetMax)) {
                i.Add(string.Format(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_Offset"], CameraInfo.OffsetMin, CameraInfo.OffsetMax, OExpr.Value));
            }

            if (iCount == i.Count) {
                return String.Empty;
            } else {
                return i[iCount];
            }
        }

        public bool CanSubsample { get; set; } = false;
        
        private BinningMode binning;

        [JsonProperty]
        public BinningMode Binning { get => binning; set { binning = value; RaisePropertyChanged(); } }

        private string imageType;

        [JsonProperty]
        public string ImageType { get => imageType; set { imageType = value; RaisePropertyChanged(); } }

        private int exposureCount;

        [JsonProperty]
        public int ExposureCount { get => exposureCount; set { exposureCount = value; RaisePropertyChanged(); } }

        private CameraInfo cameraInfo;

        public CameraInfo CameraInfo {
            get => cameraInfo;
            private set {
                cameraInfo = value;
                RaisePropertyChanged();
            }
        }

        private ObservableCollection<string> _imageTypes;

        public ObservableCollection<string> ImageTypes {
            get {
                if (_imageTypes == null) {
                    _imageTypes = new ObservableCollection<string>();

                    System.Type type = typeof(CaptureSequence.ImageTypes);
                    foreach (var p in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) {
                        var v = p.GetValue(null);
                        _imageTypes.Add(v.ToString());
                    }
                }
                return _imageTypes;
            }
            set {
                _imageTypes = value;
                RaisePropertyChanged();
            }
        }

        private double roi = 100;

        [JsonProperty]
        public double ROI {
            get => roi;
            set {
                if (value <= 0) { value = 100; }
                if (value > 100) { value = 100; }
                roi = value;
                RaisePropertyChanged();
            }
        }

        private bool roiType = true;
        public bool ROIType {
            get => roiType;
            set {
                roiType = value;
                RaisePropertyChanged();
            }
        }

        public static double LastExposureTIme = 0;
        public static double LastImageProcessTime = 0;

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
           var count = ExposureCount;
            var dsoContainer = RetrieveTarget(this.Parent);
            var specificDSOContainer = dsoContainer as DeepSkyObjectContainer;
            if (specificDSOContainer != null) {
                count = specificDSOContainer.GetOrCreateExposureCountForItemAndCurrentFilter(this, 1)?.Count ?? ExposureCount;
            }

            var info = cameraMediator.GetInfo();
            bool useSubsample = info.CanSubSample && Parent is SmartSubframeExposure && ((SmartSubframeExposure)Parent).ROIOption != "None";
            ObservableRectangle rect = null;

            if (useSubsample ) {
                if (ROIType) {
                    if (ROI < 100) {
                        double r = ROI / 100;
                        var centerX = info.XSize / 2d;
                        var centerY = info.YSize / 2d;
                        var subWidth = info.XSize * r;
                        var subHeight = info.YSize * r;
                        var startX = centerX - subWidth / 2d;
                        var startY = centerY - subHeight / 2d;
                        rect = new ObservableRectangle(startX, startY, subWidth, subHeight);
                    } else {
                        useSubsample = false;
                    }
                } else {
                    SmartSubframeExposure se = (SmartSubframeExposure)Parent;
                    rect = new ObservableRectangle(se.XExpr.Value, se.YExpr.Value, se.WExpr.Value, se.HExpr.Value);
                }
            }

            var capture = new CaptureSequence() {
                ExposureTime = ExposureTime,
                Binning = Binning,
                Gain = Gain,
                Offset = Offset,
                ImageType = ImageType,
                ProgressExposureCount = count,
                TotalExposureCount = count + 1,
                EnableSubSample = useSubsample,
                SubSambleRectangle = rect
            };

            if (rect != null) {
                Logger.Info("EnableSubSample = " + capture.EnableSubSample + "; ROIType = " + ROIType + "; rect = " + capture.SubSambleRectangle.Width + ", " + capture.SubSambleRectangle.Height);
            }

            var exposureData = await imagingMediator.CaptureImage(capture, token, progress);

            TimeSpan time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            LastExposureTIme = time.TotalSeconds;

            var imageParams = new PrepareImageParameters(null, false);
            if (IsLightSequence()) {
                imageHistoryVM.Add(exposureData.MetaData.Image.Id, ImageType);
            }

            if (imageProcessingTask != null) {
                await imageProcessingTask;
            }
            imageProcessingTask = ProcessImageData(dsoContainer, exposureData, progress, token);

            if (specificDSOContainer != null) {
                specificDSOContainer.IncrementExposureCountForItemAndCurrentFilter(this, 1);
            }
            ExposureCount++;
        }

        private async Task ProcessImageData(IDeepSkyObjectContainer dsoContainer, IExposureData exposureData, IProgress<ApplicationStatus> progress, CancellationToken token) {
            try {
                var imageParams = new PrepareImageParameters(null, false);
                if (IsLightSequence()) {
                    imageParams = new PrepareImageParameters(true, true);
                }

                var imageData = await exposureData.ToImageData(progress, token);

                if (imageData.Properties != null) {
                    Logger.Info("Processed image: Width = " + imageData.Properties.Width + ", Height = " + imageData.Properties.Height);
                }
                
                var prepareTask = imagingMediator.PrepareImage(imageData, imageParams, token);

                if (IsLightSequence()) {
                    imageHistoryVM.PopulateStatistics(imageData.MetaData.Image.Id, await imageData.Statistics);
                }

                if (dsoContainer != null) {
                    var target = dsoContainer.Target;
                    if (target != null) {
                        imageData.MetaData.Target.Name = target.DeepSkyObject.NameAsAscii;
                        imageData.MetaData.Target.Coordinates = target.InputCoordinates.Coordinates;
                        imageData.MetaData.Target.PositionAngle = target.PositionAngle;
                    }
                }

                ISequenceContainer parent = Parent;
                while (parent != null && !(parent is SequenceRootContainer)) {
                    parent = parent.Parent;
                }
                if (parent is SequenceRootContainer item) {
                    imageData.MetaData.Sequence.Title = item.SequenceTitle;
                }

                await imageSaveMediator.Enqueue(imageData, prepareTask, progress, token);

            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public bool ValidateExposureTime { get; set; } = true;

        private bool IsLightSequence() {
            return ImageType == CaptureSequence.ImageTypes.SNAPSHOT || ImageType == CaptureSequence.ImageTypes.LIGHT;
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            Validate();
        }

        private IDeepSkyObjectContainer RetrieveTarget(ISequenceContainer parent) {
            if (parent != null) {
                var container = parent as IDeepSkyObjectContainer;
                if (container != null && !(container is IfContainer)) {
                    return container;
                } else {
                    return RetrieveTarget(parent.Parent);
                }
            } else {
                return null;
            }
        }

        static System.Object LastImageLock = new System.Object();

        static Symbol.Keys iLastImageResult;
        public static Symbol.Keys LastImageResults {
            get {
                lock (Symbol.SYMBOL_LOCK) {
                    return iLastImageResult;
                }
            }
            set {
                iLastImageResult = value;
            }
        }

        public double ExposureTime { get => EExpr.Value; set { } }
        public int Gain { get => (int)GExpr.Value; set { } }
        public int Offset { get => (int)OExpr.Value; set { } }

        private static void AddOptionalResult(Symbol.Keys results, StarDetectionAnalysis a, string name) {
            if (a.HasProperty(name)) {
                var v = a.GetType().GetProperty(name).GetValue(a, null);
                if (v is double vDouble) {
                    results.Add("Image_" + name, Math.Round(vDouble, 2));
                }
            }
        }

        public static void ProcessResults(object sender, ImagePreparedEventArgs e) {
            lock (Symbol.SYMBOL_LOCK) {
                TimeSpan time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                TakeExposure.LastImageProcessTime = time.TotalSeconds;

                StarDetectionAnalysis a = (StarDetectionAnalysis)e.RenderedImage.RawImageData.StarDetectionAnalysis;

                // Clean out any old results since this instruction may be called many times
                //var foo = new Random();
                //a.HFR = foo.NextDouble();
                if (Double.IsNaN(a.HFR)) {
                    a.HFR = 0;
                }
                double rms = 0;
                RMS recordedRMS = e.RenderedImage.RawImageData.MetaData.Image.RecordedRMS;
                if (recordedRMS != null) {
                    rms = recordedRMS.Total;
                }

                var stats = e.RenderedImage.RawImageData.Statistics.Task.Result;

                Symbol.Keys results = new Symbol.Keys {
                    // These are from AF or HocusFocus
                    { "Image_HFR", Math.Round(a.HFR, 3) },
                    { "Image_StarCount", a.DetectedStars },
                    { "Image_Id", e.RenderedImage.RawImageData.MetaData.Image.Id },
                    { "Image_ExposureTime", e.RenderedImage.RawImageData.MetaData.Image.ExposureTime },
                    { "Image_MeanADU", stats.Mean }
                   , { "Image_RMS", rms }
                    , { "Image_Type", e.RenderedImage.RawImageData.MetaData.Image.ImageType }
                    , { "Image_Gain", e.RenderedImage.RawImageData.MetaData.Camera.Gain}
                    , { "image__Offset", e.RenderedImage.RawImageData.MetaData.Camera.Offset}
                };

                // Add these if they exist
                AddOptionalResult(results, a, "Eccentricity");
                AddOptionalResult(results, a, "FWHM");

                // We might also get guider info as well...

                LastImageResults = results;
                Symbol.UpdateSwitchWeatherData();
            }
        }

        public void ValidateGain(Expr expr) {
            CameraInfo = this.cameraMediator.GetInfo();
            //if (!CameraInfo.Connected) {
            //    expr.Error = Loc.Instance["LblCameraNotConnected"];
            //} else 
            if (expr.Value < -1) {
                expr.Error = "Cannot be less than -1";
            } else if (CameraInfo.CanSetGain && expr.Value > -1 && (expr.Value < CameraInfo.GainMin || expr.Value > CameraInfo.GainMax)) {
                expr.Error = string.Format("Must be between {0} and {1}", CameraInfo.GainMin, CameraInfo.GainMax);
            }
        }

        public void ValidateOffset(Expr expr) {
            CameraInfo = this.cameraMediator.GetInfo();
            //if (!CameraInfo.Connected) {
            //   expr.Error = Loc.Instance["LblCameraNotConnected"];
            //} else 
            if (expr.Value < 0) {
                expr.Error = "Cannot be less than 0";
            } else if (CameraInfo.CanSetGain && expr.Value > -1 && (expr.Value < CameraInfo.GainMin || expr.Value > CameraInfo.GainMax)) {
                expr.Error = string.Format("Must be between {0} and {1}", CameraInfo.OffsetMin, CameraInfo.OffsetMax);
            }
        }

        public bool Validate() {
            var i = new List<string>();
            CameraInfo = this.cameraMediator.GetInfo();
            bool canSubsample = true;

            if (!CameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            } else {
                if (CameraInfo.CanSetGain && GExpr.Value != -1 && (GExpr.Value < CameraInfo.GainMin || GExpr.Value > CameraInfo.GainMax)) {
                    i.Add(string.Format(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_Gain"], CameraInfo.GainMin, CameraInfo.GainMax, GExpr.Value));
                }
                if (CameraInfo.CanSetOffset && OExpr.Value != -1 && (OExpr.Value < CameraInfo.OffsetMin || OExpr.Value > CameraInfo.OffsetMax)) {
                    i.Add(string.Format(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_Offset"], CameraInfo.OffsetMin, CameraInfo.OffsetMax, OExpr.Value));
                }
                if (ValidateExposureTime && EExpr.Expression?.Length == 0) {
                    i.Add("There must be an exposure time set");
                }
                if (!CameraInfo.CanSubSample) {
                    canSubsample = false;
                }
            }
            CanSubsample = canSubsample;
            RaisePropertyChanged("CanSubsample");

            var fileSettings = profileService.ActiveProfile.ImageFileSettings;

            if (string.IsNullOrWhiteSpace(fileSettings.FilePath)) {
                i.Add(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_FilePathEmpty"]);
            } else if (!Directory.Exists(fileSettings.FilePath)) {
                i.Add(Loc.Instance["Lbl_SequenceItem_Imaging_TakeExposure_Validation_FilePathInvalid"]);
            }

            if (GExpr.Default != CameraInfo.DefaultGain) {
                GExpr.Default = CameraInfo.DefaultGain;
            }

            if (OExpr.Default != CameraInfo.DefaultOffset) {
                OExpr.Default = CameraInfo.DefaultOffset;
            }

            Expr.AddExprIssues(i, GExpr, OExpr, EExpr);

            Issues = i;
            return i.Count == 0;
        }

        public override void ResetProgress() {
            base.ResetProgress();
        }

        public override TimeSpan GetEstimatedDuration() {
            return TimeSpan.FromSeconds(this.EExpr.Value);
        }

        public override string ToString() {
            var currentGain = GExpr.Value == -1 ? CameraInfo.DefaultGain : GExpr.Value;
            var currentOffset = OExpr.Value == -1 ? CameraInfo.DefaultOffset : OExpr.Value;
            return $"Category: {Category}, Item: {nameof(TakeExposure)}, ExposureTime {EExpr.Value}, Gain {currentGain}, Offset {currentOffset}, ImageType {ImageType}, Binning {Binning?.Name ?? "1x1"}";
        }
    }
}