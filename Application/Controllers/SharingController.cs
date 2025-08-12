using Application.Shared.Models;
using Application.Shared.Services.Data;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[Route("api/datasets/{datasetId}/[controller]")]
[ApiController]
public class SharingController : ControllerBase
{
    private readonly IDatasetSharingService _datasetSharingService;

    public SharingController(IDatasetSharingService datasetSharingService)
    {
        _datasetSharingService = datasetSharingService;
    }

    // GET: api/datasets/{datasetId}/sharing
    [HttpGet]
    public async Task<ActionResult<List<DatasetUserDto>>> GetDatasetUsers(string datasetId)
    {
        try
        {
            var users = await _datasetSharingService.GetDatasetUsersAsync(datasetId);
            return Ok(users);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error retrieving dataset users: {ex.Message}");
        }
    }

    // POST: api/datasets/{datasetId}/sharing
    [HttpPost]
    public async Task<ActionResult> ShareDataset(string datasetId, [FromBody] ShareDatasetRequest request)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required");

        if (datasetId != request.DatasetId)
            return BadRequest("Dataset ID mismatch");

        try
        {
            var success = await _datasetSharingService.ShareDatasetAsync(request, userId);
            
            if (!success)
                return BadRequest("Failed to share dataset. User may not exist or dataset not found.");

            return Ok(new { message = "Dataset shared successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error sharing dataset: {ex.Message}");
        }
    }

    // PUT: api/datasets/{datasetId}/sharing/{userId}
    [HttpPut("{userId}")]
    public async Task<ActionResult> UpdateUserAccess(string datasetId, string userId, [FromBody] DatasetUserType userType)
    {
        try
        {
            var success = await _datasetSharingService.UpdateDatasetUserTypeAsync(datasetId, userId, userType);
            
            if (!success)
                return NotFound("Dataset user not found");

            return Ok(new { message = "User access updated successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error updating user access: {ex.Message}");
        }
    }

    // DELETE: api/datasets/{datasetId}/sharing/{userId}
    [HttpDelete("{userId}")]
    public async Task<ActionResult> RemoveUserAccess(string datasetId, string userId)
    {
        try
        {
            var success = await _datasetSharingService.RemoveDatasetUserAsync(datasetId, userId);
            
            if (!success)
                return NotFound("Dataset user not found");

            return Ok(new { message = "User access removed successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error removing user access: {ex.Message}");
        }
    }
}
