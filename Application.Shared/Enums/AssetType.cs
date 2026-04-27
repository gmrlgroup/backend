using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Enums;

public enum AssetType
{
    [Display(Name = "Server")]
    Server = 1,

    [Display(Name = "Report")]
    Report = 2,

    [Display(Name = "Dataset")]
    Dataset = 3,

    [Display(Name = "Database")]
    Database = 4,

    [Display(Name = "Data Pipeline")]
    DataPipeline = 5,

    [Display(Name = "Table")]
    Table = 6,

    [Display(Name = "Data Job")]
    DataJob = 7,
}
