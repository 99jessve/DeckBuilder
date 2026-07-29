namespace DeckDesigner.Export;

using System.Globalization;
using System.Text;
using DeckDesigner.Application.Reporting;

/// <summary>
/// Exports a material report as a human-readable CSV bill of materials.
/// </summary>
public sealed class CsvMaterialReportExporter : IMaterialReportExporter
{
    public string FormatName => "CSV";

    public string BuildCsv(MaterialReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.AppendLine("Item,Value,Unit");
        var ci = CultureInfo.InvariantCulture;
        sb.AppendLine($"Area,{report.AreaSquareMeters.ToString("F2", ci)},m2");
        sb.AppendLine($"Boards,{report.BoardCount},pcs");
        sb.AppendLine($"Board running length,{report.BoardRunningMeters.ToString("F2", ci)},m");
        sb.AppendLine($"Joists,{report.JoistCount},pcs");
        sb.AppendLine($"Beams,{report.BeamCount},pcs");
        sb.AppendLine($"Posts,{report.PostCount},pcs");
        sb.AppendLine($"Stock boards,{report.StockBoardCount},pcs");
        sb.AppendLine($"Waste,{report.WasteMeters.ToString("F2", ci)},m");
        sb.AppendLine($"Screws,{report.ScrewCount},pcs");
        return sb.ToString();
    }

    public async Task ExportAsync(MaterialReport report, string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var csv = BuildCsv(report);
        await File.WriteAllTextAsync(path, csv, cancellationToken).ConfigureAwait(false);
    }
}
