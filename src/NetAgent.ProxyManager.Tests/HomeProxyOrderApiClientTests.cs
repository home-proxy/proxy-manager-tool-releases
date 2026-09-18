using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;

namespace NetAgent.ProxyManager.Tests;

public sealed class HomeProxyOrderApiClientTests
{
    [Fact]
    public async Task GetProxyOrdersAsync_ShouldBuildSupportedServerFilterAndMapOrders()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""
                {
                  "data": [
                    {
                      "code": "J82S51",
                      "description": "note",
                      "order": { "code": "D260522XFKA" },
                      "protocol": "HTTP",
                      "proxy": {
                        "ipaddress": {
                          "domain": "proxy11206.zproxy.online",
                          "prevIp": "14.187.233.166",
                          "provider": "VNPT",
                          "categorytype": { "id": 1 },
                          "ip": "14.187.150.172"
                        },
                        "password": "pass",
                        "username": "user",
                        "protocol": "HTTP",
                        "port": 6814
                      },
                      "expiredAt": 1779510467744,
                      "status": { "name": "Completed" },
                      "id": 790362
                    },
                    {
                      "code": "5JWR6E",
                      "order": { "code": "D260511WDSA" },
                      "protocol": "SOCKS",
                      "proxy": {
                        "ipaddress": {
                          "domain": "proxy17039.aproxy.id.vn",
                          "provider": "CMC",
                          "categorytype": { "id": 1 },
                          "ip": "203.205.4.60"
                        },
                        "password": "dcpass",
                        "username": "dcuser",
                        "port": 30613
                      },
                      "id": 747925
                    }
                  ],
                  "total": 42,
                  "hasNextPage": true
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var page = await api.GetProxyOrdersAsync(new ProxyOrderPageRequest
        {
            Page = 2,
            Limit = 50,
            SearchField = ProxyOrderSearchField.Id,
            SearchText = "J82S51"
        }, CancellationToken.None);

        captured!.RequestUri!.OriginalString.Should().StartWith("https://api.homeproxy.vn/api/v1/users/proxies?");
        Uri.UnescapeDataString(captured.RequestUri.Query).Should().Contain("filter=code:$eq:string:J82S51;categorytype.id:$eq:number:1");
        Uri.UnescapeDataString(captured.RequestUri.Query).Should().Contain("""sort=[{"orderBy":"createdAt","order":"desc"}]""");
        page.Total.Should().Be(42);
        page.HasNextPage.Should().BeTrue();
        page.Orders[0].ProxyAddress.Should().Be("14.187.150.172:6814:user:pass");
        page.Orders[0].ProxyDomain.Should().Be("proxy11206.zproxy.online:6814:user:pass");
        page.Orders[0].IsDatacenter.Should().BeFalse();
        page.Orders[1].Protocol.Should().Be(ProxyProtocol.Socks5);
        page.Orders[1].IsDatacenter.Should().BeTrue();
    }

