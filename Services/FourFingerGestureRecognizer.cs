namespace TapOS.Services;

public sealed class FourFingerGestureRecognizer
{
    private readonly TimeSpan _maximumDuration;
    private DateTimeOffset? _startedAt;
    private int _maximumContactCount;

    public FourFingerGestureRecognizer(TimeSpan maximumDuration)
    {
        if (maximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        _maximumDuration = maximumDuration;
    }

    public int ContactCount { get; private set; }

    public bool Process(int contactCount, DateTimeOffset timestamp)
    {
        if (contactCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contactCount));
        }

        ContactCount = contactCount;
        if (_startedAt is null)
        {
            if (contactCount == 4)
            {
                _startedAt = timestamp;
                _maximumContactCount = contactCount;
            }

            return false;
        }

        _maximumContactCount = Math.Max(_maximumContactCount, contactCount);
        if (contactCount != 0)
        {
            return false;
        }

        var completed = _maximumContactCount == 4 &&
                        timestamp - _startedAt.Value <= _maximumDuration;
        Reset();
        return completed;
    }

    public void Reset()
    {
        _startedAt = null;
        _maximumContactCount = 0;
        ContactCount = 0;
    }
}
