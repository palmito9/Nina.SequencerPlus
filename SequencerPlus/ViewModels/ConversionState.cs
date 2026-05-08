using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NINA.Plugin.SequencerPlus.Models;

namespace NINA.Plugin.SequencerPlus.ViewModels {
    public class ConversionState : INotifyPropertyChanged {
        public ConversionState(SequenceConversion.Direction direction) {
            Direction = direction;
        }

        public SequenceConversion.Direction Direction { get; }

        private double _progress;
        public double Progress {
            get => _progress;
            set { _progress = value; RaisePropertyChanged(); }
        }

        private bool _progressVisible;
        public bool ProgressVisible {
            get => _progressVisible;
            set { _progressVisible = value; RaisePropertyChanged(); }
        }

        private string _error = string.Empty;
        public string Error {
            get => _error;
            set {
                _error = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasError));
            }
        }

        private ObservableCollection<string> _errors = new ObservableCollection<string>();
        public ObservableCollection<string> Errors {
            get => _errors;
            set {
                _errors = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasError));
            }
        }

        private int _converted;
        public int Converted {
            get => _converted;
            set { _converted = value; RaisePropertyChanged(); RaisePropertyChanged(nameof(HasConverted)); }
        }

        private int _skipped;
        public int Skipped {
            get => _skipped;
            set { _skipped = value; RaisePropertyChanged(); RaisePropertyChanged(nameof(HasSkipped)); }
        }

        private int _errorCount;
        public int ErrorCount {
            get => _errorCount;
            set { _errorCount = value; RaisePropertyChanged(); RaisePropertyChanged(nameof(HasError)); }
        }

        public bool HasConverted => _converted > 0;
        public bool HasSkipped => _skipped > 0;
        public bool HasError => _errorCount > 0;

        private bool _recursive = false;
        public bool Recursive {
            get => _recursive;
            set { _recursive = value; RaisePropertyChanged(); }
        }

        public void ConversionStart() {
            ProgressVisible = true;
            Progress = 0;
            Error = string.Empty;
            Errors.Clear();
            Converted = 0;
            Skipped = 0;
            ErrorCount = 0;
        }

        public void ConversionComplete(ConversionResult result) {
            Progress = 100;
            Converted = result.Converted;
            Skipped = result.Skipped;
            ErrorCount = result.Errors;

            Errors.Clear();
            const int MaxErrors = 100;
            var errorsToAdd = result.ErrorList.Count > MaxErrors 
                ? result.ErrorList.GetRange(0, MaxErrors) 
                : result.ErrorList;

            foreach (var error in errorsToAdd) {
                Errors.Add(error);
            }

            if (result.ErrorList.Count > MaxErrors) {
                Errors.Add($"... and {result.ErrorList.Count - MaxErrors} more error(s)");
            }

            ProgressVisible = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
