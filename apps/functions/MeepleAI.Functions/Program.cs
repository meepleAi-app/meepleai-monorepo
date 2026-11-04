using MeepleAI.Functions.Forwarding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient(PdfUploadedForwarder.HttpClientName, client =>
        {
            var baseUrl = context.Configuration["MeepleAiApiBaseUrl"]
                ?? throw new InvalidOperationException("MeepleAiApiBaseUrl is required");
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.Add(PdfUploadedForwarder.TokenHeader,
                context.Configuration["MeepleAiInternalServiceToken"]
                ?? throw new InvalidOperationException("MeepleAiInternalServiceToken is required"));
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IPdfUploadedForwarder, PdfUploadedForwarder>();
    })
    .Build();

host.Run();
