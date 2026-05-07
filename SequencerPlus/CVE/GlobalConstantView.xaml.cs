using NINA.View.Sequencer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NINA.Plugin.SequencerPlus
{
    /// <summary>
    /// Interaction logic for GlobalConstantView.xaml
    /// </summary>
    public partial class GlobalConstantView : UserControl
    {
        public GlobalConstantView()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SequenceItemContentProperty =
            DependencyProperty.Register(nameof(SequenceItemContent), typeof(object), typeof(GlobalConstantView));

        public object SequenceItemContent {
            get { return (object)GetValue(SequenceItemContentProperty); }
            set { SetValue(SequenceItemContentProperty, value); }
        }

    }
}
