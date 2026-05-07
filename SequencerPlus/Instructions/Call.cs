
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.ViewModel.Sequencer;
using System.Reflection;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using NINA.Core.Utility;
using System.Linq;
using Accord.Math;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Serialization;
using System.Runtime.Serialization;
using System.Windows;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Call")]
    [ExportMetadata("Description", "Call a Function (Template).")]
    [ExportMetadata("Icon", "BoxClosedSVG")]
    [ExportMetadata("Category", "Sequencer+ (Fun-ctions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class Call : IfCommand, IValidatable {

        static protected ISequenceMediator sequenceMediator;
        static protected ISequenceNavigationVM sequenceNavigationVM;
        static protected TemplateController ninaTemplateController;
        static protected TemplateControllerLite templateController;
        private static SequenceJsonConverter sequenceJsonConverter;
        private static IProfileService profileService;
        private static ISequencerFactory sequencerFactory;
        private static object TemplateLock = new object();  

        public static int instanceNumber = 0;

        [ImportingConstructor]
        public Call(ISequenceMediator seqMediator, IProfileService pService) {
            sequenceMediator = seqMediator;
            profileService = pService;
            Instructions = new TemplateContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            Instructions.Name = Name;
            Instructions.Icon = Icon;
            Name = Name;
            Id = ++instanceNumber;

            Condition = new IfContainer();

            // Get the various NINA components we need
            if (sequenceNavigationVM == null || templateController == null) {
                FieldInfo fi = sequenceMediator.GetType().GetField("sequenceNavigation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null) {
                    sequenceNavigationVM = (ISequenceNavigationVM)fi.GetValue(sequenceMediator);
                    ISequence2VM s2vm = sequenceNavigationVM.Sequence2VM;
                    if (s2vm != null) {
                        sequencerFactory = s2vm.SequencerFactory;
                        PropertyInfo pi = s2vm.GetType().GetRuntimeProperty("TemplateController");
                        ninaTemplateController = (TemplateController)pi.GetValue(s2vm);
                        fi = ninaTemplateController.GetType().GetField("sequenceJsonConverter", BindingFlags.Instance | BindingFlags.NonPublic);
                        sequenceJsonConverter = (SequenceJsonConverter)fi.GetValue(ninaTemplateController);
                        templateController = new TemplateControllerLite(sequenceJsonConverter, profileService);
                    }
                }
            }

            Arg1Expr = new Expr(this);
            Arg2Expr = new Expr(this);
            Arg3Expr = new Expr(this);
            Arg4Expr = new Expr(this);
            Arg5Expr = new Expr(this);
            Arg6Expr = new Expr(this);
            ResultExpr = new Expr(this);
        }

        [OnSerializing]
        public void OnSerializingMethod(StreamingContext context) {
            iInstructions = Instructions;
            Instructions = new TemplateContainer();
        }

        [OnSerialized]
        public void OnSerializedMethod(StreamingContext context) {
            Instructions = iInstructions;
        }

        private IfContainer iInstructions;

        public Call(Call copyMe) : this(sequenceMediator, profileService) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                try {
                    Instructions = (TemplateContainer)copyMe.Instructions.Clone();
                } catch (Exception) {
                    Instructions = copyMe.Instructions.Clone();
                }
                Instructions.PseudoParent = this;
                Instructions.Name = Name;
                Instructions.Icon = Icon;

                if (copyMe.Condition == null) {
                    Condition = new IfContainer();
                } else {
                    Condition = (IfContainer)copyMe.Condition.Clone();
                }
            }
        }

        private Expr _Arg1Expr = null;

        [JsonProperty]
        public Expr Arg1Expr {
            get => _Arg1Expr;
            set {
                _Arg1Expr = value;
                RaisePropertyChanged();
            }
        }

        private Expr _Arg2Expr = null;

        [JsonProperty]
        public Expr Arg2Expr {
            get => _Arg2Expr;
            set {
                _Arg2Expr = value;
                RaisePropertyChanged();
            }
        }

        private Expr _Arg3Expr = null;

        [JsonProperty]
        public Expr Arg3Expr {
            get => _Arg3Expr;
            set {
                _Arg3Expr = value;
                RaisePropertyChanged();
            }
        }
        private Expr _Arg4Expr = null;

        [JsonProperty]
        public Expr Arg4Expr {
            get => _Arg4Expr;
            set {
                _Arg4Expr = value;
                RaisePropertyChanged();
            }
        }
        private Expr _Arg5Expr = null;

        [JsonProperty]
        public Expr Arg5Expr {
            get => _Arg5Expr;
            set {
                _Arg5Expr = value;
                RaisePropertyChanged();
            }
        }
        private Expr _Arg6Expr = null;

        [JsonProperty]
        public Expr Arg6Expr {
            get => _Arg6Expr;
            set {
                _Arg6Expr = value;
                RaisePropertyChanged();
            }
        }

        private Expr _ResultExpr = null;

        [JsonProperty]
        public Expr ResultExpr {
            get => _ResultExpr;
            set {
                _ResultExpr = value;
                RaisePropertyChanged();
            }
        }

        public int Id { get; set; }

        private string iTemplateName = null;
        [JsonProperty]
        public string TemplateName {
            get {
                return iTemplateName;
            }
            set {
                iTemplateName = value;
                RaisePropertyChanged("TemplateName");
            }
        }

        public bool TemplateNameIsTrue {
            get {
                return TemplateName == null;
            }
        }

        public IList<TemplatedSequenceContainer> Templates {
            get {
                lock (TemplateLock) {
                    return templateController.TBRTemplates;
                }
            }
        }

        private int TemplateCompare(TemplatedSequenceContainer a, TemplatedSequenceContainer b) {
            return String.Compare(a.Container.Name, b.Container.Name);

        }

        public TemplatedSequenceContainer[] SortedTemplates {
            get {
                lock (TemplateLock) {
                    IList<TemplatedSequenceContainer> l = Templates;
                    TemplatedSequenceContainer[] lCopy = Templates.ToArray();
                    lCopy.Sort(TemplateCompare);
                    return lCopy;
                }
            }
        }

        private TemplatedSequenceContainer selectedTemplate;
        public TemplatedSequenceContainer SelectedTemplate {
            get => selectedTemplate;
            set {
                if (value == null) {
                    value = FindTemplate(TemplateName);
                    if (value == null) {
                        return;
                    }
                }
                selectedTemplate = value;
                if (Instructions.Items.Count > 0) {
                    Instructions.Items.Clear();
                }
                TemplateName = selectedTemplate.Container.Name;

                RaisePropertyChanged("SelectedTemplate");
                RaisePropertyChanged("TemplateNameIsTrue");
                Validate();
            }
        }

        public override object Clone() {
            Call clone = new Call(this);
            clone.TemplateName = TemplateName;
 
            if (TemplateName != null && templateController != null) {
                TemplatedSequenceContainer tc = FindTemplate(TemplateName);
                if (tc != null) {
                    SelectedTemplate = tc;
                    TemplateName = tc.Container.Name;
                    RaisePropertyChanged("TemplateNameIsTrue");
                }
            }

            clone.Arg1Expr = new Expr(clone, this.Arg1Expr.Expression, "Any");
            clone.Arg2Expr = new Expr(clone, this.Arg2Expr.Expression, "Any");
            clone.Arg3Expr = new Expr(clone, this.Arg3Expr.Expression, "Any");
            clone.Arg4Expr = new Expr(clone, this.Arg4Expr.Expression, "Any");
            clone.Arg5Expr = new Expr(clone, this.Arg5Expr.Expression, "Any");
            clone.Arg6Expr = new Expr(clone, this.Arg6Expr.Expression, "Any");
            clone.ResultExpr = new Expr(clone, this.ResultExpr.Expression);

            return clone;
        }

        private Stack<string> cycleStack = new Stack<string>();

        private TemplatedSequenceContainer FindTemplate(string name) {

            lock (TemplateLock) {
                for (int i = 0; i < 4; i++) {
                    try {
                        foreach (var tmp in Templates) {
                            if (tmp.Container.Name.Equals(name)) {
                                return tmp;
                            }
                        }
                    } catch (Exception) {
                        Thread.Sleep(100);
                    }
                }
                return null;
            }
        }

        public override void ResetProgress() {
            base.ResetProgress();
            //Instructions.Items.Clear();
            Instructions.IsExpanded = false;
            if (Symbol.SymbolCache.TryGetValue(Parent, out var cached)) {
                if (Arg1Expr.Expression.Length > 0) cached.TryRemove("Arg1", out _);
                if (Arg2Expr.Expression.Length > 0) cached.TryRemove("Arg2", out _);
                if (Arg3Expr.Expression.Length > 0) cached.TryRemove("Arg3", out _);
                if (Arg4Expr.Expression.Length > 0) cached.TryRemove("Arg4", out _);
                if (Arg5Expr.Expression.Length > 0) cached.TryRemove("Arg5", out _);
                if (Arg6Expr.Expression.Length > 0) cached.TryRemove("Arg6", out _);
            }
        }

        private static int CallID = 0;

        private void AssignArgument (Expr expr, string name) {
            expr.Refresh();
            if (expr.Expression.StartsWith("_")) {
                SetVariable.SetVariableReference(name, expr.Expression, Parent);
                Logger.Info("Call by reference " + name + ", expression = " + expr.Expression.Substring(1));
            } else if (expr.Value == double.NegativeInfinity) {
                SetVariable.SetVariableReference(name, "@" + expr.StringValue, Parent);
                Logger.Info("Call by reference " + name + ", expression = " + expr.StringValue);
            } else if (!Double.IsNaN(expr.Value)) {
                new SetVariable(name, expr.ValueString, Parent);
                Logger.Info("Call by value " + name + ", expression = " + expr.Expression + " evaluated to " + expr.ValueString);
            }
        }

        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            TemplatedSequenceContainer tc = SelectedTemplate;
            if (tc == null) {
                throw new SequenceEntityFailedException("Selected Function/Template not found");
            }

            AssignArgument(Arg1Expr, "Arg1");
            AssignArgument(Arg2Expr, "Arg2");
            AssignArgument(Arg3Expr, "Arg3");
            AssignArgument(Arg4Expr, "Arg4");
            AssignArgument(Arg5Expr, "Arg5");
            AssignArgument(Arg6Expr, "Arg6");

            var templateName = tc.Container.Name;
            TemplatedSequenceContainer tsc = FindTemplate(templateName);
            if (tsc == null) {
                throw new SequenceEntityFailedException("Can't find template with name: " + templateName);
            }

            ISequenceContainer clone = (ISequenceContainer)tsc.Container.Clone();
            clone.Name += (++CallID).ToString();
            Application.Current.Dispatcher.Invoke(new Action(() => { Instructions.Items.Clear(); }));
            Application.Current.Dispatcher.Invoke(new Action(() => { Instructions.Items.Add(clone); }));
            foreach (ISequenceItem item in Instructions.Items) {
                item.AttachNewParent(Instructions);
            }
            Instructions.IsExpanded = true;
            IsExpanded = true;
            RaisePropertyChanged("IsExpanded");

            Logger.Info("Call, Execute " + clone.Name + ", Symbols:");
            if (Symbol.SymbolCache.TryGetValue(Parent, out var symbols)) {
                foreach (var symbol in symbols) {
                    Logger.Info(symbol.Value.ToString());
                }
            }

            await Instructions.Run(progress, token);
        }

        public override void AfterParentChanged() {
            // New; provide link up the chain
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
        }

        public override bool Validate() {

            if (templateController == null) return true;

            var i = new List<string>();

            if (SelectedTemplate == null && TemplateName == null) {
                i.Add("A template must be selected!");
            } else if (TemplateName != null && SelectedTemplate == null) {
                TemplatedSequenceContainer tc = FindTemplate(TemplateName);
                if (tc != null) {
                    SelectedTemplate = tc;
                } else {
                    i.Add("The specified template '" + TemplateName + "' was not found.");
                }
            } else if (SelectedTemplate == null) {
                i.Add("The specified template '" + TemplateName + "' was not found.");
            }

            if (templateController.Updated) {
                SelectedTemplate = FindTemplate(TemplateName);
                _ = SortedTemplates;
                RaisePropertyChanged("SortedTemplates");
            }

            foreach (ISequenceItem item in Instructions.Items) {
                if (item is IValidatable val) {
                    _ = val.Validate();
                }
            }

            Expr.AddExprIssues(i, Arg1Expr, Arg2Expr, Arg3Expr, Arg4Expr, Arg5Expr, Arg6Expr, ResultExpr);

            Issues = i;
            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(Call)}, TemplateName: {TemplateName}";
        }
    }
}
