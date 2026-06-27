using Application.Shared.Models;

namespace Application.Shared.Services.Data;

public interface IEmailNotificationService
{
    Task SendDatasetSharedNotificationAsync(string recipientEmail, string datasetId, string datasetName, string companyId, string sharedByUserName, DatasetUserType userType, IReadOnlyCollection<string>? tables = null);
}
