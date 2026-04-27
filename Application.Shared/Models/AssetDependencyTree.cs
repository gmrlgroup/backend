using System.ComponentModel.DataAnnotations.Schema;
using Application.Shared.Enums;

namespace Application.Shared.Models;

[Table("dependency_tree")]
public class AssetDependencyTree
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public AssetType EntityType { get; set; }
    public AssetStatus CurrentStatus { get; set; }
    public List<AssetDependencyTreeNode> Dependencies { get; set; } = new();
    public List<AssetDependencyTreeNode> Dependents { get; set; } = new();
}

[Table("dependency_tree_node")]
public class AssetDependencyTreeNode
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public AssetType EntityType { get; set; }
    public AssetStatus CurrentStatus { get; set; }
    public bool IsCritical { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public int Level { get; set; } = 0;
    public List<AssetDependencyTreeNode> Children { get; set; } = new();
}
