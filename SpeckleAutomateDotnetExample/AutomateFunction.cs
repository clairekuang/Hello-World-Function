using Objects;
using Objects.Geometry;
using Speckle.Automate.Sdk;
using Speckle.Core.Logging;
using Speckle.Core.Models.Extensions;
using Speckle.Core.Models;
using System.Runtime.CompilerServices;
using Objects.Other;

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
    List<Base> displayableObjects = commitObject
      .Flatten()
      .Where(o => o.IsDisplayableObject() && !string.IsNullOrEmpty(o.id))
      .ToList();
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
        double avgDensity = GetAverageDensity(displayable);
        densityThresholdDict.Add(displayable.id, avgDensity);
      }
    }
    foreach (var entry in densityThresholdDict)
    {
      Console.WriteLine($"Object {entry.Key} has average density of {entry.Value}.");
    }

    // flag any failed objects in the commit, and set their display to red
    var failedMat = new RenderMaterial();
    failedMat.diffuseColor = System.Drawing.Color.Red;
    var succeededMat = new RenderMaterial();
    succeededMat.diffuseColor = System.Drawing.Color.Gray;
    succeededMat.opacity = 0.5;
    for (int i = 0; i < displayableObjects.Count(); i++)
    {
      var @base = displayableObjects[i];
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

          displayableObjects[i] = ColorizeDisplay(@base, failedMat);
        }
        else
        {
          displayableObjects[i] = ColorizeDisplay(@base, succeededMat);
        }
      }
    }

    // test for automation failure
    int failedCount = densityThresholdDict
      .Where(o => o.Value > functionInputs.DensityThreshold)
      .Count();
    double highDensityValue =
      Convert.ToDouble(failedCount) / Convert.ToDouble(displayableObjects.Count());
    Console.WriteLine($"High density % is {highDensityValue}.");
    if (highDensityValue > functionInputs.HighDensityObjectLimit)
    {
      automationContext.MarkRunFailed(
        $"Exceeded high density object limit with a value of {highDensityValue}"
      );
    }
    else
    {
      automationContext.MarkRunSuccess($"Finished density check.");
    }

    // Extra: create a new commit with the display objects
    Collection newCommit = new();
    newCommit.elements = displayableObjects;
    newCommit.name = "Density Check Report";
    string branch = "automations";
    var result = await automationContext.CreateNewVersionInProject(
      newCommit,
      branch,
      "Created density checker report"
    );
    Console.WriteLine($"Created new commit in branch: {branch}");
  }

  /// <summary>
  /// Adds a red render material to the base display objects
  /// </summary>
  /// <param name="base"></param>
  /// <returns></returns>
  private static Base ColorizeDisplay(Base @base, RenderMaterial mat)
  {
    List<Mesh> displayValues = @base.TryGetDisplayValue()?.Cast<Mesh>()?.ToList();
    if (displayValues != null)
    {
      foreach (Mesh display in displayValues)
      {
        display["renderMaterial"] = mat;
      }
      @base["displayValue"] = displayValues;
    }
    return @base;
  }

  /// <summary>
  /// Calculate the average density of the base
  /// </summary>
  /// <param name="base"></param>
  /// <returns></returns>
  private static double GetAverageDensity(Base @base)
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
    return displayValues != null || displayValues.Count() != 0
      ? totalDensity / displayValues.Count()
      : 0;
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

      default:
        return 0;
    }
  }

  /// <summary>
  /// Computes the density of a mesh, defined as number of faces divided by area
  /// </summary>
  /// <param name="mesh"></param>
  /// <returns>The density of the mesh by area, or total edge length if area was 0</returns>
  private static double ComputeMeshDensity(Mesh mesh)
  {
    // calculate number of mesh faces and total edge length
    var i = 0;
    int count = 0;
    double edgeLength = 0;
    List<Point> vertices = mesh.GetPoints();
    while (i < mesh.faces.Count)
    {
      var n = mesh.faces[i];
      if (n < 3)
        n += 3; // 0 -> 3, 1 -> 4 to preserve backwards compatibility

      // calculate edge length
      for (int j = 1; j < n; j++)
      {
        edgeLength += vertices[mesh.faces[n + j]].DistanceTo(
          vertices[mesh.faces[n + j + 1]]
        );
        if (j == n - 1)
        {
          edgeLength += vertices[mesh.faces[n + j + 1]].DistanceTo(
            vertices[mesh.faces[n + 1]]
          );
        }
      }

      count++;
      i += n + 1;
    }

    // return density
    double area = mesh.area is 0 ? edgeLength : mesh.area;
    return count / area;
  }
}
