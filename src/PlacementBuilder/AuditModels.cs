namespace BuildingPlacementTweaks.Builder;

internal sealed record ChangeAudit(
    string Export,
    string PropertyPath,
    string Type,
    string Before,
    string After);

internal sealed record AssetAudit(
    string Asset,
    string Group,
    List<ChangeAudit> Changes);

internal sealed record BuildManifest(
    int SchemaVersion,
    string Platform,
    string ConfigSha256,
    string MappingSha256,
    int AssetCount,
    int ChangeCount,
    List<AssetAudit> Assets);

internal sealed record VerificationReport(
    int SchemaVersion,
    string Platform,
    int AssetCount,
    List<string> Assets);
