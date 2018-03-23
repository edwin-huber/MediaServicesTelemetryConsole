using System;
using System.Configuration;
using System.IO;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

// Project set up using
// https://docs.microsoft.com/en-us/azure/media-services/media-services-dotnet-how-to-use
// Sample code taken from here:
// https://docs.microsoft.com/en-us/azure/media-services/media-services-dotnet-telemetry
namespace MediaServicesConsoleTelemetry
{
    class Program
    {
        private static readonly string _AADTenantDomain =
            ConfigurationManager.AppSettings["AMSAADTenantDomain"];
        private static readonly string _RESTAPIEndpoint =
            ConfigurationManager.AppSettings["AMSRESTAPIEndpoint"];
        private static readonly string _AMSClientId =
            ConfigurationManager.AppSettings["AMSClientId"];
        private static readonly string _AMSClientSecret =
            ConfigurationManager.AppSettings["AMSClientSecret"];

        private static readonly string _mediaServicesStorageAccountName =
            ConfigurationManager.AppSettings["StorageAccountName"];

        // Field for service context.
        private static CloudMediaContext _context = null;

        private static IStreamingEndpoint _streamingEndpoint = null;
        private static IChannel _channel = null;

        static void Main(string[] args)
        {
            AzureAdTokenCredentials tokenCredentials =
                new AzureAdTokenCredentials(_AADTenantDomain,
                    new AzureAdClientSymmetricKey(_AMSClientId, _AMSClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);

            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);

            _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);


            Console.WriteLine("Please choose your Azure Media Services telemety sources");
            _streamingEndpoint = ChooseStreamingEndpointForTelemetry();
            _channel = ChooseLiveChannelForTelemetry();

            var monitoringConfigurations = _context.MonitoringConfigurations;
            IMonitoringConfiguration monitoringConfiguration = null;

            // No more than one monitoring configuration settings is allowed.
            // Once we have created it, it is the one that we shall use in future iterations...
            // you can use monitoringConfiguration.Delete(); to delete the monitoring configuration.
            if (monitoringConfigurations.ToArray().Length != 0)
            {
                monitoringConfiguration = _context.MonitoringConfigurations.FirstOrDefault();               
            }
            else
            {
                INotificationEndPoint notificationEndPoint =
                          _context.NotificationEndPoints.Create("monitoring",
                          NotificationEndPointType.AzureTable, GetTableEndPoint());

                monitoringConfiguration = _context.MonitoringConfigurations.Create(notificationEndPoint.Id,
                    new List<ComponentMonitoringSetting>()
                    {
                    new ComponentMonitoringSetting(MonitoringComponent.Channel, MonitoringLevel.Verbose),
                    new ComponentMonitoringSetting(MonitoringComponent.StreamingEndpoint, MonitoringLevel.Verbose)

                    });
            }

            //Print metrics for a Streaming Endpoint.
            PrintMetrics();

            Console.WriteLine("\npress any key to exit...");
            Console.ReadKey();
        }

        private static void PrintMetrics()
        {
            OfferChoiceToPrintMetrics();
            int choice = ValidateKeyPress(3);
            while (choice != 2)
            {
                if (choice == 0)
                {
                    PrintStreamingEndpointMetrics();
                }
                else if (choice == 1)
                {
                    PrintChannelMetrics();
                }
                OfferChoiceToPrintMetrics();
                choice = ValidateKeyPress(3);
            }
        }

        private static void OfferChoiceToPrintMetrics()
        {
            Console.WriteLine("Please choose which metrics / telemetry to print:");
            Console.WriteLine("0. StreamingEndpoint Metrics");
            Console.WriteLine("1. Live Channel Metrics");
            Console.WriteLine("2. Quit");
        }

        private static IChannel ChooseLiveChannelForTelemetry()
        {
            int n = 0;
            Console.WriteLine("Live Channels :");
            var channels = _context.Channels.ToList();
            foreach (var channel in channels)
            {
                Console.WriteLine($"{n++}. - {channel.Name}");                
            }
            int choice = ValidateKeyPress(n);

            return channels[choice];
        }

        private static IStreamingEndpoint ChooseStreamingEndpointForTelemetry()
        {
            int n = 0;
            var endpoints = _context.StreamingEndpoints.ToList();
            Console.WriteLine("Endpoints :");
            foreach (var endpoint in endpoints)
            {
                Console.WriteLine($"{n++}. - {endpoint.Name}");                
            }
            int choice = ValidateKeyPress(n);

            return endpoints[choice];
        }

        private static int ValidateKeyPress(int n)
        {
            ConsoleKeyInfo key = Console.ReadKey();
            int choice = int.Parse(key.KeyChar.ToString());
            while (choice < 0 || choice >= n)
            {
                Console.WriteLine($"\nTry again please... you chose {choice} which is not in my list.\n");
                key = Console.ReadKey();
                choice = int.Parse(key.KeyChar.ToString());
            }

            return choice;
        }

        private static string GetTableEndPoint()
        {
            return "https://" + _mediaServicesStorageAccountName + ".table.core.windows.net/";
        }

        private static void PrintStreamingEndpointMetrics()
        {
            Console.WriteLine(string.Format("Telemetry for streaming endpoint '{0}'", _streamingEndpoint.Name));

            DateTime timerangeEnd = DateTime.UtcNow;
            DateTime timerangeStart = DateTime.UtcNow.AddHours(-5);

            // Get some streaming endpoint metrics.
            var telemetry = _streamingEndpoint.GetTelemetry();

            var res = telemetry.GetStreamingEndpointRequestLogs(timerangeStart, timerangeEnd);

            Console.Title = "Streaming endpoint metrics:";

            foreach (var log in res)
            {
                Console.WriteLine("AccountId: {0}", log.AccountId);
                Console.WriteLine("BytesSent: {0}", log.BytesSent);
                Console.WriteLine("EndToEndLatency: {0}", log.EndToEndLatency);
                Console.WriteLine("HostName: {0}", log.HostName);
                Console.WriteLine("ObservedTime: {0}", log.ObservedTime);
                Console.WriteLine("PartitionKey: {0}", log.PartitionKey);
                Console.WriteLine("RequestCount: {0}", log.RequestCount);
                Console.WriteLine("ResultCode: {0}", log.ResultCode);
                Console.WriteLine("RowKey: {0}", log.RowKey);
                Console.WriteLine("ServerLatency: {0}", log.ServerLatency);
                Console.WriteLine("StatusCode: {0}", log.StatusCode);
                Console.WriteLine("StreamingEndpointId: {0}", log.StreamingEndpointId);
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        private static void PrintChannelMetrics()
        {
            if (_channel == null)
            {
                Console.WriteLine("There are no channels in this AMS account");
                return;
            }

            Console.WriteLine(string.Format("Telemetry for channel '{0}'", _channel.Name));

            DateTime timerangeEnd = DateTime.UtcNow;
            DateTime timerangeStart = DateTime.UtcNow.AddHours(-5);

            // Get some channel metrics.
            var telemetry = _channel.GetTelemetry();

            var channelMetrics = telemetry.GetChannelHeartbeats(timerangeStart, timerangeEnd);

            // Print the channel metrics.
            Console.WriteLine("Channel metrics:");

            foreach (var channelHeartbeat in channelMetrics.OrderBy(x => x.ObservedTime))
            {
                Console.WriteLine(
                    "    Observed time: {0}, Last timestamp: {1}, Incoming bitrate: {2}",
                    channelHeartbeat.ObservedTime,
                    channelHeartbeat.LastTimestamp,
                    channelHeartbeat.IncomingBitrate);
            }

            Console.WriteLine();
        }
    }
}