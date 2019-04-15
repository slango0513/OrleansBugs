using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace OrleansBugs
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseEnvironment(Microsoft.Extensions.Hosting.EnvironmentName.Development)
                .UseOrleans((context, siloBuilder) =>
                {
                    siloBuilder
                        .UseAdoNetClustering(options =>
                        {
                            options.ConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=OrleansBugs;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
                            options.Invariant = "System.Data.SqlClient";
                        })
                        .Configure<ClusterOptions>(options =>
                        {
                            options.ClusterId = "debBug";
                            options.ServiceId = "debBug";
                        })
                        .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(SampleGrain).Assembly).WithReferences())
                        .Configure<EndpointOptions>(options =>
                        {
                            options.AdvertisedIPAddress = IPAddress.Loopback;
                            var gatewayPort = int.Parse(context.Configuration["port"] ?? throw new ArgumentNullException("port"));
                            options.GatewayPort = gatewayPort;
                            options.SiloPort = gatewayPort + 1;
                        })
                        .AddStartupTask<SampleStartupTask>();
                });
    }

    public class SampleStartupTask : IStartupTask
    {
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger _logger;

        public SampleStartupTask(IGrainFactory grainFactory, ILogger<SampleStartupTask> logger)
        {
            _grainFactory = grainFactory;
            _logger = logger;
        }

        public async Task Execute(CancellationToken cancellationToken)
        {
            await _grainFactory.GetGrain<ISampleGrain>("Default").DoNothingAsync();
            _logger.LogWarning($"StartupTask executed.");
        }
    }

    public interface ISampleGrain : IGrainWithStringKey
    {
        Task DoNothingAsync();
    }

    public class SampleGrain : Grain, ISampleGrain
    {
        private ILogger logger;
        private IDisposable interval;
        private int ticks;

        public async override Task OnActivateAsync()
        {
            await Task.CompletedTask;
            logger = ServiceProvider.GetService<ILogger<SampleGrain>>();
            logger.LogWarning($"{new string('=', 16)}\nOnActivateAsync | IdentityString: {IdentityString}\n{new string('=', 16)}");
            interval = RegisterTimer(async state =>
            {
                await Task.CompletedTask;
                ticks += 1;
                logger.LogInformation($"Ticks. ticks: {ticks}");
            },
            this, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        }

        public async Task DoNothingAsync()
        {
            await Task.CompletedTask;
        }
    }
}
