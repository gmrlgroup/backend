using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Enums;



// Enum to define request types
public enum RequestType
{
    // raw data
    [Description("Raw Data")]
    RawData,

    [Description("New Report")]
    NewReport,

    [Description("Report Modification")]
    ReportModification,

    [Description("Report Issue")]
    ReportIssue,

    [Description("Report Deletion")]
    Other
}
