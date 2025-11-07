// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.IntegrationTests.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Jung.SimpleWebSocket.IntegrationTests
{
    public class Program
    {
        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        public static async Task Main()
        {
            var procedureProvider = new ProcedureProvider();

            Console.WriteLine("Available tests:\n");

            string[] procedureNames = procedureProvider.GetNames();
            for (int i = 0; i < procedureNames.Length; i++)
            {
                Console.WriteLine($"{i + 1}: {procedureNames[i]}");
            }

            int chosenProcedureIndex;
            do
            {
                Console.Write("\nEnter the number of the test you want to run: ");
                var userInput = Console.ReadLine();
                if (userInput != null)
                {
                    userInput = userInput.Trim().ToLower();
                    if (userInput == "exit")
                    {
                        return;
                    }

                    if (!int.TryParse(userInput, out int procedureNumber))
                    {
                        Console.WriteLine("Invalid input. Please enter a number.");
                        continue;
                    }

                    if (procedureNumber >= 1 && procedureNumber <= procedureNames.Length)
                    {
                        chosenProcedureIndex = procedureNumber - 1;
                        break;
                    }
                }
            } while (true);

            var procedure = procedureProvider.GetProcedure(chosenProcedureIndex);
            var serviceProvider = CreateServiceProvider(procedure);
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                if (serviceProvider.GetService(procedure.ProcedureType) is not BaseTest test)
                {
                    logger.LogError("The chosen test procedure {procedureType} could not be loaded.", procedure.ProcedureType.FullName);
                }
                else
                {
                    logger.LogInformation("Running test: {procedureName} ({procedureDescription})", procedure.Name, procedure.Description);
                    await test.RunAsync();
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "An error occurred while running the procedure.");
            }
        }

        private static ServiceProvider CreateServiceProvider(TestProcedure procedure)
        {
            var serviceCollection = new ServiceCollection();

            Log.Logger = new LoggerConfiguration()
            .WriteTo.File($"{procedure.Name}-{DateTime.Now:g}-{Guid.NewGuid():n}.txt", rollingInterval: RollingInterval.Day)
            .WriteTo.Console(Serilog.Events.LogEventLevel.Information, outputTemplate: "{Level:u3}: {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Debug()
            .CreateLogger();

            serviceCollection.AddSerilog();
            serviceCollection.AddLogging();

            serviceCollection.AddSingleton<DisplayEvents1Test>();
            serviceCollection.AddSingleton<DisplayEvents2Test>();
            serviceCollection.AddSingleton<SendTextMessagesLoopTest>();
            serviceCollection.AddSingleton<SendBinaryMessagesLoopTest>();
            serviceCollection.AddSingleton<SimpleWebSocketClient>();

            return serviceCollection.BuildServiceProvider();
        }
    }
}