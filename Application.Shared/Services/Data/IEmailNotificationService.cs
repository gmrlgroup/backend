using Application.Shared.Models;

namespace Application.Shared.Services.Data;

public interface IEmailNotificationService
{
    Task SendDatasetSharedNotificationAsync(string recipientEmail, string datasetName, string sharedByUserName, DatasetUserType userType);
}
