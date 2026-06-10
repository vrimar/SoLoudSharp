using System.Reflection;
using SoLoudSharp;

// Minimal AOT-friendly entry point.
//
// Goal: validate that SoLoudSharp's bindings compile under PublishAot without
// trim/AOT warnings. Soloud_create is not invoked here because the native
// library may not be deployed alongside the AOT executable in all CI lanes;
// the test is purely compile-time + boot-time.

var revision = typeof(SoloudBackend)
    .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(a => a.Key == "SoLoudRevision")
    ?.Value;

Console.WriteLine($"SoLoudSharp AOT sample. Pinned SoLoud revision: {revision ?? "(unknown)"}.");

// Exercise a handful of value-type APIs to keep them rooted under trimming.
var backend = SoloudBackend.MiniAudio;
var flags = SoloudInitFlags.ClipRoundoff | SoloudInitFlags.EnableVisualization;
Console.WriteLine($"Default backend: {backend} ({(uint)backend}); flags: {flags} ({(uint)flags}).");

return 0;
