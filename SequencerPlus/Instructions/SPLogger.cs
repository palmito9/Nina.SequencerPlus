using ASCOM.Common.Interfaces;
using NINA.Core.Utility;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.SequencerPlus
{
    public class SPLogger {

        public enum Level {
            INFO,
            DEBUG,
            ERROR
        }

        public static Level LogLevel = Level.INFO;

        private const string MessageTemplate = "{source}|{member}|{line}|{message}";
        private static FieldInfo LevelSwitchField = null;

        public static void Debug(string message,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int lineNumber = 0) {

            if (LevelSwitchField == null) {
                LevelSwitchField = typeof(NINA.Core.Utility.Logger).GetField("levelSwitch", BindingFlags.NonPublic | BindingFlags.Static);
            }

            if (LevelSwitchField != null) {
                LoggingLevelSwitch foo = (LoggingLevelSwitch)LevelSwitchField.GetValue(null);
                if (foo.MinimumLevel == LogEventLevel.Debug) {
                    Serilog.Log.Debug(MessageTemplate, Path.GetFileName(sourceFilePath), memberName, lineNumber, message);
                    return;
                }
            }

            if (LogLevel == Level.DEBUG) {
                NINA.Core.Utility.Logger.Info(message + " from " + memberName + " [" + Path.GetFileName(sourceFilePath) + ":" + lineNumber + "]");
            }
        }

        public static void Warn (string msg) {
        
        }

        public static void Error (string msg) {
        
        }

    }
}
