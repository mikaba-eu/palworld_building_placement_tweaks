using System.Text.Json;

namespace BuildingPlacementTweaks.Builder;

internal sealed class PlacementConfig
{
    public int SchemaVersion { get; set; }
    public List<string> Notes { get; set; } = [];
    public List<SchemaDependency> SchemaDependencies { get; set; } = [];
    public List<PlacementGroup> Groups { get; set; } = [];
}

internal sealed class SchemaDependency
{
    public string Asset { get; set; } = "";
    public List<string> ForGroups { get; set; } = [];
    public List<string> ForPlatforms { get; set; } = [];
}

internal sealed class PlacementGroup
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<string> Assets { get; set; } = [];
    public CommonSettings Settings { get; set; } = new();
    public List<AdvancedProperty> AdvancedProperties { get; set; } = [];
    public List<DataTableRowProperty> DataTableRows { get; set; } = [];
}

internal sealed class CommonSettings
{
    public bool? AllowOverlap { get; set; }
    public string? InstallStrategy { get; set; }
}

internal sealed class AdvancedProperty
{
    public string Export { get; set; } = "";
    public List<string> PropertyPath { get; set; } = [];
    public string Type { get; set; } = "";
    public JsonElement Value { get; set; }
    public bool CreateIfMissing { get; set; }
    public string Explanation { get; set; } = "";
}

internal sealed class DataTableRowProperty
{
    public string Row { get; set; } = "";
    public List<string> PropertyPath { get; set; } = [];
    public string Type { get; set; } = "";
    public JsonElement ExpectedValue { get; set; }
    public JsonElement Value { get; set; }
    public string Explanation { get; set; } = "";
}
