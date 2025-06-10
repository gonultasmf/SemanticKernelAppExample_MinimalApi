using System.ComponentModel;
using Codeblaze.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services
    .AddKernel()
    .AddOllamaChatCompletion("deepseek-r1", "http://localhost:11434")
    .Plugins.AddFromType<CalculatorPlugin>();
builder.Services.AddOpenApi();
builder.Services.AddRequestTimeouts();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IFunctionInvocationFilter, LoggingFilter>();
builder.Services.AddSingleton<IPromptRenderFilter, SafePromptFilter>();
builder.Services.AddSingleton<IAutoFunctionInvocationFilter, EarlyTerminationFilter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRequestTimeouts();
 
app.MapPost("/chat", async (IChatCompletionService chatCompletionService, ChatModel chatModel) =>
{
    var response = await chatCompletionService.GetChatMessageContentAsync(chatModel.Input);
    return response.ToString();
}).WithRequestTimeout(TimeSpan.FromMinutes(10));

app.MapGet("/add/{number1}/{number2}", async (Kernel kernel, int number1, int number2) =>
{
    var arguments = new KernelArguments
    {
        ["number1"] = number1,
        ["number2"] = number2
    };
    var addResult = await kernel.InvokeAsync("CalculatorPlugin", "add", arguments);
    return addResult.GetValue<int>();
}).WithRequestTimeout(TimeSpan.FromMinutes(10));

app.Run();

public record ChatModel(string Input);

public class CalculatorPlugin
{
    [KernelFunction("add")]
    [Description("İki sayısal değer üzerinde toplama işlemi gerçekleştirir.")]
    [return: Description("Toplam değeri döndürür.")]
    public int Add(int number1, int number2)
        => number1 + number2;
}

public sealed class LoggingFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        Console.WriteLine($"Çalıştırılıyor = Eklenti Adı : {context.Function.PluginName} | Fonksiyon Adı : {context.Function.Name}");
        await next(context);
        Console.WriteLine($"Çalıştırıldı = Eklenti Adı : {context.Function.PluginName} | Fonksiyon Adı : {context.Function.Name}");
    }
}

public sealed class SafePromptFilter : IPromptRenderFilter
{
    public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
    {
        await next(context);
        context.RenderedPrompt = "3 + 5 sonucu kaçtır?";
    }
}

public sealed class EarlyTerminationFilter : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        await next(context);
 
        var result = context.Result.GetValue<string>();
        if (result == "desired result")
        {
            context.Terminate = true;
        }
    }
}