using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DFSchedulePOC
{
    public static class GenStockReportOrchestrator
    {
        [FunctionName("GenStockReportOrchestrator")]
        public static void RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            // Use context.NewGuid() since Random is non-deterministic
            Random rand = new Random(context.NewGuid().GetHashCode());
            string[] symbols = { "AAPL", "GOOGL", "MSFT", "AMZN", "FB" };

            log.LogInformation("GenStockReportOrchestrator started");

            // Generate random prices between 100-1000
            System.Text.StringBuilder results = new System.Text.StringBuilder();
            for (int i = 0;i < symbols.Length;i++)
            {
                log.LogInformation("Generating stock report for {stockSymbol}", symbols[i]);
                int price = rand.Next(100, 1000);
                results.Append($"{symbols[i]}: {price}");
                if (i < symbols.Length - 1)
                {
                    results.Append(", ");
                }
            }

            log.LogInformation("Results: {results}", results.ToString());
        }

        [FunctionName("CreateSchedule")]
        public static async Task<IActionResult> CreateSchedule(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("Request body is required");
            }

            var config = JsonConvert.DeserializeObject<ScheduleConfiguration>(requestBody);
            if (config == null)
            {
                return new BadRequestObjectResult("Invalid schedule configuration");
            }

            var entityId = new EntityId(nameof(Schedule), config.ScheduleId);
            await entityClient.SignalEntityAsync(entityId, "CreateSchedule", config);

            return new OkObjectResult($"Schedule creation initiated for ID: {config.ScheduleId}");
        }

        [FunctionName("UpdateSchedule")]
        public static async Task<IActionResult> UpdateSchedule(
            [HttpTrigger(AuthorizationLevel.Function, "put")] HttpRequest req,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("Request body is required");
            }

            var config = JsonConvert.DeserializeObject<ScheduleConfiguration>(requestBody);
            if (config == null)
            {
                return new BadRequestObjectResult("Invalid schedule configuration");
            }

            var entityId = new EntityId(nameof(Schedule), config.ScheduleId);
            await entityClient.SignalEntityAsync(entityId, "UpdateSchedule", config);

            return new OkObjectResult($"Schedule update initiated for ID: {config.ScheduleId}");
        }

        [FunctionName("PauseSchedule")]
        public static async Task<IActionResult> PauseSchedule(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            string scheduleId = req.Query["scheduleId"];
            if (string.IsNullOrEmpty(scheduleId))
            {
                BadRequestObjectResult error = new BadRequestObjectResult("Please provide scheduleId in query parameters");
                return error;
            }

            EntityId entityId = new EntityId(nameof(Schedule), scheduleId);
            await entityClient.SignalEntityAsync(entityId, "PauseSchedule");

            OkObjectResult result = new OkObjectResult($"Schedule pause initiated for ID: {scheduleId}");
            return result;
        }

        [FunctionName("ResumeSchedule")]
        public static async Task<IActionResult> ResumeSchedule(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            string scheduleId = req.Query["scheduleId"];
            if (string.IsNullOrEmpty(scheduleId))
            {
                BadRequestObjectResult error = new BadRequestObjectResult("Please provide scheduleId in query parameters");
                return error;
            }

            EntityId entityId = new EntityId(nameof(Schedule), scheduleId);
            await entityClient.SignalEntityAsync(entityId, "ResumeSchedule");

            OkObjectResult result = new OkObjectResult($"Schedule resume initiated for ID: {scheduleId}");
            return result;
        }

        // add deleteschedule
        [FunctionName("DeleteSchedule")]
        public static async Task<IActionResult> DeleteSchedule(
            [HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequest req,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            string scheduleId = req.Query["scheduleId"];
            if (string.IsNullOrEmpty(scheduleId))
            {
                return new BadRequestObjectResult("Please provide scheduleId in query parameters");
            }

            var entityId = new EntityId(nameof(Schedule), scheduleId);
            await entityClient.SignalEntityAsync(entityId, "delete");

            return new OkObjectResult($"Schedule deletion initiated for ID: {scheduleId}");
        }
    }
}