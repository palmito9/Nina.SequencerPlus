using Google.Protobuf.WellKnownTypes;
using NCalc;
using NCalc.Domain;
using NCalc.Handlers;
using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Profile;
using NINA.Sequencer;
using NINA.Sequencer.SequenceItem;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static WhenPlugin.When.Symbol;
using Array = WhenPlugin.When.Symbol.Array;
using Expression = NCalc.Expression;

namespace WhenPlugin.When {
    [JsonObject(MemberSerialization.OptIn)]
    public class Expr : BaseINPC {

        public Expr(string exp, Symbol sym) {
            Symbol = sym;
            SequenceEntity = sym;
            Expression = exp;
        }

        public Expr(ISequenceEntity item, string expression) {
            SequenceEntity = item;
            Expression = expression;
        }
        public Expr(ISequenceEntity item) {
            SequenceEntity = item;
        }

        public Expr(ISequenceEntity item, string expression, string type) {
            SequenceEntity = item;
            // TYPE MUST BE BEFORE EXPRESSION!!
            Type = type;
            Expression = expression;
        }

        public Expr(ISequenceEntity item, string expression, string type, Action<Expr> setter) {
            SequenceEntity = item;
            // SETTER MUST BE BEFORE EXPRESSION!!
            Setter = setter;
            Expression = expression;
            Type = type;
        }

        public Expr(ISequenceEntity item, string expression, string type, Action<Expr> setter, double def) {
            SequenceEntity = item;
            // SETTER and DEFAULT MUST BE BEFORE EXPRESSION!!
            Setter = setter;
            Default = def;
            Expression = expression;
            Type = type;
        }

        public Expr(Expr cloneMe) : this(cloneMe.SequenceEntity, cloneMe.Expression, cloneMe.Type) {
            Setter = cloneMe.Setter;
            Symbol = cloneMe.Symbol;
        }

        public static bool JustWarnings (string error) {
            string[] errors = error.Split(";");
            bool red = false;
            bool orange = false;
            foreach (string e in errors) {
                if (e.Contains("Not evaluated") || e.Contains("External")) {
                    orange = true; ;
                } else {
                    red = true;
                }
            }
            if (orange && !red) return true;
            return false;
        }

        public SolidColorBrush InfoButtonColor {
            get {
                if (Error == null) return new SolidColorBrush(Colors.White);
                return JustWarnings(Error) ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Colors.Red);
            }
            set { }
        }

        public string InfoButtonChar {
            get {
                if (Error == null) return "\u24D8"; // "?";
                return "\u26A0";
            }
            set { }
        }

        public double InfoButtonSize {
            get {
                if (Error == null) return 24;
                return 18;
            }
            set { }
        }

        public string InfoButtonMargin {
            get {
                if (Error == null) return "5,-2,0,0";
                return "5,2,0,0";
            }
            set { }
        }

        private string _expression = "";

        private object LOCK = new object();

