// top-of-file comment (before usings)
using System;
// comment between usings
using System.Collections.Generic;
/* block comment between usings */
using System.Threading;

namespace TestProject.App.Helpers;

/// <summary>
/// Fixture for FileStructureService usings-block tests.
/// Has comments both before the first using and between usings
/// to verify that the reported range spans first..last using regardless.
/// </summary>
public class FileStructureUsingsFixture
{
    public List<string> Items { get; } = new();
    public CancellationToken Token { get; init; }
}
