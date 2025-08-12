using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;



[ApiController]
[Route("api/[controller]")]
public class FileController : Controller
{
    [HttpPost("upload-csv")]
    public async Task<IActionResult> UploadCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        // You can now parse the CSV content, for example:
        var lines = content.Split('\n');

        return Ok(new { lineCount = lines.Length });
    }



}
