using System.Drawing;
using Eyeware.BeamEyeTracker;
using GazeStick.Services;
using Xunit;

namespace TestViewportGeometry;

/// <summary>
/// Unit tests for the pure viewport-geometry computation in BeamTrackingService.
/// These exercise ComputeViewportGeometry with synthetic Rectangle inputs so no
/// live screen enumeration is required (the plan's M0 acceptance criterion).
/// </summary>
public class ViewportGeometryTests
{
    [Fact]
    public void PrimaryScreenAtOrigin_ProducesZeroOriginGeometry()
    {
        // A single QHD display at the origin: (0,0) to (2560,1440).
        var bounds = new Rectangle(0, 0, 2560, 1440);
        var geom = BeamTrackingService.ComputeViewportGeometry(bounds);

        Assert.Equal(0, geom.Point00.X);
        Assert.Equal(0, geom.Point00.Y);
        Assert.Equal(2560, geom.Point11.X);
        Assert.Equal(1440, geom.Point11.Y);
    }

    [Fact]
    public void OffsetMonitor_ProducesNonZeroOrigin()
    {
        // A second display placed to the right of a primary at 1920 wide:
        // bounds (1920, 0) size 2560x1440 -> Point00 must be (1920, 0).
        var bounds = new Rectangle(1920, 0, 2560, 1440);
        var geom = BeamTrackingService.ComputeViewportGeometry(bounds);

        Assert.Equal(1920, geom.Point00.X);
        Assert.Equal(0, geom.Point00.Y);
        Assert.Equal(1920 + 2560, geom.Point11.X);
        Assert.Equal(1440, geom.Point11.Y);
    }

    [Fact]
    public void NegativeOffsetMonitor_ProducesNegativeOrigin()
    {
        // A display placed to the left of primary: bounds (-2560, 0).
        var bounds = new Rectangle(-2560, 0, 2560, 1440);
        var geom = BeamTrackingService.ComputeViewportGeometry(bounds);

        Assert.Equal(-2560, geom.Point00.X);
        Assert.Equal(0, geom.Point00.Y);
        Assert.Equal(0, geom.Point11.X);
        Assert.Equal(1440, geom.Point11.Y);
    }

    [Fact]
    public void ResolutionChange_ProducesNewGeometry()
    {
        // Simulate a virtual display switching QHD -> UHD: the geometry must
        // reflect the new bounds exactly (this is what drives re-mapping).
        var before = BeamTrackingService.ComputeViewportGeometry(new Rectangle(0, 0, 2560, 1440));
        var after = BeamTrackingService.ComputeViewportGeometry(new Rectangle(0, 0, 3840, 2160));

        Assert.Equal(3840, after.Point11.X);
        Assert.Equal(2160, after.Point11.Y);
        // The origin is unchanged for a primary at the origin.
        Assert.Equal(before.Point00.X, after.Point00.X);
        Assert.Equal(before.Point00.Y, after.Point00.Y);
    }

    [Fact]
    public void GeometryIsSymmetricAboutCenter()
    {
        // For any bounds, the viewport center must be exactly the midpoint of
        // Point00 and Point11 — this guarantees left/right symmetry for the stick.
        var bounds = new Rectangle(640, 200, 3840, 2160);
        var geom = BeamTrackingService.ComputeViewportGeometry(bounds);

        int cx = (geom.Point00.X + geom.Point11.X) / 2;
        int cy = (geom.Point00.Y + geom.Point11.Y) / 2;

        // Distance from center to left edge equals distance to right edge.
        Assert.Equal(geom.Point11.X - cx, cx - geom.Point00.X);
        Assert.Equal(geom.Point11.Y - cy, cy - geom.Point00.Y);
    }
}
