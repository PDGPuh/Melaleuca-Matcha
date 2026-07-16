using UnityEngine;
using UnityEngine.SceneManagement;

namespace RungTramTraSu.CameraSystem
{
    public class PhotoScoring : MonoBehaviour
    {
        private static PhotoScoring instance;
        public static PhotoScoring Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("PhotoScoring");
                    instance = go.AddComponent<PhotoScoring>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

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
            public float noiseScore;
            public float wbScore;
            public float separationScore;
            public float manualBonus;
            public string explanation;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

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

            // 1. Focus Sharpness Score (20 points max)
            float focusGraded = (1f - currentBlurFactor) * 20f;
            result.focusScore = Mathf.Clamp(focusGraded, 0f, 20f);

            // 2. Exposure Accuracy Score (20 points max)
            float absError = Mathf.Abs(exposureError);
            float expGraded = 0f;
            if (absError <= 0.3f) expGraded = 20f; // Perfect exposure
            else if (absError <= 1.0f) expGraded = 20f - (absError - 0.3f) * 10f; 
            else expGraded = 13f - (absError - 1.0f) * 6.5f; 
            result.exposureScore = Mathf.Clamp(expGraded, 0f, 20f);

            // 3. Subject Framing / Size (20 points max)
            float coverage = target.screenCoverage;
            float sizeGraded = 0f;
            if (coverage >= 6f && coverage <= 25f)
            {
                sizeGraded = 20f;
            }
            else if (coverage < 6f)
            {
                sizeGraded = Mathf.Lerp(5f, 20f, coverage / 6f);
            }
            else
            {
                sizeGraded = Mathf.Max(5f, 20f - (coverage - 25f) * 0.4f);
            }
            result.sizeScore = Mathf.Clamp(sizeGraded, 0f, 20f);

            // 4. Composition Rules (15 points max)
            float offsetX = Mathf.Abs(target.viewportPos.x - 0.5f);
            float offsetY = Mathf.Abs(target.viewportPos.y - 0.5f);
            float offsetFromCenter = offsetX + offsetY;

            float centerComp = (1f - Mathf.Clamp01(offsetFromCenter / 0.5f)) * 15f;
            
            // Rule of Thirds checks
            float gridX1 = Mathf.Abs(target.viewportPos.x - 0.33f);
            float gridX2 = Mathf.Abs(target.viewportPos.x - 0.66f);
            float gridY1 = Mathf.Abs(target.viewportPos.y - 0.33f);
            float gridY2 = Mathf.Abs(target.viewportPos.y - 0.66f);
            
            float gridDist = Mathf.Min(gridX1, gridX2) + Mathf.Min(gridY1, gridY2);
            float gridComp = (1f - Mathf.Clamp01(gridDist / 0.3f)) * 13f;
            result.compositionScore = Mathf.Clamp(Mathf.Max(centerComp, gridComp), 0f, 15f);

            // 5. Facing Direction (10 points max)
            result.facingScore = target.isFacingCamera ? 10f : 3f;

            // 6. Motion Blur / Speed Freeze (10 points max)
            float blurPenalty = 0f;
            bool isMovingTarget = target.displayName.Contains("Chim") || target.displayName.Contains("Cò") || 
                                  target.displayName.Contains("Sếu") || target.displayName.Contains("Diệc") || 
                                  target.displayName.Contains("Én") || target.displayName.Contains("Le le") ||
                                  target.displayName.Contains("Cá") || target.displayName.Contains("Bướm");

            if (isMovingTarget)
            {
                if (shutterSpeed <= 1f/500f) // 1/500s or faster freezes movement
                {
                    blurPenalty = 10f;
                }
                else if (shutterSpeed <= 1f/250f)
                {
                    blurPenalty = 7f;
                }
                else if (shutterSpeed <= 1f/125f)
                {
                    blurPenalty = 4f;
                }
                else
                {
                    blurPenalty = 0f;
                }
            }
            else
            {
                blurPenalty = 10f; // Landscape / stationary target does not require high shutter speed
            }
            result.motionBlurScore = blurPenalty;