        [JsonProperty]
        public virtual string Expression {
            get => _expression;
            set {
                if (value == null) return;
                value = value.Trim();
                if (value.Length == 0) {
                    IsExpression = false;
                    if (!double.IsNaN(Default)) {
                        Value = Default;
                    } else {
                        Value = Double.NaN;
                    }
                    _expression = value;
                    Parameters.Clear();
                    Resolved.Clear();
                    References.Clear();
                    Error = null;
                    return;
                }
                Double result;

                if (value.StartsWith('%') && value.EndsWith('%') && value.Length > 2) {
                    value = "__ENV_" + value.Substring(1, value.Length - 2);
                }

                if (value.StartsWith("~~")) {
                    Symbol.DumpSymbols();
                    value = value.Substring(2);
                }

                if (value != _expression && IsExpression) {
                    // The value has changed.  Clear what we had...cle
                    foreach (var symKvp in Resolved) {
                        Symbol s = symKvp.Value;
                        if (s != null) {
                            symKvp.Value.RemoveConsumer(this);
                        }
                    }
                    Resolved.Clear();
                    Parameters.Clear();
                }

                _expression = value;
                if (Double.TryParse(value, out result)) {
                    Error = null;
                    IsExpression = false;
                    Value = result;
                    // Notify consumers
                    if (Symbol != null) {
                        SymbolDirty(Symbol);
                    } else {
                        // We always want to show the result if not a Symbol
                        //IsExpression = true;
                    }
                } else if (Regex.IsMatch(value, "{(\\d+)}")) { // Should be /^\d*\.?\d*$/
                    IsExpression = false;
                } else {
                    IsExpression = true;

                    // Evaluate just so that we can parse the expression
                    Expression e = new Expression(value, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
                    e.Parameters = EmptyDictionary;
                    IsSyntaxError = false;
                    try {
                        e.Evaluate();
                    } catch (NCalc.Exceptions.NCalcParserException) {
                        // We should expect this, since we're just trying to find the parameters used
                        Error = "Syntax Error";
                        return;
                    } catch (Exception) {
                        // That's ok
                    }

                    // Find the parameters used
                    References.Clear();
                    foreach (var p in e.GetParameterNames()) {
                        References.Add(p);
                    }

                    // References now holds all of the CV's used in the expression
                    Parameters.Clear();
                    Resolved.Clear();
                    Evaluate();
                    if (Symbol != null) SymbolDirty(Symbol);
                }
                RaisePropertyChanged("Expression");
                RaisePropertyChanged("IsAnnotated");
            }
        }

        private Double iDefault = Double.NaN;
        public Double Default {
            get => iDefault;
            set {
                iDefault = value;
                RaisePropertyChanged("Default");
                RaisePropertyChanged("Value");
                RaisePropertyChanged("ValueString");
                RaisePropertyChanged("StringValue");
            }
        }

        public string ExprErrors {
            get {
                if (Error == null) {
                    return "No errors in Expression";
                } else if (JustWarnings(Error)) {
                    return "Warning(s): " + Error;
                } else {
                    return "Error(s): " + Error;
                }
            }
            set { }
        }

        public Symbol Symbol { get; set; } = null;
        public ISequenceEntity SequenceEntity { get; set; } = null;

        [JsonProperty]
        public string Type { get; set; } = "Any";


        public Action<Expr> Setter { get; set; }

        public IList<string> Switches {
            get => Symbol.GetSwitches();
            set { }
        }

        public List<string> GenericData {
            get {
                IList<string> sw = Switches;
                List<string> data = [];
                List<string> gsData = [];
                List<string> wData = [];
                foreach (string s in sw) {
                    if (s.StartsWith("W:")) {
                        wData.Add(s.Substring(3));
                    } else if (s.StartsWith("G:") || s.StartsWith("S:")) {
                        gsData.Add(s.Substring(3));
                    } else {
                        data.Add(s);
                    }
                }
                if (gsData.Count > 0) {
                    gsData.Sort();
                } else {
                    gsData.Add("None");
                }
                GaugeSwitchData = gsData;
                if (wData.Count > 0) {
                    wData.Sort();
                } else {
                    wData.Add("None");
                }
                WeatherData = wData;
                data.Sort();
                return data;
            }
            set { }
        }

        List<string> iGaugeSwitchData = null;
        public List<string> GaugeSwitchData {
            get => iGaugeSwitchData;
            set {
                iGaugeSwitchData = value;
            }
        }

        List<string> iWeatherData = null;
        public List<string> WeatherData {
            get => iWeatherData;
            set {
                iWeatherData = value;
            }
        }


        public string Constants {
            get => Symbol.ShowSymbols(this);
            set { }
        }


        private static Dictionary<string, object> EmptyDictionary = new Dictionary<string, object>();

        private double _value = Double.NaN;
        public virtual double Value {
            get {
                if (double.IsNaN(_value) && !double.IsNaN(Default)) {
                    return Default;
                }
                return _value;
            }
            set {
                if (value != _value) {
                    if ("Integer".Equals(Type)) {
                        if (StringValue != null) {
                            Error = "Value must be an Integer";
                        }
                        value = Double.Floor(value);
                    }
                    _value = value;
                    if (Setter != null) {
                        Setter(this);
                    }
                    RaisePropertyChanged("StringValue");
                    RaisePropertyChanged("ValueString");
                    RaisePropertyChanged("IsExpression");
                    RaisePropertyChanged("DockableValue");
                }
            }
        }

        private string _error;
        public virtual string Error {
            get => _error;
            set {
                if (value != _error) {
                    _error = value;
                    RaisePropertyChanged("ValueString");
                    RaisePropertyChanged("IsExpression");
                    RaisePropertyChanged("IsAnnotated");
                    RaisePropertyChanged("Error");
                    RaisePropertyChanged("StringValue");
                    RaisePropertyChanged("InfoButtonColor");
                    RaisePropertyChanged("InfoButtonChar");
                    RaisePropertyChanged("InfoButtonSize");
                    RaisePropertyChanged("InfoButtonMargin");
                }
            }
        }

        private const long ONE_YEAR = 60 * 60 * 24 * 365;

        public string StringValue { get; set; }

        public static string ExprValueString (long value) {
            long start = DateTimeOffset.Now.ToUnixTimeSeconds() - ONE_YEAR;
            long end = start + (2 * ONE_YEAR);
            if (value > start && value < end) {
                DateTime dt = ConvertFromUnixTimestamp(value).ToLocalTime();
                if (dt.Day == DateTime.Now.Day + 1) {
                    return dt.ToShortTimeString() + " tomorrow";
                } else if (dt.Day == DateTime.Now.Day - 1) {
                    return dt.ToShortTimeString() + " yesterday";
                } else
                    return dt.ToShortTimeString();
            } else {
                return value.ToString();
            }
        }

        public string ValueString {
            get {
                if (Error != null) return Error;
                if (Value is double.NegativeInfinity) {
                    return StringValue;
                }
                long start = DateTimeOffset.Now.ToUnixTimeSeconds() - ONE_YEAR;
                long end = start + (2 * ONE_YEAR);
                if (Value > start && Value < end) {
                    DateTime dt = ConvertFromUnixTimestamp(Value).ToLocalTime();
                    if (dt.Day == DateTime.Now.Day + 1) {
                        return dt.ToShortTimeString() + " tomorrow";
                    } else if (dt.Day == DateTime.Now.Day - 1) {
                        return dt.ToShortTimeString() + " yesterday";
                    } else
                        return dt.ToShortTimeString();
                } else {
                    return Value.ToString();
                }
            }
            set { }
        }

        public bool IsExpression { get; set; } = false;

        public bool IsSyntaxError { get; set; } = false;

        public bool IsAnnotated {
            get => IsExpression || Error != null;
            set { }
        }

        // References are the parsed tokens used in the Expr
        public HashSet<string> References { get; set; } = new HashSet<string>();

        // Resolved are the Symbol's that have been found (from the References)
        public Dictionary<string, Symbol> Resolved = new Dictionary<string, Symbol>();

        // Parameters are NCalc Parameters used in the call to NCalc.Evaluate()
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();

        public static DateTime ConvertFromUnixTimestamp(double timestamp) {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddSeconds(timestamp);
        }
        public long UnixTimeNow() {
            var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            return (long)timeSpan.TotalSeconds;
        }

        public static Random RNG = new Random();


        public void ExtensionFunction(string name, FunctionArgs args) {
            DateTime dt;
            try {
                if (args.Parameters.Length > 0) {
                    try {
                        var utc = ConvertFromUnixTimestamp(Convert.ToDouble(args.Parameters[0].Evaluate()));
                        dt = utc.ToLocalTime();
                    } catch (Exception) {
                        dt = DateTime.MinValue;
                    }
                } else {
                    dt = DateTime.Now;
                }
                if (name == "altitude") {
                    if (args.Parameters.Length < 2) {
                        throw new ArgumentException();
                    }
                    double _longitude = WhenPlugin.GetLongitude();
                    double _latitude = WhenPlugin.GetLatitude();
                    var siderealTime = AstroUtil.GetLocalSiderealTime(DateTime.Now, _longitude);
                    var hourAngle = AstroUtil.GetHourAngle(siderealTime, Convert.ToDouble(args.Parameters[0].Evaluate()));
                    var degAngle = AstroUtil.HoursToDegrees(hourAngle);
                    args.Result = AstroUtil.GetAltitude(degAngle, _latitude, Convert.ToDouble(args.Parameters[1].Evaluate()));
                } else if (name == "now") {
                    args.Result = UnixTimeNow();
                } else if (name == "hour") {
                    args.Result = (int)dt.Hour;
                } else if (name == "minute") {
                    args.Result = (int)dt.Minute;
                } else if (name == "day") {
                    args.Result = (int)dt.Day;
                } else if (name == "month") {
                    args.Result = (int)dt.Month;
                } else if (name == "year") {
                    args.Result = (int)dt.Year;
                } else if (name == "dow") {
                    args.Result = (int)dt.DayOfWeek;
                } else if (name == "dateTime") {
                    args.Result = 0;
                } else if (name == "CtoF") {
                    args.Result = 32 + (Convert.ToDouble(args.Parameters[0].Evaluate()) * 9 / 5);
                } else if (name == "MStoMPH") {
                    args.Result = (Convert.ToDouble(args.Parameters[0].Evaluate()) * 2.237);
                } else if (name == "KPHtoMPH") {
                    args.Result = (Convert.ToDouble(args.Parameters[0].Evaluate()) * .621);
                } else if (name == "dateString") {
                    if (args.Parameters.Length < 2) {
                        throw new ArgumentException();
                    }
                    args.Result = dt.ToString((string)args.Parameters[1].Evaluate());
                } else if (name == "defined") {
                    string str = Convert.ToString(args.Parameters[0].Evaluate());
                    ISequenceItem runningItem = WhenPlugin.GetRunningItem();
                    if (runningItem != null) {
                        args.Result = Symbol.FindSymbol(str, runningItem.Parent) != null;
                    } else {
                        args.Result = 0;
                    }
                } else if (name == "startsWith") {
                    string str = Convert.ToString(args.Parameters[0].Evaluate());
                    string f = Convert.ToString(args.Parameters[1].Evaluate());
                    args.Result = str.StartsWith(f);
                } else if (name == "length") {
                    string arrayName = Convert.ToString(args.Parameters[0].Evaluate());
                    Array array;
                    if (Arrays.TryGetValue(arrayName, out array)) {
                        args.Result = array.Count;
                    } else {
                        args.Result = -1;
                    }
                } else if (name == "strLength") {
                    var e = args.Parameters[0].Evaluate();
                    if (e is string es) {
                        args.Result = es.Length;
                    } else {
                        args.Result = -1;
                    }
                } else if (name == "strConcat") {
                    var e = args.Parameters[0].Evaluate();
                    var i = args.Parameters[1].Evaluate();
                    if (!(e is string)) {
                        e = e.ToString();
                    }
                    if (!(i is string)) {
                        i = i.ToString();
                    }
                    if (e is string es && i is string iss) {
                        args.Result = String.Concat(es, iss);
                    } else {
                        args.Result = "";
                    }
                } else if (name == "strAtPos") {
                    var e = args.Parameters[0].Evaluate();
                    var i = args.Parameters[1].Evaluate();
                    if (e is string es && i is int iint && iint >= 0 && iint < es.Length) {
                        args.Result = Convert.ToString(es[iint]);
                    } else {
                        args.Result = "";
                    }
                } else if (name == "sumOfValues" || name == "averageOfValues") {
                    string arrayName = Convert.ToString(args.Parameters[0].Evaluate());
                    Array array;
                    if (Arrays.TryGetValue(arrayName, out array)) {
                        double sum = 0;
                        foreach (var kvp in array) {
                            sum += (double)kvp.Value;
                        }
                        if (name == "sumOfValues") {
                            args.Result = sum;
                        } else {
                            args.Result = sum / array.Count;
                        }
                    } else {
                        args.Result = -1;
                    }
                } else if (name == "random") {
                    args.Result = RNG.NextDouble();
                }
            } catch (Exception ex) {
                Logger.Error("Error evaluating function " + name + ": " + ex.Message);
            }
        }
        public void RemoveParameter(string identifier) {
            Parameters.Remove(identifier);
            Resolved.Remove(identifier);
            Evaluate();
        }

        public bool Dirty { get; set; } = false;

        public bool Volatile { get; set; } = false;
        public bool ImageVolatile { get; set; } = false;
        public bool GlobalVolatile { get; set; } = false;

        public void DebugWrite() {
            Debug.WriteLine("* Expression " + Expression + " evaluated to " + ((Error != null) ? Error : Value) + " (in " + (Symbol != null ? Symbol : SequenceEntity) + ")");
        }

        public void ReferenceRemoved(Symbol sym) {
            // A definition we use was removed
            string identifier = sym.Identifier;
            Parameters.Remove(identifier);
            Resolved.Remove(identifier);
            Evaluate();
        }

        public static string NOT_DEFINED = "Parameter was not defined (Parameter";

        private void AddParameter(string reference, object value) {
            Parameters.Add(reference, value);
        }


        private void Resolve(string reference, Symbol sym) {
            Parameters.Remove(reference);
            Resolved.Remove(reference);
            if (sym.Expr.Error == null) {
                Resolved.Add(reference, sym);
                if (sym.Expr.Value == double.NegativeInfinity) {
                    AddParameter(reference, sym.Expr.StringValue);
                } else if (!Double.IsNaN(sym.Expr.Value)) {
                    AddParameter(reference, sym.Expr.Value);
                }
            }
        }

        public void Refresh() {
            Parameters.Clear();
            Resolved.Clear();
            Evaluate();
        }

        private void AddError (string s) {
            if (Error == null) {
                Error = s;
            } else {
                Error = Error + "; " + s;
            }
        }

        public void Evaluate() {
            Evaluate(false);
        }

        public void Evaluate(bool validateOnly) {
            if (Monitor.TryEnter(SYMBOL_LOCK, 1000)) {
                try {
                    if (!IsExpression) {
                        //Error = null;
                        return;
                    }
                    if (Expression.Length == 0) {
                        // How the hell to clear the Expr
                        IsExpression = false;
                        RaisePropertyChanged("Value");
                        RaisePropertyChanged("ValueString");
                        RaisePropertyChanged("StringValue");
                        RaisePropertyChanged("IsExpression");
                        return;
                    }
                    if (SequenceEntity == null) return;
                    if (!Symbol.IsAttachedToRoot(SequenceEntity)) {
                        return;
                    }
                    //Debug.WriteLine("Evaluate " + this);
                    Dictionary<string, object> DataSymbols = Symbol.GetSwitchWeatherKeys();

                    if (Volatile || GlobalVolatile) {
                        IList<string> volatiles = new List<string>();
                        foreach (KeyValuePair<string, Symbol> kvp in Resolved) {
                            if (kvp.Value == null || kvp.Value.Expr.GlobalVolatile) {
                                volatiles.Add(kvp.Key);
                            }
                        }
                        foreach (string key in volatiles) {
                            Resolved.Remove(key);
                            Parameters.Remove(key);
                        }
                    }

                    Volatile = GlobalVolatile;

                    ImageVolatile = false;

                    StringValue = null;

                    if (Parameters.Count < Resolved.Count) {
                        Parameters.Clear();
                        Resolved.Clear();
                    }

                    // External, don't report error during validation
                    bool ext = false;
                    
                    // First, validate References
                    foreach (string sRef in References) {
                        Symbol sym;
                        // Take care of "by reference" arguments
                        string symReference = sRef;
                        if (symReference.StartsWith("_") && !symReference.StartsWith("__")) {
                            symReference = sRef.Substring(1);
                        } else if (symReference.StartsWith("$")) {
                            symReference = symReference.Substring(1);
                            ext = true;
                        }
                        // Remember if we have any image data
                        if (!ImageVolatile && symReference.StartsWith("Image_")) {
                            ImageVolatile = true;
                        }
                        bool found = Resolved.TryGetValue(symReference, out sym);
                        if (!found || sym == null) {
                            // !found -> couldn't find it; sym == null -> it's a DataSymbol
                            if (!found) {
                                sym = Symbol.FindSymbol(symReference, SequenceEntity.Parent);
                            }
                            if (sym != null) {
                                // Link Expression to the Symbol
                                Resolve(symReference, sym);
                                sym.AddConsumer(this);
                            } else {
                                SymbolDictionary cached;
                                found = false;
                                if (SymbolCache.TryGetValue(WhenPluginObject.Globals, out cached)) {
                                    Symbol global;
                                    if (cached != null && cached.TryGetValue(symReference, out global)) {
                                        Resolve(symReference, global);
                                        global.AddConsumer(this);
                                        found = true;
                                    }
                                }
                                // Try in the old Switch/Weather keys
                                object Val;
                                if (!found && DataSymbols.TryGetValue(symReference, out Val)) {
                                    // We don't want these resolved, just added to Parameters
                                    Resolved.Remove(symReference);
                                    Resolved.Add(symReference, null);
                                    Parameters.Remove(symReference);
                                    AddParameter(symReference, Val);
                                    Volatile = true;
                                }
                                if (!found && symReference.StartsWith("__ENV_")) {
                                    string env = Environment.GetEnvironmentVariable(symReference.Substring(6), EnvironmentVariableTarget.User);
                                    UInt32 val;
                                    if (env == null || !UInt32.TryParse(env, out val)) {
                                        val = 0;
                                    }
                                    // We don't want these resolved, just added to Parameters
                                    Resolved.Remove(symReference);
                                    Resolved.Add(symReference, null);
                                    Parameters.Remove(symReference);
                                    AddParameter(symReference, val);
                                    Volatile = true;
                                }
                            }
                        }
                    }

                    Expression e = new Expression(Expression, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
                    e.EvaluateFunction += ExtensionFunction;
                    e.Parameters = Parameters;

                    if (e.HasErrors()) {
                        Error = "Syntax Error";
                        return;
                    }

                    Error = null;
                    try {
                        if (Parameters.Count != References.Count) {
                            foreach (string r in References) {
                                string symReference = r;
                                if (symReference.StartsWith('_') || symReference.StartsWith('@')) {
                                    symReference  = symReference.Substring(1);
                                }
                                if (!Parameters.ContainsKey(symReference)) {
                                    // Not defined or evaluated
                                    Symbol s = FindSymbol(symReference, SequenceEntity.Parent);
                                    if (s is SetVariable sv && !sv.Executed) {
                                        AddError("Not evaluated: " + r);
                                    } else if (r.StartsWith("_")) {
                                        AddError("Reference: " + r);
                                    } else {
                                        if (r.StartsWith('$') && ext && validateOnly) {
                                            AddError ("External: " + symReference);
                                        } else {
                                            AddError("Undefined: " + r);
                                        }
                                    }
                                }
                            }
                            RaisePropertyChanged("Error");
                            RaisePropertyChanged("ValueString");
                            RaisePropertyChanged("StringValue");
                            RaisePropertyChanged("Value");
                        } else {
                            object eval = e.Evaluate();
                            // We got an actual value
                            if (eval is Boolean b) {
                                Value = b ? 1 : 0;
                                Error = null;
                            } else {
                                try {
                                    Value = Convert.ToDouble(eval);
                                    Error = null;
                                } catch (Exception) {
                                    string str = (string)eval;
                                    StringValue = str;
                                    Value = double.NegativeInfinity;
                                    if ("Integer".Equals(Type)) {
                                        Error = "Syntax error";
                                    }
                                }
                            }
                            RaisePropertyChanged("Error");
                            RaisePropertyChanged("StringValue");
                            RaisePropertyChanged("ValueString");
                            RaisePropertyChanged("Value");
                        }

                    } catch (ArgumentException ex) {
                        string error = ex.Message;
                        // Shorten this common error from NCalc
                        int pos = error.IndexOf(NOT_DEFINED);
                        if (pos == 0) {
                            string var = error.Substring(NOT_DEFINED.Length).TrimEnd(')');
                            if (!var.StartsWith("'_")) {
                                Logger.Error("? Linda's error: " + ex.Message);
                                error = "Reference";
                            } else {
                                error = "Undefined: " + var;
                            }
                        }
                        Error = error;
                    } catch (Exception ex) {
                        if (ex is NCalc.Exceptions.NCalcEvaluationException || ex is NCalc.Exceptions.NCalcParserException) {
                            Error = "Syntax Error";
                            return;
                        } else {
                            Error = "Unknown Error; see log";
                            Logger.Warning("Exception evaluating " + Expression + ": " + ex.Message);
                        }
                    }
                    Dirty = false;
                } finally {
                    Monitor.Exit(SYMBOL_LOCK);
                }
            } else {
                Logger.Error("Evaluate could not get SYMBOL_LOCK: " + this);
                //if (!LOCK_ERROR) {
                //    Notification.ShowError("Evaluate could not get SYMBOL_LOCK; see log for info");
                //}
                //LOCK_ERROR = true;
            }
        }

        //private bool LOCK_ERROR = false;

        public void Validate(IList<string> issues) {
            if (Error != null || Volatile) {
                if (Expression != null && Expression.Length == 0 && Value == Default) {
                    Error = null;
                }
                Evaluate(true);
                foreach (KeyValuePair<string, Symbol> kvp in Resolved) {
                    if (kvp.Value == null || kvp.Value.Expr.GlobalVolatile) {
                        GlobalVolatile = true;
                    }
                }
            } else if (Double.IsNaN(Value) && Expression.Length > 0) {
                Error = "Not evaluated";
            } else if (Expression.Length != 0 && Value == Default && Error == null) {
                // This seems very wrong to me; need to figure it out
                Evaluate(true);
            }
        }

        public void Validate() {
            Validate(null);
        }

        public void NotNegative(Expr expr) {
            if (expr.Value < 0) {
                expr.Error = "Must not be negative";
            }
        }

        public static void AddExprIssues (IList<string> issues, params Expr[] exprs) {
            foreach (Expr expr in exprs) {
                expr.Validate();
                if (expr != null && expr.Error != null && !Expr.JustWarnings(expr.Error)) {
                    issues.Add(expr.Error);
                }
            }
        }

        public override string ToString() {
            string id = Symbol != null ? Symbol.Identifier : SequenceEntity.Name;
            if (Error != null) {
                return $"'{Expression}' in {id}, References: {References.Count}, Error: {Error}";
            } else if (Expression.Length == 0) {
                return "None";
            }
            return $"Expression: {Expression} in {id}, References: {References.Count}, Value: {Value}";
        }
    }
}

