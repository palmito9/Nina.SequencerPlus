using Newtonsoft.Json;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using NINA.Sequencer.Container;
using System.Text;
using NINA.Core.Utility;
using NINA.Sequencer;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Data;
using NINA.Core.Model.Equipment;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyDome;
using NINA.Equipment.Equipment.MyFlatDevice;
using NINA.Equipment.Equipment.MyRotator;
using NINA.Equipment.Equipment.MySafetyMonitor;
using NINA.Equipment.Equipment.MySwitch;
using NINA.Equipment.Equipment.MyWeatherData;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Equipment.Equipment.MyFilterWheel;
using Namotion.Reflection;
using System.IO;
using System.Linq;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Astrometry.Interfaces;
using System.Collections.Concurrent;
using NINA.Astrometry;
using Accord;
using NINA.Plugin.Messaging;
using NINA.Plugin.Interfaces;
using NmeaParser.Messages;
using NINA.Equipment.Equipment.MyGuider.PHD2.PhdEvents;
using NINA.Equipment.Equipment.MyGuider.PHD2;
using NINA.WPF.Base.Mediator;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using NINA.Core.Locale;
using NINA.Core.Utility.Notification;
using NINA.Profile;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using NINA.Core.Interfaces;

namespace NINA.Plugin.SequencerPlus {

    [JsonObject(MemberSerialization.OptIn)]

    public abstract class Symbol : SequenceItem, IValidatable {

        public class SymbolDictionary : ConcurrentDictionary<string, Symbol> { public static explicit operator ConcurrentDictionary<object, object>(SymbolDictionary v) { throw new NotImplementedException(); } };

        public static ConcurrentDictionary<ISequenceContainer, SymbolDictionary> SymbolCache = new ConcurrentDictionary<ISequenceContainer, SymbolDictionary>();

        public static ConcurrentDictionary<Symbol, List<string>> Orphans = new ConcurrentDictionary<Symbol, List<string>>();

        [ImportingConstructor]
        public Symbol() {
            Name = Name;
            Icon = Icon;
        }

        public Symbol(Symbol copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Name = copyMe.Name;
                Icon = copyMe.Icon;
                Identifier = copyMe.Identifier;
                Definition = copyMe.Definition;
            }
        }

        static public SequenceContainer GlobalContainer = new SequentialContainer() { Name = "Global Constants" };

        static public SequenceContainer GlobalVariables = new SequentialContainer() { Name = "Global Variables" };

        public bool IsGlobalVariable { get; set; } = false;

        public bool isDataSymbol { get; set; } = false;

        public class Keys : Dictionary<string, object>;

        public static readonly String VALID_SYMBOL = "^[a-zA-Z][a-zA-Z0-9-+_]*$";

        public bool IsDuplicate { get; private set; } = false;

        public static void Warn(string str) {
            Logger.Warning(str);
        }

        protected ISequenceContainer LastSParent { get; set; }

        static private bool IsAttachedToRoot(ISequenceContainer container) {
            ISequenceEntity p = container;
            while (p != null) {
                if (p is SequenceRootContainer || p == SequencerPlusPluginObject.Globals) {
                    return true;
                } else {
                    p = p.Parent;
                }
            }
            return false;
        }

        static public bool IsAttachedToRoot(ISequenceEntity item) {
            if (item.Parent == null) return false;
            return IsAttachedToRoot(item.Parent);
        }

        // Must prevent cycles
        public static void SymbolDirty(Symbol sym) {
            if (Debugging) {
                Logger.Info("SymbolDirty: " + sym);
            }
            List<Symbol> dirtyList = new List<Symbol>();
            iSymbolDirty(sym, dirtyList);
        }

        public static void iSymbolDirty(Symbol sym, List<Symbol> dirtyList) {
            Debug.WriteLine("SymbolDirty: " + sym);
            dirtyList.Add(sym);
            // Mark everything in the chain dirty
            foreach (var consumer in sym.Consumers) {
                Expr expr = consumer.Key;
                expr.ReferenceRemoved(sym);
                Symbol consumerSym = expr.Symbol;
                if (!expr.Dirty && consumerSym != null) {
                    if (!dirtyList.Contains(consumerSym)) {
                        iSymbolDirty(consumerSym, dirtyList);
                    }
                }
                expr.Dirty = true;
                expr.Evaluate();
            }
        }

        private string GenId(SymbolDictionary dict, string id) {

            Symbol sym;
            _ = dict.TryGetValue(id, out sym);
            if (sym is SetGlobalVariable && !IsAttachedToRoot(sym.Parent)) {
                // This is an orphaned definition; allow it to be redefined
                dict[id] = this;
                return id;
            }
            Notification.ShowWarning("The Constant/Variable " + id + " is already defined");
            return "";
        }

        private ISequenceContainer LastParent;

