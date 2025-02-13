using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RocketExplorer.Functions;

public class Function1(ILoggerFactory loggerFactory)
{
	private readonly ILogger logger = loggerFactory.CreateLogger<Function1>();

	[Function("Function1")]
	public void Run([TimerTrigger("*/15 * * * * *")] TimerInfo myTimer)
	{
		this.logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

		if (myTimer.ScheduleStatus is not null)
		{
			this.logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
		}
	}
}