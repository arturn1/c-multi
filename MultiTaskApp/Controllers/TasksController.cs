using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Text;

[ApiController]
[Route("[controller]")]
public class TasksController : ControllerBase
{
    private const string HistoryFolder = "task-history";

    [HttpPost("start-tasks")]
    public IActionResult StartTasks([FromBody] TaskRequest req, [FromQuery] bool saveToFile = false)
    {
        var taskLog = new ConcurrentBag<TaskExecutionInfo>();
        var jobs = new BlockingCollection<int>(req.NumTasks);
        var countdown = new CountdownEvent(req.NumTasks);

        Directory.CreateDirectory(HistoryFolder);

        for (int i = 1; i <= req.NumWorkers; i++)
        {
            int workerId = i;
            Task.Run(() => Worker(workerId, jobs, countdown, taskLog));
        }

        for (int i = 1; i <= req.NumTasks; i++)
        {
            jobs.Add(i);
        }

        jobs.CompleteAdding();
        countdown.Wait();

        if (saveToFile)
        {
            var fileName = $"execution-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
            var path = Path.Combine(HistoryFolder, fileName);
            var sb = new StringBuilder();

            foreach (var entry in taskLog.OrderBy(t => t.StartTime))
            {
                sb.AppendLine($"Worker {entry.WorkerId} | Task {entry.TaskId} | Início: {entry.StartTime:O} | Fim: {entry.EndTime:O} | Duração: {entry.Duration}");
            }

            System.IO.File.AppendAllText(path, sb.ToString());
            return Ok(new { message = "Execução concluída", file = fileName });
        }

        return Ok(new { message = "Execução concluída", file = (string?)null });
    }

    [HttpGet("history")]
    public IActionResult GetHistory()
    {
        Directory.CreateDirectory(HistoryFolder);
        var files = Directory.GetFiles(HistoryFolder, "*.txt");

        var allLines = new List<string>();
        foreach (var file in files.OrderBy(f => f))
        {
            var lines = System.IO.File.ReadAllLines(file);
            allLines.Add($"Arquivo: {Path.GetFileName(file)}");
            allLines.AddRange(lines);
            allLines.Add("");
        }

        return Ok(new { logs = allLines });
    }

    private void Worker(int workerId, BlockingCollection<int> jobs, CountdownEvent countdown, ConcurrentBag<TaskExecutionInfo> monitor)
    {
        foreach (var taskId in jobs.GetConsumingEnumerable())
        {
            var start = DateTime.UtcNow;
            Thread.Sleep(2000);
            var end = DateTime.UtcNow;

            monitor.Add(new TaskExecutionInfo
            {
                WorkerId = workerId,
                TaskId = taskId,
                StartTime = start,
                EndTime = end
            });

            countdown.Signal();
        }
    }

    [HttpPost("start-tasks-api")]
    public async Task<IActionResult> StartTasks([FromBody] TaskRequest req, [FromQuery] bool saveToFile = false, [FromQuery] string apiUrl = "https://dog.ceo/api/breeds/image/random")
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return BadRequest(new { message = "A URL da API externa deve ser fornecida." });
        }

        var taskLog = new ConcurrentBag<TaskExecutionInfo>();
        var jobs = new BlockingCollection<int>(req.NumTasks);
        var countdown = new CountdownEvent(req.NumTasks);

        Directory.CreateDirectory(HistoryFolder);

        // Start workers
        var tasks = new List<Task>();
        for (int i = 1; i <= req.NumWorkers; i++)
        {
            int workerId = i;
            tasks.Add(Task.Run(() => WorkerWithApiCall(workerId, jobs, countdown, taskLog, apiUrl)));
        }

        // Add jobs to the queue
        for (int i = 1; i <= req.NumTasks; i++)
        {
            jobs.Add(i);
        }

        jobs.CompleteAdding();
        await Task.WhenAll(tasks); // Wait for all workers to complete

        if (saveToFile)
        {
            var fileName = $"execution-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
            var path = Path.Combine(HistoryFolder, fileName);
            var sb = new StringBuilder();

            foreach (var entry in taskLog.OrderBy(t => t.StartTime))
            {
                sb.AppendLine($"Worker {entry.WorkerId} | Task {entry.TaskId} | Api: {entry.ApiValue} | Duração: {entry.Duration}");
            }

            System.IO.File.AppendAllText(path, sb.ToString());
            return Ok(new { message = "Execução concluída", file = fileName });
        }

        return Ok(new { message = "Execução concluída", file = (string?)null });
    }

    private async Task WorkerWithApiCall(int workerId, BlockingCollection<int> jobs, CountdownEvent countdown, ConcurrentBag<TaskExecutionInfo> monitor, string apiUrl)
    {
        using var httpClient = new HttpClient();

        foreach (var taskId in jobs.GetConsumingEnumerable())
        {
            var start = DateTime.UtcNow;

            // Make the external API call
            var response = await httpClient.GetAsync($"{apiUrl}");
            var end = DateTime.UtcNow;

            monitor.Add(new TaskExecutionInfo
            {
                WorkerId = workerId,
                TaskId = taskId,
                ApiValue = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null,
                StartTime = start,
                EndTime = end
            });

            countdown.Signal();
        }
    }

}