        public override void AfterParentChanged() {
            base.AfterParentChanged();

            if (Parent == null) {
                Logger.Info("Null");
            }

            ISequenceContainer sParent = SParent();
            if (sParent == LastSParent) {
                return;
            }
            Debug.WriteLine("APC: " + this + ", New Parent = " + ((sParent == null) ? "null" : sParent.Name));
            if (!IsAttachedToRoot(Parent)) {  //} && (Parent != SequencerPlusPluginObject.Globals) && !(this is SetGlobalVariable)) {
                if (Expr != null) {
                    // Clear out orphans of this Symbol
                    Orphans.TryRemove(this, out _);
                    // We've deleted this Symbol
                    SymbolDictionary cached;
                    if (LastSParent == null) {
                        Warn("Removed symbol " + this + " has no LastSParent?");
                        // We're saving a template?
                        return;
                    }
                    if (SymbolCache.TryGetValue(LastSParent, out cached)) {
                        if (cached.TryRemove(Identifier, out _)) {
                            SymbolDirty(this);
                        } else {
                            Warn("Deleting " + this + " but not in SParent's cache?");
                        }
                    } else {
                        Warn("Deleting " + this + " but SParent has no cache?");
                    }
                }
                return;
            }
            LastSParent = sParent;

            Expr = new Expr(Definition, this);

            try {
                if (Identifier != null && Identifier.Length == 0) return;
                SymbolDictionary cached;
                if (SymbolCache.TryGetValue(sParent, out cached)) {
                    try {
                        if (Debugging) {
                            Logger.Info("APC: Added " + Identifier + " to " + sParent.Name);
                        }
                        bool added = cached.TryAdd(Identifier, this);

                        if (!added && sParent == GlobalVariables) {
                            Symbol gv;
                            cached.TryGetValue(Identifier, out gv);
                            if (gv != null) {
                                Logger.Warning("New Symbol for Global Variable: " + Identifier);
                                SymbolDirty(gv);
                                gv.Consumers.Clear();
                                cached.TryUpdate(Identifier, this, gv);
                            }
                        } else if (!added) {
                            Identifier = GenId(cached, Identifier);
                            return;
                        }
                    } catch (ArgumentException) {
                        if (sParent != SequencerPlusPluginObject.Globals) {
                            IsDuplicate = true;
                            Identifier = GenId(cached, Identifier);
                            cached.TryAdd(Identifier, this);
                        }
                    }
                } else {
                    SymbolDictionary newSymbols = new SymbolDictionary();
                    newSymbols.TryAdd(Identifier, this);
                    SymbolCache.TryAdd(sParent, newSymbols);
                    if (Debugging) {
                        Logger.Info("APC: Added " + sParent.Name + " to SymbolCache");
                        Logger.Info("APC: Added " + Identifier + " to " + sParent.Name);
                    }

                    foreach (var consumer in Consumers) {
                        consumer.Key.RemoveParameter(Identifier);
                    }

                    // Can we see if the Parent moves?
                    // Parent.AfterParentChanged += ??
                }
            } catch (Exception ex) {
                Logger.Error("Exception in Symbol evaluation: " + ex.Message);
            }

            LastParent = Parent;
        }

        protected static bool Debugging = false;
        
        private string _identifier = "";

        [JsonProperty]
        public string Identifier {
            get => _identifier;
            set {
                if (Parent == null) {
                    _identifier = value;
                    return;
                }

                ISequenceContainer sParent = SParent();

                SymbolDictionary cached = null;
                if (value == _identifier) {
                    return;
                } else if (_identifier.Length != 0) {
                    // If there was an old value, remove it from Parent's dictionary
                    if (!IsDuplicate && SymbolCache.TryGetValue(sParent, out cached)) { 
                        if (Debugging) {
                            Logger.Info("Removing " + value + " from " + sParent.Name);
                        }
                        cached.TryRemove(value, out _);
                        SymbolDirty(this);
                    }
                }

                _identifier = value;

                if (value.Length == 0) return;

                // Store the symbol in the SymbolCache for this Parent
                if (Parent != null) {
                    if (cached != null || SymbolCache.TryGetValue(sParent, out cached)) {
                        try {
                            if (!cached.TryAdd(Identifier, this)) {
                                _identifier = GenId(cached, Identifier);
                            }
                            if (Debugging) {
                                Logger.Info("Adding " + Identifier + " to " + sParent.Name);
                            }
                        } catch (ArgumentException) {
                            Logger.Warning("Attempt to add duplicate Symbol at same level in sequence: " + Identifier);
                        }
                    } else {
                        SymbolDictionary newSymbols = new SymbolDictionary();
                        if (Debugging) {
                            Logger.Info("Creating new SymbolCache entry for " + this.Name);
                        }
                        SymbolCache.TryAdd(sParent, newSymbols);
                        newSymbols.TryAdd(Identifier, this);
                    }
                }

                if (this is SetConstant constant && constant.GlobalName != null) {
                    constant.SetGlobalName(Identifier);
                }
            }
        }

        private string _definition = "";

