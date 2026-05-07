#region "copyright"

/*
    Copyright © 2016 - 2024 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Utility.Extensions;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {

    public class ExternalCommandExecutor {
        private IProgress<ApplicationStatus> progress;

        public ExternalCommandExecutor(IProgress<ApplicationStatus> progress) {
            this.progress = progress;
        }

        public async Task<Int32> RunSequenceCompleteCommandTask(string sequenceCompleteCommand, CancellationToken ct) {
            if (!CommandExists(sequenceCompleteCommand)) {
                Logger.Error($"Command not found: {sequenceCompleteCommand}");
                return int.MinValue;
            }
            try {
                string executableLocation = GetComandFromString(sequenceCompleteCommand);
                string args = GetArgumentsFromString(sequenceCompleteCommand);
                Logger.Info($"Running - {executableLocation}");

                // set environment variable to string equiv of int MinValue before calling the batch script
                SetEnvironmentVariableValue("NINAESRC", int.MinValue.ToString(), EnvironmentVariableTarget.User);

                Process process = new Process();
                process.StartInfo.FileName = executableLocation;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardError = false;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += (object sender, DataReceivedEventArgs e) => {
                    if (!string.IsNullOrWhiteSpace(e.Data)) {
                        StatusUpdate("External Command", e.Data);
                        Logger.Info($"STDOUT: {e.Data}");
                    }
                };
                process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
                    if (!string.IsNullOrWhiteSpace(e.Data)) {
                        StatusUpdate("External Command", e.Data);
                        Logger.Error($"STDERR: {e.Data}");
                    }
                };
                if (args != null)
                    process.StartInfo.Arguments = args;

                SPLogger.Debug($"Starting process '{executableLocation}' with args '{args}'");
                process.Start();
                await process.WaitForExitAsync(ct);

                int ninaesrc = GetEnvironmentVariableValue("NINAESRC", EnvironmentVariableTarget.User);

                // if int MinValue returned, user did not set NINAESRC so the return the std exitcode
                if (ninaesrc == int.MinValue) {
                    return process.ExitCode; ;
                }

                // return the user set value of NINAESRC environment variable
                return ninaesrc;


            } catch (Exception e) {
                Logger.Error($"Error running command {sequenceCompleteCommand}: {e.Message}", e);
            } finally {
                StatusUpdate("ExternalCommand", "");
            }
            return int.MinValue;

        }

        private void StatusUpdate(string src, string data) {
            progress?.Report(new ApplicationStatus() {
                Source = src,
                Status = data,
            });
        }

        public static bool CommandExists(string commandLine) {
            try {
                string cmd = GetComandFromString(commandLine);
                FileInfo fi = new FileInfo(cmd);
                return fi.Exists;
            } catch (Exception e) { Logger.Trace(e.Message); }
            return false;
        }

        public static string GetComandFromString(string commandLine) {
            //if you enclose the command (with spaces) in quotes, then you must remove them
            return @"" + ParseArguments(commandLine)[0].Replace("\"", "").Trim();
        }

        public static string GetArgumentsFromString(string commandLine) {
            string[] args = ParseArguments(commandLine);
            if (args.Length > 1) {
                return string.Join(" ", new List<string>(args).GetRange(1, args.Length - 1).ToArray());
            }
            return null;
        }

        public static string[] ParseArguments(string commandLine) {
            char[] parmChars = commandLine.ToCharArray();
            bool inQuote = false;
            for (int index = 0; index < parmChars.Length; index++) {
                if (parmChars[index] == '"')
                    inQuote = !inQuote;
                if (!inQuote && parmChars[index] == ' ')
                    parmChars[index] = '\n';
            }
            return (new string(parmChars)).Split('\n');
        }

        private static int GetEnvironmentVariableValue(string variableName, EnvironmentVariableTarget target) {
            // Retrieve the value of the environment variable
            string value = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);
            SPLogger.Debug($"ES+ environment variable NINAESRC value '{value}'");
            // Check if the environment variable is not string equiv of int MinValue then return its int value
            if (value != int.MinValue.ToString()) {
                return Int32.Parse(value);
            } else {
                // otherwise return min value
                return int.MinValue;
            }
        }

        private static void SetEnvironmentVariableValue(string variableName, string variableValue, EnvironmentVariableTarget target) {
            // Set the value of the environment variable
            Environment.SetEnvironmentVariable(variableName, variableValue, target);
        }

    }
}