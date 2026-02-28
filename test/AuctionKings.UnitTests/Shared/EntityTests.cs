using AuctionKings.Shared.Common;

namespace AuctionKings.UnitTests.Shared;

public class EntityTests
{
    private sealed class TestEntity : Entity
    {
        public TestEntity(Guid id)
            : base(id) { }
    }

    [Fact]
    public void Entity_WithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id);
        var b = new TestEntity(id);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Entity_WithDifferentId_ShouldNotBeEqual()
    {
        var a = new TestEntity(Guid.NewGuid());
        var b = new TestEntity(Guid.NewGuid());

        Assert.NotEqual(a, b);
        Assert.True(a != b);
        Assert.False(a == b);
    }

    [Fact]
    public void Entity_WithEmptyGuid_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new TestEntity(Guid.Empty));
    }

    [Fact]
    public void Entity_HashCode_ShouldMatchIdHashCode()
    {
        var id = Guid.NewGuid();
        var entity = new TestEntity(id);

        Assert.Equal(id.GetHashCode(), entity.GetHashCode());
    }

    [Fact]
    public void Entity_EqualsNull_ShouldReturnFalse()
    {
        var entity = new TestEntity(Guid.NewGuid());

        Assert.False(entity.Equals(null));
        Assert.False(entity == null);
    }
}
