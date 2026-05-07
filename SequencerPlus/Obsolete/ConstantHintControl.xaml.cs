using NINA.Sequencer.SequenceItem;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NINA.Plugin.SequencerPlus {

    public partial class ConstantHintControl : UserControl {
        public ConstantHintControl() {
            InitializeComponent();
        }

        public static readonly DependencyProperty ExprProperty =
            DependencyProperty.Register("Expr", typeof(String), typeof(ConstantHintControl), null);

        public String Expr { get; set; }

        public static readonly DependencyProperty ValuProperty =
            DependencyProperty.Register("Valu", typeof(String), typeof(ConstantHintControl), null);

        public String Valu { get; set; }

        public static readonly DependencyProperty DefaultProperty =
             DependencyProperty.Register("Default", typeof(String), typeof(ConstantHintControl), null);

        public String Default { get; set; }

        public static readonly DependencyProperty ValidateProperty =
             DependencyProperty.Register("Validate", typeof(String), typeof(ConstantHintControl), null);

        public String Validate { get; set; }

        public static readonly DependencyProperty TypeProperty =
              DependencyProperty.Register("Type", typeof(String), typeof(ConstantHintControl), null);

        public String Type { get; set; }

        public void ShowConstants(object sender, ToolTipEventArgs e) {
            TextBox tb = (TextBox)sender;
            ISequenceItem item = (ISequenceItem)tb.DataContext;
            var stack = ConstantExpression.GetKeyStack(item);
            tb.ToolTip = ConstantExpression.DissectExpression(item, tb.Text, stack);
        }

    }
}

