using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace BuildingPlacementTweaks.Builder;

internal static class Program
{
    private static readonly FieldInfo NamesReferencedFromExportDataCountField =
        typeof(UAsset).GetField(
            "NamesReferencedFromExportDataCount",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "UAssetAPI field NamesReferencedFromExportDataCount was not found.");
    private static readonly FieldInfo NameCountField =
        typeof(UAsset).GetField(
            "NameCount",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "UAssetAPI field NameCount was not found.");

    private static readonly JsonSerializerOptions ConfigJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions OutputJson = new()
    {
        WriteIndented = true
    };

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "list-assets")
            {
                PlacementConfig config = LoadAndValidateConfig(args[1]);
                foreach (string asset in EnumerateEnabledAssets(config))
                {
                    Console.WriteLine(asset);
                }

                return 0;
            }

            if (args.Length == 2 && args[0] == "list-source-assets")
            {
                PlacementConfig config = LoadAndValidateConfig(args[1]);
                foreach (string asset in EnumerateSourceAssets(config))
                {
                    Console.WriteLine(asset);
                }

                return 0;
            }

            if (args.Length == 5 && args[0] == "scan-source")
            {
                ScanSource(args[1], args[2], args[3], args[4]);
                return 0;
            }

            if (args.Length == 4 && args[0] == "stable-check")
            {
                string assetPath = Path.GetFullPath(args[1]);
                var mappings = new Usmap(Path.GetFullPath(args[2]));
                UAsset stable = LoadStableAsset(
                    assetPath,
                    mappings,
                    Path.GetFileNameWithoutExtension(assetPath),
                    args[3]);
                Console.WriteLine(
                    $"stable {args[3]}: {assetPath} (unversioned={stable.HasUnversionedProperties})");
                return 0;
            }

            if (args.Length == 7 && args[0] == "patch")
            {
                Patch(
                    args[1],
                    args[2],
                    args[3],
                    args[4],
                    args[5],
                    args[6]);
                return 0;
            }

            if (args.Length == 7 && args[0] == "verify")
            {
                Verify(
                    args[1],
                    args[2],
                    args[3],
                    args[4],
                    args[5],
                    args[6]);
                return 0;
            }

            Usage();
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    private static void Usage()
    {
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  PlacementBuilder list-assets <config.jsonc>");
        Console.Error.WriteLine("  PlacementBuilder list-source-assets <config.jsonc>");
        Console.Error.WriteLine("  PlacementBuilder scan-source <config.jsonc> <source-dir> <mapping.usmap> <platform>");
        Console.Error.WriteLine("  PlacementBuilder stable-check <asset.uasset> <mapping.usmap> <platform>");
        Console.Error.WriteLine("  PlacementBuilder patch <config.jsonc> <source-dir> <output-dir> <mapping.usmap> <platform> <manifest.json>");
        Console.Error.WriteLine("  PlacementBuilder verify <config.jsonc> <patched-dir> <schema-source-dir> <mapping.usmap> <platform> <report.json>");
    }

    private static PlacementConfig LoadAndValidateConfig(string path)
    {
        string fullPath = Path.GetFullPath(path);
        PlacementConfig? config = JsonSerializer.Deserialize<PlacementConfig>(
            File.ReadAllText(fullPath),
            ConfigJson);
        if (config is null)
        {
            throw new InvalidOperationException($"Configuration could not be read: {fullPath}");
        }

        if (config.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported schemaVersion {config.SchemaVersion}; expected 1.");
        }

        var groupNames = new HashSet<string>(StringComparer.Ordinal);
        var assetOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (SchemaDependency dependency in config.SchemaDependencies)
        {
            ValidateAssetPath(dependency.Asset, "schemaDependencies");
            if (dependency.ForGroups.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Schema dependency '{dependency.Asset}' has no forGroups entries.");
            }
            if (dependency.ForPlatforms.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Schema dependency '{dependency.Asset}' has no forPlatforms entries.");
            }
        }

        if (config.SchemaDependencies.Count
            != config.SchemaDependencies.Select(dependency => dependency.Asset).Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException("schemaDependencies contains duplicate assets.");
        }

        foreach (PlacementGroup group in config.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.Name) || !groupNames.Add(group.Name))
            {
                throw new InvalidOperationException($"Group name is missing or duplicated: '{group.Name}'.");
            }

            if (!group.Enabled)
            {
                continue;
            }

            if (group.Assets.Count == 0)
            {
                throw new InvalidOperationException($"Enabled group '{group.Name}' contains no assets.");
            }

            foreach (string asset in group.Assets)
            {
                ValidateAssetPath(asset, group.Name);
                if (!assetOwners.TryAdd(asset, group.Name))
                {
                    throw new InvalidOperationException(
                        $"Asset '{asset}' is duplicated by groups '{assetOwners[asset]}' and '{group.Name}'.");
                }
            }

            if (group.Settings.InstallStrategy is { Length: 0 })
            {
                throw new InvalidOperationException($"Empty installStrategy in group '{group.Name}'.");
            }

            foreach (AdvancedProperty property in group.AdvancedProperties)
            {
                ValidateAdvancedProperty(group.Name, property);
            }

            foreach (DataTableRowProperty property in group.DataTableRows)
            {
                ValidateDataTableRowProperty(group.Name, property);
            }
        }

        foreach (SchemaDependency dependency in config.SchemaDependencies)
        {
            foreach (string groupName in dependency.ForGroups)
            {
                if (!groupNames.Contains(groupName))
                {
                    throw new InvalidOperationException(
                        $"Schema dependency '{dependency.Asset}' references unknown group '{groupName}'.");
                }
            }
        }

        return config;
    }

    private static void ValidateAssetPath(string asset, string group)
    {
        if (string.IsNullOrWhiteSpace(asset)
            || asset.StartsWith("/", StringComparison.Ordinal)
            || asset.Contains("..", StringComparison.Ordinal)
            || asset.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
            || asset.EndsWith(".uexp", StringComparison.OrdinalIgnoreCase)
            || !asset.StartsWith("Pal/Content/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Invalid asset path in group '{group}': '{asset}'. Expected a Pal/Content/... base path without an extension.");
        }
    }

    private static void ValidateAdvancedProperty(string group, AdvancedProperty property)
    {
        if (string.IsNullOrWhiteSpace(property.Export))
        {
            throw new InvalidOperationException($"advancedProperties.export is missing in group '{group}'.");
        }

        if (property.PropertyPath.Count == 0 || property.PropertyPath.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"advancedProperties.propertyPath is missing in group '{group}'.");
        }

        string[] allowedTypes = ["Bool", "Int", "Float", "Enum", "Name"];
        if (!allowedTypes.Contains(property.Type, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported type '{property.Type}' in group '{group}'.");
        }
    }

    private static void ValidateDataTableRowProperty(
        string group,
        DataTableRowProperty property)
    {
        if (string.IsNullOrWhiteSpace(property.Row))
        {
            throw new InvalidOperationException($"dataTableRows.row is missing in group '{group}'.");
        }

        if (property.PropertyPath.Count == 0 || property.PropertyPath.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"dataTableRows.propertyPath is missing in group '{group}'.");
        }

        string[] allowedTypes = ["Bool", "Int", "Float", "Enum", "Name"];
        if (!allowedTypes.Contains(property.Type, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported DataTable type '{property.Type}' in group '{group}'.");
        }
    }

    private static IEnumerable<string> EnumerateEnabledAssets(PlacementConfig config)
    {
        return config.Groups
            .Where(group => group.Enabled)
            .SelectMany(group => group.Assets)
            .OrderBy(asset => asset, StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateSourceAssets(PlacementConfig config)
    {
        return EnumerateEnabledAssets(config)
            .Concat(config.SchemaDependencies.Select(dependency => dependency.Asset))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(asset => asset, StringComparer.Ordinal);
    }

    private static IEnumerable<(PlacementGroup Group, string Asset)> EnumerateEnabledEntries(
        PlacementConfig config)
    {
        return config.Groups
            .Where(group => group.Enabled)
            .SelectMany(group => group.Assets.Select(asset => (group, asset)))
            .OrderBy(entry => entry.asset, StringComparer.Ordinal);
    }

    private static void ScanSource(
        string configPath,
        string sourceDirectory,
        string mappingPath,
        string platform)
    {
        PlacementConfig config = LoadAndValidateConfig(configPath);
        string sourceRoot = Path.GetFullPath(sourceDirectory);
        string mappingsFullPath = Path.GetFullPath(mappingPath);
        foreach ((PlacementGroup group, string assetPath) in EnumerateEnabledEntries(config))
        {
            var mappings = new Usmap(mappingsFullPath);
            PreloadSchemas(config, group, sourceRoot, mappings, platform);
            string inputPath = ResolveAsset(sourceRoot, assetPath);
            RequireAssetPair(inputPath);
            var asset = new UAsset(inputPath, EngineVersion.VER_UE5_1, mappings);
            string cdoName = $"Default__{Path.GetFileNameWithoutExtension(inputPath)}_C";
            Export? cdo = asset.Exports.SingleOrDefault(
                export => string.Equals(export.ObjectName.ToString(), cdoName, StringComparison.Ordinal));
            string superName = asset.Exports
                .OfType<ClassExport>()
                .Single()
                .SuperStruct
                .ToImport(asset)
                .ObjectName
                .ToString();
            Console.WriteLine($"{assetPath}\t{cdo?.GetType().Name ?? "<missing>"}\t{superName}");
        }
    }

    private static void Patch(
        string configPath,
        string sourceDirectory,
        string outputDirectory,
        string mappingPath,
        string platform,
        string manifestPath)
    {
        PlacementConfig config = LoadAndValidateConfig(configPath);
        string sourceRoot = Path.GetFullPath(sourceDirectory);
        string outputRoot = Path.GetFullPath(outputDirectory);
        string mappingsFullPath = Path.GetFullPath(mappingPath);
        Directory.CreateDirectory(outputRoot);

        var assetAudits = new List<AssetAudit>();

        foreach ((PlacementGroup group, string assetPath) in EnumerateEnabledEntries(config))
        {
            var mappings = new Usmap(mappingsFullPath);
            PreloadSchemas(config, group, sourceRoot, mappings, platform);
            string inputPath = ResolveAsset(sourceRoot, assetPath);
            RequireAssetPair(inputPath);
            UAsset asset = LoadStableAsset(inputPath, mappings, assetPath, platform);

            var changes = new List<ChangeAudit>();
            ApplyGroup(asset, group, changes);
            asset.ResolveAncestries();

            string outputPath = ResolveAsset(outputRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            asset.Write(outputPath);

            UAsset written = LoadStableAsset(outputPath, mappings, assetPath, platform);

            VerifyGroup(written, group);
            assetAudits.Add(new AssetAudit(assetPath, group.Name, changes));
            Console.WriteLine($"patched {platform}: {assetPath} ({changes.Count} changes)");
        }

        var manifest = new BuildManifest(
            1,
            platform,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.GetFullPath(configPath)))).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mappingsFullPath))).ToLowerInvariant(),
            assetAudits.Count,
            assetAudits.Sum(asset => asset.Changes.Count),
            assetAudits);
        WriteJson(manifestPath, manifest);
    }

    private static void Verify(
        string configPath,
        string patchedDirectory,
        string schemaSourceDirectory,
        string mappingPath,
        string platform,
        string reportPath)
    {
        PlacementConfig config = LoadAndValidateConfig(configPath);
        string patchedRoot = Path.GetFullPath(patchedDirectory);
        string schemaSourceRoot = Path.GetFullPath(schemaSourceDirectory);
        string mappingsFullPath = Path.GetFullPath(mappingPath);
        var verified = new List<string>();

        foreach ((PlacementGroup group, string assetPath) in EnumerateEnabledEntries(config))
        {
            var mappings = new Usmap(mappingsFullPath);
            PreloadSchemas(config, group, schemaSourceRoot, mappings, platform);
            string inputPath = ResolveAsset(patchedRoot, assetPath);
            RequireAssetPair(inputPath);
            UAsset asset = LoadStableAsset(inputPath, mappings, assetPath, platform);

            VerifyGroup(asset, group);
            verified.Add(assetPath);
            Console.WriteLine($"verified {platform}: {assetPath}");
        }

        var report = new VerificationReport(
            1,
            platform,
            verified.Count,
            verified);
        WriteJson(reportPath, report);
    }

    private static void PreloadSchemas(
        PlacementConfig config,
        PlacementGroup group,
        string sourceRoot,
        Usmap mappings,
        string platform)
    {
        foreach (SchemaDependency dependency in config.SchemaDependencies
                     .Where(dependency =>
                         dependency.ForGroups.Contains(group.Name, StringComparer.Ordinal)
                         && dependency.ForPlatforms.Contains(platform, StringComparer.Ordinal)))
        {
            string dependencyPath = ResolveAsset(sourceRoot, dependency.Asset);
            RequireAssetPair(dependencyPath);
            var loaded = new UAsset(dependencyPath, EngineVersion.VER_UE5_1, mappings);
            if (!loaded.VerifyBinaryEquality())
            {
                throw new InvalidOperationException(
                    $"Schema dependency is not binary-stable in a no-op round trip: {dependency.Asset} ({platform}).");
            }

        }
    }

    private static UAsset LoadStableAsset(
        string path,
        Usmap mappings,
        string assetPath,
        string platform)
    {
        var mapped = new UAsset(path, EngineVersion.VER_UE5_1, mappings);
        if (mapped.HasUnversionedProperties)
        {
            if (mapped.VerifyBinaryEquality())
            {
                return mapped;
            }
        }
        else
        {
            var tagged = new UAsset(path, EngineVersion.VER_UE5_1);
            NormalizeMappedNameMap(mapped, tagged);
            mapped.Mappings = null;
            if (mapped.VerifyBinaryEquality())
            {
                return mapped;
            }

            if (tagged.VerifyBinaryEquality())
            {
                return tagged;
            }
        }

        throw new InvalidOperationException(
            $"No-op round trip is not binary-identical: {assetPath} ({platform}).");
    }

    private static void NormalizeMappedNameMap(UAsset mapped, UAsset original)
    {
        int originalNameCount = (int)(
            NameCountField.GetValue(original)
            ?? throw new InvalidOperationException("Original NameCount value is missing."));
        FString[] originalNames = original
            .GetNameMapIndexList()
            .Take(originalNameCount)
            .Select(name => new FString(name.Value))
            .ToArray();
        int originalReferencedCount = (int)(
            NamesReferencedFromExportDataCountField.GetValue(original)
            ?? throw new InvalidOperationException(
                "Original NamesReferencedFromExportDataCount value is missing."));

        mapped.ClearNameIndexList();
        foreach (FString name in originalNames)
        {
            mapped.AddNameReference(name, forceAddDuplicates: true, skipFixes: true);
        }

        NameCountField.SetValue(mapped, originalNameCount);
        NamesReferencedFromExportDataCountField.SetValue(mapped, originalReferencedCount);
    }

    private static void ApplyGroup(
        UAsset asset,
        PlacementGroup group,
        List<ChangeAudit> changes)
    {
        if (group.Settings.AllowOverlap is bool allowOverlap)
        {
            NormalExport cdo = FindExport(asset, "$CDO");
            ApplyProperty(
                asset,
                cdo,
                "$CDO",
                ["bSpawnableIfOverlapped"],
                "Bool",
                JsonSerializer.SerializeToElement(allowOverlap),
                true,
                changes);
        }

        if (group.Settings.InstallStrategy is string installStrategy)
        {
            NormalExport cdo = FindExport(asset, "$CDO");
            ApplyProperty(
                asset,
                cdo,
                "$CDO",
                ["InstallStrategy"],
                "Enum",
                JsonSerializer.SerializeToElement(installStrategy),
                false,
                changes);
        }

        foreach (AdvancedProperty property in group.AdvancedProperties)
        {
            NormalExport target = FindExport(asset, property.Export);
            ApplyProperty(
                asset,
                target,
                property.Export,
                property.PropertyPath,
                property.Type,
                property.Value,
                property.CreateIfMissing,
                changes);
        }

        foreach (DataTableRowProperty property in group.DataTableRows)
        {
            StructPropertyData row = FindDataTableRow(asset, property.Row);
            ApplyDataTableProperty(asset, row, property, changes);
        }
    }

    private static void VerifyGroup(UAsset asset, PlacementGroup group)
    {
        if (group.Settings.AllowOverlap is bool allowOverlap)
        {
            NormalExport cdo = FindExport(asset, "$CDO");
            VerifyProperty(
                asset,
                cdo,
                "$CDO",
                ["bSpawnableIfOverlapped"],
                "Bool",
                JsonSerializer.SerializeToElement(allowOverlap));
        }

        if (group.Settings.InstallStrategy is string installStrategy)
        {
            NormalExport cdo = FindExport(asset, "$CDO");
            VerifyProperty(
                asset,
                cdo,
                "$CDO",
                ["InstallStrategy"],
                "Enum",
                JsonSerializer.SerializeToElement(installStrategy));
        }

        foreach (AdvancedProperty property in group.AdvancedProperties)
        {
            VerifyProperty(
                asset,
                FindExport(asset, property.Export),
                property.Export,
                property.PropertyPath,
                property.Type,
                property.Value);
        }

        foreach (DataTableRowProperty property in group.DataTableRows)
        {
            VerifyDataTableProperty(
                asset,
                FindDataTableRow(asset, property.Row),
                property);
        }
    }

    private static StructPropertyData FindDataTableRow(UAsset asset, string rowName)
    {
        DataTableExport[] tables = asset.Exports.OfType<DataTableExport>().ToArray();
        if (tables.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one DataTable export, found {tables.Length} in {asset.FilePath}.");
        }

        StructPropertyData[] matches = tables[0].Table.Data
            .Where(row => string.Equals(row.Name.ToString(), rowName, StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"DataTable row '{rowName}' is missing from {asset.FilePath}."),
            _ => throw new InvalidOperationException(
                $"DataTable row '{rowName}' is not unique in {asset.FilePath}.")
        };
    }

    private static void ApplyDataTableProperty(
        UAsset asset,
        StructPropertyData row,
        DataTableRowProperty configured,
        List<ChangeAudit> changes)
    {
        PropertyData? property = ResolveProperty(row, configured.PropertyPath);
        string rowLabel = $"$Row:{configured.Row}";
        if (property is null)
        {
            throw new InvalidOperationException(
                $"Property '{string.Join(".", configured.PropertyPath)}' is missing from DataTable row "
                + $"'{configured.Row}' ({asset.FilePath}).");
        }

        string before = FormatValue(property);
        if (configured.ExpectedValue.ValueKind != JsonValueKind.Undefined)
        {
            string expectedBefore = ExpectedValue(
                asset,
                property,
                configured.Type,
                configured.ExpectedValue);
            if (!string.Equals(expectedBefore, before, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected source value: {rowLabel}."
                    + $"{string.Join(".", configured.PropertyPath)} expected '{expectedBefore}', "
                    + $"found '{before}' ({asset.FilePath}).");
            }
        }

        SetPropertyValue(asset, property, configured.Type, configured.Value);
        string after = FormatValue(property);
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Configured patch makes no change: {rowLabel}."
                + $"{string.Join(".", configured.PropertyPath)} = {after} ({asset.FilePath}).");
        }

        changes.Add(new ChangeAudit(
            rowLabel,
            string.Join(".", configured.PropertyPath),
            configured.Type,
            before,
            after));
    }

    private static void VerifyDataTableProperty(
        UAsset asset,
        StructPropertyData row,
        DataTableRowProperty configured)
    {
        PropertyData? property = ResolveProperty(row, configured.PropertyPath);
        string rowLabel = $"$Row:{configured.Row}";
        if (property is null)
        {
            throw new InvalidOperationException(
                $"Property is missing after patch: {rowLabel}."
                + $"{string.Join(".", configured.PropertyPath)} ({asset.FilePath}).");
        }

        string expected = ExpectedValue(asset, property, configured.Type, configured.Value);
        string actual = FormatValue(property);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Incorrect value after patch: {rowLabel}."
                + $"{string.Join(".", configured.PropertyPath)} expected '{expected}', "
                + $"found '{actual}' ({asset.FilePath}).");
        }
    }

    private static NormalExport FindExport(UAsset asset, string requested)
    {
        string target = requested;
        if (requested == "$CDO")
        {
            string assetName = Path.GetFileNameWithoutExtension(asset.FilePath);
            target = $"Default__{assetName}_C";
        }

        NormalExport[] matches = asset.Exports
            .OfType<NormalExport>()
            .Where(export => string.Equals(export.ObjectName.ToString(), target, StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"Export '{target}' is missing from {asset.FilePath}."),
            _ => throw new InvalidOperationException(
                $"Export '{target}' is not unique in {asset.FilePath}.")
        };
    }

    private static void ApplyProperty(
        UAsset asset,
        NormalExport target,
        string exportName,
        IReadOnlyList<string> path,
        string type,
        JsonElement desired,
        bool createIfMissing,
        List<ChangeAudit> changes)
    {
        PropertyData? property = ResolveProperty(target, path);
        string before;
        if (property is null)
        {
            if (!createIfMissing || path.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Property '{string.Join(".", path)}' is missing from export '{exportName}' ({asset.FilePath}).");
            }

            property = CreateProperty(asset, path[0], type, desired);
            target[path[0]] = property;
            before = "<missing>";
        }
        else
        {
            before = FormatValue(property);
            SetPropertyValue(asset, property, type, desired);
        }

        string after = FormatValue(property);
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Configured patch makes no change: {exportName}.{string.Join(".", path)} = {after} ({asset.FilePath}).");
        }

        changes.Add(new ChangeAudit(
            exportName,
            string.Join(".", path),
            type,
            before,
            after));
    }

    private static void VerifyProperty(
        UAsset asset,
        NormalExport target,
        string exportName,
        IReadOnlyList<string> path,
        string type,
        JsonElement desired)
    {
        PropertyData? property = ResolveProperty(target, path);
        if (property is null)
        {
            throw new InvalidOperationException(
                $"Property is missing after patch: {exportName}.{string.Join(".", path)} ({asset.FilePath}).");
        }

        string expected = ExpectedValue(asset, property, type, desired);
        string actual = FormatValue(property);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Incorrect value after patch: {exportName}.{string.Join(".", path)} expected '{expected}', found '{actual}' ({asset.FilePath}).");
        }
    }

    private static PropertyData? ResolveProperty(
        NormalExport target,
        IReadOnlyList<string> path)
    {
        PropertyData? current = target[path[0]];
        for (int index = 1; index < path.Count && current is not null; index++)
        {
            if (current is not StructPropertyData structure)
            {
                throw new InvalidOperationException(
                    $"'{string.Join(".", path.Take(index))}' is not a struct property.");
            }

            current = structure[path[index]];
        }

        return current;
    }

    private static PropertyData? ResolveProperty(
        StructPropertyData target,
        IReadOnlyList<string> path)
    {
        PropertyData? current = target[path[0]];
        for (int index = 1; index < path.Count && current is not null; index++)
        {
            if (current is not StructPropertyData structure)
            {
                throw new InvalidOperationException(
                    $"'{string.Join(".", path.Take(index))}' is not a struct property.");
            }

            current = structure[path[index]];
        }

        return current;
    }

    private static PropertyData CreateProperty(
        UAsset asset,
        string name,
        string type,
        JsonElement desired)
    {
        FName propertyName = FName.FromString(asset, name);
        PropertyData property = type switch
        {
            "Bool" => new BoolPropertyData(propertyName)
            {
                Value = RequireBool(desired),
                IsZero = false
            },
            "Int" => new IntPropertyData(propertyName)
            {
                Value = RequireInt(desired),
                IsZero = false
            },
            "Float" => new FloatPropertyData(propertyName)
            {
                Value = RequireFloat(desired),
                IsZero = false
            },
            _ => throw new InvalidOperationException(
                $"Type '{type}' cannot be created automatically.")
        };
        return property;
    }

    private static void SetPropertyValue(
        UAsset asset,
        PropertyData property,
        string type,
        JsonElement desired)
    {
        switch (type)
        {
            case "Bool" when property is BoolPropertyData boolean:
                boolean.Value = RequireBool(desired);
                boolean.IsZero = asset.HasUnversionedProperties && !boolean.Value;
                return;
            case "Int" when property is IntPropertyData integer:
                integer.Value = RequireInt(desired);
                integer.IsZero = asset.HasUnversionedProperties && integer.Value == 0;
                return;
            case "Float" when property is FloatPropertyData number:
                number.Value = RequireFloat(desired);
                number.IsZero = asset.HasUnversionedProperties && number.Value == 0;
                return;
            case "Name" when property is NamePropertyData name:
                name.Value = FName.FromString(asset, RequireString(desired));
                name.IsZero = false;
                return;
            case "Enum" when property is EnumPropertyData enumeration:
            {
                string shortValue = RequireString(desired);
                string serializedValue = asset.HasUnversionedProperties
                    ? shortValue
                    : shortValue.Contains("::", StringComparison.Ordinal)
                        ? shortValue
                        : $"{enumeration.EnumType}::{shortValue}";
                enumeration.Value = asset.HasUnversionedProperties
                    ? FName.DefineDummy(asset, serializedValue)
                    : FName.FromString(asset, serializedValue);
                enumeration.IsZero = false;
                return;
            }
            default:
                throw new InvalidOperationException(
                    $"Property type mismatch: expected {type}, found {property.GetType().Name}.");
        }
    }

    private static string ExpectedValue(
        UAsset asset,
        PropertyData property,
        string type,
        JsonElement desired)
    {
        return type switch
        {
            "Bool" => RequireBool(desired).ToString(),
            "Int" => RequireInt(desired).ToString(CultureInfo.InvariantCulture),
            "Float" => RequireFloat(desired).ToString("R", CultureInfo.InvariantCulture),
            "Name" => RequireString(desired),
            "Enum" when property is EnumPropertyData enumeration =>
                asset.HasUnversionedProperties
                    ? RequireString(desired)
                    : RequireString(desired).Contains("::", StringComparison.Ordinal)
                        ? RequireString(desired)
                        : $"{enumeration.EnumType}::{RequireString(desired)}",
            _ => throw new InvalidOperationException($"Unsupported type '{type}'.")
        };
    }

    private static string FormatValue(PropertyData property)
    {
        return property switch
        {
            BoolPropertyData boolean => boolean.Value.ToString(),
            IntPropertyData integer => integer.Value.ToString(CultureInfo.InvariantCulture),
            FloatPropertyData number => number.Value.ToString("R", CultureInfo.InvariantCulture),
            NamePropertyData name => name.Value?.ToString() ?? "<null>",
            EnumPropertyData enumeration => enumeration.Value?.ToString() ?? "<null>",
            _ => throw new InvalidOperationException(
                $"Unsupported property type {property.GetType().Name}.")
        };
    }

    private static bool RequireBool(JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidOperationException("Expected a Boolean value.");
        }

        return value.GetBoolean();
    }

    private static int RequireInt(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
        {
            throw new InvalidOperationException("Expected a 32-bit integer.");
        }

        return result;
    }

    private static float RequireFloat(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out float result))
        {
            throw new InvalidOperationException("Expected a floating-point value.");
        }

        return result;
    }

    private static string RequireString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not string result)
        {
            throw new InvalidOperationException("Expected a string value.");
        }

        return result;
    }

    private static string ResolveAsset(string root, string assetPath)
    {
        string resolved = Path.GetFullPath(
            Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar) + ".uasset"));
        string rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Asset path escapes the working directory: {assetPath}");
        }

        return resolved;
    }

    private static void RequireAssetPair(string uassetPath)
    {
        string uexpPath = Path.ChangeExtension(uassetPath, ".uexp");
        if (!File.Exists(uassetPath) || !File.Exists(uexpPath))
        {
            throw new FileNotFoundException(
                $"Asset pair is missing: {uassetPath} / {uexpPath}");
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(value, OutputJson) + Environment.NewLine);
    }
}
