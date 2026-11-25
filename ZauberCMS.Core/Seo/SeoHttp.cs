using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Seo;

/// <summary>
/// The HTTP client the SEO checks use to fetch pages and links.
/// The address comes from an admin (or from links on the fetched page), so the client must not become a way to
/// reach services that only the server can see: other machines on the private network, cloud metadata endpoints,
/// or ports on the server itself. Every connection (including each redirect hop) is checked after DNS resolution
/// and refused unless the address is public. Two exceptions make checking the site itself possible:
/// the host of <c>Zauber:SiteUrl</c>, and the ports this application listens on at a loopback address.
/// </summary>
public static class SeoHttp
{
    /// <summary>Name of the client: <c>IHttpClientFactory.CreateClient(SeoHttp.ClientName)</c>.</summary>
    public const string ClientName = "ZauberCMS.Seo";

    /// <summary>Largest response body that is read.</summary>
    public const int MaxResponseBytes = 5 * 1024 * 1024;

    internal static void AddSeoHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient(ClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.MaxResponseContentBufferSize = MaxResponseBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(serviceProvider => new SocketsHttpHandler
            {
                // A proxy would make every connection look like a connection to the proxy.
                UseProxy = false,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectCallback = (context, cancellationToken) =>
                    ConnectAsync(serviceProvider, context.DnsEndPoint, cancellationToken)
            });
    }

    /// <summary>
    /// True for an absolute http or https address. Use it to give the user a clear message before a request is
    /// made; whether the host may be contacted is decided when the connection is opened.
    /// </summary>
    public static bool IsHttpUrl(string? url, out Uri? uri) =>
        Uri.TryCreate(url, UriKind.Absolute, out uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>True when the address is routable on the public internet.</summary>
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] switch
            {
                0 or 10 or 127 => false,                      // "this" network, private, loopback
                100 when b[1] is >= 64 and <= 127 => false,  // carrier-grade NAT
                169 when b[1] == 254 => false,               // link-local, cloud metadata
                172 when b[1] is >= 16 and <= 31 => false,   // private
                192 when b[1] == 168 => false,               // private
                192 when b[1] == 0 && b[2] == 0 => false,    // protocol assignments
                198 when b[1] is 18 or 19 => false,          // benchmarking
                >= 224 => false,                             // multicast, reserved, broadcast
                _ => true
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            var isNat64 = b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b; // 64:ff9b::/96 wraps an IPv4 address
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal ||
                     address.IsIPv6Multicast || isNat64);
        }

        return false;
    }

    /// <summary>The check behind every connection of the SEO client.</summary>
    internal static bool IsAllowedTarget(string host, IPAddress address, int port, string? siteUrl,
        IReadOnlyCollection<int> ownPorts)
    {
        if (IsPublicAddress(address))
        {
            return true;
        }

        // The site's own configured address may be private (an intranet site).
        if (Uri.TryCreate(siteUrl, UriKind.Absolute, out var site) &&
            string.Equals(site.IdnHost, host, StringComparison.OrdinalIgnoreCase) && site.Port == port)
        {
            return true;
        }

        // This application itself, as reached on a development machine (https://localhost:5001).
        return IPAddress.IsLoopback(address) && ownPorts.Contains(port);
    }

    private static async ValueTask<Stream> ConnectAsync(IServiceProvider serviceProvider, DnsEndPoint endPoint,
        CancellationToken cancellationToken)
    {
        var siteUrl = serviceProvider.GetService<IOptions<ZauberSettings>>()?.Value.SiteUrl;
        var ownPorts = OwnPorts(serviceProvider);

        var resolved = IPAddress.TryParse(endPoint.Host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken);
        var allowed = resolved
            .Where(address => IsAllowedTarget(endPoint.Host, address, endPoint.Port, siteUrl, ownPorts))
            .ToArray();
        if (allowed.Length == 0)
        {
            throw new HttpRequestException(
                $"'{endPoint.Host}' is not a public address, so it is not checked. " +
                "To check this site on a private address, set Zauber:SiteUrl.");
        }

        // Connect to the addresses that were checked, not to the name: a second lookup could answer differently.
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, endPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static int[] OwnPorts(IServiceProvider serviceProvider)
    {
        var addresses = serviceProvider.GetService<IServer>()?.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses == null)
        {
            return [];
        }

        var ports = new List<int>();
        foreach (var address in addresses)
        {
            try
            {
                ports.Add(BindingAddress.Parse(address).Port);
            }
            catch (FormatException)
            {
                // Not a TCP address (a named pipe or socket path): nothing to allow.
            }
        }

        return ports.ToArray();
    }
}
