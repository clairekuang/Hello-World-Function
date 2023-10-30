using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

/// <summary>
/// This class describes the user specified variables that the function wants to work with.
/// </summary>
/// This class is used to generate a JSON Schema to ensure that the user provided values
/// are valid and match the required schema.
struct FunctionInputs
{
  /// <summary>
  /// Set a density value as the threshold.
  /// </summary>
  /// <remarks>
  /// Objects with densities exceeding this value will be highlighted.
  /// </remarks>
  [Required]
  [DisplayName("Density threshold")]
  [Description("As unit of vertices divided by total edge length")]
  public double DensityThreshold { get; set; }

  /// <summary>
  /// THe maximum percentage of objects to allow that exceed the DensityThreshold.
  /// </summary>
  /// <remarks>
  /// Should be a value between [0,1].
  /// For example, a value of 0.1 means up to 10% of the objects
  /// with a density exceeding the threshold will be tolerated.
  /// </remarks>
  [Required]
  public double HighDensityObjectLimit { get; set; }
}
