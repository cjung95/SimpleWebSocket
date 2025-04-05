// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.IntegrationTests.Tests;
using Jung.SimpleWebSocket.Models;
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
        /// <param name="args">The command line arguments.</param>
        public static async Task Main(string[] args)
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
                    logger.LogError("The chosen test procedure could not be loaded");
                }
                else
                {
                    Console.WriteLine($"\nRunning test: {procedure.Name}: {procedure.Description}");
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
            .MinimumLevel.Debug()
            .CreateLogger();

            serviceCollection.AddSerilog();
            serviceCollection.AddLogging();

            serviceCollection.AddSingleton<DisplayEventsTest>();
            serviceCollection.AddSingleton<SendMessagesLoopTest>();
            serviceCollection.AddSingleton<SimpleWebSocketServer>();
            serviceCollection.AddSingleton<SimpleWebSocketClient>();

            serviceCollection.Configure<SimpleWebSocketServerOptions>(options =>
            {
                options.LocalIpAddress = System.Net.IPAddress.Any;
                options.Port = 8085;
            });
            return serviceCollection.BuildServiceProvider();
        }
    }
}