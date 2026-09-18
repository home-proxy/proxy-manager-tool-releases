using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Infrastructure.Api;

namespace NetAgent.ProxyManager.Tests;

public sealed class HomeProxyDepositApiClientTests
{
    [Fact]
    public async Task CreateDepositTransactionAsync_ShouldPostAmountAndMapQrFields()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            captured = request;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return JsonResponse("""
                {
                  "id": "tx-1",
                  "amount": 50000,
                  "accountName": "Nguyen Van A",
                  "accountNumber": "123456789",
                  "bankName": "Vietcombank",
                  "bankCode": "VCB",
                  "description": "NAPTX1",
                  "qrCode": "https://example.test/qr.png",
                  "status": { "id": "4", "name": "Processing" },
                  "expiredAt": "2026-07-02T02:41:09.636Z"
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyDepositApiClient(client, Options.Create(new BackendApiOptions()));

        var transaction = await api.CreateDepositTransactionAsync(50_000, CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/transactions");
        body.Should().Contain("\"amount\":50000");
        transaction.Id.Should().Be("tx-1");
        transaction.Amount.Should().Be(50_000);
        transaction.AccountName.Should().Be("Nguyen Van A");
        transaction.AccountNumber.Should().Be("123456789");
        transaction.BankName.Should().Be("Vietcombank");
        transaction.BankCode.Should().Be("VCB");
        transaction.Description.Should().Be("NAPTX1");
        transaction.QrCode.Should().Be("https://example.test/qr.png");
        transaction.StatusId.Should().Be(4);
        transaction.StatusName.Should().Be("Processing");
        transaction.ExpiredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDepositTransactionAsync_ShouldGetDetailAndParseNumericPaidStatus()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""
                {
                  "data": {
                    "id": "tx-1",
                    "amount": "100000",
                    "status": { "id": 6, "name": "Completed" },
                    "updatedAt": "2026-07-02T02:41:09.636Z"
                  }
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyDepositApiClient(client, Options.Create(new BackendApiOptions()));

        var transaction = await api.GetDepositTransactionAsync("tx-1", CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/transactions/tx-1");
        transaction.Id.Should().Be("tx-1");
        transaction.Amount.Should().Be(100_000);
        transaction.StatusId.Should().Be(6);
        transaction.IsPaid.Should().BeTrue();
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class AsyncStubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request);
    }
}
