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

    public partial class ExprHintControl : UserControl {
        public ExprHintControl() {
            InitializeComponent();
        }

        public static readonly DependencyProperty ExpProperty =
            DependencyProperty.Register("Exp", typeof(Expr), typeof(ExprHintControl), null);

        public Expr Exp { get; set; }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(String), typeof(ExprHintControl), null);

        public String Label { get; set; }

        public static readonly DependencyProperty DefaultProperty =
             DependencyProperty.Register("Default", typeof(String), typeof(ExprHintControl), null);

        public String Default { get; set; }

        public void ShowConstants(object sender, ToolTipEventArgs e) {
            Symbol.ShowSymbols(sender);
        }

    }
}

