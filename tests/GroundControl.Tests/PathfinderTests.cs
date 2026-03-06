using GroundControl.Core.Models;
using GroundControl.Core.Services;
using FluentAssertions;
using Xunit;

namespace GroundControl.Tests;

public class PathfinderTests
{
    private readonly DijkstraPathfinder _pathfinder;

    public PathfinderTests()
    {
        _pathfinder = new DijkstraPathfinder();
    }

    [Fact]
    public void FindPath_ShouldReturnEmptyPath_WhenFromAndToAreSame()
    {
        // Arrange
        var edges = new List<Edge>();
        
        // Act
        var result = _pathfinder.FindPath("A", "A", edges);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindPath_ShouldReturnNull_WhenNoPathExists()
    {
        // Arrange
        var edges = new List<Edge>
        {
            new Edge { EdgeId = "E1", FromNode = "A", ToNode = "B", Length = 1 },
            new Edge { EdgeId = "E2", FromNode = "C", ToNode = "D", Length = 1 }
        };
        
        // Act
        var result = _pathfinder.FindPath("A", "D", edges);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FindPath_ShouldReturnDirectPath_WhenDirectEdgeExists()
    {
        // Arrange
        var edges = new List<Edge>
        {
            new Edge { EdgeId = "E1", FromNode = "A", ToNode = "B", Length = 1 }
        };
        
        // Act
        var result = _pathfinder.FindPath("A", "B", edges);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].EdgeId.Should().Be("E1");
        result[0].FromNode.Should().Be("A");
        result[0].ToNode.Should().Be("B");
    }

    [Fact]
    public void FindPath_ShouldReturnShortestPath_WhenMultiplePathsExist()
    {
        // Arrange
        var edges = new List<Edge>
        {
            new Edge { EdgeId = "E1", FromNode = "A", ToNode = "B", Length = 1 },
            new Edge { EdgeId = "E2", FromNode = "B", ToNode = "C", Length = 1 },
            new Edge { EdgeId = "E3", FromNode = "A", ToNode = "D", Length = 5 },
            new Edge { EdgeId = "E4", FromNode = "D", ToNode = "C", Length = 5 }
        };
        
        // Act
        var result = _pathfinder.FindPath("A", "C", edges);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].EdgeId.Should().Be("E1");
        result[1].EdgeId.Should().Be("E2");
    }

    [Fact]
    public void FindPath_ShouldReturnNull_WhenFromNodeDoesNotExist()
    {
        // Arrange
        var edges = new List<Edge>
        {
            new Edge { EdgeId = "E1", FromNode = "A", ToNode = "B", Length = 1 }
        };
        
        // Act
        var result = _pathfinder.FindPath("Z", "B", edges);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FindPath_ShouldHandleComplexGraph()
    {
        // Arrange
        var edges = new List<Edge>
        {
            new Edge { EdgeId = "E1", FromNode = "A", ToNode = "B", Length = 4 },
            new Edge { EdgeId = "E2", FromNode = "A", ToNode = "C", Length = 2 },
            new Edge { EdgeId = "E3", FromNode = "B", ToNode = "D", Length = 5 },
            new Edge { EdgeId = "E4", FromNode = "C", ToNode = "B", Length = 1 },
            new Edge { EdgeId = "E5", FromNode = "C", ToNode = "D", Length = 8 },
            new Edge { EdgeId = "E6", FromNode = "D", ToNode = "E", Length = 3 }
        };
        
        // Act
        var result = _pathfinder.FindPath("A", "E", edges);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        // Path should be A->C->B->D->E (total: 2+1+5+3=11)
        result![0].EdgeId.Should().Be("E2"); // A->C
        result[1].EdgeId.Should().Be("E4"); // C->B
        result[2].EdgeId.Should().Be("E3"); // B->D
        result[3].EdgeId.Should().Be("E6"); // D->E
    }
}