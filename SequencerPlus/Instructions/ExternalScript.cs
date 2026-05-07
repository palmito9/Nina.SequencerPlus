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

    [ExportMetadata("Name", "External Script +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Utility_ExternalScript_Description")]
    [ExportMetadata("Icon", "ScriptSVG")]
    [ExportMetadata("Category", "Sequencer+ (Enhanced Instructions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class ExternalScript : SequenceItem, IValidatable {
        public System.Windows.Input.ICommand OpenDialogCommand { get; private set; }

        public ExternalScript() {
            OpenDialogCommand = new GalaSoft.MvvmLight.Command.RelayCommand<object>((object o) => {
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Title = Loc.Instance["Lbl_SequenceItem_Utility_ExternalScript_Name"];
                dialog.FileName = "";
                dialog.DefaultExt = ".*";
                dialog.Filter = "Any executable command |*.*";

                if (dialog.ShowDialog() == true) {
                    Script = "\"" + dialog.FileName + "\"";
                }
            });
        }

        private ExternalScript(ExternalScript cloneMe) : this() {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new ExternalScript(this) {
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
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ProcessedScript));
                RaisePropertyChanged(nameof(ProcessedScriptAnnotated));
            }
        }

        public string ProcessedScriptAnnotated {
            get { return "As processed: " + iProcessedScript; }
            set { }
        }


        private string iProcessedScript;
        public string ProcessedScript {
            get {
                string value = Script;
                RaisePropertyChanged();
                if (value != null) {
                    ProcessedScriptError = null;
                    while (true) {
                        string toReplace = Regex.Match(value, @"\{([^\}]+)\}").Groups[1].Value;
                        if (toReplace.Length == 0) break;
                        Expr ex = new Expr(this, toReplace, "Any");
                        if (ex.Error != null) {
                            ProcessedScriptError = ex.Error;
                            //Logger.Warning("External Script +, error processing script, " + ex.Error);
                            return "Error";
                        } else if (ex.StringValue != null) {
                            value = value.Replace("{" + toReplace + "}", ex.StringValue);
                        } else {
                            value = value.Replace("{" + toReplace + "}", ex.ValueString);
                        }
                        if (Symbol.IsAttachedToRoot(this)) {
                            //Logger.Info("Replacing " + toReplace + " with " + ex.ValueString);
                        }
                    }
                }
                iProcessedScript = value;
                RaisePropertyChanged("ProcessedScriptAnnotated");
                return value;
            }
            set { }
        }

        public string ProcessedScriptError {  get; set; } = null;

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            await Symbol.UpdateSwitchWeatherData();
            Logger.Info("External Script +, script = " + Script + ", processed script = " + ProcessedScript);
            string sequenceCompleteCommand = ProcessedScript;
            ExternalCommandExecutor externalCommandExecutor = new ExternalCommandExecutor(progress);
            var success = await externalCommandExecutor.RunSequenceCompleteCommandTask(sequenceCompleteCommand, token);
            if (success == int.MinValue) {
                throw new SequenceEntityFailedException("External script was unable to run successfully");
            } else {
                // Save the value
                Symbol.LastExitCode = success;
                Logger.Info("External Script +, exit code = " + success);
            }
        }

        public bool Validate() {
            var i = new List<string>();
            var sequenceCompleteCommand = ProcessedScript;
            if (ProcessedScriptError != null) {
                i.Add(ProcessedScriptError);
            } else if (!string.IsNullOrWhiteSpace(sequenceCompleteCommand) && !ExternalCommandExecutor.CommandExists(sequenceCompleteCommand)) {
                i.Add(string.Format("External Command Not Found", ExternalCommandExecutor.GetComandFromString(sequenceCompleteCommand)));
            }
            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ExternalScript)}, Script: {Script} ProcessedScript: {ProcessedScript}";
        }
    }
}

//\{                 # Escaped curly parentheses, means "starts with a '{' character"
//        (          # Parentheses in a regex mean "put (capture) the stuff 
//                   #     in between into the Groups array" 
//           [^}]    # Any character that is not a '}' character
//           *       # Zero or more occurrences of the aforementioned "non '}' char"
//        )          # Close the capturing group
//\}                 # "Ends with a '}' character"