public sealed record CreateKeyRequest(string Plan, string? Role = null);
public sealed record RevokeKeyRequest(string Key);
public sealed record AdminKeyView(string Key, string Plan, string Role, string? HardwareId, DateTime? ActivatedAtUtc, DateTime ExpiresAtUtc, bool Revoked);
