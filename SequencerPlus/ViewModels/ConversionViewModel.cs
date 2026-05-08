using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using NINA.Plugin.SequencerPlus.Models;

namespace NINA.Plugin.SequencerPlus.ViewModels {
    public class ConversionViewModel : INotifyPropertyChanged {
        private readonly IProfileService _profileService;

        public ConversionViewModel(IProfileService profileService) {
            _profileService = profileService;
            ToSequencerPlus = new ConversionState(SequenceConversion.Direction.ToSequencerPlus);
            ToPowerups = new ConversionState(SequenceConversion.Direction.ToPowerups);

            ConvertToSequencerPlusFileCommand = new AsyncRelayCommand(async () => await ConvertFilesAsync(ToSequencerPlus), () => !IsConverting);
            ConvertToSequencerPlusFolderCommand = new AsyncRelayCommand(async () => await ConvertFolderAsync(ToSequencerPlus), () => !IsConverting);
            ConvertToPowerupsFileCommand = new AsyncRelayCommand(async () => await ConvertFilesAsync(ToPowerups), () => !IsConverting);
            ConvertToPowerupsFolderCommand = new AsyncRelayCommand(async () => await ConvertFolderAsync(ToPowerups), () => !IsConverting);
        }

        public ConversionState ToSequencerPlus { get; }
        public ConversionState ToPowerups { get; }

        private bool _isConverting;
        public bool IsConverting {
            get => _isConverting;
            private set {
                _isConverting = value;
                RaisePropertyChanged();
                ConvertToSequencerPlusFileCommand.NotifyCanExecuteChanged();
                ConvertToSequencerPlusFolderCommand.NotifyCanExecuteChanged();
                ConvertToPowerupsFileCommand.NotifyCanExecuteChanged();
                ConvertToPowerupsFolderCommand.NotifyCanExecuteChanged();
            }
        }

        public AsyncRelayCommand ConvertToSequencerPlusFileCommand { get; }
        public AsyncRelayCommand ConvertToSequencerPlusFolderCommand { get; }
        public AsyncRelayCommand ConvertToPowerupsFileCommand { get; }
        public AsyncRelayCommand ConvertToPowerupsFolderCommand { get; }

        private async Task<bool> ConvertFilesAsync(ConversionState state) {
            string[]? paths = PickFiles();
            if (paths == null || paths.Length == 0) return false;

            IsConverting = true;
            try {
                state.ConversionStart();

                var progress = new Progress<ConversionProgress>(p => {
                    state.Progress = p.Progress;
                    state.Converted = p.Converted;
                    state.Skipped = p.Skipped;
                    state.ErrorCount = p.ErrorCount;
                    if (p.Error != null) {
                        state.Errors.Add(p.Error);
                    }
                });
                var result = await SequenceConversion.ConvertFilesAsync(paths, state.Direction, progress, CancellationToken.None);

                state.ConversionComplete(result);
                return true;
            } finally {
                IsConverting = false;
            }
        }

        private async Task<bool> ConvertFolderAsync(ConversionState state) {
            string? path = PickFolder();
            if (path == null) return false;

            IsConverting = true;
            try {
                state.ConversionStart();

                var progress = new Progress<ConversionProgress>(p => {
                    state.Progress = p.Progress;
                    state.Converted = p.Converted;
                    state.Skipped = p.Skipped;
                    state.ErrorCount = p.ErrorCount;
                    if (p.Error != null) {
                        state.Errors.Add(p.Error);
                    }
                });
                var result = await SequenceConversion.ConvertFolderAsync(path, state.Recursive, state.Direction, progress, CancellationToken.None);

                state.ConversionComplete(result);
                return true;
            } finally {
                IsConverting = false;
            }
        }

        private string[]? PickFiles() {
            var dialog = new OpenFileDialog {
                Filter = "NINA sequence/template files (*.json)|*.json",
                Title = "Select sequence or template files",
                InitialDirectory = _profileService.ActiveProfile.SequenceSettings.DefaultSequenceFolder,
                Multiselect = true
            };
            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        private string? PickFolder() {
            var dialog = new OpenFolderDialog {
                InitialDirectory = _profileService.ActiveProfile.SequenceSettings.DefaultSequenceFolder,
                Title = "Select a folder"
            };
            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
