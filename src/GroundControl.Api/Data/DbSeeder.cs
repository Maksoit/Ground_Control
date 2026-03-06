using GroundControl.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GroundControl.Api.Data;

/// <summary>Seeds the database with a sample airport taxiway graph on first run.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(GroundDbContext db)
    {
        if (await db.Nodes.AnyAsync())
            return;

        // Sample airport graph:
        // Nodes: Parking spots (P-1..P-4), Taxiway intersections (T-1..T-4),
        //        Runway (RW-1), Gate holding areas (G-1, G-2)
        var nodes = new List<NodeEntity>
        {
            new() { NodeId = "P-1",  X = 0,   Y = 0,   NodeType = "parking" },
            new() { NodeId = "P-2",  X = 0,   Y = 100, NodeType = "parking" },
            new() { NodeId = "P-3",  X = 0,   Y = 200, NodeType = "parking" },
            new() { NodeId = "P-4",  X = 0,   Y = 300, NodeType = "parking" },
            new() { NodeId = "T-1",  X = 150, Y = 0,   NodeType = "taxiway" },
            new() { NodeId = "T-2",  X = 150, Y = 100, NodeType = "taxiway" },
            new() { NodeId = "T-3",  X = 150, Y = 200, NodeType = "taxiway" },
            new() { NodeId = "T-4",  X = 150, Y = 300, NodeType = "taxiway" },
            new() { NodeId = "G-1",  X = 300, Y = 50,  NodeType = "gate" },
            new() { NodeId = "G-2",  X = 300, Y = 250, NodeType = "gate" },
            new() { NodeId = "RW-1", X = 450, Y = 150, NodeType = "runway" },
        };

        // Bidirectional edges (each direction gets its own edge)
        var edges = new List<EdgeEntity>
        {
            // P-1 <-> T-1
            new() { EdgeId = "E-P1-T1",  FromNode = "P-1",  ToNode = "T-1",  Length = 150 },
            new() { EdgeId = "E-T1-P1",  FromNode = "T-1",  ToNode = "P-1",  Length = 150 },
            // P-2 <-> T-2
            new() { EdgeId = "E-P2-T2",  FromNode = "P-2",  ToNode = "T-2",  Length = 150 },
            new() { EdgeId = "E-T2-P2",  FromNode = "T-2",  ToNode = "P-2",  Length = 150 },
            // P-3 <-> T-3
            new() { EdgeId = "E-P3-T3",  FromNode = "P-3",  ToNode = "T-3",  Length = 150 },
            new() { EdgeId = "E-T3-P3",  FromNode = "T-3",  ToNode = "P-3",  Length = 150 },
            // P-4 <-> T-4
            new() { EdgeId = "E-P4-T4",  FromNode = "P-4",  ToNode = "T-4",  Length = 150 },
            new() { EdgeId = "E-T4-P4",  FromNode = "T-4",  ToNode = "P-4",  Length = 150 },
            // T-1 <-> T-2
            new() { EdgeId = "E-T1-T2",  FromNode = "T-1",  ToNode = "T-2",  Length = 100 },
            new() { EdgeId = "E-T2-T1",  FromNode = "T-2",  ToNode = "T-1",  Length = 100 },
            // T-2 <-> T-3
            new() { EdgeId = "E-T2-T3",  FromNode = "T-2",  ToNode = "T-3",  Length = 100 },
            new() { EdgeId = "E-T3-T2",  FromNode = "T-3",  ToNode = "T-2",  Length = 100 },
            // T-3 <-> T-4
            new() { EdgeId = "E-T3-T4",  FromNode = "T-3",  ToNode = "T-4",  Length = 100 },
            new() { EdgeId = "E-T4-T3",  FromNode = "T-4",  ToNode = "T-3",  Length = 100 },
            // T-1 <-> G-1
            new() { EdgeId = "E-T1-G1",  FromNode = "T-1",  ToNode = "G-1",  Length = 180 },
            new() { EdgeId = "E-G1-T1",  FromNode = "G-1",  ToNode = "T-1",  Length = 180 },
            // T-2 <-> G-1
            new() { EdgeId = "E-T2-G1",  FromNode = "T-2",  ToNode = "G-1",  Length = 160 },
            new() { EdgeId = "E-G1-T2",  FromNode = "G-1",  ToNode = "T-2",  Length = 160 },
            // T-3 <-> G-2
            new() { EdgeId = "E-T3-G2",  FromNode = "T-3",  ToNode = "G-2",  Length = 160 },
            new() { EdgeId = "E-G2-T3",  FromNode = "G-2",  ToNode = "T-3",  Length = 160 },
            // T-4 <-> G-2
            new() { EdgeId = "E-T4-G2",  FromNode = "T-4",  ToNode = "G-2",  Length = 180 },
            new() { EdgeId = "E-G2-T4",  FromNode = "G-2",  ToNode = "T-4",  Length = 180 },
            // G-1 <-> RW-1
            new() { EdgeId = "E-G1-RW1", FromNode = "G-1",  ToNode = "RW-1", Length = 200 },
            new() { EdgeId = "E-RW1-G1", FromNode = "RW-1", ToNode = "G-1",  Length = 200 },
            // G-2 <-> RW-1
            new() { EdgeId = "E-G2-RW1", FromNode = "G-2",  ToNode = "RW-1", Length = 200 },
            new() { EdgeId = "E-RW1-G2", FromNode = "RW-1", ToNode = "G-2",  Length = 200 },
        };

        await db.Nodes.AddRangeAsync(nodes);
        await db.Edges.AddRangeAsync(edges);
        await db.SaveChangesAsync();
    }
}
