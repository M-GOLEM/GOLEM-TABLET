using System.Xml.Linq;

namespace Golem.Tablet.Core.Firmware;

/// <summary>
/// Inspects a Qualcomm EDL firmware folder by locating the firehose programmer,
/// rawprogram/patch XML descriptors, and the partition labels they declare.
/// </summary>
public static class QualcommFirmwareInspector
{
    public static FirmwareInspection Inspect(
        string directory,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<FirmwareIssue>();
        var entries = new List<string>();

        if (!Directory.Exists(directory))
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Error, "Folder does not exist."));
            return new FirmwareInspection(directory, "Unknown", entries, issues);
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Error, "Cannot read folder: " + ex.Message));
            return new FirmwareInspection(directory, "Unknown", entries, issues);
        }

        var rawPrograms = files
            .Where(f => IsPattern(Path.GetFileName(f), "rawprogram") && HasXmlExtension(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var patches = files
            .Where(f => IsPattern(Path.GetFileName(f), "patch") && HasXmlExtension(f))
            .ToList();
        var programmers = files
            .Where(IsProgrammer)
            .ToList();

        if (rawPrograms.Count == 0)
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Error,
                "No rawprogram*.xml found; this does not look like an EDL firmware folder."));
        }

        foreach (string rawProgram in rawPrograms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                XDocument document = XDocument.Load(rawProgram, LoadOptions.None);
                List<string> labels = document
                    .Descendants("program")
                    .Select(program => (string?)program.Attribute("label") ?? string.Empty)
                    .Where(label => label.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (string label in labels)
                {
                    if (!entries.Contains(label, StringComparer.OrdinalIgnoreCase))
                    {
                        entries.Add(label);
                    }
                }

                issues.Add(new FirmwareIssue(FirmwareSeverity.Info,
                    $"{Path.GetFileName(rawProgram)}: {labels.Count} partition(s) declared."));
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException)
            {
                issues.Add(new FirmwareIssue(FirmwareSeverity.Error,
                    $"{Path.GetFileName(rawProgram)} is not valid XML: {ex.Message}"));
            }
        }

        if (rawPrograms.Count > 0 && patches.Count == 0)
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Info,
                "No patch*.xml present; the package may be read/write only."));
        }

        if (programmers.Count == 0)
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Warning,
                "No firehose programmer found (prog_firehose*.elf/.mbn)."));
        }
        else
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Info,
                "Programmer: " + string.Join(", ", programmers.Select(Path.GetFileName))));
        }

        return new FirmwareInspection(directory, "Qualcomm EDL firmware folder", entries, issues);
    }

    private static bool IsPattern(string fileName, string prefix) =>
        fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool HasXmlExtension(string path) =>
        string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsProgrammer(string path)
    {
        string name = Path.GetFileName(path);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".elf" or ".mbn"))
        {
            return false;
        }

        return name.Contains("firehose", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("prog_", StringComparison.OrdinalIgnoreCase);
    }
}
