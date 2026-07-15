using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class PhotoScoring : MonoBehaviour
    {
        public static PhotoScoring Instance { get; private set; }

        public struct ScoreResult
        {
            public float totalScore;
            public int starRating;
            public float focusScore;
            public float exposureScore;
            public float sizeScore;
            public float compositionScore;
            public float facingScore;
            public float motionBlurScore;
            public float manualBonus;
            public string explanation;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // Main scoring formula based on camera parameters and detected target details
        public ScoreResult CalculateScore(
            WildlifeDetector.DetectedTarget target,
            float currentFocusDistance,
            float currentBlurFactor,
            float exposureError,
            float shutterSpeed,
            float aperture,
            int iso,
            bool isManualMode)
        {
            ScoreResult result = new ScoreResult();

            // 1. Focus Score (20 points max)
            // blur factor is 0 (perfect) to 1 (terrible)
            float focusGraded = (1f - currentBlurFactor) * 20f;
            result.focusScore = Mathf.Clamp(focusGraded, 0f, 20f);

            // 2. Exposure Score (20 points max)
            // exposure error: 0 is perfect, >2 or <-2 is unusable
            float absError = Mathf.Abs(exposureError);
            float expGraded = 0f;
            if (absError <= 0.3f) expGraded = 20f; // Perfect exposure
            else if (absError <= 1.0f) expGraded = 20f - (absError - 0.3f) * 10f; // Soft penalty
            else expGraded = 13f - (absError - 1.0f) * 6.5f; // Hard penalty
            result.exposureScore = Mathf.Clamp(expGraded, 0f, 20f);

            // 3. Size/Distance Score (20 points max)
            // Optimal screen coverage is between 6% and 25% for standard framing
            float coverage = target.screenCoverage;
            float sizeGraded = 0f;
            if (coverage >= 6f && coverage <= 25f)
            {
                sizeGraded = 20f;
            }
            else if (coverage < 6f)
            {
                // Too far away
                sizeGraded = Mathf.Lerp(5f, 20f, coverage / 6f);
            }
            else
            {
                // Too close (subject cut off)
                sizeGraded = Mathf.Max(5f, 20f - (coverage - 25f) * 0.4f);
            }
            result.sizeScore = Mathf.Clamp(sizeGraded, 0f, 20f);

            // 4. Composition (15 points max)
            // Evaluates alignment with central target and Rule of Thirds grid lines
            // Center is (0.5, 0.5) in viewport coordinates
            float offsetX = Mathf.Abs(target.viewportPos.x - 0.5f);
            float offsetY = Mathf.Abs(target.viewportPos.y - 0.5f);
            float offsetFromCenter = offsetX + offsetY; // max is 1.0f

            float centerComp = (1f - Mathf.Clamp01(offsetFromCenter / 0.5f)) * 15f;
            
            // Check Rule of Thirds grid intersection points: x = 0.33, 0.66, y = 0.33, 0.66
            float gridX1 = Mathf.Abs(target.viewportPos.x - 0.33f);
            float gridX2 = Mathf.Abs(target.viewportPos.x - 0.66f);
            float gridY1 = Mathf.Abs(target.viewportPos.y - 0.33f);
            float gridY2 = Mathf.Abs(target.viewportPos.y - 0.66f);
            
            float gridDist = Mathf.Min(gridX1, gridX2) + Mathf.Min(gridY1, gridY2);
            float gridComp = (1f - Mathf.Clamp01(gridDist / 0.3f)) * 13f;

            result.compositionScore = Mathf.Clamp(Mathf.Max(centerComp, gridComp), 0f, 15f);

            // 5. Facing Direction (15 points max)
            // If the animal is looking generally towards the lens, add points
            result.facingScore = target.isFacingCamera ? 15f : 5f;

            // 6. Motion Blur / Action Freeze (10 points max)
            // Flying birds move fast, requiring fast shutter speeds (1/500 or faster)
            // Slow shutter speeds (1/30 to 1/125) on moving targets cause motion blur
            float blurPenalty = 0f;
            bool isMovingTarget = target.displayName.Contains("Chim") || target.displayName.Contains("Cò") || 
                                  target.displayName.Contains("Sếu") || target.displayName.Contains("Diệc") || 
                                  target.displayName.Contains("Én") || target.displayName.Contains("Le le") ||
                                  target.displayName.Contains("Cá") || target.displayName.Contains("Bướm");

            if (isMovingTarget)
            {
                if (shutterSpeed >= 1f/250f)
                {
                    blurPenalty = 10f; // Crisp, frozen action!
                }
                else if (shutterSpeed >= 1f/125f)
                {
                    blurPenalty = 6f;  // Minor blur
                }
                else
                {
                    blurPenalty = 1f;  // Heavy motion blur
                }
            }
            else
            {
                blurPenalty = 10f; // Still landscape target, no shutter motion blur
            }
            result.motionBlurScore = blurPenalty;

            // 7. Manual Mode Bonus (+10 points)
            result.manualBonus = isManualMode ? 10f : 0f;

            // Calculate Sum
            float finalSum = result.focusScore + result.exposureScore + result.sizeScore + 
                             result.compositionScore + result.facingScore + result.motionBlurScore + 
                             result.manualBonus;

            // Clip final score between 10 and 100
            result.totalScore = Mathf.Clamp(finalSum, 10f, 100f);
            
            // Map total score to star ratings (1 to 5 stars)
            if (result.totalScore >= 90f) result.starRating = 5;
            else if (result.totalScore >= 75f) result.starRating = 4;
            else if (result.totalScore >= 55f) result.starRating = 3;
            else if (result.totalScore >= 30f) result.starRating = 2;
            else result.starRating = 1;

            // Generate textual summary feedback
            result.explanation = BuildFeedbackString(result, target.displayName, isManualMode, exposureError, currentBlurFactor);

            return result;
        }

        private string BuildFeedbackString(ScoreResult res, string name, bool manual, float expErr, float blur)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            sb.Append($"Chủ thể: {name}. ");
            
            if (manual) sb.Append("Chụp Thủ Công (+10đ). ");
            else sb.Append("Chụp Tự Động. ");

            if (blur > 0.4f) sb.Append("Hình bị nhòe nét nặng. ");
            else if (blur > 0.1f) sb.Append("Hình hơi out nét nhẹ. ");
            else sb.Append("Lấy nét chuẩn! ");

            if (expErr > 1.0f) sb.Append("Ảnh bị dư sáng (chói). ");
            else if (expErr < -1.0f) sb.Append("Ảnh bị thiếu sáng (tối). ");
            else sb.Append("Ánh sáng hài hòa. ");

            if (res.sizeScore < 10f) sb.Append("Chủ thể quá nhỏ trong khung hình.");
            else if (res.sizeScore >= 18f) sb.Append("Bố cục kích thước đẹp.");

            return sb.ToString();
        }
    }
}
