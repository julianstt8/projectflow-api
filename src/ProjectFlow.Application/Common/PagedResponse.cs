namespace ProjectFlow.Application.Common;

/// <summary>One page of results plus what a client needs to page through the rest.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public const int MaxPageSize = 100;

    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;
}
