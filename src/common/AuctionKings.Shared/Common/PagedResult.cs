namespace AuctionKings.Shared.Common;

/// <summary>
/// Generic paginated result wrapper. Pagination is 1-based.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedResult(IReadOnlyCollection<T> items, int totalCount, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be at least 1.");
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalCount),
                "TotalCount cannot be negative."
            );
        }

        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public static PagedResult<T> Empty(int page = 1, int pageSize = 20) =>
        new([], 0, page, pageSize);
}
