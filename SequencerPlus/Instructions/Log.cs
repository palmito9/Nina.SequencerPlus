#region "copyright"

/*
    Copyright © 2016 - 2024 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Locale;
using NINA.Sequencer.SequenceItem;
using System.Text.RegularExpressions;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Annotation +")]
    [ExportMetadata("Description", "Add the specified text to the log, expanding any expressions within {}'s")]
    [ExportMetadata("Icon", "ScriptSVG")]
    [ExportMetadata("Category", "Sequencer+ (Enhanced Instructions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class Log : SequenceItem, IValidatable {

        public Log() {
        }

        private Log(Log cloneMe) : this() {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new Log(this) {
                Script = Script
            };
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        private string script;

        [JsonProperty]
        public string Script {
            get => script;
            set {
                script = value;
                _ = ProcessedScript;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ProcessedScript));
                RaisePropertyChanged(nameof(ProcessedScriptAnnotated));
            }
        }

        public string ProcessedScriptAnnotated {
            get { return "Will be logged as: " + iProcessedScript; }
            set { }
        }


        private string iProcessedScript;
        public string ProcessedScript {
            get {
                string value = Script;
                RaisePropertyChanged();
                if (value != null) {
                    while (true) {
                        string toReplace = Regex.Match(value, @"\{([^\}]+)\}").Groups[1].Value;
                        if (toReplace.Length == 0) break;
                        Expr ex = new Expr(this, toReplace);
                        ProcessedScriptError = null;
                        if (ex.Error != null) {
                            ProcessedScriptError = ex.Error;
                            //Logger.Warning("External Script +, error processing script, " + ex.Error);
                            return "Error";
                        }
                        value = value.Replace("{" + toReplace + "}", ex.ValueString);
                    }
                }
                iProcessedScript = value;
                RaisePropertyChanged("ProcessedScriptAnnotated");
                return value;
            }
            set { }
        }

        public string ProcessedScriptError { get; set; } = null;

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            await Symbol.UpdateSwitchWeatherData();
            Logger.Warning("User log: " + ProcessedScript);
        }

        public bool Validate() {
            var i = new List<string>();
            _ = ProcessedScript;
            if (ProcessedScriptError != null) {
                i.Add(ProcessedScriptError);
            }
            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(Log)}, Text: {Script} ProcessedText: {ProcessedScript}";
        }
    }
}
