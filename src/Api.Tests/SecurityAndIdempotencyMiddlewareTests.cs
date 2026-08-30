using System.Text;
using gesFactu.Api.Configuration;
using gesFactu.Api.Middleware;
using gesFactu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Api.Tests;

public sealed class SecurityAndIdempotencyMiddlewareTests
{
    private const string ValidApiKey =
        "0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task ApiKey_Missing_Returns401()
    {
        var called = false;
        var middleware = new ApiKeyAuthenticationMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            new FakeHostEnvironment(Environments.Production));

        var context = NewContext("GET", "/api/v1/taxpayers");

        await middleware.InvokeAsync(
            context,
            Options.Create(new SecurityOptions
            {
                ApiKey = ValidApiKey
            }));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public async Task ApiKey_Valid_AllowsRequest()
    {
        var called = false;
        var middleware = new ApiKeyAuthenticationMiddleware(
            context =>
            {
                called = true;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            new FakeHostEnvironment(Environments.Production));

        var context = NewContext("GET", "/api/v1/taxpayers");
        context.Request.Headers[ApiKeyAuthenticationMiddleware.HeaderName] =
            ValidApiKey;

        await middleware.InvokeAsync(
            context,
            Options.Create(new SecurityOptions
            {
                ApiKey = ValidApiKey
            }));

        Assert.True(called);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task Idempotency_MissingKey_Returns400()
    {
        await using var db = NewDbContext();

        var middleware = new IdempotencyMiddleware(
            _ => Task.CompletedTask);

        var context = NewContext(
            "POST",
            "/api/v1/BillingRecords",
            "{\"a\":1}");

        await middleware.InvokeAsync(
            context,
            db,
            Options.Create(new IdempotencyOptions
            {
                RequireForUnsafeMethods = true
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Idempotency_SameKeyAndPayload_ReplaysFirstResponse()
    {
        await using var db = NewDbContext();
        var calls = 0;

        var middleware = new IdempotencyMiddleware(async context =>
        {
            calls++;
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Location = "/api/v1/BillingRecords/77";
            await context.Response.WriteAsync("{\"id\":77}");
        });

        var options = Options.Create(new IdempotencyOptions
        {
            RequireForUnsafeMethods = true,
            RetentionHours = 48
        });

        var first = NewContext(
            "POST",
            "/api/v1/BillingRecords",
            "{\"a\":1}");
        first.Request.Headers[IdempotencyMiddleware.HeaderName] = "idem-001";

        await middleware.InvokeAsync(first, db, options);

        var second = NewContext(
            "POST",
            "/api/v1/BillingRecords",
            "{\"a\":1}");
        second.Request.Headers[IdempotencyMiddleware.HeaderName] = "idem-001";

        await middleware.InvokeAsync(second, db, options);

        Assert.Equal(1, calls);
        Assert.Equal(StatusCodes.Status201Created, second.Response.StatusCode);
        Assert.Equal("true", second.Response.Headers["Idempotency-Replayed"]);
        Assert.Equal(
            "/api/v1/BillingRecords/77",
            second.Response.Headers.Location.ToString());
        Assert.Equal("{\"id\":77}", await ReadResponseAsync(second));
    }

    [Fact]
    public async Task Idempotency_SameKeyDifferentPayload_Returns409()
    {
        await using var db = NewDbContext();
        var calls = 0;

        var middleware = new IdempotencyMiddleware(async context =>
        {
            calls++;
            context.Response.StatusCode = StatusCodes.Status201Created;
            await context.Response.WriteAsync("{\"id\":1}");
        });

        var options = Options.Create(new IdempotencyOptions
        {
            RequireForUnsafeMethods = true
        });

        var first = NewContext(
            "POST",
            "/api/v1/BillingRecords",
            "{\"amount\":1}");
        first.Request.Headers[IdempotencyMiddleware.HeaderName] = "idem-conflict";

        await middleware.InvokeAsync(first, db, options);

        var second = NewContext(
            "POST",
            "/api/v1/BillingRecords",
            "{\"amount\":2}");
        second.Request.Headers[IdempotencyMiddleware.HeaderName] = "idem-conflict";

        await middleware.InvokeAsync(second, db, options);

        Assert.Equal(1, calls);
        Assert.Equal(StatusCodes.Status409Conflict, second.Response.StatusCode);
    }

    private static ApplicationDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"api-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static DefaultHttpContext NewContext(
        string method,
        string path,
        string body = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(
            Encoding.UTF8.GetBytes(body));
        context.Request.ContentLength = context.Request.Body.Length;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseAsync(
        HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(
            context.Response.Body,
            Encoding.UTF8,
            leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "gesFactu.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
