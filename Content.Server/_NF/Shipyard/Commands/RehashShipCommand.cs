using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server._NF.Shipyard.Commands;

/// <summary>
/// Admin command for stamping the server's security hash onto legacy ship YAML files
/// that were saved before the hashing system was introduced.
///
/// Usage:
///   ship_rehash Exports/MyShip.yml          — rehash one file
///   ship_rehash Exports/                    — rehash every .yml in that directory (recursive)
/// Paths are always relative to the server's UserData root (./bin/Content.Server/data/UserData).
/// </summary>
[AdminCommand(AdminFlags.Host)] //Change to AdminFlags.Admin if you want any admin to be able to use this command
public sealed class RehashShipCommand : IConsoleCommand
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _resources = default!;

    public string Command => "ship_rehash";
    public string Description => "Stamps the server security hash onto legacy ship YAML files that predate the hashing system.";
    public string Help =>
        "Usage: ship_rehash <path>\n" +
        "  <path> is relative to the server UserData root, e.g. 'Exports/MyShip.yml' or 'Exports/'.\n" +
        "  When a directory is given every .yml file inside it is processed (recursive).\n" +
        "  Files that already carry a valid hash are skipped.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        // Normalise to an absolute ResPath under UserData root: /Exports/MyShip.yml
        var norm = args[0].Replace('\\', '/').Trim('/');
        var resPath = new ResPath("/" + norm);

        // Decide: single .yml file or directory?
        if (resPath.Extension == "yml")
        {
            ProcessFile(shell, resPath);
        }
        else
        {
            ProcessDirectory(shell, resPath);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Directory processing
    // ──────────────────────────────────────────────────────────────────────────────

    private void ProcessDirectory(IConsoleShell shell, ResPath dir)
    {
        if (!_resources.UserData.IsDir(dir))
        {
            shell.WriteError($"'{dir}' is not a directory in UserData.");
            return;
        }

        // Find all .yml files in UserData and filter to those under our target directory
        var dirPrefix = dir.ToString().TrimEnd('/') + "/";
        var (allYml, _) = _resources.UserData.Find("*.yml", recursive: true);
        var ymlFiles = allYml
            .Where(p => p.ToString().StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (ymlFiles.Count == 0)
        {
            shell.WriteLine($"No .yml files found under '{dir}'.");
            return;
        }

        int ok = 0, skipped = 0, failed = 0;
        foreach (var filePath in ymlFiles)
        {
            var result = ProcessFile(shell, filePath, silent: true);
            switch (result)
            {
                case FileResult.Rehashed: ok++;      break;
                case FileResult.Skipped:  skipped++; break;
                default:                  failed++;  break;
            }
        }

        shell.WriteLine($"Done. Rehashed: {ok} | Already valid / skipped: {skipped} | Failed: {failed}");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Single-file processing
    // ──────────────────────────────────────────────────────────────────────────────

    private enum FileResult { Rehashed, Skipped, Failed }

    private FileResult ProcessFile(IConsoleShell shell, ResPath path, bool silent = false)
    {
        // 1. Read
        string yaml;
        try
        {
            using var reader = _resources.UserData.OpenText(path);
            yaml = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            shell.WriteError($"Could not read '{path}': {ex.Message}");
            return FileResult.Failed;
        }

        var serverSecret = _cfg.GetCVar(CCVars.UniqueServerHash);

        // 2. Strip any existing hash so we hash only the real content
        var cleanYaml = RemoveHashField(yaml);

        // 3. Compute HMAC-SHA256
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(serverSecret));
        var newHash = BitConverter.ToString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(cleanYaml))
        ).Replace("-", "").ToLowerInvariant();

        // 4. Check whether the file already carries this exact hash (nothing to do)
        var existingHash = ExtractHash(yaml);
        if (!string.IsNullOrWhiteSpace(existingHash) &&
            existingHash.Equals(newHash, StringComparison.OrdinalIgnoreCase))
        {
            if (!silent)
                shell.WriteLine($"'{path}' already has a valid hash — skipped.");
            return FileResult.Skipped;
        }

        // 5. Inject the new hash into the clean YAML
        var finalYaml = InjectHash(cleanYaml, newHash);

        // 6. Write back
        try
        {
            using var writer = _resources.UserData.OpenWriteText(path);
            writer.Write(finalYaml);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Could not write '{path}': {ex.Message}");
            return FileResult.Failed;
        }

        if (!silent)
            shell.WriteLine($"'{path}' → hash {newHash}");
        else
            shell.WriteLine($"Rehashed '{path}'");

        return FileResult.Rehashed;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers  (mirror of ShipyardSystem / ShipyardGridSaveSystem logic)
    // ──────────────────────────────────────────────────────────────────────────────

    private static string ExtractHash(string yaml)
    {
        var match = Regex.Match(yaml, @"securityHash:\s*""?([a-fA-F0-9]+)""?", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : string.Empty;
    }

    private static string RemoveHashField(string yaml)
    {
        return Regex.Replace(
            yaml,
            @"^[ \t]*securityHash:[ \t]*[a-fA-F0-9]+[ \t]*(?:\r\n|\r|\n)?",
            "",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Inserts "  securityHash: {hash}" as the first key inside the meta: block,
    /// matching the exact same insertion point used by ShipyardGridSaveSystem.
    /// </summary>
    private static string InjectHash(string yaml, string hash)
    {
        var metaIdx = yaml.IndexOf("meta:", StringComparison.Ordinal);
        if (metaIdx >= 0)
        {
            // Skip past "meta:" and any trailing whitespace to find the first real content line
            var idx = metaIdx + 5;
            while (idx < yaml.Length &&
                   (yaml[idx] == '\r' || yaml[idx] == '\n' || yaml[idx] == ' ' || yaml[idx] == '\t'))
                idx++;

            // Advance to the end of that first content line (find the \n that terminates it)
            while (idx < yaml.Length && yaml[idx - 1] != '\n')
                idx++;

            return yaml.Insert(idx, $"  securityHash: {hash}\n");
        }

        // No meta: section at all — prepend one
        return $"meta:\n  securityHash: {hash}\n{yaml}";
    }
}
