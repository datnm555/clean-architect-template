namespace SharedKernel;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetAtomicValues();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;
        using var thisEnumerator = GetAtomicValues().GetEnumerator();
        using var otherEnumerator = other.GetAtomicValues().GetEnumerator();

        while (thisEnumerator.MoveNext() && otherEnumerator.MoveNext())
        {
            if (thisEnumerator.Current is null)
            {
                if (otherEnumerator.Current is not null)
                {
                    return false;
                }
            }
            else if (!thisEnumerator.Current.Equals(otherEnumerator.Current))
            {
                return false;
            }
        }

        return !thisEnumerator.MoveNext() && !otherEnumerator.MoveNext();
    }

    public override int GetHashCode() =>
        GetAtomicValues()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate(0, (x, y) => (x * 397) ^ y);

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
