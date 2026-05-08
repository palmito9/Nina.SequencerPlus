using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus.Controls {
    public partial class ConversionPanel : UserControl {
        public ConversionPanel() {
            InitializeComponent();
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ConversionPanel), new PropertyMetadata(string.Empty));

        public string Title {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty FileCommandProperty =
            DependencyProperty.Register(nameof(FileCommand), typeof(ICommand), typeof(ConversionPanel), new PropertyMetadata(null));

        public ICommand FileCommand {
            get => (ICommand)GetValue(FileCommandProperty);
            set => SetValue(FileCommandProperty, value);
        }

        public static readonly DependencyProperty FolderCommandProperty =
            DependencyProperty.Register(nameof(FolderCommand), typeof(ICommand), typeof(ConversionPanel), new PropertyMetadata(null));

        public ICommand FolderCommand {
            get => (ICommand)GetValue(FolderCommandProperty);
            set => SetValue(FolderCommandProperty, value);
        }

        public static readonly DependencyProperty RecursiveProperty =
            DependencyProperty.Register(nameof(Recursive), typeof(bool), typeof(ConversionPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool Recursive {
            get => (bool)GetValue(RecursiveProperty);
            set => SetValue(RecursiveProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ConversionPanel), new PropertyMetadata(0.0));

        public double Progress {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly DependencyProperty ProgressVisibleProperty =
            DependencyProperty.Register(nameof(ProgressVisible), typeof(bool), typeof(ConversionPanel), new PropertyMetadata(false));

        public bool ProgressVisible {
            get => (bool)GetValue(ProgressVisibleProperty);
            set => SetValue(ProgressVisibleProperty, value);
        }

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(string), typeof(ConversionPanel), new PropertyMetadata(string.Empty));

        public string Status {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public static readonly DependencyProperty ErrorProperty =
            DependencyProperty.Register(nameof(Error), typeof(string), typeof(ConversionPanel), new PropertyMetadata(string.Empty));

        public string Error {
            get => (string)GetValue(ErrorProperty);
            set => SetValue(ErrorProperty, value);
        }

        public static readonly DependencyProperty ErrorsProperty =
            DependencyProperty.Register(nameof(Errors), typeof(ObservableCollection<string>), typeof(ConversionPanel), new PropertyMetadata(null));

        public ObservableCollection<string> Errors {
            get => (ObservableCollection<string>)GetValue(ErrorsProperty);
            set => SetValue(ErrorsProperty, value);
        }

        public static readonly DependencyProperty ConvertedProperty =
            DependencyProperty.Register(nameof(Converted), typeof(int), typeof(ConversionPanel), new PropertyMetadata(0));

        public int Converted {
            get => (int)GetValue(ConvertedProperty);
            set => SetValue(ConvertedProperty, value);
        }

        public static readonly DependencyProperty SkippedProperty =
            DependencyProperty.Register(nameof(Skipped), typeof(int), typeof(ConversionPanel), new PropertyMetadata(0));

        public int Skipped {
            get => (int)GetValue(SkippedProperty);
            set => SetValue(SkippedProperty, value);
        }

        public static readonly DependencyProperty ErrorCountProperty =
            DependencyProperty.Register(nameof(ErrorCount), typeof(int), typeof(ConversionPanel), new PropertyMetadata(0));

        public int ErrorCount {
            get => (int)GetValue(ErrorCountProperty);
            set => SetValue(ErrorCountProperty, value);
        }

        public static readonly DependencyProperty HasErrorProperty =
            DependencyProperty.Register(nameof(HasError), typeof(bool), typeof(ConversionPanel), new PropertyMetadata(false));

        public bool HasError {
            get => (bool)GetValue(HasErrorProperty);
            set => SetValue(HasErrorProperty, value);
        }

        public static readonly DependencyProperty HasConvertedProperty =
            DependencyProperty.Register(nameof(HasConverted), typeof(bool), typeof(ConversionPanel), new PropertyMetadata(false));

        public bool HasConverted {
            get => (bool)GetValue(HasConvertedProperty);
            set => SetValue(HasConvertedProperty, value);
        }

        public static readonly DependencyProperty HasSkippedProperty =
            DependencyProperty.Register(nameof(HasSkipped), typeof(bool), typeof(ConversionPanel), new PropertyMetadata(false));

        public bool HasSkipped {
            get => (bool)GetValue(HasSkippedProperty);
            set => SetValue(HasSkippedProperty, value);
        }
    }
}