        [JsonProperty]
        public string Definition {
            get => _definition;
            set {
                if (value == _definition) {
                    if (Expr != null && value != Expr.Expression) {
                        Logger.Warning("Definition not reflected in Expression; user changed value manually");
                    } else {
                        return;
                    }
                }
                _definition = value;
                if (SParent() != null) {
                    if (Expr != null) {
                        if (Debugging) {
                            Logger.Info("Setting Definition for " + Identifier + " in " + SParent().Name + ": " + value);
                        }
                        Expr.Expression = value;
                    }
                }
                RaisePropertyChanged("Expr");

                if (this is SetConstant constant && constant.GlobalValue != null) {
                    constant.SetGlobalValue(value);
                }

            }
        }

        private Expr _expr = null;
        public Expr Expr {
            get => _expr;
            set {
                _expr = value;
                RaisePropertyChanged();
            }
        }

        public IList<string> Issues { get; set; }

        public bool IsReference { get; set; } = false;

        protected bool IsAttachedToRoot() {
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is SequenceRootContainer) {
                    return true;
                }
                p = p.Parent;
            }
            return false;
        }

        public ConcurrentDictionary<Expr, byte> Consumers = new ConcurrentDictionary<Expr, byte>();
        public static SequencerPlusPlugin SequencerPlusPluginObject { get; set; }

        public ISequenceContainer SParent() {
            if (Parent == null) {
                return null;
            } else if (this is SetGlobalVariable) {
                return GlobalVariables;
            } else if (Parent is CVContainer cvc) {
                if (cvc.Parent is TemplateContainer tc) {
                    return tc.Parent;
                } else {
                    return cvc.Parent;
                }
            } else {
                return Parent;
            }
        }


        public void AddConsumer(Expr expr) {
            if (!Consumers.ContainsKey(expr)) {
                Consumers.TryAdd(expr, 0);
            }
        }

        public void RemoveConsumer(Expr expr) {
            if (!Consumers.TryRemove(expr, out _)) {
                Warn("RemoveConsumer: " + expr + " not found in " + this);
            }
        }

        public static Symbol FindSymbol(string identifier, ISequenceContainer context) {
            while (context != null) {
                SymbolDictionary cached;
                if (SymbolCache.TryGetValue(context, out cached)) {
                    if (cached.ContainsKey(identifier)) {
                        if (Debugging) {
                            Logger.Info("FindSymbol '" + identifier + "' returning " + cached[identifier]);
                        }
                        return cached[identifier];
                    }
                }
                context = context.Parent;
            }
            return FindGlobalSymbol(identifier);
        }

        public static void DumpSymbols () {
            foreach (var c in SymbolCache) {
                Logger.Info("\r\nIn SymbolCache for " + c.Key.Name);
                foreach (var d in c.Value) {
                    Logger.Info("  -- " + d.Key + " / " + d.Value.ToString());
                }
            }
        }

         public static Symbol FindGlobalSymbol(string identifier) {
            SymbolDictionary cached;
            Symbol global = null;
            if (SymbolCache.TryGetValue(GlobalVariables, out cached)) {

                // Prune orphaned global symbolsf
                foreach (var kvp in cached) {
                    Symbol sym = kvp.Value;
                    ISequenceEntity context = sym.Expr.SequenceEntity;
                    if (context == null || !IsAttachedToRoot(context)) {
                        cached.TryRemove(kvp.Key, out _);
                    }
                }

                if (cached.ContainsKey(identifier)) {
                    global = cached[identifier];
                    // Don't find symbols that aren't part of the current sequence
                    if (!IsAttachedToRoot(global)) {
                        return null;
                    }
                }
            }
            if (global is SetGlobalVariable) return global;
            return null;
        }

        public static void ShowSymbols(object sender) {
            TextBox tb = (TextBox)sender;
            BindingExpression be = tb.GetBindingExpression(TextBox.TextProperty);
            Expr exp = be.ResolvedSource as Expr;
            Dictionary<string, object> DataSymbols = Symbol.GetSwitchWeatherKeys();

            if (exp == null) {
                Symbol s = be.ResolvedSource as Symbol;
                if (s != null) {
                    exp = s.Expr;
                } else {
                    tb.ToolTip = "??";
                    return;
                }
            }

            Dictionary<string, Symbol> syms = exp.Resolved;
            int cnt = syms.Count;
            if (cnt == 0) {
                if (exp.References.Count == 1) {
                    tb.ToolTip = "The symbol is not yet defined";
                } else {
                    tb.ToolTip = "No defined symbols used in this expression";
                }
                return;
            }
            StringBuilder sb = new StringBuilder(cnt == 1 ? "Symbol: " : "Symbols: ");

            foreach (var kvp in syms) {
                Symbol sym = kvp.Value as Symbol;
                sb.Append(kvp.Key.ToString());
                if (sym != null) {
                    sb.Append(" (in ");
                    sb.Append(sym.SParent().Name);
                    ISequenceContainer sParent = sym.SParent();
                    if (sParent != sym.Parent) {
                        if (sym.Parent is CVContainer) {
                            sb.Append("/" + sym.Parent.Name);
                            if (sym.Parent.Parent is TemplateContainer tc) {
                                sb.Append("/TBR");
                                if (tc.PseudoParent != null && tc.PseudoParent is TemplateByReference tbr) {
                                    sb.Append("-" + tbr.TemplateName);
                                }
                            }
                        } else if (sParent != GlobalVariables) {
                            sb.Append(" - WTF");
                        }
                    }
                    sb.Append(") = ");
                    sb.Append(sym.Expr.Error != null ? sym.Expr.Error : sym.Expr.ValueString);
                } else {
                    // We're a data value
                    sb.Append(" (Data) = ");
                    sb.Append(DataSymbols.GetValueOrDefault(kvp.Key, "??"));
                }
                if (--cnt > 0) sb.Append("; ");
            }

            tb.ToolTip = sb.ToString();
        }
        public static string ShowSymbols(Expr exp) {
            Dictionary<string, object> DataSymbols = Symbol.GetSwitchWeatherKeys();

            if (exp == null) {
                return "??";
            }

            Dictionary<string, Symbol> syms = exp.Resolved;
            int cnt = syms.Count;
            if (cnt == 0) {
                if (exp.References.Count == 1) {
                    return "The symbol is not yet defined\r\n";
                } else {
                    return "No defined symbols used in this expression\r\n";
                }
            }
            StringBuilder sb = new StringBuilder(cnt == 1 ? "Symbol: " : "Symbols: ");

            foreach (var kvp in syms) {
                Symbol sym = kvp.Value as Symbol;
                sb.Append(kvp.Key.ToString());
                if (sym != null) {
                    sb.Append(" (in ");
                    sb.Append(sym.SParent().Name);
                    ISequenceContainer sParent = sym.SParent();
                    if (sParent != sym.Parent) {
                        if (sym.Parent is CVContainer) {
                            sb.Append("/" + sym.Parent.Name);
                            if (sym.Parent.Parent is TemplateContainer tc) {
                                sb.Append("/TBR");
                                if (tc.PseudoParent != null && tc.PseudoParent is TemplateByReference tbr) {
                                    sb.Append("-" + tbr.TemplateName);
                                }
                            }
                        } else {
                            sb.Append(" - WTF");
                        }
                    }
                    sb.Append(") = ");
                    sb.Append(sym.Expr.Error != null ? sym.Expr.Error : sym.Expr.Value.ToString());
                } else {
                    // We're a data value
                    sb.Append(" (Data) = ");
                    sb.Append(DataSymbols.GetValueOrDefault(kvp.Key, "??"));
                }
                if (--cnt > 0) sb.Append("; ");
            }

            sb.Append("\r\n");
            return sb.ToString();
        }


        public abstract bool Validate();

        public override string ToString() {
            return $"Symbol: Identifier {Identifier}, in {SParent()?.Name} with value {Expr.Value}";
        }


        // DATA SYMBOLS


        private static string[] WeatherData = new string[] { "CloudCover", "DewPoint", "Humidity", "Pressure", "RainRate", "SkyBrightness", "SkyQuality", "SkyTemperature",
            "StarFWHM", "Temperature", "WindDirection", "WindGust", "WindSpeed"};

        public static string RemoveSpecialCharacters(string str) {
            if (str == null) {
                return "__Null__";
            }
            StringBuilder sb = new StringBuilder();
            foreach (char c in str) {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_') {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }


        private static ISwitchMediator SwitchMediator { get; set; }
        private static IWeatherDataMediator WeatherDataMediator { get; set; }
        private static ICameraMediator CameraMediator { get; set; }
        private static IDomeMediator DomeMediator { get; set; }
        private static IFlatDeviceMediator FlatMediator { get; set; }
        private static IFilterWheelMediator FilterWheelMediator { get; set; }
        private static IProfileService ProfileService { get; set; }
        private static IRotatorMediator RotatorMediator { get; set; }
        private static ISafetyMonitorMediator SafetyMonitorMediator { get; set; }
        private static IFocuserMediator FocuserMediator { get; set; }
        private static ITelescopeMediator TelescopeMediator { get; set; }
        private static IMessageBroker MessageBroker { get; set; }
        private static IGuiderMediator GuiderMediator { get; set; }


        private static ConditionWatchdog ConditionWatchdog { get; set; }
        private static IList<string> Switches { get; set; } = new List<string>();

        public class Array : Dictionary<object, object>;
        public static Dictionary<string, Array> Arrays { get; set; } = new Dictionary<string, Array>();


        public class VariableMessage {
            public object value;
            public DateTimeOffset? expiration;

            public VariableMessage(object value, DateTimeOffset? expiration) {
                this.value = value;
                this.expiration = expiration;
            }
        }

        public class Subscriber : ISubscriber {
            Task ISubscriber.OnMessageReceived(IMessage message) {
                Logger.Info("Received message from " + message.Sender + " re: " + message.Topic);

                if (message.Sender == "Target Scheduler") {
                    if (message.Topic == "TargetScheduler-WaitStart") {
                        DateTimeOffset dto = new DateTimeOffset((DateTime)message.Content);
                        MessageKeys["TS_WaitStart"] = new VariableMessage(dto.ToUnixTimeSeconds(), message.Expiration);
                    } else if (message.Topic == "TargetScheduler-NewTargetStart" || message.Topic == "TargetScheduler-TargetStart") {
                        MessageKeys["TS_TargetName"] = new VariableMessage(message.Content, message.Expiration);
                        Logger.Info("TS_TargetName = " + message.Content);
                        Logger.Info("Expires at " + message.Expiration);
                        object p;
                        message.CustomHeaders.TryGetValue("ProjectName", out p);
                        if (p != null) {
                            string pn = (string)p;
                            MessageKeys["TS_ProjectName"] = new VariableMessage(pn, message.Expiration);
                        }
                        message.CustomHeaders.TryGetValue("Rotation", out p);
                        if (p != null) {
                            double rotation = (double)p;
                            MessageKeys["TS_Target_Rotation"] = new VariableMessage(rotation, message.Expiration);
                        }
                        message.CustomHeaders.TryGetValue("Coordinates", out p);
                        if (p != null) {
                            Coordinates coords = (Coordinates)p;
                            MessageKeys["TS_Target_RA"] = new VariableMessage(coords.RA, message.Expiration);
                            MessageKeys["TS_Target_Dec"] = new VariableMessage(coords.Dec, message.Expiration);
                        }
                    } else {
                        Logger.Info("Message not handled");
                    }
                }
                
                return Task.CompletedTask;
            }
        }

        public static Keys MessageKeys = new Keys();


        public static ISubscriber SequencerPlusSubscriber { get; set; }

        public static void InitMediators(ISwitchMediator switchMediator, IWeatherDataMediator weatherDataMediator, ICameraMediator cameraMediator, IDomeMediator domeMediator,
            IFlatDeviceMediator flatMediator, IFilterWheelMediator filterWheelMediator, IProfileService profileService, IRotatorMediator rotatorMediator, ISafetyMonitorMediator safetyMonitorMediator,
            IFocuserMediator focuserMediator, ITelescopeMediator telescopeMediator, IMessageBroker messageBroker, IGuiderMediator guiderMediator) {
            SwitchMediator = switchMediator;
            WeatherDataMediator = weatherDataMediator;
            CameraMediator = cameraMediator;
            DomeMediator = domeMediator;
            FlatMediator = flatMediator;
            FilterWheelMediator = filterWheelMediator;
            ProfileService = profileService;
            RotatorMediator = rotatorMediator;
            SafetyMonitorMediator = safetyMonitorMediator;
            FocuserMediator = focuserMediator;
            TelescopeMediator = telescopeMediator;
            MessageBroker = messageBroker;
            GuiderMediator = guiderMediator;

            ConditionWatchdog = new ConditionWatchdog(UpdateSwitchWeatherData, TimeSpan.FromSeconds(5));
            ConditionWatchdog.Start();

            SequencerPlusSubscriber = new Subscriber();

            MessageBroker.Subscribe("TargetScheduler-WaitStart", SequencerPlusSubscriber);
            MessageBroker.Subscribe("TargetScheduler-TargetStart", SequencerPlusSubscriber);
            MessageBroker.Subscribe("TargetScheduler-NewTargetStart", SequencerPlusSubscriber);
            
            //GuiderMediator.GuideEvent += GuiderMediator_GuideEvent;
        
        }

 
        public static Keys SwitchWeatherKeys { get; set; } = new Keys();

        public static Keys GetSwitchWeatherKeys() {
            lock (SYMBOL_LOCK) {
                return SwitchWeatherKeys;
            }
        }

        public static IList<string> GetSwitches() {
            lock (SYMBOL_LOCK) {
                return Switches;
            }
        }

        public static Symbol.SymbolDictionary DataSymbols { get; set; } = new Symbol.SymbolDictionary();

        public ConcurrentDictionary<string, Symbol> GetDataSymbols() {
            lock (SYMBOL_LOCK) {
                return DataSymbols;
            }
        }

        public static void AddSymbolData(string id, double value) {
            if (DataSymbols.ContainsKey(id)) {

            }
        }

        public static int LastExitCode { get; set; } = 0;

        private static bool TelescopeConnected = false;
        private static bool DomeConnected = false;
        private static bool SafetyConnected = false;
        private static bool FocuserConnected = false;
        private static bool CameraConnected = false;
        private static bool FlatConnected = false;
        private static bool FilterWheelConnected = false;
        private static bool RotatorConnected = false;
        private static bool SwitchConnected = false;
        private static bool WeatherConnected = false;

        public static bool SwitchWeatherConnectionStatusCurrent() {
            long milliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (TelescopeConnected != TelescopeMediator.GetInfo().Connected) { return false; }
            if (DomeConnected != DomeMediator.GetInfo().Connected) { return false; }
            if (SafetyConnected != SafetyMonitorMediator.GetInfo().Connected) { return false; }
            if (FocuserConnected != FocuserMediator.GetInfo().Connected) { return false; }
            if (CameraConnected != CameraMediator.GetInfo().Connected) { return false; }
            if (FlatConnected != FlatMediator.GetInfo().Connected) { return false; }
            if (FilterWheelConnected != FilterWheelMediator.GetInfo().Connected) { return false; }
            if (RotatorConnected != RotatorMediator.GetInfo().Connected) { return false; }
            if (SwitchConnected != SwitchMediator.GetInfo().Connected) { return false; }
            if (WeatherConnected != WeatherDataMediator.GetInfo().Connected) { return false; }
            return true;
        }

        private static double HFD = 0;

        private static double StarMass = 0;

        private static double SNR = 0;
        
        private static void GuiderMediator_GuideEvent(object sender, NINA.Core.Interfaces.IGuideStep e) {
            if (GuiderMediator.GetInfo().Connected) {
                if (GuiderMediator.GetDevice() is PHD2Guider) {
                    if (e is PhdEventGuideStep eventGuideStep) {
                        HFD = eventGuideStep.HFD;
                        StarMass = eventGuideStep.StarMass;
                        SNR = eventGuideStep.SNR;
                    }
                }
            }
        }

        private static ObserverInfo Observer = null;

        public static Object SYMBOL_LOCK = new object();

        private static HashSet<string> LoggedOnce = new HashSet<string>();
        public static void LogOnce (string message) {
            if (LoggedOnce.Contains(message)) return;
            Logger.Warning(message);
            LoggedOnce.Add(message);
        }

        private static void AddSymbol(List<string> i, string token, object value) {
            AddSymbol(i, token, value, null, false);
        }
        private static void AddSymbol(List<string> i, string token, object value, string[] values) {
            AddSymbol(i, token, value, values, false);
        }

        private static void AddSymbol(List<string> i, string token, object value, string[] values, bool silent) {
            try {
                SwitchWeatherKeys.TryAdd(token, value);
                Logger.Trace("Adding key " + token + " with value " + value);
            } catch (Exception ex) {
                Logger.Error(ex);
            }
            if (silent) {
                return;
            }
            StringBuilder sb = new StringBuilder(token);
            try {
                sb.Append(": ");
                if (values != null) {
                    sb.Append(values[(int)value + 1]);
                } else if (value is double d) {
                    sb.Append(Math.Round(d, 2));
                } else if (value is long l) {
                    sb.Append(Expr.ExprValueString(l));
                } else if (value is int n) {
                    sb.Append(n);
                } else {
                    sb.Append("'" + value.ToString() + "'");
                }
                //sb.Append(')');
                i.Add(sb.ToString());

                if (values != null) {
                    for (int v = 0; v < values.Length; v++) {
                        if (values[v] != null) {
                            SwitchWeatherKeys.TryAdd(values[v], v - 1);
                        }
                    }
                }
            } catch (Exception e) {
                i.Add("Error adding " + token);
                Logger.Warning("Exception (" + e.Message + "): " + token + ", " + value + ", " + values);
            }
        }

        private static string[] PierConstants = new string[] { "PierUnknown", "PierEast", "PierWest" };

        private static string[] RoofConstants = new string[] { null, "RoofNotOpen", "RoofOpen", "RoofCannotOpenOrRead" };

        private static string[] ShutterConstants = new string[] { "ShutterNone", "ShutterOpen", "ShutterClosed", "ShutterOpening", "ShutterClosing", "ShutterError" };

        private static string[] CoverConstants = new string[] { null, "CoverUnknown", "CoverNeitherOpenNorClosed", "CoverClosed", "CoverOpen", "CoverError", "CoverNotPresent" };

        private static string LastTargetName = null;
        private static InputTarget LastTarget = null;

        private static void NoTarget(List<string> i) {
            // Always show TargetValid
            AddSymbol(i, "TargetValid", 0, null, false);
            AddSymbol(i, "TargetRA", 0, null, true);
            AddSymbol(i, "TargetDec", 0, null, true);
            AddSymbol(i, "TargetName", "", null, true);
        }

        private static int lastValidRoofStatus = -1;
        private static int invalidRoofStatusCount = 0;

        public static Task UpdateSwitchWeatherData() {

            //var watch = System.Diagnostics.Stopwatch.StartNew();

            lock (SYMBOL_LOCK) {
                var i = new List<string>();
                SwitchWeatherKeys = new Keys();

                string targetName = null;
                ISequenceItem runningItem = SequencerPlusPlugin.GetRunningItem();
                InputTarget foundTarget = null;
                if (runningItem != null && runningItem.Parent != null) {
                    foundTarget = DSOTarget.FindTarget(runningItem.Parent);
                    if (foundTarget != null) {
                        targetName = foundTarget.TargetName;
                        LastTarget = foundTarget;
                        LastTargetName = targetName;
                    }
                }
                if (targetName == null) {
                    targetName = LastTargetName;
                    foundTarget = LastTarget;
                }

                if (targetName != null && targetName.Length > 0) {
                    if (foundTarget != null && foundTarget.InputCoordinates != null) {
                        Coordinates c = foundTarget.InputCoordinates.Coordinates;
                        if (c.RA != 0 && c.Dec != 0) {
                            AddSymbol(i, "TargetRA", c.RA);
                            AddSymbol(i, "TargetDec", c.Dec);
                            AddSymbol(i, "TargetValid", 1);
                            AddSymbol(i, "TargetName", targetName);
                            AddSymbol(i, "TargetRotation", foundTarget.PositionAngle);
                        } else {
                            NoTarget(i);
                        }
                    } else {
                        NoTarget(i);
                    }
                } else {
                    NoTarget(i);
                }

                foreach (var kvp in MessageKeys) {
                    VariableMessage vm = (VariableMessage)kvp.Value;
                    AddSymbol(i, kvp.Key, vm.value);
                }
                
                if (Observer == null) {
                    Observer = new ObserverInfo() {
                        Latitude = ProfileService.ActiveProfile.AstrometrySettings.Latitude,
                        Longitude = ProfileService.ActiveProfile.AstrometrySettings.Longitude
                    };
                }

                var sunPos = AstroUtil.GetSunPosition(DateTime.Now, AstroUtil.GetJulianDate(DateTime.Now), Observer);
                Coordinates sunCoords = new Coordinates(sunPos.RA, sunPos.Dec, Epoch.JNOW, Coordinates.RAType.Hours);
                var tc = sunCoords.Transform(Angle.ByDegree(Observer.Latitude), Angle.ByDegree(Observer.Longitude));

                AddSymbol(i, "MoonAltitude", AstroUtil.GetMoonAltitude(DateTime.UtcNow, Observer));
                AddSymbol(i, "MoonIllumination", AstroUtil.GetMoonIllumination(DateTime.Now));
                AddSymbol(i, "SunAltitude", tc.Altitude.Degree);
                AddSymbol(i, "SunAzimuth", tc.Azimuth.Degree);

                double lst = AstroUtil.GetLocalSiderealTimeNow(ProfileService.ActiveProfile.AstrometrySettings.Longitude);
                if (lst < 0) {
                    lst = AstroUtil.EuclidianModulus(lst, 24);
                }
                AddSymbol(i, "LocalSiderealTime", lst);

                TimeSpan time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                double timeSeconds = Math.Floor(time.TotalSeconds);
                AddSymbol(i, "TIME", timeSeconds);

                AddSymbol(i, "EXITCODE", LastExitCode);

                TelescopeInfo telescopeInfo = TelescopeMediator.GetInfo();
                TelescopeConnected = telescopeInfo.Connected;
                if (TelescopeConnected) {
                    AddSymbol(i, "Altitude", telescopeInfo.Altitude);
                    AddSymbol(i, "Azimuth", telescopeInfo.Azimuth);
                    AddSymbol(i, "AtPark", telescopeInfo.AtPark);
                    
                    Coordinates c = telescopeInfo.Coordinates.Transform(Epoch.J2000);
                    AddSymbol(i, "RightAscension", c.RA); // telescopeInfo.RightAscension);
                    AddSymbol(i, "Declination", c.Dec); // telescopeInfo.Declination);

                    AddSymbol(i, "SideOfPier", (int)telescopeInfo.SideOfPier, PierConstants);
                }

                SafetyMonitorInfo safetyInfo = SafetyMonitorMediator.GetInfo();
                SafetyConnected = safetyInfo.Connected;
                if (SafetyConnected) {
                    AddSymbol(i, "IsSafe", safetyInfo.IsSafe);
                } else {
                    AddSymbol(i, "IsSafe", false);
                }

                string roofStatus = SequencerPlusPluginObject.RoofStatus;
                string roofOpenString = SequencerPlusPluginObject.RoofOpenString;
                if (roofStatus?.Length > 0 && roofOpenString?.Length > 0) {
                    // It's actually a file name..
                    int status = 0;
                    try {
                        var lastLine = File.ReadLines(roofStatus).Last();
                        if (lastLine.ToLower().Contains(roofOpenString.ToLower())) {
                            status = 1;
                        }
                        lastValidRoofStatus = status;
                        invalidRoofStatusCount = 0;
                    } catch (Exception e) {
                        Logger.Warning("Roof status, error: " + e.Message);
                        if (++invalidRoofStatusCount > 4) {
                            Logger.Warning("Four consecutive roof status errors, reporting status 2");
                            status = 2;
                        } else {
                            Logger.Warning("Roof status error #" + invalidRoofStatusCount + ", reporting status " + lastValidRoofStatus + " for now");
                            status = lastValidRoofStatus;
                        }
                    }
                    AddSymbol(i, "RoofStatus", status, RoofConstants);
                }

                    FocuserInfo fInfo = FocuserMediator.GetInfo();
                FocuserConnected = fInfo.Connected;
                if (fInfo != null && FocuserConnected) {
                    AddSymbol(i, "FocuserPosition", fInfo.Position);
                    AddSymbol(i, "FocuserTemperature", fInfo.Temperature);
                }

                // Get SensorTemp
                CameraInfo cameraInfo = CameraMediator.GetInfo();
                CameraConnected = cameraInfo.Connected;
                if (CameraConnected) {
                    AddSymbol(i, "SensorTemp", cameraInfo.Temperature);

                    // Hidden
                    SwitchWeatherKeys.Add("camera__PixelSize", cameraInfo.PixelSize);
                    SwitchWeatherKeys.Add("camera__XSize", cameraInfo.XSize);
                    SwitchWeatherKeys.Add("camera__YSize", cameraInfo.YSize);
                    SwitchWeatherKeys.Add("camera__CoolerPower", cameraInfo.CoolerPower);
                    SwitchWeatherKeys.Add("camera__CoolerOn", cameraInfo.CoolerOn);
                    SwitchWeatherKeys.Add("telescope__FocalLength", ProfileService.ActiveProfile.TelescopeSettings.FocalLength);
                }

                DomeInfo domeInfo = DomeMediator.GetInfo();
                DomeConnected = domeInfo.Connected;
                if (DomeConnected) {
                    AddSymbol(i, "ShutterStatus", (int)domeInfo.ShutterStatus, ShutterConstants);
                    AddSymbol(i, "DomeAzimuth", domeInfo.Azimuth);
                }

                FlatDeviceInfo flatInfo = FlatMediator.GetInfo();
                FlatConnected = flatInfo.Connected;
                if (FlatConnected) {
                    AddSymbol(i, "CoverState", (int)flatInfo.CoverState, CoverConstants);
                }

                RotatorInfo rotatorInfo = RotatorMediator.GetInfo();
                RotatorConnected = rotatorInfo.Connected;
                if (RotatorConnected) {
                    AddSymbol(i, "RotatorPosition", rotatorInfo.MechanicalPosition);
                }

                FilterWheelInfo filterWheelInfo = FilterWheelMediator.GetInfo();
                FilterWheelConnected = filterWheelInfo.Connected;
                if (FilterWheelConnected) {
                    var f = ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters;
                    foreach (FilterInfo filterInfo in f) {
                        try {
                            SwitchWeatherKeys.Add("Filter_" + RemoveSpecialCharacters(filterInfo.Name), filterInfo.Position);
                        } catch (Exception) {
                            LogOnce("Exception trying to add filter '" + filterInfo.Name + "' in UpdateSwitchWeatherData");
                        }
                    }

                    if (filterWheelInfo.SelectedFilter != null) {
                        SwitchWeatherKeys.Add("CurrentFilter", filterWheelInfo.SelectedFilter.Position);
                        i.Add("CurrentFilter: Filter_" + RemoveSpecialCharacters(filterWheelInfo.SelectedFilter.Name));
                    }
                }

                // Get switch values
                SwitchInfo switchInfo = SwitchMediator.GetInfo();
                SwitchConnected = switchInfo.Connected;
                if (SwitchConnected) {
                    foreach (ISwitch sw in switchInfo.ReadonlySwitches) {
                        string key = RemoveSpecialCharacters(sw.Name);
                        SwitchWeatherKeys.TryAdd(key, sw.Value);
                        i.Add("G: " + key + ": " + sw.Value);
                    }
                    foreach (ISwitch sw in switchInfo.WritableSwitches) {
                        string key = RemoveSpecialCharacters(sw.Name);
                        SwitchWeatherKeys.TryAdd(key, sw.Value);
                        i.Add("S: " + key + ": " + sw.Value);
                    }
                }

                // Get weather values
                WeatherDataInfo weatherInfo = WeatherDataMediator.GetInfo();
                WeatherConnected = weatherInfo.Connected;
                if (WeatherConnected) {
                    foreach (string dataName in WeatherData) {
                        double t = weatherInfo.TryGetPropertyValue(dataName, Double.NaN);
                        if (!Double.IsNaN(t)) {
                            t = Math.Round(t, 2);
                            string key = RemoveSpecialCharacters(dataName);
                            SwitchWeatherKeys.TryAdd(key, t);
                            i.Add("W: " + key + ": " + t);
                        }
                    }
                }

                Keys imageKeys = TakeExposure.LastImageResults;
                if (imageKeys != null) {
                    foreach (KeyValuePair<string, object> kvp in imageKeys) {
                        SwitchWeatherKeys.TryAdd(kvp.Key, kvp.Value);
                        var v = kvp.Value;
                        if (v is double d) {
                            v = Math.Round(d, 2);
                        }
                        if (!kvp.Key.Contains("__")) {
                            i.Add(kvp.Key + ": " + v);
                        }
                    }
                } else {
                    SwitchWeatherKeys.TryAdd("HFR", Double.NaN);
                    SwitchWeatherKeys.TryAdd("StarCount", Double.NaN);
                    SwitchWeatherKeys.TryAdd("FWHM", Double.NaN);
                    SwitchWeatherKeys.TryAdd("Eccentricity", Double.NaN);
                    //i.Add(" No image data");
                }

                Switches = i;

                SequencerPlusPluginDockable.UpdateData();

                //watch.Stop();
                //Logger.Info("Update time: " + watch.ElapsedTicks/10000.0 + "ms, Keys: " + SwitchWeatherKeys.Count());

                return Task.CompletedTask;
            }
        }
    }
}
