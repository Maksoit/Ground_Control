using GroundControl.Api.Models;
using GroundControl.Api.Services;
using Xunit;

namespace GroundControl.Tests;

public class PathfinderServiceTests
{
    private readonly IPathfinderService _sut = new PathfinderService();

    private static List<EdgeEntity> SimpleGraph() => new()
    {
        new EdgeEntity { EdgeId = "E-A-B", FromNode = "A", ToNode = "B", Length = 10 },
        new EdgeEntity { EdgeId = "E-B-A", FromNode = "B", ToNode = "A", Length = 10 },
        new EdgeEntity { EdgeId = "E-B-C", FromNode = "B", ToNode = "C", Length = 20 },
        new EdgeEntity { EdgeId = "E-C-B", FromNode = "C", ToNode = "B", Length = 20 },
        new EdgeEntity { EdgeId = "E-A-C", FromNode = "A", ToNode = "C", Length = 50 },
        new EdgeEntity { EdgeId = "E-C-A", FromNode = "C", ToNode = "A", Length = 50 },
    };

    [Fact]
    public void FindPath_DirectEdge_ReturnsSingleEdge()
    {
        var path = _sut.FindPath("A", "B", SimpleGraph());

        Assert.NotNull(path);
        Assert.Single(path);
        Assert.Equal("E-A-B", path[0].EdgeId);
    }

    [Fact]
    public void FindPath_ShortestPath_PrefersTwoStepsOverLongDirect()
    {
        // A->B->C costs 30, A->C direct costs 50
        var path = _sut.FindPath("A", "C", SimpleGraph());

        Assert.NotNull(path);
        Assert.Equal(2, path.Count);
        Assert.Equal("E-A-B", path[0].EdgeId);
        Assert.Equal("E-B-C", path[1].EdgeId);
    }

    [Fact]
    public void FindPath_SameNode_ReturnsEmptyList()
    {
        var path = _sut.FindPath("A", "A", SimpleGraph());

        Assert.NotNull(path);
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_Disconnected_ReturnsNull()
    {
        var edges = new List<EdgeEntity>
        {
            new() { EdgeId = "E-A-B", FromNode = "A", ToNode = "B", Length = 10 },
        };

        var path = _sut.FindPath("A", "Z", edges);

        Assert.Null(path);
    }

    [Fact]
    public void FindPath_EmptyGraph_ReturnsNull()
    {
        var path = _sut.FindPath("A", "B", new List<EdgeEntity>());

        Assert.Null(path);
    }

    [Fact]
    public void FindPath_ReturnsCorrectEdgeOrder()
    {
        // Chain: A -> B -> C -> D
        var edges = new List<EdgeEntity>
        {
            new() { EdgeId = "E-A-B", FromNode = "A", ToNode = "B", Length = 1 },
            new() { EdgeId = "E-B-C", FromNode = "B", ToNode = "C", Length = 1 },
            new() { EdgeId = "E-C-D", FromNode = "C", ToNode = "D", Length = 1 },
        };

        var path = _sut.FindPath("A", "D", edges);

        Assert.NotNull(path);
        Assert.Equal(3, path.Count);
        Assert.Equal("E-A-B", path[0].EdgeId);
        Assert.Equal("E-B-C", path[1].EdgeId);
        Assert.Equal("E-C-D", path[2].EdgeId);
    }
}
