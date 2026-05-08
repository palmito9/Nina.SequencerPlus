namespace NINA.Plugin.SequencerPlus.Models {
    public record ConversionProgress(double Progress, int Converted = 0, int Skipped = 0, int ErrorCount = 0, string? Error = null);
}