            // 7. Sensor Noise Penalty (Up to -10 points)
            // ISO > 800 introduces noise/grain which degrades clean rating
            float noisePenalty = 0f;
            if (iso > 800)
            {
                noisePenalty = ((iso - 800f) / 12000f) * 10f;
            }
            result.noiseScore = -Mathf.Clamp(noisePenalty, 0f, 10f);

            // 8. White Balance Matching (5 points max)
            float wbGraded = 5f;
            string sceneName = SceneManager.GetActiveScene().name;
            if (CameraManager.Instance != null && CameraManager.Instance.ExpSys != null)
            {
                var wbMode = CameraManager.Instance.ExpSys.CurrentWBMode;
                int kelvin = CameraManager.Instance.ExpSys.CurrentKelvin;

                if (sceneName.Contains("Phase5")) // Sunset needs Cloudy (6500K) or Kelvin > 6000K
                {
                    if (wbMode == ExposureSystem.WhiteBalanceMode.Cloudy || wbMode == ExposureSystem.WhiteBalanceMode.Shade || kelvin >= 6000)
                    {
                        wbGraded = 5f;
                    }
                    else
                    {
                        wbGraded = 2f;
                    }
                }
                else // Daylight needs Sunny or Auto or Kelvin ~5200K
                {
                    if (wbMode == ExposureSystem.WhiteBalanceMode.Sunny || wbMode == ExposureSystem.WhiteBalanceMode.Auto || (kelvin >= 5000 && kelvin <= 5600))
                    {
                        wbGraded = 5f;
                    }
                    else
                    {
                        wbGraded = 3f;
                    }
                }
            }
            result.wbScore = wbGraded;

            // 9. Background Separation Bokeh Bonus (5 points max)
            // Low f-stop values isolate the subject creating smooth background separation
            float separationGraded = 0f;
            if (aperture <= 1.8f) separationGraded = 5f;
            else if (aperture <= 2.8f) separationGraded = 3.5f;
            else if (aperture <= 4.0f) separationGraded = 2f;
            else separationGraded = 0f;
            result.separationScore = separationGraded;

            // 10. Manual Mode Bonus
            result.manualBonus = isManualMode ? 10f : 0f;

            // Calculate final sum
            float finalSum = result.focusScore + result.exposureScore + result.sizeScore + 
                             result.compositionScore + result.facingScore + result.motionBlurScore + 
                             result.noiseScore + result.wbScore + result.separationScore + 
                             result.manualBonus;

            result.totalScore = Mathf.Clamp(finalSum, 10f, 100f);
            
            // Map total score to star ratings (1 to 5 stars)
            if (result.totalScore >= 90f) result.starRating = 5;
            else if (result.totalScore >= 75f) result.starRating = 4;
            else if (result.totalScore >= 55f) result.starRating = 3;
            else if (result.totalScore >= 30f) result.starRating = 2;
            else result.starRating = 1;

            result.explanation = BuildFeedbackString(result, target.displayName, isManualMode, exposureError, currentBlurFactor, aperture);

            return result;
        }

        private string BuildFeedbackString(ScoreResult res, string name, bool manual, float expErr, float blur, float ap)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            sb.Append($"Chủ thể: {name}. ");
            
            if (manual) sb.Append("Chụp Thủ Công (+10đ). ");
            else sb.Append("Chụp Tự Động. ");

            if (blur > 0.4f) sb.Append("Ảnh bị out nét nặng. ");
            else if (blur > 0.1f) sb.Append("Ảnh hơi out nét nhẹ. ");
            else sb.Append("Lấy nét chuẩn! ");

            if (expErr > 1.0f) sb.Append("Ảnh quá sáng. ");
            else if (expErr < -1.0f) sb.Append("Ảnh quá tối. ");
            else sb.Append("Ánh sáng cân bằng. ");

            if (res.separationScore >= 4f) sb.Append("Hiệu ứng xóa phông tuyệt đẹp! ");
            
            if (res.noiseScore < -5f) sb.Append("Ảnh bị nhiễu hạt nặng (giảm ISO). ");

            if (res.sizeScore < 10f) sb.Append("Chủ thể hơi nhỏ.");
            else if (res.sizeScore >= 18f) sb.Append("Căn chỉnh kích thước hoàn hảo.");

            return sb.ToString();
        }
    }
}
