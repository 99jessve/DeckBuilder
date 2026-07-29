namespace DeckDesigner.Export;

using DeckDesigner.Application.Reporting;

/// <summary>
/// Exports a material report to a target format.
/// </summary>
public interface IMaterialReportExporter
{
    string FormatName { get; }

    Task ExportAsync(MaterialReport report, string path, CancellationToken cancellationToken = default);
}
