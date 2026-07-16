using System;
using UnityEngine;
using System.Collections.Generic;

namespace RungTramTraSu.CameraSystem
{
    public enum MeteringMode { Matrix, CenterWeighted, Spot }
    public enum FocusMode { Manual, SingleAF, ContinuousAF }
    public enum ImageFormat { JPEG, RAW }

    public interface ICameraSystem
    {
        bool IsCameraActive { get; }
        bool IsManualMode { get; }
        float BatteryPercentage { get; }
        int AvailableStorage { get; }
        void ToggleCameraMode();
        void ConsumeBattery(float amount);
    }

    public interface ILensSystem
    {
        float CurrentFocalLength { get; }
        float CurrentFieldOfView { get; }
        float LensBreathingOffset { get; }
        float LensDistortionIntensity { get; }
        void CycleLens(int direction);
        void SetFocalLengthPreset(int index);
    }

    public interface IFocusSystem
    {
        FocusMode ActiveFocusMode { get; }
        float FocusDistance { get; }
        bool HasTargetLock { get; }
        Transform LockTarget { get; }
        void SetFocusMode(FocusMode mode);
        void AdjustFocusDistance(float delta);
        void LockActiveTarget();
        float GetBlurFactor(float subjectDist, float aperture);
    }

    public interface IExposureSystem
    {
        int ISO { get; }
        float Aperture { get; }
        float ShutterSpeed { get; }
        float EVValue { get; }
        MeteringMode CurrentMetering { get; }
        void CycleISO(int direction);
        void CycleAperture(int direction);
        void CycleShutter(int direction);
        void AdjustEV(float step);
        void SetMeteringMode(MeteringMode mode);
        float CalculateLuminanceDeviation();
    }

    public interface IPhotoCapture
    {
        bool IsCapturing { get; }
        bool IsBurstModeActive { get; }
        void CaptureSingleShot(Action<Texture2D> callback);
        void StartBurstCapture(Action<List<Texture2D>> callback);
        void StopBurstCapture();
    }

    public struct PhotoMetadata
    {
        public string targetId;
        public string vietnameseName;
        public string scientificName;
        public string category;
        public string conservationStatus;
        public int iso;
        public float aperture;
        public float shutterSpeed;
        public string whiteBalance;
    }

    public struct ScoreResult
    {
        public float totalScore;
        public int starRating;
    }

    public struct PhotoLogEntry
    {
        public string photoId;
        public string vietnameseName;
        public float score;
        public int stars;
    }

    public interface IAlbumManager
    {
        void SavePhoto(Texture2D photo, PhotoMetadata meta, ScoreResult score);
        List<PhotoLogEntry> ListPhotos();
    }
}
