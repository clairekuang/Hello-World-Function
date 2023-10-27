using Objects;
using Objects.Geometry;
using Speckle.Automate.Sdk;
using Speckle.Core.Logging;
using Speckle.Core.Models.Extensions;
using Speckle.Core.Models;
using System.Runtime.CompilerServices;

static class AutomateFunction
{
  public static async Task Run(
    AutomationContext automationContext,
    FunctionInputs functionInputs
  )
  {
    Console.WriteLine("Starting execution");
    _ = typeof(ObjectsKit).Assembly; // INFO: Force objects kit to initialize

    Console.WriteLine("Receiving version");
    Base? commitObject = await automationContext.ReceiveVersion();

    Console.WriteLine("Received version: " + commitObject);

    // flatten the received objects and filter by displayable objects with valid ids
    List<Base> displayableObjects = commitObject.Flatten().Where(o => o.IsDisplayableObject() && !string.IsNullOrEmpty(o.id)).ToList();
    if (!displayableObjects.Any())
    {
      automationContext.MarkRunFailed("No displayable objects with valid ids found.");
      return;
    }
    Console.WriteLine($"Found {displayableObjects.Count()} displayable objects.");

    // store the density check result of each object
    Dictionary<string, double> densityThresholdDict = new();
    foreach (Base displayable in displayableObjects)
    {
      if (!densityThresholdDict.ContainsKey(displayable.id))
      {
        double avgDensity = TestDensityThreshold(displayable);
        densityThresholdDict.Add(displayable.id, avgDensity);
      }
    }
    foreach (var entry in densityThresholdDict)
    {
      Console.WriteLine($"Object {entry.Key} has average density of {entry.Value}.");
    }

    // flag any failed objects in the commit, and create a new commit
    foreach (Base @base in displayableObjects)
    {
      if (@base.id != null && densityThresholdDict.ContainsKey(@base.id))
      {
        double avgDensity = densityThresholdDict[@base.id];
        if (avgDensity > functionInputs.DensityThreshold)
        {
          automationContext.AttachErrorToObjects(
            "",
            new[] { @base.id },
            $"This object with average density of {avgDensity} exceeded threshold."
          );
        }
      }
    }

    // test for automation failure
    int failedCount = densityThresholdDict.Where(o => o.Value >= functionInputs.DensityThreshold).Count();
    double highDensityValue = failedCount / displayableObjects.Count();
    if (highDensityValue > functionInputs.HighDensityObjectLimit)
    {
      automationContext.MarkRunFailed($"Exceeded high density object limit with a value of {highDensityValue}");
      return;
    }

    automationContext.MarkRunSuccess($"Created new density commit objects");
    return;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <param name="base"></param>
  /// <returns></returns>
  private static double TestDensityThreshold(Base @base)
  {
    IEnumerable<Base>? displayValues = @base.TryGetDisplayValue();
    double totalDensity = 0;
    if (displayValues != null)
    {
      foreach (Base displayValue in displayValues)
      {
        totalDensity += ComputeDensity(displayValue);
      }
    }
    return displayValues != null || displayValues.Count() != 0 ? totalDensity/displayValues.Count() : 0;
  }
  /// <summary>
  /// Computes the density of a base, defined as number of faces divided by area (mesh) or number of segments divided by length (polyline)
  /// </summary>
  /// <param name="base">A mesh or polyline</param>
  /// <returns>The density of the base, or 0 if area or length was missing or base was some other type</returns>
  private static double ComputeDensity(Base @base)
  {
    switch (@base)
    {
      case Mesh o:
        return ComputeMeshDensity(o);

      case Polyline o:
        return ComputePolylineDensity(o);

      default:
        return 0;
    }
  }

  /// <summary>
  /// Computes the density of a mesh, defined as number of faces divided by area
  /// </summary>
  /// <param name="bases"></param>
  /// <returns>The density of the mesh, or 0 if mesh had no area </returns>
  private static double ComputeMeshDensity(Mesh mesh)
  {
    // calculate number of mesh faces
    var i = 0;
    int count = 0;
    while (i < mesh.faces.Count)
    {
      var n = mesh.faces[i];
      if (n < 3) n += 3; // 0 -> 3, 1 -> 4 to preserve backwards compatibility

      count++;
      i += n + 1;
    }

    // return density or 0 if area doesn't exist
    return mesh.area != 0 ? count / mesh.area : 0;
  }

  /// <summary>
  /// Computes the density of a polyline, defined as number of segments divided by length (polyline)
  /// </summary>
  /// <param name="line"></param>
  /// <returns>The density of the polyline, or 0 if polyline had no length</returns>
  private static double ComputePolylineDensity(Polyline polyline)
  {
    // calculate the number of segments
    int count = ( polyline.value.Count / 3 ) - 1;

    return polyline.length != 0 ? count / polyline.length : 0;
  }

  /*
  private static async int AlternativeFunction(
    Base commitBase,
    FunctionInputs functionInputs
  )
  {
    // count the number of walls in your commit
    // calculate total volume
  }
  */

}
