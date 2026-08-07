namespace FikenCli;

public interface IFikenRequestPolicy
{
    void Authorize(HttpMethod method, string relativePath);
}

public sealed class AllowAllFikenRequestPolicy : IFikenRequestPolicy
{
    public static AllowAllFikenRequestPolicy Instance { get; } = new();

    private AllowAllFikenRequestPolicy()
    {
    }

    public void Authorize(HttpMethod method, string relativePath)
    {
    }
}

public sealed class FikenHttpMethodPolicy(params HttpMethod[] allowedMethods) : IFikenRequestPolicy
{
    private readonly HashSet<string> _allowedMethods = allowedMethods
        .Select(method => method.Method)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Authorize(HttpMethod method, string relativePath)
    {
        if (!_allowedMethods.Contains(method.Method))
        {
            throw new FikenPolicyException($"HTTP method {method.Method} is not permitted for this operation.");
        }
    }
}

public sealed class FikenPolicyException(string message) : Exception(message);
