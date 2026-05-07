using NINA.Sequencer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NINA.Plugin.SequencerPlus {

    public partial class ExprComboControl : UserControl {
        public ExprComboControl() {
            InitializeComponent();
        }

        public static readonly DependencyProperty ExpProperty =
            DependencyProperty.Register("Exp", typeof(Expr), typeof(ExprComboControl), null);

        public Expr Exp { get; set; }

        public static readonly DependencyProperty ComboProperty =
            DependencyProperty.Register("Combo", typeof(IList<string>), typeof(ExprComboControl), null);

        public IList<string> Combo { get; set; }

        public void ShowConstants(object sender, ToolTipEventArgs e) {
            Symbol.ShowSymbols(e);
        }

    }
}

