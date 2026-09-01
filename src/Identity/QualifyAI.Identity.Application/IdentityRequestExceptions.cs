namespace QualifyAI.Identity.Application;

public sealed class IdentityValidationException : Exception
{
    public IdentityValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(string.Join("; ", errors.Values.SelectMany(x => x)))
    {
        Errors = errors;
    }

    public IdentityValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class IdentityConflictException(string message) : Exception(message);
