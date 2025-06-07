using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

[ApiController]
[Route("[controller]")]
public class JsonImportController : ControllerBase
{
    private readonly JsonImportService _jsonImportService;

    public JsonImportController(JsonImportService jsonImportService)
    {
        _jsonImportService = jsonImportService;
    }

    [HttpPost("import-json-folder")]
    public async Task<IActionResult> ImportJsonFolder([FromQuery] string folderPath = "companyfacts", [FromQuery] int maxDegreeOfParallelism = 4)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return BadRequest(new { message = "The folder path must be provided." });
        }

        if (!Directory.Exists(folderPath))
        {
            return BadRequest(new { message = $"The folder '{folderPath}' does not exist." });
        }

        var jsonFiles = Directory.GetFiles(folderPath, "*.json");
        var taskLog = new ConcurrentBag<string>();

        // Process files with limited parallelism
        await ProcessFilesWithLimitedParallelism(jsonFiles, maxDegreeOfParallelism, taskLog);

        // Generate log file
        var logFileName = $"import-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
        var logFilePath = Path.Combine(folderPath, logFileName);
        await System.IO.File.WriteAllLinesAsync(logFilePath, taskLog);

        return Ok(new { message = "JSON import completed", logFile = logFilePath });
    }

private async Task ProcessFilesWithLimitedParallelism(string[] files, int maxDegreeOfParallelism, ConcurrentBag<string> taskLog)
{
    var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

    var tasks = files
        .Chunk(500) // Process files in chunks of 500
        .Select(async chunk =>
        {
            await semaphore.WaitAsync();
            try
            {
                await _jsonImportService.ImportFromJsonFiles(chunk);
                taskLog.Add($"SUCCESS: Processed batch of {chunk.Length} files.");
            }
            catch (Exception ex)
            {
                taskLog.Add($"FAILURE: Error processing batch. Details: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

    await Task.WhenAll(tasks);
}
}