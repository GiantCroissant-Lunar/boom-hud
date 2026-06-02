namespace BoomHud.Abstractions.IR;

/// <summary>
/// Shape used to arrange 2D component faces in 3D space.
/// </summary>
public enum SpatialShape
{
    Cylinder,
    Radial,
    Helix,
    Grid3D,
    Free
}

/// <summary>
/// Where a child's face points.
/// </summary>
public enum SpatialFacing
{
    Inward,
    Outward,
    Camera
}

/// <summary>
/// Describes how a container arranges its (2D) children in 3D space.
/// Present on a ComponentNode whose children are placed spatially;
/// absent => normal 2D layout.
/// </summary>
public sealed record SpatialSpec
{
    /// <summary>
    /// Shape of the 3D arrangement.
    /// </summary>
    public required SpatialShape Shape { get; init; }

    /// <summary>
    /// Direction each child face points.
    /// </summary>
    public SpatialFacing Facing { get; init; } = SpatialFacing.Camera;

    /// <summary>
    /// Binding expression that yields the angle around the axis (cylinder/radial/helix).
    /// </summary>
    public BindingSpec? AngleFrom { get; init; }

    /// <summary>
    /// Binding expression that yields the position along the axis (cylinder/helix); time/tick.
    /// </summary>
    public BindingSpec? DepthFrom { get; init; }

    /// <summary>
    /// Binding expression that yields the radial distance (radial; optional cylinder jitter).
    /// </summary>
    public BindingSpec? RadiusFrom { get; init; }

    /// <summary>
    /// Binding expression that yields the across-track offset within a lane.
    /// </summary>
    public BindingSpec? LateralFrom { get; init; }

    /// <summary>
    /// Base radius of the shape.
    /// </summary>
    public double Radius { get; init; } = 1.0;

    /// <summary>
    /// Angular spacing between items in degrees.
    /// </summary>
    public double AngularSpacingDeg { get; init; } = 30.0;

    /// <summary>
    /// Scale factor applied to depth values.
    /// </summary>
    public double DepthScale { get; init; } = 1.0;

    /// <summary>
    /// Curvature of the card face. 0 = flat quad, 1 = fully curved to the wall.
    /// </summary>
    public double Curvature { get; init; }
}
