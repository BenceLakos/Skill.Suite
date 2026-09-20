namespace Skill.Suite.Application.Competitors;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// Decides which competitor, if any, is sitting at the address a request arrived from.
/// </summary>
/// <remarks>
/// Comparison is on the parsed address rather than on the string, because the two sides are written by
/// different authors: a competitor's workstation address is typed into a form by an expert, while the request's
/// address is produced by the network stack — which hands out an IPv4-mapped IPv6 form
/// (<c>::ffff:10.0.0.5</c>) for an IPv4 client on a dual-stack socket. A string comparison misses every one of
/// those, silently, and the page it feeds simply never pre-fills.
/// <para>
/// Nothing here is done in SQL for the same reason: the canonical form of an address is not something a
/// <c>WHERE</c> clause can compute, and a competition has tens of competitors, not millions.
/// </para>
/// </remarks>
internal static class WorkstationMatch
{
    /// <summary>
    /// The address as a comparison can use it, or null when the value is absent or not an address.
    /// </summary>
    /// <remarks>
    /// An unparseable value is deliberately not an error. The stored side is validated on write, so the only
    /// way to reach this with rubbish is a row written before that validation or a header from a client, and
    /// "no match" is the right answer to both.
    /// </remarks>
    public static IPAddress? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return IPAddress.TryParse(value.Trim(), out var address) ? Normalize(address) : null;
    }

    public static bool IsSameWorkstation(IPAddress? client, string? storedAddress)
    {
        if (client is null)
            return false;

        // Both sides are canonicalised here rather than trusting the caller to have done it: the address on
        // the request comes straight off the connection, and the two forms this collapses are exactly the
        // ones that arrive that way.
        var normalizedClient = Normalize(client);

        var stored = Parse(storedAddress);
        if (stored is null)
            return false;

        // Loopback is the one equivalence worth stating: a browser on the same machine arrives as ::1 while
        // the competitor row says 127.0.0.1, and both mean "this very host" — which is how a rehearsal on a
        // single machine is run.
        if (IPAddress.IsLoopback(normalizedClient) && IPAddress.IsLoopback(stored))
            return true;

        return normalizedClient.Equals(stored);
    }

    /// <summary>
    /// The one form of an address that two spellings of the same machine both reduce to.
    /// </summary>
    /// <remarks>
    /// Two reductions, both of which <see cref="IPAddress.Equals"/> would otherwise treat as different hosts:
    /// an IPv4 client seen through a dual-stack socket arrives as <c>::ffff:10.0.0.5</c>, and an IPv6 address
    /// can carry a scope id, which names the interface it was seen on rather than the host it belongs to.
    /// </remarks>
    private static IPAddress Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4();

        return address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0
            ? new IPAddress(address.GetAddressBytes())
            : address;
    }

    /// <summary>
    /// Positions in <paramref name="storedAddresses"/> that are the same workstation as <paramref name="client"/>.
    /// </summary>
    /// <remarks>
    /// Every match is returned rather than the first, because more than one is a meaningful answer: two
    /// competitors recorded at one address is a data-entry mistake, and the caller has to be able to refuse
    /// instead of handing one competitor's credentials to the other's machine.
    /// </remarks>
    public static List<int> MatchIndexes(IPAddress? client, IReadOnlyList<string> storedAddresses)
    {
        var matches = new List<int>();

        for (var index = 0; index < storedAddresses.Count; index++)
        {
            if (IsSameWorkstation(client, storedAddresses[index]))
                matches.Add(index);
        }

        return matches;
    }
}
