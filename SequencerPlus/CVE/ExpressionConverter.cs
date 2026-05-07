#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Utility;
using NINA.Sequencer;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace NINA.Plugin.SequencerPlus {

    public class ExpressionConverter : IMultiValueConverter {

        public static Dictionary<ISequenceEntity, bool> ValidityCache = new Dictionary<ISequenceEntity, bool>();

        public static string NOT_DEFINED = "Parameter was not defined (Parameter";

        private const int VALUE_EXPR = 0;              // The expression to be evaluated
        private const int VALUE_ITEM = 1;              // The ISequenceItem (instruction)
        private const int VALUE_VALU = 2;              // Present to cause source->target updates; not used in code
        private const int VALUE_VALIDATE = 3;          // If present, a validation method (range check, etc.)
        private const int VALUE_HINT = 4;              // For ConstantHintControl, the "hint"
        private const int VALUE_TYPE = 5;              // If present, the type of result needed ("Integer" is the only value supported; others will be Double)
        private const int VALUE_COMBO = 6;             // If present, a IList<string> of combo box values

        private string Validate (ISequenceEntity item, double val, object[] values) {
            if ((values.Length > (VALUE_VALIDATE -1)) && values[VALUE_VALIDATE] is string validationMethod) {
                MethodInfo m = item.GetType().GetMethod(validationMethod);
                if (m != null) {
                    string error = (string)m.Invoke(item, new object[] { val });
                    if (error != string.Empty && item is IValidatable vitem) {
                        vitem.Issues.Add(error);
                        ValidityCache.Remove(item);
                        if (error.Equals("True")) {
                            ValidityCache.Add(item, true);
                        }
                        return " { " + error + " } ";
                    }
                }              
            }
            return string.Empty;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            // value will be a string
            ISequenceEntity item = values[VALUE_ITEM] as ISequenceEntity;
            string type = (string)values[VALUE_TYPE];
            if (values[VALUE_EXPR] is string expr) {
                double val;
                if (string.IsNullOrEmpty(expr) && parameter != null && parameter.GetType() == typeof(String) && parameter.Equals("Hint")) {
                    ValidityCache.Remove(item);
                    ValidityCache.Add(item, true);
                    return 0;
                } else if (string.Equals(expr, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(expr, "false", StringComparison.OrdinalIgnoreCase)) {
                    ValidityCache.Remove(item);
                    ValidityCache.Add(item, true);
                    return "";
                } else if (double.TryParse(expr, out val)) {
                    if (item != null) {
                        ValidityCache.Remove(item);
                        string valid = Validate(item, val, values);
                        if (valid != string.Empty) return valid;
                        ValidityCache.Add(item, true);
                    }
                    if ("Integer".Equals(type) && Double.Floor(val) != val) {
                        return " {" + (int)val + "}  ";
                    }
                    return val;
                } else {
                    double result;
                    IList<string> issues = new List<string>();
                    if (ConstantExpression.IsValid(item, "*Converter*", expr, out result, issues)) {
                        ValidityCache.Remove(item);
                        if (item != null) {
                            string valid = Validate(item, result, values);
                            if (valid != string.Empty) return valid;
                            ValidityCache.Add(item, true);
                        }
                        if ("Integer".Equals(type)) {
                            result = (int)result;
                            if (values.Length > VALUE_COMBO && values[VALUE_COMBO] != null) {
                                IList<string> combo = (IList<string>)values[VALUE_COMBO];
                                if (combo.Count > 0) {
                                    int idx = (int)result;
                                    if (idx >= 0 && idx < combo.Count) {
                                        return " {" + combo[idx] + "}";
                                    }
                                }
                            }
                        }
                        return " {" + result.ToString() + "}";
                    } else if (issues.Count > 0) {
                        ValidityCache.Remove(item);
                        string errorString = issues[0];
                        // Shorten this common error from NCalc
                        int pos = errorString.IndexOf(NOT_DEFINED);
                        if (pos == 0) {
                            errorString = "Undefined: " + errorString.Substring(NOT_DEFINED.Length).TrimEnd(')');
                        }
                        return " {" + errorString + "} ";
                    } else {
                        if (item != null) {
                            ValidityCache.Remove(item);
                        }
                        if (ValidityCache == null || ValidityCache.Count == 0) return ""; // "There are no valid constants defined.";
                        return ""; // return " {Error} ";
                    }
                }
            }
            if (item != null) {
                ValidityCache.Remove(item);
            }
            return "Illegal";
        }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}