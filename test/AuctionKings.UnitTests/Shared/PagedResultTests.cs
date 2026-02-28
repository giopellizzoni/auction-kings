using AuctionKings.Shared.Common;

namespace AuctionKings.UnitTests.Shared;

public class PagedResultTests
{
    [Fact]
    public void PagedResult_ShouldCalculatePaginationCorrectly()
    {
        // 25 items, page 2 of 3 (pageSize=10)
        var items = Enumerable.Range(1, 10).ToList();
        var result = new PagedResult<int>(items, totalCount: 25, page: 2, pageSize: 10);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void PagedResult_FirstPage_ShouldNotHavePreviousPage()
    {
        var result = new PagedResult<int>([], totalCount: 50, page: 1, pageSize: 10);

        Assert.False(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void PagedResult_LastPage_ShouldNotHaveNextPage()
    {
        var result = new PagedResult<int>([], totalCount: 20, page: 2, pageSize: 10);

        Assert.True(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void PagedResult_SinglePage_ShouldHaveNoPrevOrNext()
    {
        var result = new PagedResult<string>([], totalCount: 5, page: 1, pageSize: 10);

        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void PagedResult_Empty_ShouldReturnZeroTotalPages()
    {
        var result = PagedResult<int>.Empty();

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void PagedResult_InvalidPage_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PagedResult<int>([], totalCount: 0, page: 0, pageSize: 10));
    }

    [Fact]
    public void PagedResult_InvalidPageSize_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PagedResult<int>([], totalCount: 0, page: 1, pageSize: 0));
    }

    [Fact]
    public void PagedResult_NegativeTotalCount_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PagedResult<int>([], totalCount: -1, page: 1, pageSize: 10));
    }
}
