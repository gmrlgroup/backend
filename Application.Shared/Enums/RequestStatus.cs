using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Enums;

public enum RequestStatus
{

    [Description("Pending")]
    Pending,

    [Description("In Progress")]
    InProgress,

    [Description("Completed")]
    Completed,

    [Description("Rejected")]
    Rejected
}
