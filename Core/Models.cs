using System;
using System.Collections.Generic;
namespace BimComparison.Core
{
    public sealed class ComparisonSettings
    {
        public List<int> Categories { get; set; } = new List<int>();
        public bool Parameters { get; set; } = true;
        public bool Location { get; set; } = true;
        public bool Geometry { get; set; }
        public bool Mep { get; set; }
        public bool Connectors { get; set; }
        public string GeometryMode { get; set; } = "Detailed";
        public double AreaToleranceSquareFeet { get; set; } = 1e-5;
        public double VolumeToleranceCubicFeet { get; set; } = 1e-6;
        public double LengthToleranceFeet { get; set; } = 0.001 / 0.3048;
        public double NumberTolerance { get; set; } = 1e-8;
        public double AngleToleranceRadians { get; set; } = 1e-6;
    }
    public sealed class ModelSnapshot
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string Scope { get; set; } = "Phase1-Identity-Parameters-Location";
        public string ModelName { get; set; }
        public string ModelPath { get; set; }
        public string ProjectUniqueId { get; set; }
        public string RevitVersion { get; set; }
        public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;
        public ComparisonSettings Settings { get; set; } = new ComparisonSettings();
        public List<ElementSnapshot> Elements { get; set; } = new List<ElementSnapshot>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> ExtractionErrors { get; set; } = new List<string>();
    }
    public sealed class ElementSnapshot
    {
        public string UniqueId { get; set; }
        public int ElementId { get; set; }
        public int CategoryId { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string TypeUniqueId { get; set; }
        public string Workset { get; set; }
        public string DesignOption { get; set; }
        public string CreatedPhase { get; set; }
        public string DemolishedPhase { get; set; }
        public List<ParameterSnapshot> Parameters { get; set; } = new List<ParameterSnapshot>();
        public LocationSnapshot Location { get; set; }
        public GeometrySnapshot Geometry { get; set; }
        public MepSnapshot Mep { get; set; }
        public ConnectorSnapshot Connectors { get; set; }
    }
    public sealed class GeometrySnapshot
    {
        public string Status { get; set; }
        public string Mode { get; set; }
        public string Algorithm { get; set; } = "world-triangles-fine-v1";
        public string Fingerprint { get; set; }
        public PointData Minimum { get; set; }
        public PointData Maximum { get; set; }
        public PointData Center { get; set; }
        public string CenterMethod { get; set; } = "BoundsMidpoint";
        public double Volume { get; set; }
        public double SurfaceArea { get; set; }
        public double CurveLength { get; set; }
        public int SolidCount { get; set; }
        public int MeshCount { get; set; }
        public int CurveCount { get; set; }
        public List<TriangleData> Triangles { get; set; } = new List<TriangleData>();
        public List<List<PointData>> Curves { get; set; } = new List<List<PointData>>();
        public List<string> Errors { get; set; } = new List<string>();
    }
    public sealed class TriangleData { public PointData A {get;set;} public PointData B {get;set;} public PointData C {get;set;} }
    public sealed class MepSnapshot
    {
        public string Status {get;set;}
        public List<SystemSnapshot> Systems {get;set;} = new List<SystemSnapshot>();
        public Dictionary<string,double> Dimensions {get;set;} = new Dictionary<string,double>();
        public List<string> Errors {get;set;} = new List<string>();
    }
    public sealed class SystemSnapshot
    {
        public string UniqueId {get;set;}
        public string Name {get;set;}
        public string TypeUniqueId {get;set;}
        public string TypeName {get;set;}
    }
    public sealed class ConnectorSnapshot
    {
        public string Status {get;set;}
        public List<ConnectorData> Items {get;set;} = new List<ConnectorData>();
        public List<string> Errors {get;set;} = new List<string>();
    }
    public sealed class ConnectorData
    {
        public string Domain {get;set;}
        public string Type {get;set;}
        public string Shape {get;set;}
        public PointData Origin {get;set;}
        public PointData Direction {get;set;}
        public double? Radius {get;set;}
        public double? Width {get;set;}
        public double? Height {get;set;}
        public List<string> References {get;set;} = new List<string>();
        public List<ConnectedPortReference> ConnectedPorts {get;set;} = new List<ConnectedPortReference>();
    }
    public sealed class ConnectedPortReference
    {
        public string OwnerUniqueId {get;set;}
        public string Domain {get;set;}
        public string Type {get;set;}
        public PointData Origin {get;set;}
    }
    public sealed class ParameterSnapshot
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string BuiltInId { get; set; }
        public string Guid { get; set; }
        public string StorageType { get; set; }
        public bool HasValue { get; set; }
        public string RawValue { get; set; }
        public string DisplayValue { get; set; }
        public string Spec { get; set; }
        public string Unit { get; set; }
        public bool IsReadOnly { get; set; }
    }
    public sealed class PointData { public double X { get; set; } public double Y { get; set; } public double Z { get; set; } }
    public sealed class LocationSnapshot
    {
        public string Kind { get; set; }
        public PointData Point { get; set; }
        public PointData Start { get; set; }
        public PointData End { get; set; }
        public double? Rotation { get; set; }
    }
    public sealed class PropertyChange
    {
        public string ChangeType { get; set; }
        public string Property { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Unit { get; set; }
    }
    public sealed class ElementResult
    {
        public string Status { get; set; }
        public ElementSnapshot Element { get; set; }
        public List<PropertyChange> Changes { get; set; } = new List<PropertyChange>();
    }
    public sealed class ComparisonReport
    {
        public string ComparisonId { get; set; } = System.Guid.NewGuid().ToString();
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string ModelA { get; set; }
        public string ModelB { get; set; }
        public string ProjectA { get; set; }
        public string ProjectB { get; set; }
        public ComparisonSettings Settings { get; set; }
        public int TotalA { get; set; }
        public int TotalB { get; set; }
        public List<ElementResult> AddedElements { get; set; } = new List<ElementResult>();
        public List<ElementResult> DeletedElements { get; set; } = new List<ElementResult>();
        public List<ElementResult> ModifiedElements { get; set; } = new List<ElementResult>();
        public List<ElementResult> UnchangedElements { get; set; } = new List<ElementResult>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> ExtractionErrors { get; set; } = new List<string>();
    }
}