    [Fact]
    public async Task GetProxyOrdersAsync_ShouldIncludeProviderFilterForDatacenterRequests()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""{ "data": [], "total": 2, "hasNextPage": false }""");
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var page = await api.GetProxyOrdersAsync(new ProxyOrderPageRequest
        {
            Page = 1,
            Limit = 50,
            CategoryTypeId = 1,
            OrderKind = ProxyOrderKind.Datacenter,
            ProviderIn = ["US", "CMC"]
        }, CancellationToken.None);

        var decodedQuery = Uri.UnescapeDataString(captured!.RequestUri!.Query);
        decodedQuery.Should().Contain("ipaddress.provider:$in:string:US,CMC");
        decodedQuery.Should().Contain("categorytype.id:$eq:number:1");
        page.Total.Should().Be(2);
        page.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetProxyOrdersAsync_ShouldUseIpInForUsDatacenterOrdersOnly()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            JsonResponse("""
                {
                  "data": [
                    {
                      "code": "USDC01",
                      "order": { "code": "D260528WRYH" },
                      "protocol": "HTTP",
                      "proxy": {
                        "ipIn": "103.51.120.98",
                        "ipaddress": {
                          "domain": "163.123.201.87",
                          "location": "US",
                          "provider": "US",
                          "categorytype": { "id": 1 },
                          "ip": "163.123.201.87"
                        },
                        "password": "1123123123",
                        "username": "123",
                        "protocol": "HTTP",
                        "port": 62349
                      },
                      "id": 814271
                    },
                    {
                      "code": "CMC01",
                      "order": { "code": "D260528CMC" },
                      "protocol": "HTTP",
                      "proxy": {
                        "ipIn": "10.0.0.1",
                        "ipaddress": {
                          "domain": "203.205.4.60",
                          "provider": "CMC",
                          "categorytype": { "id": 1 },
                          "ip": "203.205.4.60"
                        },
                        "password": "pass",
                        "username": "user",
                        "protocol": "HTTP",
                        "port": 30613
                      },
                      "id": 814272
                    }
                  ],
                  "total": 2,
                  "hasNextPage": false
                }
                """)))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var page = await api.GetProxyOrdersAsync(new ProxyOrderPageRequest
        {
            CategoryTypeId = 1
        }, CancellationToken.None);

        page.Orders[0].Provider.Should().Be("US");
        page.Orders[0].ProxyAddress.Should().Be("103.51.120.98:62349:123:1123123123");
        page.Orders[0].ProxyDomain.Should().Be("163.123.201.87:62349:123:1123123123");
        page.Orders[1].Provider.Should().Be("CMC");
        page.Orders[1].ProxyAddress.Should().Be("203.205.4.60:30613:user:pass");
    }

    [Fact]
    public async Task GetPurchaseHistoryAsync_ShouldBuildSearchAndStatusFilterAndMapRows()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""
                {
                  "data": [
                    {
                      "id": 101,
                      "code": "D260528UBBB",
                      "amount": 2100,
                      "products": [
                        { "quantity": 2, "dayOfUse": 1 },
                        { "quantity": null, "dayOfUse": 1 },
                        { "quantity": 3, "dayOfUse": 7 }
                      ],
                      "user": { "phone": "+84825590201", "email": "user@example.com", "name": "User" },
                      "taxCode": { "mst": "0312345678" },
                      "status": { "id": 6, "name": "Completed" },
                      "createdAt": "2025-11-15T13:35:10Z"
                    }
                  ],
                  "total": 133,
                  "hasNextPage": true
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var page = await api.GetPurchaseHistoryAsync(new HistoryPageRequest
        {
            Page = 2,
            Limit = 10,
            SearchField = "code",
            SearchText = "D260528UBBB",
            Status = HistoryStatusFilter.Completed
        }, CancellationToken.None);

        captured!.RequestUri!.OriginalString.Should().StartWith("https://api.homeproxy.vn/api/v1/orders?");
        var decodedQuery = Uri.UnescapeDataString(captured.RequestUri.Query);
        decodedQuery.Should().Contain("""sort=[{"orderBy":"createdAt","order":"desc"}]""");
        decodedQuery.Should().Contain("filter=status.id:$eq:number:6;code:$eq:string:D260528UBBB");
        decodedQuery.Should().NotContain("s=");
        page.Total.Should().Be(133);
        page.HasNextPage.Should().BeTrue();
        page.Items[0].Code.Should().Be("D260528UBBB");
        page.Items[0].Account.Should().Be("+84825590201");
        page.Items[0].Quantity.Should().Be(6);
        page.Items[0].DayOfUse.Should().Be("1, 7");
        page.Items[0].TaxCode.Should().Be("0312345678");
        page.Items[0].StatusId.Should().Be(6);
        page.Items[0].CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_ShouldBuildSearchAllAndProcessingFilterAndMapRows()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""
                {
                  "data": [
                    {
                      "id": "tx-1",
                      "code": "D26U555UBBB",
                      "type": 2,
                      "amount": "10000000",
                      "content": "Thanh toán đơn hàng Proxy tĩnh",
                      "description": "#D260528UBBB",
                      "orderCode": "D260528UBBB",
                      "status": { "id": "4", "name": "Processing" },
                      "transactionDate": "2025-11-15T13:35:00Z",
                      "createdAt": "2025-11-14T13:35:00Z"
                    }
                  ],
                  "total": 42,
                  "hasNextPage": false
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var page = await api.GetTransactionHistoryAsync(new HistoryPageRequest
        {
            Page = 1,
            Limit = 20,
            SearchField = HistorySearchFields.All,
            SearchText = "proxy",
            Status = HistoryStatusFilter.Processing
        }, CancellationToken.None);

        captured!.RequestUri!.OriginalString.Should().StartWith("https://api.homeproxy.vn/api/v1/transactions?");
        var decodedQuery = Uri.UnescapeDataString(captured.RequestUri.Query);
        decodedQuery.Should().Contain("s=proxy");
        decodedQuery.Should().Contain("filter=status.id:$in:number:3,4");
        page.Total.Should().Be(42);
        page.HasNextPage.Should().BeFalse();
        page.Items[0].Code.Should().Be("D26U555UBBB");
        page.Items[0].Type.Should().Be(2);
        page.Items[0].Amount.Should().Be(10000000m);
        page.Items[0].Content.Should().Be("Thanh toán đơn hàng Proxy tĩnh");
        page.Items[0].OrderCode.Should().Be("D260528UBBB");
        page.Items[0].StatusId.Should().Be(4);
        page.Items[0].CreatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(ProxyOrderKind.Datacenter, "proxy.ipIn")]
    [InlineData(ProxyOrderKind.Static, "ipaddress.ip")]
    public async Task GetProxyOrdersAsync_ShouldBuildProxySearchFilterForOrderKind(
        ProxyOrderKind orderKind,
        string expectedField)
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""{"data":[],"total":0,"hasNextPage":false}""");
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.GetProxyOrdersAsync(new ProxyOrderPageRequest
        {
            CategoryTypeId = 1,
            OrderKind = orderKind,
            SearchField = ProxyOrderSearchField.Proxy,
            SearchText = "103.51.120.98"
        }, CancellationToken.None);

        Uri.UnescapeDataString(captured!.RequestUri!.Query)
            .Should().Contain($"filter={expectedField}:$eq:string:103.51.120.98;categorytype.id:$eq:number:1");
    }

    [Fact]
    public async Task ChangeProxyInfoAsync_ShouldSendBackendProtocolToken()
    {
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/orders/change-info-proxies");
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.ChangeProxyInfoAsync([12345, 12346], "newuser", "newpass", ProxyProtocol.Socks5, CancellationToken.None);

        body.Should().Contain("\"userProxyIds\":[12345,12346]");
        body.Should().Contain("\"username\":\"newuser\"");
        body.Should().Contain("\"password\":\"newpass\"");
        body.Should().Contain("\"protocol\":\"SOCKS\"");
    }

    [Fact]
    public async Task RenewProxiesAsync_ShouldSendDefaultRenewalFlagAndCategoryType()
    {
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/orders/renewal-proxies");
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.RenewProxiesAsync([12345], 30, 1, CancellationToken.None);

        body.Should().Contain("\"userProxyIds\":[12345]");
        body.Should().Contain("\"dayOfRenewal\":30");
        body.Should().Contain("\"isRenewal\":false");
        body.Should().Contain("\"categoryTypeId\":1");
        body.Should().NotContain("rotateInterval");
        body.Should().NotContain("isAutoRotate");
    }

    [Theory]
    [InlineData(false, "proxy.isCdk:$eq:boolean:false;categorytype.id:$eq:number:2")]
    [InlineData(true, "proxy.isCdk:$eq:boolean:true;categorytype.id:$eq:number:2")]
    public async Task GetProxyOrdersAsync_ShouldBuildRotateCategoryAndCdkFilters(bool isCdk, string expectedFilter)
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""{"data":[],"total":0,"hasNextPage":false}""");
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.GetProxyOrdersAsync(new ProxyOrderPageRequest
        {
            Page = 1,
            Limit = 20,
            CategoryTypeId = 2,
            IsCdk = isCdk
        }, CancellationToken.None);

        Uri.UnescapeDataString(captured!.RequestUri!.Query).Should().Contain($"filter={expectedFilter}");
    }

    [Fact]
    public async Task GetProxyOrdersAsync_ShouldMapRotateFields()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            JsonResponse("""
                {
                  "data": [
                    {
                      "code": "ROT123",
                      "description": "rotate note",
                      "order": { "code": "ORDER123" },
                      "protocol": "HTTP",
                      "proxy": {
                        "isCdk": true,
                        "rotateInterval": 5,
                        "ipaddress": {
                          "provider": "VNPT",
                          "categorytype": { "id": 2 },
                          "ip": "1.2.3.4"
                        },
                        "password": "pass",
                        "username": "user",
                        "port": 8080
                      },
                      "status": { "id": "6", "name": "Completed" },
                      "id": 12345
                    }
                  ],
                  "total": 1,
                  "hasNextPage": false
                }
                """)))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var page = await api.GetProxyOrdersAsync(new ProxyOrderPageRequest
        {
            CategoryTypeId = 2,
            IsCdk = true
        }, CancellationToken.None);

        page.Orders[0].CategoryTypeId.Should().Be(2);
        page.Orders[0].IsCdk.Should().BeTrue();
        page.Orders[0].RotateInterval.Should().Be(5);
        page.Orders[0].StatusId.Should().Be(6);
        page.Orders[0].ProxyAddress.Should().Be("1.2.3.4:8080:user:pass");
    }

    [Fact]
    public async Task GetProxyOrdersAsync_ShouldMapRotateProxySampleWithCdkFalse()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""
                {
                  "data": [
                    {
                      "code": "ZT3I2L",
                      "order": { "code": "D260522MFBB" },
                      "protocol": "HTTP",
                      "proxy": {
                        "ipaddress": {
                          "domain": "api-proxy-1.homeproxy.vn",
                          "provider": "HOMEPROXY",
                          "categorytype": { "id": 2 },
                          "ip": "180.93.2.171"
                        },
                        "password": "nzk2mdq3nju=",
                        "isCdk": false,
                        "username": "denaondrick24139",
                        "protocol": "HTTP",
                        "port": 3129
                      },
                      "expiredAt": 1779510489542,
                      "status": { "id": 6, "name": "Completed" },
                      "id": 790364
                    }
                  ],
                  "total": 1,
                  "hasNextPage": false
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var page = await api.GetProxyOrdersAsync(new ProxyOrderPageRequest
        {
            CategoryTypeId = 2,
            IsCdk = false
        }, CancellationToken.None);

        Uri.UnescapeDataString(captured!.RequestUri!.Query)
            .Should().Contain("filter=proxy.isCdk:$eq:boolean:false;categorytype.id:$eq:number:2");
        page.Orders[0].IsCdk.Should().BeFalse();
        page.Orders[0].CategoryTypeId.Should().Be(2);
        page.Orders[0].ProxyAddress.Should().Be("180.93.2.171:3129:denaondrick24139:nzk2mdq3nju=");
        page.Orders[0].ProxyDomain.Should().Be("api-proxy-1.homeproxy.vn:3129:denaondrick24139:nzk2mdq3nju=");
    }

    [Theory]
    [InlineData(false, "https://api.homeproxy.vn/api/v1/proxies/12345/generate-token")]
    [InlineData(true, "https://api.homeproxy.vn/api/v2/proxies/12345/generate-token")]
    public async Task GenerateProxyTokenAsync_ShouldUseExpectedApiVersion(bool isCdk, string expectedUri)
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""{"token":"token-value"}""");
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var token = await api.GenerateProxyTokenAsync(12345, isCdk, CancellationToken.None);

        token.Should().Be("token-value");
        captured!.RequestUri!.OriginalString.Should().Be(expectedUri);
    }

    [Fact]
    public async Task RotateProxiesByIdsAsync_ShouldSendUserProxiesIdsBody()
    {
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/proxies/rotate-by-ids");
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.RotateProxiesByIdsAsync([12345, 12346], CancellationToken.None);

        body.Should().Contain("\"userproxiesIds\":[12345,12346]");
    }

    [Fact]
    public async Task RenewProxiesAsync_ShouldSupportRotateCategoryType()
    {
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async _ =>
        {
            body = _.Content is null ? null : await _.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.RenewProxiesAsync([12345], 7, 2, CancellationToken.None);

        body.Should().Contain("\"userProxyIds\":[12345]");
        body.Should().Contain("\"dayOfRenewal\":7");
        body.Should().Contain("\"isRenewal\":false");
        body.Should().Contain("\"categoryTypeId\":2");
        body.Should().NotContain("rotateInterval");
        body.Should().NotContain("isAutoRotate");
    }

    [Fact]
    public async Task RenewProxiesAsync_ShouldSendRotateFieldsWhenProvided()
    {
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/orders/renewal-proxies");
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.RenewProxiesAsync([12345], 7, 2, CancellationToken.None, rotateInterval: 5, isAutoRotate: true);

        body.Should().Contain("\"userProxyIds\":[12345]");
        body.Should().Contain("\"dayOfRenewal\":7");
        body.Should().Contain("\"isRenewal\":false");
        body.Should().Contain("\"categoryTypeId\":2");
        body.Should().Contain("\"rotateInterval\":5");
        body.Should().Contain("\"isAutoRotate\":true");
    }

    [Fact]
    public async Task ChangeRotateProxyInfoAsync_ShouldSendRotateFields()
    {
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/orders/change-info-proxies");
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.ChangeRotateProxyInfoAsync([12345, 12346], "newpass", 10, true, CancellationToken.None);

        body.Should().Contain("\"userProxyIds\":[12345,12346]");
        body.Should().Contain("\"password\":\"newpass\"");
        body.Should().Contain("\"rotateInterval\":10");
        body.Should().Contain("\"isAutoRotate\":true");
    }

    [Fact]
    public void BuildPublicRotateUrl_ShouldUseRegisterDomainBeforeBaseUrl()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("{}")))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions
        {
            RegisterDomain = "app.homeproxy.vn",
            BaseUrl = "https://api.homeproxy.vn/api"
        }), new StubAuthService());

        api.BuildPublicRotateUrl("a token", checkOnly: true)
            .Should().Be("https://app.homeproxy.vn/api/v3/users/rotatev2?token=a%20token&checkOnly=true");
    }

    [Fact]
    public async Task GetProductsAsync_ShouldUseFiltersQueryAndMapProductKind()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""
                {
                  "data": [
                    {
                      "provider": "CMC",
                      "imageUrl": "/api/v1/files/cmc.png",
                      "price": 28000,
                      "originPrice": 47500,
                      "category": {
                        "categorytype": { "id": 1, "name": "Static", "slug": "static" },
                        "unit": "thang",
                        "period": "1",
                        "name": "Proxy datacenter vn",
                        "id": "category-id"
                      },
                      "description": "Proxy Datacenter VN",
                      "name": "Proxy Datacenter VN",
                      "id": "product-id"
                    }
                  ],
                  "total": 1,
                  "hasNextPage": false
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var page = await api.GetProductsAsync(1, CancellationToken.None);

        var query = Uri.UnescapeDataString(captured!.RequestUri!.Query);
        query.Should().Contain("""filters={"category":{"categorytype":{"id":1}}}""");
        page.Total.Should().Be(1);
        page.Products[0].Kind.Should().Be(ProxyProductKind.Datacenter);
        page.Products[0].Category.Id.Should().Be("category-id");
        page.Products[0].Price.Should().Be(28000);
    }

    [Fact]
    public async Task GetDiscountRulesAsync_ShouldMapMinDayAsUnitRange()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/discount-rules");
            return JsonResponse("""
                {
                  "scope": "merchant-id",
                  "rulesByCategory": {
                    "category-id": [
                      { "minDay": 6, "maxDay": 10, "discount": 0.9 },
                      { "minDay": 11, "maxDay": null, "discount": 0.8 }
                    ]
                  }
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        var rules = await api.GetDiscountRulesAsync(CancellationToken.None);

        rules.Scope.Should().Be("merchant-id");
        rules.GetDiscountMultiplier("category-id", 5).Should().Be(1m);
        rules.GetDiscountMultiplier("category-id", 7).Should().Be(0.9m);
        rules.GetDiscountMultiplier("category-id", 30).Should().Be(0.8m);
    }

    [Fact]
    public async Task PurchaseProxyAsync_ShouldSendExpectedOrderBody()
    {
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/orders");
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService());

        await api.PurchaseProxyAsync(new ProxyPurchaseOrder
        {
            Products =
            [
                new ProxyPurchaseProduct
                {
                    IsCdk = false,
                    DayOfUse = 2,
                    RotateInterval = 0,
                    Password = "password9",
                    User = "random",
                    ProtocolType = "HTTP",
                    Provider = "VNPT",
                    Quantity = 1,
                    Product = new ProxyPurchaseProductReference { Id = "product-id" }
                }
            ]
        }, CancellationToken.None);

        body.Should().Contain("\"paymentMethod\":\"WALLET\"");
        body.Should().Contain("\"isCdk\":false");
        body.Should().Contain("\"dayOfUse\":2");
        body.Should().Contain("\"rotateInterval\":0");
        body.Should().Contain("\"password\":\"password9\"");
        body.Should().Contain("\"user\":\"random\"");
        body.Should().Contain("\"protocolType\":\"HTTP\"");
        body.Should().Contain("\"provider\":\"VNPT\"");
        body.Should().Contain("\"quantity\":1");
        body.Should().Contain("\"product\":{\"id\":\"product-id\"}");
        body.Should().NotContain("taxCode");
        body.Should().NotContain("merchantAmount");
        body.Should().NotContain("location");
    }

    [Fact]
    public async Task PurchaseProxyAsync_ShouldUseMerchantEndpoint_WhenCurrentUserIsMerchantRole()
    {
        // Backend rejects POST /orders with 403 for merchant-role (roleId "2") accounts — they
        // must purchase through /orders/merchant instead, same as the resell website does.
        string? requestedUrl = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestedUrl = request.RequestUri!.OriginalString;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var merchantSession = new AuthSession { User = new AuthUser { RoleId = "2" } };
        var api = new HomeProxyOrderApiClient(client, Options.Create(new BackendApiOptions()), new StubAuthService(merchantSession));

        await api.PurchaseProxyAsync(new ProxyPurchaseOrder(), CancellationToken.None);

        requestedUrl.Should().Be("https://api.homeproxy.vn/api/v1/orders/merchant");
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

    /// <summary>Minimal IAuthService test double. Only GetSessionAsync is exercised by
    /// HomeProxyOrderApiClient; every other member is unused by these tests.</summary>
    private sealed class StubAuthService(AuthSession? session = null) : IAuthService
    {
        public Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(session);

        public Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthSession> LoginAsync(string phone, string password, bool rememberSession, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthSession> RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthSession> RegisterAsync(AuthRegisterRequest request, bool rememberSession, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthSession?> RefreshAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthUser?> GetCurrentUserAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthUser> UpdateCurrentUserAsync(AuthUpdateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthSession> ChangePasswordAsync(AuthUpdateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> EnsureValidSessionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LogoutAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public string MaskToken(string token) =>
            throw new NotSupportedException();
    }
}
