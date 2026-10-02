using System.Security.Cryptography;
using System.Text;

namespace Ch16.NorthwindAssist.Api.Deployment;

/// <summary>
/// Assigns a share of conversations to a canary version (Chapter 16.4). Conversations, not
/// requests, are assigned, so a conversation never switches versions halfway through.
/// </summary>
public sealed class CanaryAssignment(int canaryPercent)
{
    public bool IsCanary(string conversationId)
    {
        // A stable hash: the same conversation always gets the same assignment, on every server.
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(conversationId));
        return BitConverter.ToUInt32(hash, 0) % 100 < canaryPercent;
    }
}
