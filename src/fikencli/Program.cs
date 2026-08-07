using FikenCli;

using var httpClient = new HttpClient
{
    BaseAddress = FikenTransport.DefaultBaseAddress
};

var app = new FikenCliApp(
    httpClient,
    () => Environment.GetEnvironmentVariable("FIKEN_API_TOKEN"),
    Console.Out,
    Console.Error,
    Console.In);

return await app.InvokeAsync(args);
