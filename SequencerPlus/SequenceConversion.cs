using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NINA.Plugin.SequencerPlus.Models;

namespace NINA.Plugin.SequencerPlus {

    public static class SequenceConversion {

        public enum Direction { ToSequencerPlus, ToPowerups }

        private static readonly Regex ToSequencerPlusPattern = new Regex(
            @"WhenPlugin\.When\.([^,""]+),\s*WhenPlugin",
            RegexOptions.Compiled);

        private static readonly Regex ToPowerupsPattern = new Regex(
            @"NINA\.Plugin\.SequencerPlus\.([^,""]+),\s*NINA\.Plugin\.SequencerPlus",
            RegexOptions.Compiled);

        public static bool NeedsConversion(string content, Direction direction) {
            return direction == Direction.ToSequencerPlus
                ? content.Contains("WhenPlugin")
                : content.Contains("NINA.Plugin.SequencerPlus");
        }

        public static string Convert(string content, Direction direction) {
            if (direction == Direction.ToSequencerPlus) {
                return ToSequencerPlusPattern.Replace(content,
                    m => $"NINA.Plugin.SequencerPlus.{m.Groups[1].Value.Trim()}, NINA.Plugin.SequencerPlus");
            } else {
                return ToPowerupsPattern.Replace(content,
                    m => $"WhenPlugin.When.{m.Groups[1].Value.Trim()}, WhenPlugin");
            }
        }

        public static async Task<ConversionResult> ConvertFileAsync(string filePath, Direction direction, IProgress<ConversionProgress>? progress = null) {
            try {
                string content = await File.ReadAllTextAsync(filePath);

                if (!NeedsConversion(content, direction))
                    return new ConversionResult { Skipped = 1 };

                string converted = Convert(content, direction);
                await File.WriteAllTextAsync(filePath, converted);
                return new ConversionResult { Converted = 1 };

            } catch (Exception ex) {
                var errorMessage = $"{Path.GetFileName(filePath)}: {ex.Message}";
                progress?.Report(new ConversionProgress(0, 0, 0, 1, errorMessage));
                var result = new ConversionResult { Errors = 1 };
                result.ErrorList.Add(errorMessage);
                return result;
            }
        }

        public static async Task<ConversionResult> ConvertFilesAsync(
                string[] filePaths,
                Direction direction,
                IProgress<ConversionProgress>? progress = null,
                CancellationToken ct = default) {

            var result = new ConversionResult();

            for (int i = 0; i < filePaths.Length; i++) {
                ct.ThrowIfCancellationRequested();

                var fileResult = await ConvertFileAsync(filePaths[i], direction, progress);
                result.Converted += fileResult.Converted;
                result.Skipped += fileResult.Skipped;
                result.Errors += fileResult.Errors;
                result.ErrorList.AddRange(fileResult.ErrorList);

                progress?.Report(new ConversionProgress(
                    (double)(i + 1) / filePaths.Length * 100.0,
                    result.Converted,
                    result.Skipped,
                    result.Errors));
            }

            return result;
        }

        public static async Task<ConversionResult> ConvertFolderAsync(
                string folderPath,
                bool recursive,
                Direction direction,
                IProgress<ConversionProgress>? progress = null,
                CancellationToken ct = default) {

            var searchOption = recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.GetFiles(folderPath, "*.json", searchOption);
            var result = new ConversionResult();

            for (int i = 0; i < files.Length; i++) {
                ct.ThrowIfCancellationRequested();

                var fileResult = await ConvertFileAsync(files[i], direction, progress);
                result.Converted += fileResult.Converted;
                result.Skipped += fileResult.Skipped;
                result.Errors += fileResult.Errors;
                result.ErrorList.AddRange(fileResult.ErrorList);

                progress?.Report(new ConversionProgress(
                    (double)(i + 1) / files.Length * 100.0,
                    result.Converted,
                    result.Skipped,
                    result.Errors));
            }

            return result;
        }
    }
}
