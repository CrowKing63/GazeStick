using System;
using System.Runtime.InteropServices;

namespace Eyeware.BeamEyeTracker
{
    public static class Constants
    {
        public const double NullDataTimestamp = -1.0;
    }

    public enum TrackingDataReceptionStatus : Int32
    {
        NotReceivingTrackingData = 0,
        ReceivingTrackingData = 1,
        AttemptingTrackingAutoStart = 2
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct Version
    {
        public UInt32 Major;
        public UInt32 Minor;
        public UInt32 Patch;
        public UInt32 Padding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct Point
    {
        public Int32 X;
        public Int32 Y;

        public Point(Int32 x, Int32 y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct PointF
    {
        public float X;
        public float Y;

        public PointF(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ViewportGeometry
    {
        public Point Point00;
        public Point Point11;

        public ViewportGeometry(Point point00, Point point11)
        {
            Point00 = point00;
            Point11 = point11;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct Vector3D
    {
        public float X;
        public float Y;
        public float Z;
        UInt32 _Padding;
    }

    public enum TrackingConfidence : Int32
    {
        LostTracking = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct UnifiedScreenGaze
    {
        public TrackingConfidence Confidence;
        UInt32 _Padding;
        public Point PointOfRegard;
        public Point UnboundedPointOfRegard;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ViewportGaze
    {
        public TrackingConfidence Confidence;
        UInt32 _Padding;
        public PointF NormalizedPointOfRegard;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct HeadPose
    {
        public TrackingConfidence Confidence;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public float[] RotationFromHcsToWcs;
        public Vector3D TranslationFromHcsToWcs;
        public ulong TrackSessionUid;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct UserState
    {
        public UInt64 StructVersion;
        public double TimestampInSeconds;
        public HeadPose HeadPose;
        public UnifiedScreenGaze UnifiedScreenGaze;
        public ViewportGaze ViewportGaze;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] Reserved;

        public static UserState Create()
        {
            return new UserState
            {
                StructVersion = 1,
                TimestampInSeconds = Constants.NullDataTimestamp,
                HeadPose = new HeadPose(),
                UnifiedScreenGaze = new UnifiedScreenGaze(),
                ViewportGaze = new ViewportGaze(),
                Reserved = new byte[128]
            };
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct SimCameraTransform3D
    {
        public float RollInRadians;
        public float PitchInRadians;
        public float YawInRadians;
        public float XInMeters;
        public float YInMeters;
        public float ZInMeters;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct SimGameCameraState
    {
        public UInt64 StructVersion;
        public double TimestampInSeconds;
        public SimCameraTransform3D EyeTrackingPoseComponent;
        public SimCameraTransform3D HeadTrackingPoseComponent;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public ulong[] Reserved;

        public static SimGameCameraState Create()
        {
            return new SimGameCameraState
            {
                StructVersion = 1,
                TimestampInSeconds = Constants.NullDataTimestamp,
                EyeTrackingPoseComponent = new SimCameraTransform3D(),
                HeadTrackingPoseComponent = new SimCameraTransform3D(),
                Reserved = new ulong[128]
            };
        }
    }

    public class TrackingStateSet : IDisposable
    {
        private readonly IntPtr _handle;
        private bool _disposed;

        public UserState UserState { get; }
        public SimGameCameraState SimGameCameraState { get; }

        internal TrackingStateSet(IntPtr trackingStateSetHandle)
        {
            if (trackingStateSetHandle == IntPtr.Zero)
                throw new ArgumentException("Invalid handle", nameof(trackingStateSetHandle));

            _handle = trackingStateSetHandle;

            IntPtr userStatePtr = EW_BET_API_GetUserState(trackingStateSetHandle);
            IntPtr cameraStatePtr = EW_BET_API_GetSimGameCameraState(trackingStateSetHandle);

            UserState = Marshal.PtrToStructure<UserState>(userStatePtr);
            SimGameCameraState = Marshal.PtrToStructure<SimGameCameraState>(cameraStatePtr);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                    EW_BET_API_DestroyTrackingStateSet(_handle);
                _disposed = true;
            }
        }

        [DllImport("beam_eye_tracker_client")]
        private static extern IntPtr EW_BET_API_GetUserState(IntPtr trackingStateSetHandle);

        [DllImport("beam_eye_tracker_client")]
        private static extern IntPtr EW_BET_API_GetSimGameCameraState(IntPtr trackingStateSetHandle);

        [DllImport("beam_eye_tracker_client")]
        private static extern void EW_BET_API_DestroyTrackingStateSet(IntPtr trackingStateSetHandle);
    }

    public class API : IDisposable
    {
        private IntPtr apiHandle = IntPtr.Zero;
        private bool disposed = false;

        public bool IsDisposed => disposed;

        [DllImport("beam_eye_tracker_client")]
        private static extern int EW_BET_API_Create(
            string friendlyName,
            ViewportGeometry initialViewportGeometry,
            out IntPtr apiHandle);

        [DllImport("beam_eye_tracker_client")]
        private static extern void EW_BET_API_Destroy(IntPtr apiHandle);

        [DllImport("beam_eye_tracker_client")]
        private static extern TrackingDataReceptionStatus EW_BET_API_GetTrackingDataReceptionStatus(IntPtr apiHandle);

        [DllImport("beam_eye_tracker_client")]
        private static extern bool EW_BET_API_WaitForNewTrackingStateSet(
            IntPtr apiHandle,
            ref double lastReceivedTimestamp,
            uint timeoutMs);

        [DllImport("beam_eye_tracker_client")]
        private static extern int EW_BET_API_CreateAndFillLatestTrackingStateSet(
            IntPtr apiHandle,
            out IntPtr trackingStateSetHandle);

        [DllImport("beam_eye_tracker_client")]
        private static extern void EW_BET_API_GetVersion(IntPtr apiHandle, out Version version);

        public const uint DefaultTrackingDataTimeoutMs = 1000;

        public API(string friendlyName, ViewportGeometry initialViewportGeometry)
        {
            if (string.IsNullOrEmpty(friendlyName))
                throw new ArgumentNullException(nameof(friendlyName));

            int result = EW_BET_API_Create(friendlyName, initialViewportGeometry, out apiHandle);
            if (result != 0)
                throw new Exception($"Failed to create Beam Eye Tracker API. Error code: {result}");
        }

        public Version GetVersion()
        {
            ThrowIfDisposed();
            EW_BET_API_GetVersion(apiHandle, out Version version);
            return version;
        }

        public TrackingDataReceptionStatus GetTrackingDataReceptionStatus()
        {
            ThrowIfDisposed();
            return EW_BET_API_GetTrackingDataReceptionStatus(apiHandle);
        }

        public bool WaitForNewTrackingData(
            ref double lastReceivedTimestamp,
            uint timeoutMs = DefaultTrackingDataTimeoutMs)
        {
            ThrowIfDisposed();
            return EW_BET_API_WaitForNewTrackingStateSet(apiHandle, ref lastReceivedTimestamp, timeoutMs);
        }

        public TrackingStateSet GetLatestTrackingStateSet()
        {
            ThrowIfDisposed();

            int result = EW_BET_API_CreateAndFillLatestTrackingStateSet(apiHandle, out IntPtr trackingStateSetHandle);
            if (result != 0)
                throw new Exception($"Failed to get latest tracking state set. Error code: {result}");

            return new TrackingStateSet(trackingStateSetHandle);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(API));
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (apiHandle != IntPtr.Zero)
                {
                    EW_BET_API_Destroy(apiHandle);
                    apiHandle = IntPtr.Zero;
                }
                disposed = true;
            }
        }
    }
}